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
    public record Job(string Path, string? EntryDisk = null);

    /// <summary>Las extensiones que dicen la consola. Las de disco (iso, chd, cue…) no: esas salen de su cabecera, su carpeta o su hash.</summary>
    static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".nsp"] = "switch",
        [".nsz"] = "switch",
        [".xci"] = "switch",
        [".xcz"] = "switch",
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
    };

    /// <summary>Valida la carpeta y encola. No se importa desde las carpetas de un disco ni desde una que contenga un disco.</summary>
    public static (long? Task, ApiError? Error) Enqueue(Database db, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return (null, ApiError.Of("path.notAbsolute"));
        if (!Directory.Exists(path)) return (null, ApiError.Of("path.notFound"));
        foreach (var d in Disk.List(db))
        {
            var mine = d.Type == "disk"
                ? Disk.DiskFolders.Values.Select(f => Path.Combine(d.Path, f)).Any(f => Disk.Same(path, f) || Disk.Inside(path, f))
                : Disk.Inside(path, d.Path);
            if (mine || Disk.Same(path, d.Path) || Disk.Inside(d.Path, path)) return (null, ApiError.Of("import.overlapsDisk", ("name", d.Name)));
        }
        return (TaskQueue.Enqueue(db, Kind, JsonSerializer.Serialize(new Job(Disk.Normalize(path)), JsonSerializerOptions.Web)), null);
    }

    /// <summary>Encola la importación de la `Entrada\` de un disco, salvo que ya haya una esperando.</summary>
    public static long EnqueueEntry(Database db, DiskInfo disk)
    {
        var data = JsonSerializer.Serialize(new Job(Disk.Normalize(Path.Combine(disk.Path, Disk.EntryFolder)), disk.Id), JsonSerializerOptions.Web);
        using (var c = db.Open())
            if (c.ExecuteScalar<long?>("SELECT id FROM task WHERE kind = @Kind AND status = 'queued' AND data = @data LIMIT 1", new { Kind, data }) is { } waiting)
                return waiting;
        return TaskQueue.Enqueue(db, Kind, data);
    }

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

    record Item(string Path, string? Console, string? TitleId, string? Component);

    public static async Task Run(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<Job>(run.Data!, JsonSerializerOptions.Web)!;
        if (!Directory.Exists(job.Path)) throw new TaskException(ApiError.Of("path.notFound"));
        var db = run.Db;
        List<(string Id, string Name)> consoles;
        using (var c = db.Open()) consoles = c.Query<(string, string)>("SELECT id, name FROM console").ToList();
        var catalog = Scan.Catalog(db);
        var aliases = Scan.Aliases(db);
        var keys = SwitchTitle.HeaderKey(Scan.SystemFolders(db, Disk.List(db)));

        var files = Directory.EnumerateFiles(job.Path, "*", new EnumerationOptions { RecurseSubdirectories = true }).ToList();
        var entryDisk = job.EntryDisk is null ? null : Disk.Get(db, job.EntryDisk);
        if (job.EntryDisk is not null && entryDisk is not { Connected: true }) throw new TaskException(ApiError.Of("library.notConnected"));
        if (entryDisk is not null)
        {
            files = files.Where(f => !InUse(f)).ToList();
            // Lo que estaba esperando y ya no está (lo sacaste a mano) deja de esperar.
            using var c = db.Open();
            var prefix = Disk.EntryFolder + Path.DirectorySeparatorChar;
            foreach (var (id, relative) in c.Query<(long, string)>(
                         "SELECT id, relative_path FROM source WHERE kind = 'file' AND library_id = @Id AND substr(relative_path, 1, length(@prefix)) = @prefix", new { entryDisk.Id, prefix }).ToList())
                if (!File.Exists(Path.Combine(entryDisk.Path, relative))) c.Execute("DELETE FROM source WHERE id = @id", new { id });
        }
        foreach (var archive in files.Where(f => Path.GetExtension(f).ToLowerInvariant() is ".zip" or ".7z" or ".rar"))
            run.Log(ApiError.Of("import.log.archive", ("path", archive)), "warn");
        var items = files.Where(f => Scan.GameExtensions.Contains(Path.GetExtension(f)) && Path.GetExtension(f).ToLowerInvariant() is not (".zip" or ".7z"))
            .Select(f => Classify(f, job.Path, consoles, keys))
            // Primero lo que lleva otros archivos, y el BASE antes que sus UPD y DLC, que van a su carpeta.
            .OrderBy(i => Path.GetExtension(i.Path).ToLowerInvariant() switch { ".m3u" => 0, ".cue" => 1, _ => 2 })
            .ThenBy(i => i.Component switch { "BASE" => 0, null => 1, _ => 2 })
            .ThenBy(i => i.Path, StringComparer.OrdinalIgnoreCase).ToList();

        var bases = new Dictionary<string, (long? Version, string Folder)>();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int moved = 0, skipped = 0, waiting = 0;
        for (var i = 0; i < items.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = items[i];
            if (!taken.Add(item.Path)) continue; // ya viajó con su .cue o su .m3u
            run.Report(i * 100 / items.Count, ApiError.Of("import.file", ("path", item.Path)));

            var (sha1, datConsole, datName) = await Hash(run, item, ct);
            if ((item.Console ?? datConsole) is not { } console)
            {
                run.Log(ApiError.Of("import.log.unknown", ("path", item.Path)), "warn");
                skipped++;
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(item.Path);
            var parent = Path.GetDirectoryName(item.Path)!;
            var folder = Disk.Same(parent, job.Path) ? null : Path.GetFileName(parent);
            var versions = catalog.GetValueOrDefault(console) ?? [];
            var learned = aliases.GetValueOrDefault(console) ?? [];
            var baseId = item.Component is null ? null : Scan.BaseOf(item.TitleId!);
            (long? Version, string Folder)? ofBase = baseId is not null && bases.TryGetValue(baseId, out var b) ? b : null;
            var version = Scan.Match(learned, name) ?? Scan.Match(versions, name) ?? (folder is null ? null : Scan.Match(versions, folder))
                ?? (datName is null ? null : Scan.Match(versions, datName)) ?? ofBase?.Version ?? VersionOfBase(db, baseId);

            // Lo que llega solo se mueve si se reconoció con seguridad (#222); lo demás espera acá, y Revisar y Claude lo ven (#180).
            if (entryDisk is not null && version is null)
            {
                Register(db, entryDisk.Id, Path.GetRelativePath(entryDisk.Path, item.Path), console, item, null, sha1, false, new FileInfo(item.Path).Length);
                run.Log(ApiError.Of("import.log.waiting", ("path", item.Path)));
                skipped++;
                waiting++;
                continue;
            }

            var disk = entryDisk ?? DiskOf(db, console);
            var gameFolder = ofBase?.Folder ?? GameFolder(db, version, folder, name);
            if (baseId is not null) bases.TryAdd(baseId, (version, gameFolder));
            var relative = Path.Combine(ConsoleFolder(db, disk, console, consoles), gameFolder);

            // El archivo y los que necesita (los .bin de un .cue, los discos de un .m3u), con la misma ruta relativa entre ellos.
            var group = Companions(item.Path).Where(p => !Disk.Same(p, item.Path)).Prepend(item.Path).ToList();
            var targets = group.Select(p => (From: p, To: Path.Combine(disk.Path, relative, Path.GetRelativePath(parent, p)))).ToList();
            taken.UnionWith(group);
            // Un archivo distinto con ese nombre no se pisa; decidir qué hacer con un duplicado es de los megapacks (§9.4).
            if (targets.FirstOrDefault(t => File.Exists(t.To) && !FolderMove.SameContent(t.From, t.To)) is { To: not null } conflict)
            {
                run.Log(ApiError.Of("import.log.conflict", ("path", conflict.From), ("to", conflict.To)), "warn");
                skipped++;
                continue;
            }
            var size = new FileInfo(item.Path).Length;
            if (!Mounts.Same(item.Path, disk.Path) && disk.Free is { } free && free < group.Sum(p => new FileInfo(p).Length))
            {
                run.Log(ApiError.Of("import.log.noSpace", ("path", item.Path), ("disk", disk.Name)), "warn");
                skipped++;
                continue;
            }
            foreach (var (from, to) in targets) MoveIn(from, to);
            Register(db, disk.Id, Path.GetRelativePath(disk.Path, targets[0].To), console, item, version, sha1, datName is not null, size);
            if (entryDisk is not null)
                using (var c = db.Open())
                    c.Execute("DELETE FROM source WHERE kind = 'file' AND library_id = @Id AND relative_path = @relative",
                        new { entryDisk.Id, relative = Path.GetRelativePath(entryDisk.Path, item.Path) });
            foreach (var (from, _) in targets) RemoveEmptyFolders(Path.GetDirectoryName(from)!, job.Path);
            run.Log(ApiError.Of("import.log.moved", ("path", item.Path), ("to", targets[0].To)));
            moved++;
        }
        run.Log(ApiError.Of("import.log.summary", ("moved", moved.ToString()), ("skipped", skipped.ToString())));
        // Completa lo demás y manda a Claude lo que no se reconoció (#180). Sin nada nuevo no hay nada que escanear.
        if (moved + waiting > 0) Scan.Enqueue(db);
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

    static Item Classify(string file, string root, List<(string Id, string Name)> consoles, byte[]? keys)
    {
        var ext = Path.GetExtension(file);
        // Una carpeta del camino que se llama como una consola manda, también la que se eligió («…\GameCube»).
        var segments = Path.GetRelativePath(root, Path.GetDirectoryName(file)!).Split(Path.DirectorySeparatorChar).Prepend(Path.GetFileName(root));
        var console = segments.Reverse().Select(s => consoles.FirstOrDefault(k => string.Equals(k.Name, s, StringComparison.OrdinalIgnoreCase)
                || string.Equals(k.Id, s, StringComparison.OrdinalIgnoreCase)).Id).FirstOrDefault(id => id is not null)
            ?? ByExtension.GetValueOrDefault(ext);
        var serial = Scan.Serial(file);
        // El serial de un disco: G y D son de GameCube; R y S, de Wii.
        console ??= serial?[0] switch { 'G' or 'D' => "gc", 'R' or 'S' => "wii", _ => null };
        if (console != "switch") return new(file, console, serial, null);
        var name = Path.GetFileNameWithoutExtension(file);
        var titleId = Scan.TitleIdInName().Match(name) is { Success: true } m ? m.Groups[1].Value.ToUpperInvariant() : SwitchTitle.Read(file, keys);
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
        var sha1 = await Dats.Sha1(item.Path, ct);
        using var db = run.Db.Open();
        var match = db.QueryFirstOrDefault<(string Console, string Name)?>(
            "SELECT console, name FROM dat_entry WHERE sha1 = @sha1 AND (@known IS NULL OR console = @known)", new { sha1, known = item.Console });
        return (sha1, match?.Console, match?.Name);
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
    static List<string> Companions(string file)
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

    [GeneratedRegex("""FILE\s+"?([^"]+?)"?\s+\w+\s*$""", RegexOptions.IgnoreCase)]
    private static partial Regex CueFile();

    [GeneratedRegex("""[<>"/\\|?*\x00-\x1f]""")]
    private static partial Regex Invalid();
}
