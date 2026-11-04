using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>
/// Vigila `Juegos\` y `Entrada\` de cada disco conectado mientras la app está abierta (#223): si copias o borras algo a mano, la
/// colección se entera sola, y lo que dejas en `Entrada\` se importa (#221). Los cambios seguidos se juntan y, después de
/// <see cref="Quiet"/> sin cambios, se encola la tarea.
/// </summary>
/// <remarks>ponytail: escanea todo (por nombre, sin hash, es rápido); escanear solo la consola que cambió si llega a pesar.</remarks>
public sealed class GamesWatcher : IDisposable
{
    public static TimeSpan Quiet { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Cada cuánto se revisa qué discos hay que vigilar (uno nuevo, uno que se desconectó).</summary>
    public static TimeSpan Resync { get; set; } = TimeSpan.FromSeconds(30);

    readonly Dictionary<string, FileSystemWatcher> watchers = new(StringComparer.OrdinalIgnoreCase);
    readonly Lock gate = new();
    Dictionary<string, string> entries = new(StringComparer.OrdinalIgnoreCase); // carpeta Entrada → id del disco
    readonly HashSet<string> pendingEntries = new(StringComparer.OrdinalIgnoreCase);
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
        bool scan;
        List<string> toImport;
        lock (gate)
        {
            if (now - lastChange < Quiet) return null;
            scan = pending;
            toImport = pendingEntries.ToList();
            pending = false;
            pendingEntries.Clear();
        }
        long? task = null;
        foreach (var id in toImport)
            if (Disk.Get(db, id) is { Connected: true } disk) task = Import.EnqueueEntry(db, disk);
        return scan ? Scan.Enqueue(db) : task;
    }

    void Sync(Database db)
    {
        var disks = Disk.List(db).Where(d => d.Connected && d.Type == "disk").ToList();
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var disk in disks)
            try
            {
                // Un disco de antes de #221 no la tiene todavía.
                current[Directory.CreateDirectory(Path.Combine(disk.Path, Disk.EntryFolder)).FullName] = disk.Id;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        // Los eventos de archivos la leen desde otro hilo.
        lock (gate) entries = current;
        var folders = disks.Select(d => Disk.FolderOf(d, "game")!).Concat(current.Keys)
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
                    // LastWrite avisa también cuando termina una copia larga.
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
                    InternalBufferSize = 64 * 1024,
                };
                void Mark(object? _, EventArgs? __) => Changed(folder);
                w.Created += Mark;
                w.Deleted += Mark;
                w.Renamed += Mark;
                w.Changed += Mark;
                w.Error += Mark; // se desbordó el búfer: por las dudas, se vuelve a mirar
                w.EnableRaisingEvents = true;
                watchers[folder] = w;
                // Lo que quedó esperando en Entrada mientras la app estaba cerrada.
                if (current.ContainsKey(folder) && Directory.EnumerateFileSystemEntries(folder).Any()) Changed(folder);
            }
            catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { } // disco de red sin soporte
    }

    void Changed(string folder)
    {
        lock (gate)
        {
            if (entries.TryGetValue(folder, out var disk)) pendingEntries.Add(disk);
            else pending = true;
            lastChange = DateTime.UtcNow;
        }
    }

    public void Dispose()
    {
        foreach (var w in watchers.Values) w.Dispose();
        watchers.Clear();
    }
}
