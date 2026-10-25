using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Otteretro.Api;

namespace Otteretro.App;

/// <param name="Step">Código del paso: preparing, moving, copying, verifying, deletingSource, done o error (clave `move.step.*` del front, #142).</param>
public record MoveState(string Step, long Done, long Total, bool Running, string? Destination, ApiError? Error, ApiError? Warning);

/// <summary>Ejecuta la mudanza en segundo plano y la publica como evento "move" (panel de tareas y Ajustes).</summary>
public sealed class FolderMoveService(AppSession session, Events events)
{
    public MoveState State { get; private set; } = new("", 0, 0, false, null, null, null);
    readonly Lock gate = new();

    /// <summary>Devuelve un error si no puede empezar; si empieza, el resultado llega por eventos.</summary>
    public ApiError? Start(string destination)
    {
        string source;
        // Comprobar y marcar "en curso" de una vez: dos mudanzas a la vez pueden borrar origen y destino.
        lock (gate)
        {
            if (State.Running) return ApiError.Of("move.inProgress");
            if (session.Config.DataFolder is not { } current) return ApiError.Of("move.noDataFolder");
            source = current;
            var error = FolderMove.Validate(source, destination);
            if (error is not null) return error;
            destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
            Set(new("preparing", 0, 0, true, destination, null, null));
        }
        // ponytail: en F0 nada más escribe en la carpeta de datos; cuando haya tareas que la usen, pausarlas antes de mudarse.
        _ = Task.Run(() =>
        {
            FileLog.Folder = Path.Combine(Path.GetDirectoryName(AppConfig.DefaultPath)!, "logs");
            try
            {
                FolderMove.CopyAndVerify(source, destination, p => Set(State with { Step = p.Step, Done = p.Done, Total = p.Total }));
                try
                {
                    session.Config.DataFolder = destination;
                    session.Config.Save(session.ConfigPath);
                }
                catch
                {
                    // config.json sigue apuntando al origen (se escribe de forma atómica): se deshace la copia o el movimiento.
                    session.Config.DataFolder = source;
                    FolderMove.Undo(source, destination);
                    throw;
                }
                Set(State with { Step = "deletingSource" });
                var warning = FolderMove.DeleteSource(source);
                FileLog.Folder = DataFolder.LogsPath(destination);
                FileLog.Write(LogLevel.Information, "FolderMove", $"Carpeta de datos movida de {source} a {destination}.");
                Set(State with { Step = "done", Running = false, Warning = warning });
            }
            catch (Exception e)
            {
                // Si algo falló al deshacer, se dice dónde quedaron de verdad los datos.
                var where = DataFolder.IsReady(source) ? source : destination;
                FileLog.Folder = DataFolder.LogsPath(where);
                FileLog.Write(LogLevel.Error, "FolderMove", e.ToString());
                var mismatch = e as FolderMove.CopyMismatchException;
                var detail = mismatch?.RelativePath ?? e.Message;
                var error = where == source
                    ? ApiError.Of(mismatch is null ? "move.failedNothingMoved" : "move.copyMismatch", ("path", source), ("detail", detail))
                    : ApiError.Of("move.failedDataAt", ("path", destination), ("detail", detail));
                Set(State with { Step = "error", Running = false, Error = error });
            }
        });
        return null;
    }

    void Set(MoveState state)
    {
        State = state;
        events.Publish("move", state);
    }
}

/// <summary>
/// Mueve la carpeta de datos a otra ubicación sin perder nada (P7):
/// copiar → verificar cada archivo por hash → (quien llama cambia la configuración) → borrar el origen.
/// En el mismo disco se mueve sin copiar.
/// </summary>
public static class FolderMove
{
    /// <summary>Un archivo copiado no coincide con el original al compararlos por hash.</summary>
    public sealed class CopyMismatchException(string relativePath) : IOException(relativePath)
    {
        public string RelativePath { get; } = relativePath;
    }

    public record MoveProgress(string Step, long Done, long Total);

    static readonly StringComparison IgnoreCase = StringComparison.OrdinalIgnoreCase;

    /// <summary>Devuelve el error o null si se puede mover de <paramref name="source"/> a <paramref name="destination"/>.</summary>
    public static ApiError? Validate(string source, string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination) || !Path.IsPathFullyQualified(destination))
            return ApiError.Of("path.notAbsolute");
        var s = Normalize(source);
        var d = Normalize(destination);
        if (string.Equals(s, d, IgnoreCase))
            return ApiError.Of("move.sameFolder");
        if (d.StartsWith(s + Path.DirectorySeparatorChar, IgnoreCase))
            return ApiError.Of("move.insideCurrent");
        if (File.Exists(d))
            return ApiError.Of("path.isFile");
        if (Directory.Exists(d) && Directory.EnumerateFileSystemEntries(d).Any())
            return ApiError.Of("move.destinationNotEmpty");
        if (!Mounts.Same(s, d))
        {
            var root = Mounts.Of(d);
            if (!Directory.Exists(root)) return ApiError.Of("move.driveNotFound", ("drive", root));
            try
            {
                var free = new DriveInfo(root).AvailableFreeSpace;
                var needed = Size(s);
                if (free < needed)
                    return ApiError.Of("move.notEnoughSpace", ("drive", root), ("needed", needed.ToString()), ("free", free.ToString()));
            }
            catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // Rutas de red (\\nas\carpeta) o discos sin acceso.
                return ApiError.Of("move.driveUnavailable", ("drive", root), ("detail", e.Message));
            }
        }
        return null;
    }

    /// <summary>Copia (o mueve, en el mismo disco) y verifica. El origen sigue intacto salvo en el movimiento del mismo disco.</summary>
    /// <param name="forceCopy">Forzar la copia aunque sea el mismo disco (las pruebas lo usan para probar la copia).</param>
    public static void CopyAndVerify(string source, string destination, Action<MoveProgress> report, bool forceCopy = false)
    {
        CloseDatabase(source);

        if (!forceCopy && Mounts.Same(source, destination))
        {
            report(new("moving", 0, 1));
            if (Directory.Exists(destination)) Directory.Delete(destination); // está vacía (lo garantiza Validate)
            Directory.CreateDirectory(Path.GetDirectoryName(Normalize(destination))!);
            Directory.Move(source, destination);
            report(new("moving", 1, 1));
            return;
        }

        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(f => (relative: Path.GetRelativePath(source, f), size: new FileInfo(f).Length))
            .ToList();
        var total = files.Sum(f => f.size);
        try
        {
            foreach (var folder in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, folder)));

            long done = 0;
            foreach (var (relative, size) in files)
            {
                var target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(source, relative), target);
                FlushToDisk(target);
                report(new("copying", done += size, total));
            }

            done = 0;
            foreach (var (relative, size) in files)
            {
                if (!SameContent(Path.Combine(source, relative), Path.Combine(destination, relative)))
                    throw new CopyMismatchException(relative);
                report(new("verifying", done += size, total));
            }
        }
        catch
        {
            try { Undo(source, destination); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    /// <summary>
    /// Deja todo como antes de <see cref="CopyAndVerify"/>: si se movió (mismo disco) la devuelve a su sitio;
    /// si se copió, borra del destino solo lo copiado y las carpetas que quedan vacías, nunca otra cosa.
    /// </summary>
    public static void Undo(string source, string destination)
    {
        if (!Directory.Exists(destination)) return;
        if (!Directory.Exists(source))
        {
            Directory.Move(destination, source);
            return;
        }
        foreach (var f in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var copy = Path.Combine(destination, Path.GetRelativePath(source, f));
            if (!File.Exists(copy)) continue;
            File.SetAttributes(copy, FileAttributes.Normal);
            File.Delete(copy);
        }
        var folders = Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)
            .Select(c => Path.Combine(destination, Path.GetRelativePath(source, c)))
            .Append(destination)
            .OrderByDescending(c => c.Length);
        foreach (var c in folders)
            if (Directory.Exists(c) && !Directory.EnumerateFileSystemEntries(c).Any())
                Directory.Delete(c);
    }

    /// <summary>Obliga a Windows a escribir el archivo en el disco: sin esto, la verificación puede leer la caché y se borraría el origen antes de tiempo.</summary>
    static void FlushToDisk(string path)
    {
        var attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        using (var f = new FileStream(path, FileMode.Open, FileAccess.Write))
            f.Flush(flushToDisk: true);
        File.SetAttributes(path, attributes);
    }

    /// <summary>Borra la carpeta anterior. Si algo queda bloqueado, devuelve un aviso en vez de fallar: los datos ya están en el destino.</summary>
    public static ApiError? DeleteSource(string source)
    {
        if (!Directory.Exists(source)) return null;
        try
        {
            Directory.Delete(source, true);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ApiError.Of("move.sourceNotDeleted", ("path", source), ("detail", e.Message));
        }
    }

    public static long Size(string folder) =>
        Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
            : 0;

    /// <summary>Vuelca el WAL a la base y suelta las conexiones, para copiar o mover el archivo sin que esté en uso.</summary>
    static void CloseDatabase(string folder)
    {
        var path = DataFolder.DatabasePath(folder);
        if (File.Exists(path))
        {
            using var c = new SqliteConnection($"Data Source={path}");
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
    }

    static bool SameContent(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
        using var fa = File.OpenRead(a);
        using var fb = File.OpenRead(b);
        return SHA256.HashData(fa).AsSpan().SequenceEqual(SHA256.HashData(fb));
    }

    static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
