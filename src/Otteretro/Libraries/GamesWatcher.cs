using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>
/// Vigila `Juegos\` de cada disco conectado mientras la app está abierta (#223): si copias o borras algo a mano, la colección se
/// entera sola. Los cambios seguidos se juntan y, después de <see cref="Quiet"/> sin cambios, se encola un escaneo.
/// </summary>
/// <remarks>ponytail: escanea todo (por nombre, sin hash, es rápido); escanear solo la consola que cambió si llega a pesar.</remarks>
public sealed class GamesWatcher : IDisposable
{
    public static TimeSpan Quiet { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Cada cuánto se revisa qué discos hay que vigilar (uno nuevo, uno que se desconectó).</summary>
    public static TimeSpan Resync { get; set; } = TimeSpan.FromSeconds(30);

    readonly Dictionary<string, FileSystemWatcher> watchers = new(StringComparer.OrdinalIgnoreCase);
    readonly Lock gate = new();
    DateTime lastSync = DateTime.MinValue, lastChange;
    bool pending;

    /// <summary>Lo llama la cola en cada vuelta. Devuelve la tarea de escaneo si encoló una.</summary>
    public long? Tick(Database db)
    {
        var now = DateTime.UtcNow;
        if (now - lastSync >= Resync)
        {
            Sync(db);
            lastSync = now;
        }
        lock (gate)
        {
            if (!pending || now - lastChange < Quiet) return null;
            pending = false;
        }
        return Scan.Enqueue(db);
    }

    void Sync(Database db)
    {
        var folders = Disk.List(db).Where(d => d.Connected).Select(d => Disk.FolderOf(d, "game")).OfType<string>()
            .Where(Directory.Exists).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var gone in watchers.Keys.Where(k => !folders.Contains(k)).ToList())
        {
            watchers[gone].Dispose();
            watchers.Remove(gone);
        }
        foreach (var folder in folders.Where(f => !watchers.ContainsKey(f)))
            try
            {
                var w = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size,
                    InternalBufferSize = 64 * 1024,
                };
                w.Created += Changed;
                w.Deleted += Changed;
                w.Renamed += Changed;
                w.Changed += Changed;
                w.Error += (_, _) => Changed(null, null); // se desbordó el búfer: por las dudas, se escanea
                w.EnableRaisingEvents = true;
                watchers[folder] = w;
            }
            catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { } // disco de red sin soporte
    }

    void Changed(object? sender, EventArgs? e)
    {
        lock (gate)
        {
            pending = true;
            lastChange = DateTime.UtcNow;
        }
    }

    public void Dispose()
    {
        foreach (var w in watchers.Values) w.Dispose();
        watchers.Clear();
    }
}
