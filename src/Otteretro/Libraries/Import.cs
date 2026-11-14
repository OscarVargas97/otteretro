using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>
/// La importación (§9.1, #193): todo origen termina acá. Cada archivo de juego de una carpeta se identifica (consola y juego) y se
/// mueve a `Juegos\&lt;Consola&gt;\&lt;Juego&gt;` del disco de su consola: copiar, verificar y recién entonces borrar (#220). Lo que se
/// movió ya no está en el origen, así que repetir la tarea sigue con lo que falta. Lo que no se sabe de qué consola es se queda donde está.
/// </summary>
public static partial class Import
{
    public const string Kind = "library.import";

    /// <param name="EntryDisk">Es la `Entrada\` de ese disco (#221): lo reconocido va a ese mismo disco, y solo si se identificó con seguridad (#222).</param>
    /// <param name="Skip">Lo que se desmarcó en la vista previa (#197), relativo a la carpeta.</param>
    /// <param name="GameId">El juego del que se sabe que es (lo que se bajó desde su ficha, §9.6).</param>
    /// <param name="DownloadTask">La descarga de la que salió: al identificarse el juego, sus enlaces quedan en la ficha (#210).</param>
    public record Job(string Path, string? EntryDisk = null, List<string>? Skip = null, long? GameId = null, long? DownloadTask = null);

    /// <summary>Las extensiones que dicen la consola. Las de disco (iso, chd, cue…) no: esas salen de su cabecera, su carpeta o su hash.</summary>
    static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".nsp"] = "switch",
        [".nsz"] = "switch",
        [".xci"] = "switch",
        [".xcz"] = "switch",
        [".nup"] = "switch", // volcado que unifica el juego (#309): el TitleID del nombre dice si es Switch 2
        [".nes"] = "nes",
        [".fds"] = "nes",
        [".sfc"] = "snes",
        [".smc"] = "snes",
        [".n64"] = "n64",
        [".z64"] = "n64",
        [".v64"] = "n64",
        [".gb"] = "gb",
        [".gbc"] = "gb",
        [".gba"] = "gba",
        [".vb"] = "vb",
        [".nds"] = "ds",
        [".3ds"] = "3ds",
        [".cia"] = "3ds",
        [".cci"] = "3ds",
        [".wua"] = "wiiu",
        [".wux"] = "wiiu",
        [".wbfs"] = "wii",
        [".gcm"] = "gc",
        [".gcz"] = "gc",
        [".cso"] = "psp",
        [".pbp"] = "psp",
        [".vpk"] = "vita",
    };

    /// <summary>Valida la carpeta y encola. No se importa desde las carpetas de un disco ni desde una que contenga un disco.</summary>
    public static (long? Task, ApiError? Error) Enqueue(Database db, string path, List<string>? skip = null)
    {
        if (CheckFolder(db, path) is { } error) return (null, error);
        return (TaskQueue.Enqueue(db, Kind, JsonSerializer.Serialize(new Job(Disk.Normalize(path), null, skip), JsonSerializerOptions.Web)), null);
    }

    /// <summary>Se puede importar: existe y no se cruza con un disco.</summary>
    public static ApiError? CheckFolder(Database db, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return ApiError.Of("path.notAbsolute");
        if (!Directory.Exists(path)) return ApiError.Of("path.notFound");
        foreach (var d in Disk.List(db))
        {
            var mine = d.Type == "disk"
                ? Disk.DiskFolders.Values.Select(f => Path.Combine(d.Path, f)).Any(f => Disk.Same(path, f) || Disk.Inside(path, f))
                : Disk.Inside(path, d.Path);
            if (mine || Disk.Same(path, d.Path) || Disk.Inside(d.Path, path)) return ApiError.Of("import.overlapsDisk", ("name", d.Name));
        }
        return null;
    }

    /// <summary>Encola la importación de la `Entrada\` de un disco, salvo que ya haya una esperando.</summary>
    /// <param name="folder">Solo esa carpeta de `Entrada\` (un paquete recién bajado); si no, toda.</param>
    public static long EnqueueEntry(Database db, DiskInfo disk, string? folder = null, long? gameId = null, long? downloadTask = null)
    {
        var data = JsonSerializer.Serialize(new Job(Disk.Normalize(folder ?? Path.Combine(disk.Path, Disk.EntryFolder)), disk.Id, null, gameId, downloadTask), JsonSerializerOptions.Web);
        using (var c = db.Open())
            if (c.ExecuteScalar<long?>("SELECT id FROM task WHERE kind = @Kind AND status = 'queued' AND data = @data LIMIT 1", new { Kind, data }) is { } waiting)
                return waiting;
        return TaskQueue.Enqueue(db, Kind, data);
    }

    /// <summary>«Subir archivo» de la ficha (#465): lo deja en su carpeta de `Entrada\` y lo importa como de ese juego.</summary>
    public static async Task<(long? Task, ApiError? Error)> Upload(Database db, long gameId, string name, Stream content, CancellationToken ct)
    {
        using (var c = db.Open())
            if (c.ExecuteScalar<long>("SELECT count(*) FROM game WHERE id = @gameId", new { gameId }) == 0) return (null, ApiError.Of("import.badGame"));
        name = Path.GetFileName(name ?? "");
        if (name.Length == 0 || name.StartsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return (null, ApiError.Of("import.badName"));
        if (Disk.DefaultDisk(db) is not { Connected: true } disk) return (null, ApiError.Of("library.notConnected"));

        // Se escribe fuera de `Entrada\`: el vigilante no lo ve a medias ni lo importa sin el juego.
        var temp = Path.Combine(Directory.CreateDirectory(Path.Combine(disk.Path, Uploading)).FullName, Path.GetRandomFileName());
        try
        {
            await using (var file = File.Create(temp)) await content.CopyToAsync(file, ct);
            App.FolderMove.FlushToDisk(temp);
            var stem = Path.GetFileNameWithoutExtension(name);
            var folder = Path.Combine(disk.Path, Disk.EntryFolder, stem);
            for (var k = 2; Directory.Exists(folder); k++) folder = Path.Combine(disk.Path, Disk.EntryFolder, $"{stem} ({k})");
            Directory.CreateDirectory(folder);
            File.Move(temp, Path.Combine(folder, name));
            return (EnqueueEntry(db, disk, folder, gameId), null);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    const string Uploading = ".subiendo";

    /// <summary>Lo están escribiendo (una copia en curso): se deja para cuando termine, que avisa el vigilante.</summary>
    static bool InUse(string file)
    {
        try
        {
            using var s = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return true; }
    }

    /// <param name="Title">El nombre que trae el juego en sus archivos de sistema (#240), si lo trae.</param>
    internal record Item(string Path, string? Console, string? TitleId, string? Component, string? Title = null);

    public static async Task Run(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<Job>(run.Data!, JsonSerializerOptions.Web)!;
        if (!Directory.Exists(job.Path)) throw new TaskException(ApiError.Of("path.notFound"));
        var db = run.Db;
        List<(string Id, string Name)> consoles;
        using (var c = db.Open()) consoles = c.Query<(string, string)>("SELECT id, name FROM console").ToList();
        var catalog = Scan.Catalog(db);
        var aliases = Scan.Aliases(db);

        var entryDisk = job.EntryDisk is null ? null : Disk.Get(db, job.EntryDisk);
        if (job.EntryDisk is not null && entryDisk is not { Connected: true }) throw new TaskException(ApiError.Of("library.notConnected"));
        // Lo que dejó a medias una extracción cortada no se importa: esa extracción se repite.
        var files = Directory.EnumerateFiles(job.Path, "*", new EnumerationOptions { RecurseSubdirectories = true })
            .Where(f => !f.Contains(Extracting + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            // Lo apartado (#237) no es juego: no se vuelve a importar.
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + SetAsideFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).ToList();
        if (job.Skip is { Count: > 0 } skip)
        {
            var skipped = skip.ToHashSet(StringComparer.OrdinalIgnoreCase);
            files = files.Where(f => !skipped.Contains(Path.GetRelativePath(job.Path, f))).ToList();
        }
        if (entryDisk is not null)
        {
            files = files.Where(f => !InUse(f)).ToList();
            // Lo que estaba esperando y ya no está (lo sacaste a mano) deja de esperar.
            using var c = db.Open();
            var prefix = Disk.EntryFolder + Path.DirectorySeparatorChar;
            foreach (var (id, relative) in c.Query<(long, string)>(
                         "SELECT id, relative_path FROM source WHERE kind = 'file' AND library_id = @Id AND substr(relative_path, 1, length(@prefix)) = @prefix", new { entryDisk.Id, prefix }).ToList())
                if (!File.Exists(Path.Combine(entryDisk.Path, relative))) c.Execute("DELETE FROM source WHERE id = @id", new { id });
            // Igual con los paquetes sin juegos que esperaban en Revisar (#310).
            foreach (var relative in c.Query<string>("SELECT json_extract(data, '$.relativePath') FROM review_item WHERE entity = 'entry' AND status = 'open' AND json_extract(data, '$.libraryId') = @Id",
                         new { entryDisk.Id }).ToList())
                if (!File.Exists(Path.Combine(entryDisk.Path, relative))) CloseReview(c, null, entryDisk.Id, relative, "approved");
        }

        var pass = new Pass(run, db, job, entryDisk, consoles, catalog, aliases,
            (run.Credentials.Read("archives.passwords") ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        await Place(pass, files.Where(f => !Archives.IsArchive(f)).ToList(), job.Path, null, ct);
        foreach (var main in files.Where(Archives.IsMain).ToList()) await Unpack(pass, main, ct);
        run.Log(ApiError.Of("import.log.summary", ("moved", pass.Moved.ToString()), ("skipped", pass.Skipped.ToString())));
        // El paquete bajado que ya se ordenó no deja su carpeta vacía en Entrada.
        if (entryDisk is not null && !Disk.Same(job.Path, Path.Combine(entryDisk.Path, Disk.EntryFolder)) && !Directory.EnumerateFileSystemEntries(job.Path).Any())
            Directory.Delete(job.Path);
        // Completa lo demás y manda a Claude lo que no se reconoció (#180). Sin nada nuevo no hay nada que escanear.
        if (pass.Moved + pass.Waiting > 0) Scan.Enqueue(db);
    }

    /// <summary>La carpeta temporal de una extracción, fuera de `Entrada\` para que el vigilante no la vea.</summary>
    const string Extracting = ".extrayendo";

    /// <summary>Lo que se comparte en una importación.</summary>
    sealed record Pass(TaskRun Run, Database Db, Job Job, DiskInfo? EntryDisk, List<(string Id, string Name)> Consoles,
        Dictionary<string, Dictionary<string, HashSet<long>>> Catalog, Dictionary<string, Dictionary<string, HashSet<long>>> Aliases, string[] Passwords)
    {
        public ScreenScraper Scraper { get; } = new(Run);
        public Dictionary<string, (long? Version, string Folder)> Bases { get; } = [];
        public HashSet<string> Taken { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int Moved { get; set; }
        public int Skipped { get; set; }
        public int Waiting { get; set; }
    }

    /// <summary>De dónde salieron los archivos que se están ubicando: el comprimido, su nombre y las carpetas de su camino.</summary>
    sealed record FromArchive(string Path, string Label, string[] Folders);

    /// <summary>
    /// Extrae un comprimido en un temporal y ubica sus juegos (§9.5, #230). El comprimido se borra solo si todo lo de adentro quedó en
    /// su lugar; si algo no se pudo ubicar, se queda entero y lo ya movido no se repite.
    /// </summary>
    static async Task Unpack(Pass p, string main, CancellationToken ct)
    {
        var parts = Archives.Parts(main);
        if (p.EntryDisk is not null && parts.Any(InUse)) return; // sigue llegando alguna parte
        var label = Label(main);
        var size = parts.Sum(f => new FileInfo(f).Length);
        if (p.EntryDisk is { } watched && AlreadyTried(p, watched, main, label, size))
        {
            p.Skipped++;
            return;
        }
        var target = p.EntryDisk is { } disk
            ? Path.Combine(disk.Path, Extracting, label)
            : Path.Combine(Path.GetDirectoryName(main)!, label + Extracting);
        p.Run.Report(0, ApiError.Of("import.extracting", ("path", main)));
        if (Archives.Extract(main, target, p.Passwords, ct) is { } error)
        {
            p.Run.Log(error, "warn");
            // Llegó dañado de una descarga: esa descarga queda con error, para volver a bajarla (#202). Una contraseña que falta no es eso.
            if (error.Code == "import.log.archiveFailed" && p.Job.DownloadTask is { } from) Downloads.MarkBroken(p.Db, from, main);
            p.Skipped++;
            RemoveExtracting(p);
            return;
        }
        try
        {
            var inner = Files(target);
            bool HasGames() => inner.Any(f => Scan.GameExtensions.Contains(Path.GetExtension(f)) && !Archives.IsArchive(f)) || FolderGame.Find(target).Count > 0;
            // Un comprimido dentro del paquete (Mario Kart World: un zip de 23 GB dentro del rar) se abre ahí mismo, hasta dos niveles.
            for (var level = 0; level < 2 && !HasGames(); level++)
            {
                var nested = inner.Where(Archives.IsMain).ToList();
                if (nested.Count == 0) break;
                foreach (var archive in nested)
                    if (Archives.Extract(archive, Path.Combine(Path.GetDirectoryName(archive)!, Label(archive)), p.Passwords, ct) is null)
                        foreach (var part in Archives.Parts(archive)) File.Delete(part);
                inner = Files(target);
            }
            var dumps = FolderGame.Find(target);
            if (!HasGames())
            {
                p.Run.Log(ApiError.Of("import.log.noGames", ("path", main)), "warn");
                if (p.EntryDisk is { } tried)
                {
                    Tried(p.Db, tried, main, size);
                    ToReview(p.Db, tried, main, label, inner.Select(f => Path.GetRelativePath(target, f)).ToList());
                }
                p.Skipped++;
                return;
            }
            var skipped = p.Skipped;
            var from = new FromArchive(main, label, Folders(Path.GetDirectoryName(main)!, p.Job.Path));
            var inDumps = inner.Where(f => dumps.Any(d => f.StartsWith(d.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))).ToHashSet(StringComparer.OrdinalIgnoreCase);
            await Place(p, inner.Where(f => !inDumps.Contains(f)).ToList(), target, from, ct);
            foreach (var dump in dumps) await PlaceDump(p, dump, from, ct);
            if (p.Skipped != skipped) return;
            SetAside(p, target, label, main);
            foreach (var part in parts) File.Delete(part);
            if (p.EntryDisk is { } entry)
            {
                RemoveRow(p.Db, entry, main);
                CloseReview(p.Db, entry.Id, Path.GetRelativePath(entry.Path, main));
            }
            RemoveEmptyFolders(Path.GetDirectoryName(main)!, p.Job.Path);
            p.Run.Log(ApiError.Of("import.log.archiveDone", ("path", main)));
        }
        finally
        {
            Directory.Delete(target, recursive: true);
            RemoveExtracting(p);
        }
    }

    static List<string> Files(string folder) => Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true }).ToList();

    /// <summary>
    /// Ya se intentó (#237): se descomprimió sin juegos y no cambió, o espera en Revisar y nadie lo resolvió (sin alias para su nombre). Así
    /// el vigilante de `Entrada\` no lo vuelve a descomprimir cada vez; «Importar carpeta» no pasa por acá.
    /// </summary>
    static bool AlreadyTried(Pass p, DiskInfo disk, string main, string label, long size)
    {
        var relative = Path.GetRelativePath(disk.Path, main);
        using var c = p.Db.Open();
        if (c.ExecuteScalar<long?>("SELECT size FROM entry_tried WHERE library_id = @Id AND relative_path = @relative", new { disk.Id, relative }) == size) return true;
        var waiting = c.QueryFirstOrDefault<string?>("SELECT console FROM source WHERE kind = 'file' AND library_id = @Id AND relative_path = @relative AND version_id IS NULL",
            new { disk.Id, relative });
        if (waiting is null) return false;
        var learned = p.Aliases.GetValueOrDefault(waiting) ?? [];
        return Scan.Match(learned, label) is null && Scan.Match(learned, Path.GetFileNameWithoutExtension(main)) is null;
    }

    const string TriedVersionSetting = "import.triedVersion";

    /// <summary>
    /// Con otra versión de Otteretro, lo ya intentado se olvida (#311, decisión 322): puede que ahora reconozca su formato. El vigilante de
    /// `Entrada\` lo vuelve a importar al arrancar, porque la encuentra con algo adentro.
    /// </summary>
    public static void RetryTriedOnNewVersion(Database db, string version)
    {
        using var c = db.Open();
        if (c.ExecuteScalar<string?>("SELECT value FROM setting WHERE machine_id = '' AND name = @TriedVersionSetting", new { TriedVersionSetting }) == version) return;
        c.Execute("DELETE FROM entry_tried");
        c.Execute("INSERT INTO setting (machine_id, name, value) VALUES ('', @TriedVersionSetting, @version) ON CONFLICT (name, machine_id) DO UPDATE SET value = excluded.value",
            new { TriedVersionSetting, version });
    }

    static void Tried(Database db, DiskInfo disk, string main, long size)
    {
        using var c = db.Open();
        c.Execute("INSERT INTO entry_tried (library_id, relative_path, size) VALUES (@Id, @relative, @size) ON CONFLICT DO UPDATE SET size = excluded.size",
            new { disk.Id, relative = Path.GetRelativePath(disk.Path, main), size });
    }

    /// <summary>Lo que trae un paquete en Revisar (#310, decisión 321): dónde está, su nombre y sus archivos.</summary>
    public record EntryReview(string LibraryId, string RelativePath, string Name, List<string> Files);

    /// <summary>
    /// Un paquete de `Entrada\` sin juegos queda en Revisar › Sin identificar (#310, decisión 321), con su nombre y sus archivos. Uno abierto por
    /// paquete: el nuevo reemplaza al anterior.
    /// </summary>
    static void ToReview(Database db, DiskInfo disk, string main, string label, List<string> files)
    {
        var relative = Path.GetRelativePath(disk.Path, main);
        using var c = db.Open();
        using var t = c.BeginTransaction();
        CloseReview(c, t, disk.Id, relative, "rejected");
        c.Execute("INSERT INTO review_item (kind, entity, data, created_at) VALUES ('unidentified', 'entry', @data, @now)", new
        {
            data = JsonSerializer.Serialize(new EntryReview(disk.Id, relative, label, files), JsonSerializerOptions.Web),
            now = DateTime.UtcNow.ToString("O"),
        }, t);
        t.Commit();
    }

    /// <summary>El paquete ya se importó (o ya no está): su pendiente de Revisar se resuelve.</summary>
    static void CloseReview(Database db, string libraryId, string relative)
    {
        using var c = db.Open();
        CloseReview(c, null, libraryId, relative, "approved");
    }

    static void CloseReview(System.Data.IDbConnection c, System.Data.IDbTransaction? t, string libraryId, string relative, string status) =>
        c.Execute("""
            UPDATE review_item SET status = @status WHERE entity = 'entry' AND status = 'open'
              AND json_extract(data, '$.libraryId') = @libraryId AND json_extract(data, '$.relativePath') = @relative
            """, new { status, libraryId, relative }, t);

    /// <summary>
    /// «Reintentar» de un paquete en Revisar (#310, decisión 322): olvida que ya se intentó y encola su carpeta de `Entrada\`. El pendiente se
    /// resuelve solo si esta vez se reconoce. Devuelve el error, o null.
    /// </summary>
    public static ApiError? Retry(Database db, long reviewId)
    {
        string? data;
        using (var c = db.Open())
            data = c.ExecuteScalar<string?>("SELECT data FROM review_item WHERE id = @reviewId AND entity = 'entry' AND status = 'open'", new { reviewId });
        if (data is null) return ApiError.Of("review.notFound");
        var item = JsonSerializer.Deserialize<EntryReview>(data, JsonSerializerOptions.Web)!;
        if (Disk.Get(db, item.LibraryId) is not { Connected: true } disk) return ApiError.Of("library.notConnected");
        var main = Path.Combine(disk.Path, item.RelativePath);
        if (!File.Exists(main))
        {
            CloseReview(db, item.LibraryId, item.RelativePath);
            return ApiError.Of("review.notFound");
        }
        using (var c = db.Open())
            c.Execute("DELETE FROM entry_tried WHERE library_id = @LibraryId AND relative_path = @RelativePath", item);
        var folder = Path.GetDirectoryName(main)!;
        EnqueueEntry(db, disk, Disk.Same(folder, Path.Combine(disk.Path, Disk.EntryFolder)) ? null : folder);
        return null;
    }

    /// <summary>
    /// Una carpeta extraída de Switch o Switch 2 (#237) va entera a `Juegos\&lt;Consola&gt;\&lt;Juego&gt;`, con su estructura: es lo que carga el emulador.
    /// El juego sale del TitleID (tabla de Switchbrew, sin IA), de un alias o del nombre del paquete; si no, espera en `Entrada\` para Revisar y Claude.
    /// </summary>
    static async Task PlaceDump(Pass p, FolderGame dump, FromArchive archive, CancellationToken ct)
    {
        var (run, db) = (p.Run, p.Db);
        if (dump.Console is not { } console)
        {
            run.Log(ApiError.Of("import.log.unknown", ("path", archive.Path)), "warn");
            p.Skipped++;
            return;
        }
        // Switch: la tabla de TitleID; PS3, Vita, PS4, PS5 y Wii U: su propio nombre o, si no, el de su serial en el DAT (#240).
        var title = dump.Name ?? (dump.TitleId is null ? null
            : console is "switch" or "switch2" ? await SwitchTitles.NameOf(db, dump.TitleId, ct) : Scan.DatBySerial(db, console, dump.TitleId));
        var versions = p.Catalog.GetValueOrDefault(console) ?? [];
        var learned = p.Aliases.GetValueOrDefault(console) ?? [];
        var names = new[] { archive.Label, Path.GetFileNameWithoutExtension(archive.Path), Path.GetFileName(dump.Root) };
        var version = names.Select(n => Scan.Match(learned, n)).FirstOrDefault(v => v is not null)
            ?? (dump.TitleId is null ? null : Scan.VersionByCode(db, console, dump.TitleId)) // el código del catálogo (#249)
            ?? (title is null ? null : Scan.Match(versions, title))
            ?? names.Select(n => Scan.Match(versions, n)).FirstOrDefault(v => v is not null)
            ?? VersionOfGame(db, p.Job.GameId, console);
        var item = new Item(archive.Path, console, dump.TitleId, "BASE");
        var size = FolderGame.SizeOf(dump.Root);

        if (p.EntryDisk is { } entry && version is null)
        {
            var waiting = Path.GetRelativePath(entry.Path, archive.Path);
            Register(db, entry.Id, waiting, console, item, null, null, false, new FileInfo(archive.Path).Length);
            SetDownloadTask(db, entry.Id, waiting, p.Job.DownloadTask ?? DownloadTaskOf(db, entry.Id, waiting));
            run.Log(ApiError.Of("import.log.waiting", ("path", archive.Path)));
            p.Skipped++;
            p.Waiting++;
            return;
        }

        var disk = p.EntryDisk ?? DiskOf(db, console);
        var relative = Path.Combine(ConsoleFolder(db, disk, console, p.Consoles), GameFolder(db, version, title, archive.Label));
        var destination = Path.Combine(disk.Path, relative);
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
        {
            run.Log(ApiError.Of("import.log.duplicate", ("path", archive.Path)));
            p.Skipped++;
            return;
        }
        if (!Mounts.Same(dump.Root, disk.Path) && disk.Free is { } free && free < size)
        {
            run.Log(ApiError.Of("import.log.noSpace", ("path", archive.Path), ("disk", disk.Name)), "warn");
            p.Skipped++;
            return;
        }
        // Archivo por archivo: copiar, verificar y recién entonces borrar (como todo lo que se mueve).
        foreach (var file in Files(dump.Root)) MoveIn(file, Path.Combine(destination, Path.GetRelativePath(dump.Root, file)));
        Register(db, disk.Id, relative, console, item, version, null, false, size);
        SetDownloadTask(db, disk.Id, relative, p.Job.DownloadTask ?? (p.EntryDisk is { } origin ? DownloadTaskOf(db, origin.Id, Path.GetRelativePath(origin.Path, archive.Path)) : null));
        if (p.EntryDisk is { } mine) RemoveRow(db, mine, archive.Path);
        run.Log(ApiError.Of("import.log.moved", ("path", archive.Path), ("to", destination)));
        p.Moved++;
    }

    /// <summary>
    /// Lo que quedó del paquete y no es juego (un emulador, un léeme) va a `Entrada\Apartado\&lt;paquete&gt;` y se avisa, sin borrarlo (#237,
    /// decisión 277): qué hacer con un emulador que viene con el juego se decide con los emuladores.
    /// </summary>
    static void SetAside(Pass p, string target, string label, string main)
    {
        var left = Files(target).Where(f => !Path.GetFileName(f).StartsWith('.')).ToList();
        // Un léeme, una portada o una suma de verificación no hace falta guardarlos: se van con el temporal, como siempre.
        if (left.All(f => Notes.Contains(Path.GetExtension(f)))) return;
        var aside = Path.Combine(p.EntryDisk?.Path is { } disk ? Path.Combine(disk, Disk.EntryFolder) : Path.GetDirectoryName(main)!, SetAsideFolder, label);
        foreach (var file in left) MoveIn(file, Path.Combine(aside, Path.GetRelativePath(target, file)));
        p.Run.Log(ApiError.Of("import.log.setAside", ("path", main), ("to", aside)), "warn");
    }

    static readonly HashSet<string> Notes = new(StringComparer.OrdinalIgnoreCase) { ".txt", ".nfo", ".diz", ".url", ".html", ".htm", ".jpg", ".jpeg", ".png", ".sfv", ".md5", ".sha1" };

    /// <summary>Dentro de `Entrada\`: lo que llegó con un juego y no es juego. El vigilante no lo vuelve a importar.</summary>
    public const string SetAsideFolder = "Apartado";

    /// <summary>La carpeta de las extracciones de `Entrada\`, cuando ya no tiene nada.</summary>
    static void RemoveExtracting(Pass p)
    {
        if (p.EntryDisk is not { } disk) return;
        var folder = Path.Combine(disk.Path, Extracting);
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
    }

    /// <summary>El nombre de un comprimido sin su extensión ni su número de parte.</summary>
    internal static string Label(string main) => ArchiveSuffix().Replace(Path.GetFileName(main), "");

    /// <summary>Las carpetas del camino desde la elegida, incluida ella: una que se llama como una consola la dice.</summary>
    internal static string[] Folders(string folder, string root) =>
        Path.GetRelativePath(root, folder).Split(Path.DirectorySeparatorChar).Prepend(Path.GetFileName(root)).Reverse().ToArray();

    static void RemoveRow(Database db, DiskInfo disk, string path)
    {
        using var c = db.Open();
        c.Execute("DELETE FROM source WHERE kind = 'file' AND library_id = @Id AND relative_path = @relative", new { disk.Id, relative = Path.GetRelativePath(disk.Path, path) });
    }

    /// <summary>Identifica y mueve cada archivo de juego de <paramref name="files"/>.</summary>
    static async Task Place(Pass p, List<string> files, string root, FromArchive? archive, CancellationToken ct)
    {
        var (run, db) = (p.Run, p.Db);
        var extra = archive?.Folders ?? [];
        var items = files.Where(f => Scan.GameExtensions.Contains(Path.GetExtension(f)) && !Archives.IsArchive(f))
            .Select(f => Classify(f, Folders(Path.GetDirectoryName(f)!, root).Concat(extra), p.Consoles))
            // Primero lo que lleva otros archivos, y el BASE antes que sus UPD y DLC, que van a su carpeta.
            .OrderBy(i => Path.GetExtension(i.Path).ToLowerInvariant() switch { ".m3u" => 0, ".cue" => 1, _ => 2 })
            .ThenBy(i => i.Component switch { "BASE" => 0, null => 1, _ => 2 })
            .ThenBy(i => i.Path, StringComparer.OrdinalIgnoreCase).ToList();

        for (var i = 0; i < items.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = items[i];
            if (!p.Taken.Add(item.Path)) continue; // ya viajó con su .cue o su .m3u
            run.Report(i * 100 / items.Count, ApiError.Of("import.file", ("path", item.Path)));

            var (sha1, datConsole, datName) = await Hash(run, item, ct);
            if ((item.Console ?? datConsole) is not { } console)
            {
                run.Log(ApiError.Of("import.log.unknown", ("path", archive?.Path ?? item.Path)), "warn");
                p.Skipped++;
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(item.Path);
            var parent = Path.GetDirectoryName(item.Path)!;
            var folder = Disk.Same(parent, root) ? archive?.Label : Path.GetFileName(parent);
            var versions = p.Catalog.GetValueOrDefault(console) ?? [];
            var learned = p.Aliases.GetValueOrDefault(console) ?? [];
            var baseId = item.Component is null ? null : Scan.BaseOf(item.TitleId!);
            (long? Version, string Folder)? ofBase = baseId is not null && p.Bases.TryGetValue(baseId, out var b) ? b : null;
            // Primero lo que corregiste (alias) y el código que publica el catálogo (#249); después, el nombre.
            var byCode = item.TitleId is { } code ? Scan.VersionByCode(db, console, item.Component is null or "BASE" ? code : Scan.BaseOf(code)) : null;
            var version = Scan.Match(learned, name) ?? byCode ?? Scan.Match(versions, name) ?? (folder is null ? null : Scan.Match(learned, folder) ?? Scan.Match(versions, folder))
                ?? (datName is null ? null : Scan.Match(versions, datName)) ?? ofBase?.Version ?? VersionOfBase(db, baseId) ?? VersionOfGame(db, p.Job.GameId, console);

            // Switch y Switch 2 (#241): el TitleID del nombre contra titledb o Switchbrew, sin IA. Un UPD o un DLC busca el nombre de su base.
            if (version is null && item.TitleId is { } tid && console is "switch" or "switch2"
                && await SwitchTitles.NameOf(db, item.Component is null ? tid : Scan.BaseOf(tid), ct) is { } title)
                version = Scan.Match(learned, title) ?? Scan.Match(versions, title);

            // El nombre o el serial que trae el juego (#240): el serial se busca en el DAT de su consola, sin IA.
            if (version is null && item.Title is { } own) version = Scan.Match(learned, own) ?? Scan.Match(versions, own);
            if (version is null && item.TitleId is { } serial && console is not ("switch" or "switch2") && Scan.DatBySerial(db, console, serial) is { } fromDat)
                version = Scan.Match(versions, fromDat);

            // Lo que no coincide con nada se le pregunta a ScreenScraper, antes que a Claude (§9.6).
            if (version is null && item.Component is null)
                foreach (var known in await p.Scraper.NamesOf(console, item.Path, sha1, ct))
                    if ((version = Scan.Match(versions, known)) is not null) break;

            // Lo que llega solo se mueve si se reconoció con seguridad (#222); lo demás espera acá, y Revisar y Claude lo ven (#180).
            if (p.EntryDisk is { } entry && version is null)
            {
                var waits = archive?.Path ?? item.Path;
                var waiting = Path.GetRelativePath(entry.Path, waits);
                var from = p.Job.DownloadTask ?? DownloadTaskOf(db, entry.Id, waiting);
                Register(db, entry.Id, waiting, console, item, null, archive is null ? sha1 : null, false, new FileInfo(waits).Length);
                SetDownloadTask(db, entry.Id, waiting, from);
                run.Log(ApiError.Of("import.log.waiting", ("path", waits)));
                p.Skipped++;
                p.Waiting++;
                continue;
            }

            // Lo que llega solo no trae hacks, betas ni demos (§9.4); en la vista previa lo decides tú.
            if (p.EntryDisk is not null && Megapack.IsExtra(archive?.Label ?? name))
            {
                run.Log(ApiError.Of("import.log.extra", ("path", archive?.Path ?? item.Path)));
                p.Skipped++;
                continue;
            }

            // Duplicados (§9.4): si ya lo tienes se salta, salvo que el nuevo esté verificado y el tuyo no.
            if (version is not null && item.Component is null && Megapack.DiscOf(name) is null && Owned(db, version.Value, item.Path) is { } owned)
            {
                if (datName is null || owned.Verified)
                {
                    run.Log(ApiError.Of("import.log.duplicate", ("path", archive?.Path ?? item.Path)));
                    p.Skipped++;
                    continue;
                }
                // Reemplazar: el tuyo va a la papelera, para poder recuperarlo. Si no se puede, los dos se quedan como están.
                if (Disk.TrashSources(db, [owned.Id]) is { } trashError)
                {
                    run.Log(trashError, "warn");
                    p.Skipped++;
                    continue;
                }
                run.Log(ApiError.Of("import.log.replaced", ("path", item.Path)));
            }

            var disk = p.EntryDisk ?? DiskOf(db, console);
            var gameFolder = ofBase?.Folder ?? GameFolder(db, version, folder, name);
            if (baseId is not null) p.Bases.TryAdd(baseId, (version, gameFolder));
            var relative = Path.Combine(ConsoleFolder(db, disk, console, p.Consoles), gameFolder);

            // El archivo y los que necesita (los .bin de un .cue, los discos de un .m3u), con la misma ruta relativa entre ellos.
            var group = Companions(item.Path).Where(f => !Disk.Same(f, item.Path)).Prepend(item.Path).ToList();
            var targets = group.Select(f => (From: f, To: Path.Combine(disk.Path, relative, Path.GetRelativePath(parent, f)))).ToList();
            p.Taken.UnionWith(group);
            // Un archivo distinto con ese nombre no se pisa; decidir qué hacer con un duplicado es de los megapacks (§9.4).
            if (targets.FirstOrDefault(t => File.Exists(t.To) && !FolderMove.SameContent(t.From, t.To)) is { To: not null } conflict)
            {
                run.Log(ApiError.Of("import.log.conflict", ("path", conflict.From), ("to", conflict.To)), "warn");
                p.Skipped++;
                continue;
            }
            var size = new FileInfo(item.Path).Length;
            if (!Mounts.Same(item.Path, disk.Path) && disk.Free is { } free && free < group.Sum(f => new FileInfo(f).Length))
            {
                run.Log(ApiError.Of("import.log.noSpace", ("path", item.Path), ("disk", disk.Name)), "warn");
                p.Skipped++;
                continue;
            }
            foreach (var (from, to) in targets) MoveIn(from, to);
            var placed = Path.GetRelativePath(disk.Path, targets[0].To);
            var chdOk = Path.GetExtension(targets[0].To).Equals(".chd", StringComparison.OrdinalIgnoreCase) && await Converter.ChdIntact(db, targets[0].To, ct) == true;
            // Lo que esperó en Entrada se acuerda de su descarga en su fila, si esta pasada no viene de ella.
            var download = p.Job.DownloadTask ?? (p.EntryDisk is { } origin ? DownloadTaskOf(db, origin.Id, Path.GetRelativePath(origin.Path, archive?.Path ?? item.Path)) : null);
            Register(db, disk.Id, placed, console, item, version, sha1, datName is not null || chdOk, size);
            SetDownloadTask(db, disk.Id, placed, download);
            if (p.EntryDisk is { } mine) RemoveRow(db, mine, item.Path);
            if (archive is null)
                foreach (var (from, _) in targets) RemoveEmptyFolders(Path.GetDirectoryName(from)!, root);
            run.Log(ApiError.Of("import.log.moved", ("path", item.Path), ("to", targets[0].To)));
            p.Moved++;
            long id;
            using (var c = db.Open())
                id = c.ExecuteScalar<long>("SELECT id FROM source WHERE kind = 'file' AND library_id = @Id AND relative_path = @placed", new { disk.Id, placed });
            await Converter.Convert(run, id, ct); // el formato liviano, al importar (#233)
        }
    }

    static long? DownloadTaskOf(Database db, string disk, string relative)
    {
        using var c = db.Open();
        return c.ExecuteScalar<long?>("SELECT download_task FROM source WHERE kind = 'file' AND library_id = @disk AND relative_path = @relative", new { disk, relative });
    }

    static void SetDownloadTask(Database db, string disk, string relative, long? task)
    {
        if (task is null) return;
        using var c = db.Open();
        c.Execute("UPDATE source SET download_task = @task WHERE kind = 'file' AND library_id = @disk AND relative_path = @relative", new { task, disk, relative });
    }

    /// <summary>Copiar, forzar a disco y verificar, y recién entonces borrar. Un corte deja el origen entero: la tarea se repite sin daño.</summary>
    public static void MoveIn(string from, string to, bool forceCopy = false)
    {
        if (File.Exists(to))
        {
            // Ya está y es igual: la tarea se cortó antes de borrar el origen. Si es distinto, nunca se pisa ni se borra.
            if (!FolderMove.SameContent(from, to)) throw new TaskException(ApiError.Of("library.conflict", ("path", to)));
            File.Delete(from);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        if (!forceCopy && Mounts.Same(from, to))
        {
            File.Move(from, to);
            return;
        }
        var part = to + ".parte";
        File.Copy(from, part, overwrite: true);
        FolderMove.FlushToDisk(part);
        if (!FolderMove.SameContent(from, part))
        {
            File.Delete(part);
            throw new TaskException(ApiError.Of("move.copyMismatch", ("path", from), ("detail", Path.GetFileName(from))));
        }
        File.Move(part, to);
        File.Delete(from);
    }

    /// <summary>La consola por una carpeta del camino o por la extensión; null si eso no alcanza.</summary>
    internal static string? ConsoleOf(string file, IEnumerable<string> folders, List<(string Id, string Name)> consoles) =>
        folders.Select(s => consoles.FirstOrDefault(k => string.Equals(k.Name, s, StringComparison.OrdinalIgnoreCase)
                || string.Equals(k.Id, s, StringComparison.OrdinalIgnoreCase)).Id).FirstOrDefault(id => id is not null)
            ?? ByExtension.GetValueOrDefault(Path.GetExtension(file));

    internal static Item Classify(string file, IEnumerable<string> folders, List<(string Id, string Name)> consoles)
    {
        // Una carpeta del camino que se llama como una consola manda, también la que se eligió («…\GameCube»).
        var console = ConsoleOf(file, folders, consoles);
        var serial = Scan.Serial(file);
        // El serial de un disco: G y D son de GameCube; R y S, de Wii.
        console ??= serial?[0] switch { 'G' or 'D' => "gc", 'R' or 'S' => "wii", _ => null };
        // Lo que el juego dice de sí mismo (#240): PlayStation, 3DS, DS, GBA, N64. Una carpeta con nombre de consola sigue mandando.
        if (serial is null && console is not ("switch" or "switch2") && GameIds.FromFile(file) is { } id)
            return new(file, console ?? id.Console, id.Serial, null, id.Name);
        if (console != "switch") return new(file, console, serial, null);
        var name = Path.GetFileNameWithoutExtension(file);
        var titleId = Scan.TitleIdInName().Match(name) is { Success: true } m ? m.Groups[1].Value.ToUpperInvariant() : null;
        if (titleId?.StartsWith("04") == true) console = "switch2"; // la misma extensión, otra consola (#249)
        return new(file, console, titleId, titleId is null ? null : Scan.ComponentOf(titleId));
    }

    /// <summary>
    /// El SHA-1 contra el DAT de su consola (lo baja si falta) y, si no se sabe la consola, contra los DAT que ya están.
    /// </summary>
    /// <remarks>ponytail: un .iso de PS2 sin carpeta que lo diga solo se reconoce con su DAT ya bajado; bajarlos todos si hace falta.</remarks>
    static async Task<(string? Sha1, string? Console, string? Name)> Hash(TaskRun run, Item item, CancellationToken ct)
    {
        if (item.Console is { } known)
        {
            if (!Dats.Files.ContainsKey(known)) return (null, null, null);
            using (var c = run.Db.Open())
                if (c.ExecuteScalar<long>("SELECT count(*) FROM dat_entry WHERE console = @known", new { known }) == 0)
                    try { await Dats.Download(run.Db, known, ct); }
                    catch (HttpRequestException) { run.Log(ApiError.Of("import.log.noDat", ("console", known)), "warn"); }
        }
        var sha1 = await Converter.Sha1Of(run.Db, item.Path, ct);
        using var db = run.Db.Open();
        var match = db.QueryFirstOrDefault<(string Console, string Name)?>(
            "SELECT console, name FROM dat_entry WHERE sha1 = @sha1 AND (@known IS NULL OR console = @known)", new { sha1, known = item.Console });
        return (sha1, match?.Console, match?.Name);
    }

    /// <summary>Lo que ya tienes de esa versión, sin contar lo que espera en `Entrada\\`.</summary>
    static (long Id, bool Verified)? Owned(Database db, long version, string path)
    {
        using var c = db.Open();
        return c.QueryFirstOrDefault<(long, bool)?>("""
            SELECT id, verified FROM source WHERE kind = 'file' AND version_id = @version AND component IS NULL AND substr(relative_path, 1, 8) <> 'Entrada\\'
            ORDER BY verified DESC LIMIT 1
            """, new { version });
    }

    static long? VersionOfGame(Database db, long? gameId, string console)
    {
        if (gameId is null) return null;
        using var c = db.Open();
        return c.QueryFirstOrDefault<long?>("SELECT id FROM version WHERE game_id = @gameId AND platform = @console", new { gameId, console });
    }

    static long? VersionOfBase(Database db, string? baseId)
    {
        if (baseId is null) return null;
        using var c = db.Open();
        return c.QueryFirstOrDefault<long?>("SELECT version_id FROM source WHERE title_id = @baseId AND version_id IS NOT NULL", new { baseId });
    }

    /// <summary>El disco que eligió la consola para lo nuevo (#195) o el ★.</summary>
    static DiskInfo DiskOf(Database db, string console)
    {
        using var c = db.Open();
        var chosen = c.QueryFirstOrDefault<string?>("SELECT library_id FROM console_destination WHERE console_id = @console", new { console });
        if (chosen is not null && Disk.Get(db, chosen) is { Connected: true, Type: "disk" } disk) return disk;
        return Disk.DefaultDisk(db) is { Connected: true } home ? home : throw new TaskException(ApiError.Of("library.notConnected"));
    }

    /// <summary>La carpeta de la consola en ese disco, relativa: la que ya tenga o una nueva con su nombre.</summary>
    static string ConsoleFolder(Database db, DiskInfo disk, string console, List<(string Id, string Name)> consoles)
    {
        if (Disk.ConsoleFolders(db).FirstOrDefault(f => f.LibraryId == disk.Id && f.Console == console) is { } found) return found.Relative;
        var name = consoles.First(k => k.Id == console).Name;
        return Path.Combine(Disk.DiskFolders["game"], name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? console : name);
    }

    /// <summary>La carpeta del juego: su nombre del catálogo; si no se reconoció, la carpeta de donde venía o el nombre del archivo.</summary>
    static string GameFolder(Database db, long? version, string? folder, string name)
    {
        string? game = null;
        if (version is not null)
        {
            using var c = db.Open();
            game = c.QueryFirstOrDefault<string?>("SELECT g.name_es FROM version v JOIN game g ON g.id = v.game_id WHERE v.id = @version", new { version });
        }
        var clean = Invalid().Replace((game ?? folder ?? name).Replace(":", " -"), "").Trim(' ', '.');
        return clean.Length > 0 ? clean : "Juego";
    }

    /// <summary>Lo que nombra un .cue o un .m3u, solo si está en su misma carpeta o debajo.</summary>
    internal static List<string> Companions(string file)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext is not (".cue" or ".m3u")) return [];
        var folder = Path.GetDirectoryName(file)!;
        var names = File.ReadAllLines(file).Select(line => ext == ".m3u" ? line.Trim() : CueFile().Match(line) is { Success: true } m ? m.Groups[1].Value : "")
            .Where(n => n.Length > 0 && !n.StartsWith('#'));
        var found = new List<string>();
        foreach (var name in names)
        {
            var path = Path.GetFullPath(Path.Combine(folder, name));
            if (!Disk.Inside(path, folder) || !File.Exists(path) || found.Contains(path, StringComparer.OrdinalIgnoreCase)) continue;
            found.Add(path);
            if (ext == ".m3u") found.AddRange(Companions(path).Where(p => !found.Contains(p, StringComparer.OrdinalIgnoreCase)));
        }
        return found;
    }

    static void Register(Database db, string diskId, string relative, string console, Item item, long? version, string? sha1, bool verified, long size)
    {
        using var c = db.Open();
        c.Execute("""
            INSERT INTO source (kind, library_id, relative_path, console, size, component, title_id, version_id, sha1, verified)
            VALUES ('file', @diskId, @relative, @console, @size, @Component, @TitleId, @version, @sha1, @verified)
            ON CONFLICT (library_id, relative_path) WHERE kind = 'file' DO UPDATE SET
                version_id = coalesce(source.version_id, excluded.version_id), sha1 = excluded.sha1, verified = excluded.verified
            """, new { diskId, relative, console, size, item.Component, item.TitleId, version, sha1, verified });
    }

    /// <summary>Quita las carpetas que quedaron vacías, hasta la que se eligió, que se queda.</summary>
    static void RemoveEmptyFolders(string folder, string root)
    {
        for (var dir = folder; Disk.Inside(dir, root) && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any(); dir = Path.GetDirectoryName(dir)!)
            Directory.Delete(dir);
    }

    /// <summary>Lo que se le quita al nombre de un comprimido para quedarse con el del juego.</summary>
    [GeneratedRegex(@"(\.part0*1\.rar|\.7z\.001|\.zip|\.7z|\.rar)$", RegexOptions.IgnoreCase)]
    private static partial Regex ArchiveSuffix();

    [GeneratedRegex("""FILE\s+"?([^"]+?)"?\s+\w+\s*$""", RegexOptions.IgnoreCase)]
    private static partial Regex CueFile();

    [GeneratedRegex("""[<>"/\\|?*\x00-\x1f]""")]
    private static partial Regex Invalid();
}
