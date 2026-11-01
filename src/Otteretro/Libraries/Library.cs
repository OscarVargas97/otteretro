using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Otteretro.Api;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>
/// El marcador `otteretro-ubicacion.json` de la raíz de cada ubicación (§5.2, #199). Sus claves las ve la persona: van en
/// español. <c>contenido</c> es el tipo de cosa que guarda (#198); sin él, juegos.
/// </summary>
public record LibraryMarker(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("nombre")] string Name,
    [property: JsonPropertyName("tipo")] string Kind,
    [property: JsonPropertyName("creada")] string CreatedAt,
    [property: JsonPropertyName("formato")] int Format,
    [property: JsonPropertyName("contenido")] string? Type = null);

/// <param name="Connected">Está en su última ruta y su marcador tiene el mismo id.</param>
/// <param name="Legacy">Todavía tiene la estructura antigua (`<Consola>\Juegos|Emuladores|Ports`): adoptarla la migra.</param>
/// <param name="Type">game, emulator, port, complement o system (#198).</param>
public record LibraryInfo(string Id, string Name, string Kind, string Path, bool Connected, long? Free, long? Total, bool Legacy, string Type);

/// <summary>La carpeta de una consola en una ubicación (#190).</summary>
public record ConsoleFolder(string Console, string LibraryId, string Relative);

/// <summary>
/// Ubicaciones (§5, #198, #199): carpetas registradas con marcador para un tipo de cosa (juegos, emuladores, ports,
/// complementos o sistema). Crear, adoptar, renombrar, quitar sin borrar y reconectar. En el código y la base siguen
/// llamándose «library».
/// </summary>
public static class Library
{
    public const string MarkerFile = "otteretro-ubicacion.json";
    /// <summary>El nombre de antes (#191): se sigue leyendo y se reemplaza al escribir.</summary>
    public const string OldMarkerFile = "otteretro-biblioteca.json";
    public static readonly string[] Types = ["game", "emulator", "port", "complement", "system"];
    public const int MarkerFormat = 1;

    /// <summary>Carpetas de la estructura antigua dentro de cada consola (§5.5).</summary>
    public static readonly string[] LegacyFolders = ["Juegos", "Emuladores", "Ports"];

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static LibraryMarker? ReadMarker(string folder)
    {
        foreach (var file in new[] { MarkerFile, OldMarkerFile })
            try { return JsonSerializer.Deserialize<LibraryMarker>(File.ReadAllText(Path.Combine(folder, file))); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        return null;
    }

    /// <summary>Escribe el marcador a un temporal y lo reemplaza: un corte no lo deja a medias.</summary>
    public static void WriteMarker(string folder, LibraryMarker marker)
    {
        var path = Path.Combine(folder, MarkerFile);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(marker, Json));
        File.Move(path + ".tmp", path, overwrite: true);
        File.Delete(Path.Combine(folder, OldMarkerFile));
    }

    /// <summary>Una carpeta de red (\\nas\roms o //nas/roms); en Linux un montaje NFS parece local y se registra como local.</summary>
    public static bool IsNetwork(string path) => path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);

    public static bool IsLegacy(string folder) =>
        Directory.Exists(folder) && Directory.EnumerateDirectories(folder).Any(c =>
            !Path.GetFileName(c).StartsWith('_') && LegacyFolders.Any(l => Directory.Exists(Path.Combine(c, l))));

    record Row(string Id, string Name, string Kind, string Path, string Type);

    public static List<LibraryInfo> List(Database db)
    {
        using var c = db.Open();
        return c.Query<Row>("SELECT id, name, kind, path, type FROM library ORDER BY name").Select(Info).ToList();
    }

    public static LibraryInfo? Get(Database db, string id)
    {
        using var c = db.Open();
        return c.QuerySingleOrDefault<Row>("SELECT id, name, kind, path, type FROM library WHERE id = @id", new { id }) is { } r ? Info(r) : null;
    }

    static LibraryInfo Info(Row r)
    {
        var connected = ReadMarker(r.Path)?.Id == r.Id;
        long? free = null, total = null;
        if (connected && !IsNetwork(r.Path))
            try
            {
                var drive = new DriveInfo(App.Mounts.Of(r.Path));
                (free, total) = (drive.AvailableFreeSpace, drive.TotalSize);
            }
            catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException) { }
        return new(r.Id, r.Name, r.Kind, r.Path, connected, free, total, connected && r.Type == "game" && IsLegacy(r.Path), r.Type);
    }

    /// <summary>Crea una ubicación nueva de <paramref name="type"/> en <paramref name="path"/> (la crea si no existe). Devuelve su id o el error.</summary>
    public static (string? Id, ApiError? Error) Create(Database db, string path, string? name, string type = "game")
    {
        if (!Types.Contains(type)) return (null, ApiError.Of("library.badType"));
        if (Check(db, path) is { } error) return (null, error);
        if (ReadMarker(path) is not null) return (null, ApiError.Of("library.hasMarker"));
        try
        {
            Directory.CreateDirectory(path);
            return (Register(db, path, NewMarker(path, name, type)), null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (null, ApiError.Of("folder.createFailed", ("detail", e.Message)));
        }
    }

    /// <summary>
    /// Adopta una carpeta que ya existe: si tiene marcador, se registra con ese id; si no, se le escribe uno. Con la estructura
    /// antigua devuelve <c>Legacy = true</c> y quien llama encola la migración (§5.5).
    /// </summary>
    public static (string? Id, bool Legacy, ApiError? Error) Adopt(Database db, string path, string? name = null, string type = "game")
    {
        if (!Types.Contains(type)) return (null, false, ApiError.Of("library.badType"));
        if (!Directory.Exists(path)) return (null, false, ApiError.Of("path.notFound"));
        var marker = ReadMarker(path);
        if (marker is not null && Get(db, marker.Id) is { } known)
        {
            // Ya registrada: es una reconexión a la ruta nueva.
            using var c = db.Open();
            c.Execute("UPDATE library SET path = @path WHERE id = @Id", new { path = Normalize(path), known.Id });
            return (known.Id, known.Type == "game" && IsLegacy(path), null);
        }
        if (Check(db, path) is { } error) return (null, false, error);
        // Un marcador sin tipo (de antes de #198) toma el que se eligió al adoptar.
        marker = marker is null ? NewMarker(path, name, type) : marker with { Type = marker.Type ?? type };
        try { return (Register(db, path, marker), marker.Type == "game" && IsLegacy(path), null); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (null, false, ApiError.Of("folder.createFailed", ("detail", e.Message)));
        }
    }

    public static ApiError? Rename(Database db, string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return ApiError.Of("library.noName");
        if (Get(db, id) is not { } library) return ApiError.Of("library.notFound");
        using (var c = db.Open()) c.Execute("UPDATE library SET name = @name WHERE id = @id", new { id, name = name.Trim() });
        if (library.Connected && ReadMarker(library.Path) is { } marker) WriteMarker(library.Path, marker with { Name = name.Trim() });
        return null;
    }

    /// <summary>
    /// Dónde están las cosas de cada consola (#190, #192): las carpetas de primer nivel de cada ubicación conectada de
    /// <paramref name="type"/> que se llaman como la consola (su nombre o su id, sin distinguir mayúsculas). Una consola
    /// puede estar en varias ubicaciones.
    /// </summary>
    public static List<ConsoleFolder> ConsoleFolders(Database db, string type = "game")
    {
        using var c = db.Open();
        var consoles = c.Query<(string Id, string Name)>("SELECT id, name FROM console").ToList();
        var found = new List<ConsoleFolder>();
        foreach (var library in List(db).Where(l => l.Connected && l.Type == type))
            foreach (var folder in Directory.EnumerateDirectories(library.Path).Select(Path.GetFileName))
                if (consoles.FirstOrDefault(k => string.Equals(k.Name, folder, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(k.Id, folder, StringComparison.OrdinalIgnoreCase)) is { Id: not null } console)
                    found.Add(new ConsoleFolder(console.Id, library.Id, folder!));
        return found;
    }

    /// <summary>
    /// Dónde buscar lo instalado (#198): `Herramientas\Emuladores|Ports|Complementos` de la carpeta de datos, que es la
    /// primera ubicación de cada tipo, y después cada ubicación conectada de emuladores, ports o complementos.
    /// </summary>
    public static List<ToolRoot> ToolRoots(Database db)
    {
        var roots = Recipes.DefaultRoots(LibraryTasks.ToolsOf(db));
        roots.AddRange(List(db).Where(l => l.Connected && l.Type is "emulator" or "port" or "complement").Select(l => new ToolRoot(l.Type, l.Path, l.Id)));
        return roots;
    }

    /// <summary>Saca la biblioteca del registro. Nunca borra archivos, ni el marcador.</summary>
    public static bool Remove(Database db, string id)
    {
        using var c = db.Open();
        return c.Execute("DELETE FROM library WHERE id = @id", new { id }) > 0;
    }

    /// <summary>Busca las desconectadas en las raíces de los discos (2 niveles) y actualiza su ruta. Devuelve cuántas volvieron.</summary>
    public static int Reconnect(Database db, IEnumerable<string>? roots = null)
    {
        var lost = List(db).Where(l => !l.Connected).ToList();
        if (lost.Count == 0) return 0;
        roots ??= DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.Name).Where(r => OperatingSystem.IsWindows() || App.Mounts.IsUserMount(r));
        var found = Search(lost.Select(l => l.Id).ToHashSet(), roots, 2);
        using var c = db.Open();
        foreach (var (id, path) in found) c.Execute("UPDATE library SET path = @path WHERE id = @id", new { id, path });
        return found.Count;
    }

    /// <summary>Las carpetas con marcador de alguno de esos ids, hasta <paramref name="depth"/> niveles bajo cada raíz.</summary>
    public static Dictionary<string, string> Search(HashSet<string> ids, IEnumerable<string> roots, int depth)
    {
        var found = new Dictionary<string, string>();
        var level = roots.ToList();
        for (var d = 0; d <= depth && level.Count > 0 && found.Count < ids.Count; d++)
        {
            var next = new List<string>();
            foreach (var folder in level)
            {
                if (ReadMarker(folder) is { } m && ids.Contains(m.Id)) found.TryAdd(m.Id, Normalize(folder));
                if (d < depth)
                    try { next.AddRange(Directory.EnumerateDirectories(folder).Where(f => !IsSystem(f))); }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
            level = next;
        }
        return found;
    }

    static bool IsSystem(string folder)
    {
        var name = Path.GetFileName(folder);
        return name.StartsWith('$') || name is "Windows" or "System Volume Information" or "proc" or "sys" or "dev" or "nix";
    }

    static ApiError? Check(Database db, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return ApiError.Of("path.notAbsolute");
        if (File.Exists(path)) return ApiError.Of("path.isFile");
        var full = Normalize(path);
        foreach (var l in List(db))
        {
            var other = Normalize(l.Path);
            if (Same(other, full) || Inside(full, other) || Inside(other, full)) return ApiError.Of("library.overlaps", ("name", l.Name));
        }
        return null;
    }

    static LibraryMarker NewMarker(string path, string? name, string type) => new(Guid.NewGuid().ToString("N"),
        string.IsNullOrWhiteSpace(name) ? Path.GetFileName(Normalize(path)) is { Length: > 0 } n ? n : Normalize(path) : name.Trim(),
        IsNetwork(path) ? "network" : "local", DateTime.UtcNow.ToString("O"), MarkerFormat, type);

    static string Register(Database db, string path, LibraryMarker marker)
    {
        WriteMarker(path, marker);
        using var c = db.Open();
        c.Execute("INSERT INTO library (id, name, kind, path, created_at, type) VALUES (@Id, @Name, @kind, @path, @CreatedAt, @type)",
            new { marker.Id, marker.Name, kind = IsNetwork(path) ? "network" : "local", path = Normalize(path), marker.CreatedAt, type = marker.Type ?? "game" });
        return marker.Id;
    }

    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    static readonly StringComparison PathCase = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static bool Same(string a, string b) => string.Equals(Normalize(a), Normalize(b), PathCase);

    /// <summary><paramref name="path"/> está dentro de <paramref name="folder"/> (no es la misma).</summary>
    public static bool Inside(string path, string folder) =>
        Normalize(path).StartsWith(Normalize(folder) + Path.DirectorySeparatorChar, PathCase);
}
