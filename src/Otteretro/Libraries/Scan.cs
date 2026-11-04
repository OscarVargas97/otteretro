using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>
/// Escanear (§7.2, #178): recorre `Juegos\&lt;Consola&gt;` de cada disco conectado y deja cada archivo de juego en `source`, con
/// su consola, tamaño, componente y TitleID o serial, atado a su versión del catálogo si el nombre coincide sin dudas. Rápido:
/// sin hash. Nunca mueve ni borra archivos; solo quita de la base lo que ya no está.
/// </summary>
public static partial class Scan
{
    public const string Kind = "library.scan";

    /// <summary>Lo que se cuenta como juego: de un CD, el .cue y no cada .bin. PC tiene su propio escaneo (#182).</summary>
    internal static readonly HashSet<string> GameExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nsp", ".nsz", ".xci", ".xcz", ".iso", ".rvz", ".gcm", ".gcz", ".wbfs", ".wia", ".ciso", ".chd", ".cue", ".cso", ".pbp", ".m3u",
        ".nes", ".fds", ".sfc", ".smc", ".n64", ".z64", ".v64", ".gb", ".gbc", ".gba", ".vb", ".nds", ".3ds", ".cia", ".cci", ".wua", ".wux",
        ".zip", ".7z", ".pkg", ".vpk",
    };

    /// <summary>Encola un escaneo, salvo que ya haya uno esperando.</summary>
    public static long Enqueue(Database db)
    {
        using (var c = db.Open())
            if (c.ExecuteScalar<long?>("SELECT id FROM task WHERE kind = @Kind AND status = 'queued' ORDER BY id LIMIT 1", new { Kind }) is { } waiting)
                return waiting;
        return TaskQueue.Enqueue(db, Kind);
    }

    /// <param name="Executable">PC: el ejecutable principal, relativo a la carpeta del juego (#182).</param>
    record Found(string LibraryId, string Relative, string Console, long Size, string? Component, string? TitleId, string Name, string? Folder,
        string? Executable = null);

    public static Task Run(TaskRun run, CancellationToken ct)
    {
        var folders = Disk.ConsoleFolders(run.Db);
        var libraries = Disk.List(run.Db).ToDictionary(l => l.Id);
        var catalog = Catalog(run.Db);
        var aliases = Aliases(run.Db);
        int files = 0, matched = 0;

        for (var i = 0; i < folders.Count; i++)
        {
            var (console, libraryId, relative) = folders[i];
            var root = libraries[libraryId].Path;
            run.Report(i * 100 / Math.Max(1, folders.Count), ApiError.Of("scan.folder", ("path", Path.Combine(root, relative))));
            // PC (#182): cada carpeta de `Juegos\PC` es un juego, con su ejecutable.
            var found = console == "pc" ? PcGames(libraryId, root, relative, ct) : [];
            // Switch y Switch 2 (#237): cada carpeta extraída (`exefs\main`, `romfs`) es un juego, por su TitleID; sus archivos no se cuentan sueltos.
            var dumps = console == "pc" ? [] : FolderGame.Find(Path.Combine(root, relative)).Where(d => d.Console is null || d.Console == console).ToList();
            foreach (var dump in dumps)
            {
                var title = dump.Name ?? (dump.TitleId is null ? null : console is "switch" or "switch2"
                    ? SwitchTitles.NameOf(run.Db, dump.TitleId, ct).GetAwaiter().GetResult() : DatBySerial(run.Db, console, dump.TitleId));
                found.Add(new(libraryId, Path.GetRelativePath(root, dump.Root), console, FolderGame.SizeOf(dump.Root), "BASE", dump.TitleId,
                    title ?? Path.GetFileName(dump.Root), Path.GetFileName(dump.Root)));
            }
            if (console != "pc")
                foreach (var file in Directory.EnumerateFiles(Path.Combine(root, relative), "*", new EnumerationOptions { RecurseSubdirectories = true }))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!GameExtensions.Contains(Path.GetExtension(file)) || file.Contains(".convirtiendo.", StringComparison.OrdinalIgnoreCase)) continue;
                    if (dumps.Any(d => file.StartsWith(d.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) continue;
                    var name = Path.GetFileNameWithoutExtension(file);
                    var parent = Path.GetDirectoryName(file)!;
                    var folder = Disk.Same(parent, Path.Combine(root, relative)) ? null : Path.GetFileName(parent);
                    string? titleId = null, component = null;
                    if (console is "switch" or "switch2")
                    {
                        titleId = TitleIdInName().Match(name) is { Success: true } m ? m.Groups[1].Value.ToUpperInvariant() : null;
                        component = titleId is null ? null : ComponentOf(titleId);
                    }
                    else titleId = Serial(file);
                    found.Add(new(libraryId, Path.GetRelativePath(root, file), console, new FileInfo(file).Length, component, titleId, name, folder));
                }

            // Cada archivo con su versión: por nombre, o por el TitleID base (UPD y DLC van con su BASE).
            var versions = catalog.GetValueOrDefault(console) ?? [];
            var learned = aliases.GetValueOrDefault(console) ?? [];
            using var c = run.Db.Open();
            var hashes = c.Query<(string Path, string Sha1, long Size)>(
                    "SELECT relative_path, sha1, size FROM source WHERE kind = 'file' AND library_id = @libraryId AND sha1 IS NOT NULL", new { libraryId })
                .ToDictionary(r => r.Path, r => r, StringComparer.OrdinalIgnoreCase);
            // Por nombre, por el de su carpeta y, si no, por el nombre del DAT: con el hash ya calculado (#179) o con el serial.
            // Primero los alias (lo que corregiste o resolvió Claude, #181), después el catálogo.
            var byName = found.ToDictionary(f => f, f => Match(learned, f.Name)
                // El código que publica el catálogo (#249): un UPD o un DLC, por el de su base.
                ?? (f.TitleId is { } code ? VersionByCode(run.Db, console, f.Component is null or "BASE" ? code : BaseOf(code)) : null)
                ?? Match(versions, f.Name) ?? (f.Folder is null ? null : Match(versions, f.Folder))
                // Switch (#241): el TitleID del nombre contra titledb, sin IA; un UPD o un DLC, por el de su base.
                ?? (f.TitleId is { } tid && f.Console is "switch" or "switch2"
                    && SwitchTitles.NameOf(run.Db, f.Component is null or "BASE" ? tid : BaseOf(tid), ct).GetAwaiter().GetResult() is { } title ? Match(versions, title) : null)
                ?? (f.Console is not ("switch" or "switch2") && f.TitleId is null && GameIds.FromFile(Path.Combine(root, f.Relative)) is { } own
                    ? (own.Serial is { } ownCode ? VersionByCode(run.Db, console, ownCode) : null)
                      ?? (own.Name is { } ownName ? Match(learned, ownName) ?? Match(versions, ownName) : null)
                      ?? (own.Serial is { } ownSerial && DatBySerial(run.Db, console, ownSerial) is { } fromDat ? Match(versions, fromDat) : null)
                    : null)
                ?? (DatName(c, f, hashes.TryGetValue(f.Relative, out var h) && h.Size == f.Size ? h.Sha1 : null) is { } dat ? Match(versions, dat) : null));
            var byBase = found.Where(f => f.Component is not null && byName[f] is not null)
                .GroupBy(f => BaseOf(f.TitleId!)).ToDictionary(g => g.Key, g => byName[g.First()]);
            // El ejecutable que elegiste se queda mientras exista.
            var chosen = c.Query<(string Path, string Exe)>(
                    "SELECT relative_path, executable FROM source WHERE kind = 'file' AND library_id = @libraryId AND executable IS NOT NULL", new { libraryId })
                .ToDictionary(r => r.Path, r => r.Exe, StringComparer.OrdinalIgnoreCase);
            using var t = c.BeginTransaction();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in found)
            {
                var version = byName[f] ?? (f.TitleId is not null && f.Component is not null ? byBase.GetValueOrDefault(BaseOf(f.TitleId)) : null);
                if (version is not null) matched++;
                files++;
                seen.Add(f.Relative);
                // Una versión ya puesta (por la persona o por Claude) no se pisa.
                c.Execute("""
                    INSERT INTO source (kind, library_id, relative_path, console, size, component, title_id, version_id, executable)
                    VALUES ('file', @LibraryId, @Relative, @Console, @Size, @Component, @TitleId, @version, @executable)
                    ON CONFLICT (library_id, relative_path) WHERE kind = 'file' DO UPDATE SET
                        console = excluded.console, size = excluded.size, component = excluded.component,
                        title_id = coalesce(excluded.title_id, source.title_id), -- el serial que leyó la importación (#240) no se pierde
                        sha1 = CASE WHEN source.size = excluded.size THEN source.sha1 END,
                        verified = CASE WHEN source.size = excluded.size THEN source.verified ELSE 0 END,
                        version_id = coalesce(source.version_id, excluded.version_id), executable = excluded.executable
                    """, new
                {
                    f.LibraryId,
                    f.Relative,
                    f.Console,
                    f.Size,
                    f.Component,
                    f.TitleId,
                    version,
                    executable = chosen.TryGetValue(f.Relative, out var exe) && File.Exists(Path.Combine(root, f.Relative, exe)) ? exe : f.Executable,
                }, t);
            }
            // Lo que ya no está en la carpeta sale de la base (el archivo no se toca: no existe).
            var prefix = relative + Path.DirectorySeparatorChar;
            foreach (var (id, path) in c.Query<(long, string)>("""
                         SELECT id, relative_path FROM source WHERE kind = 'file' AND library_id = @libraryId AND substr(relative_path, 1, length(@prefix)) = @prefix
                         """, new { libraryId, prefix }, t))
                if (!seen.Contains(path)) c.Execute("DELETE FROM source WHERE id = @id", new { id }, t);
            t.Commit();
        }
        run.Log(ApiError.Of("scan.log.summary", ("files", files.ToString()), ("matched", matched.ToString()), ("pending", (files - matched).ToString())));
        if (Claude.Identify.AfterScan) Claude.Identify.EnqueueIfNeeded(run.Db); // lo que quedó sin identificar va a Claude (#180)
        return Task.CompletedTask;
    }

    /// <summary>Los juegos de PC sueltos: cada carpeta con al menos un .exe, con su tamaño y su ejecutable principal.</summary>
    static List<Found> PcGames(string libraryId, string root, string relative, CancellationToken ct)
    {
        var list = new List<Found>();
        foreach (var folder in Directory.EnumerateDirectories(Path.Combine(root, relative)))
        {
            ct.ThrowIfCancellationRequested();
            var files = Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true }).Select(f => new FileInfo(f)).ToList();
            if (MainExecutable(folder, files) is not { } exe) continue;
            var name = Path.GetFileName(folder);
            list.Add(new(libraryId, Path.GetRelativePath(root, folder), "pc", files.Sum(f => f.Length), null, null, name, null, exe));
        }
        return list;
    }

    /// <summary>
    /// El .exe más grande fuera de instaladores, desinstaladores y redistribuibles; si todos lo parecen (un juego que se llama
    /// «Dispatch»), el más grande igual. Null si no hay ninguno.
    /// </summary>
    public static string? MainExecutable(string folder, IEnumerable<FileInfo> files)
    {
        var exes = files.Where(f => f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)).OrderByDescending(f => f.Length)
            .Select(f => Path.GetRelativePath(folder, f.FullName)).ToList();
        return exes.FirstOrDefault(x => !NotTheGame().IsMatch(x)) ?? exes.FirstOrDefault();
    }

    /// <summary>Los ejecutables de una carpeta de juego de PC, para elegir otro (#182).</summary>
    public static List<string> Executables(string folder) =>
        Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*.exe", new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive })
                .Select(f => Path.GetRelativePath(folder, f)).Order().Take(200).ToList()
            : [];

    [GeneratedRegex(@"setup|unins|install|redist|vcredist|dxsetup|directx|dotnet|crash|prereq|_commonredist|easyanticheat|battleye|update|patch|config", RegexOptions.IgnoreCase)]
    private static partial Regex NotTheGame();

    /// <summary>Las versiones de cada consola por nombre normalizado (español y original). Un nombre con varias versiones queda dudoso.</summary>
    internal static Dictionary<string, Dictionary<string, HashSet<long>>> Catalog(Database db)
    {
        using var c = db.Open();
        var result = new Dictionary<string, Dictionary<string, HashSet<long>>>();
        foreach (var (id, platform, es, original) in c.Query<(long, string, string, string?)>(
                     "SELECT v.id, v.platform, g.name_es, g.original_name FROM version v JOIN game g ON g.id = v.game_id"))
        {
            var names = result.TryGetValue(platform, out var n) ? n : result[platform] = [];
            foreach (var name in new[] { es, original }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Normalize(x!)).Distinct())
                (names.TryGetValue(name, out var set) ? set : names[name] = []).Add(id);
        }
        return result;
    }

    /// <summary>El nombre que le da el DAT de su consola, por su SHA-1 o por el serial del disco («GZ2P01» está en «DL-DOL-GZ2P-EUR»).</summary>
    static string? DatName(Microsoft.Data.Sqlite.SqliteConnection c, Found f, string? sha1) =>
        sha1 is not null && c.QueryFirstOrDefault<string?>("SELECT name FROM dat_entry WHERE console = @Console AND sha1 = @sha1", new { f.Console, sha1 }) is { } byHash ? byHash
        : f.Component is null && f.TitleId is { Length: 6 } serial
            ? c.QueryFirstOrDefault<string?>("SELECT name FROM dat_entry WHERE console = @Console AND serial LIKE @like", new { f.Console, like = $"%-{serial[..4]}-%" })
            : null;

    /// <summary>
    /// La versión con ese TitleID o serial en el catálogo (#249, decisión 278): lo publica el catálogo, así que va antes que el nombre y
    /// que las tablas. Un serial de GameCube o Wii («GZLE01») busca su código de 4 letras; uno de PlayStation acepta un sufijo
    /// («SLPM-65002-0»). Null si no está o si es de más de una versión.
    /// </summary>
    public static long? VersionByCode(Database db, string console, string code)
    {
        using var c = db.Open();
        var like = code.Length == 6 && console is "gc" or "wii" ? $"%{code[..4]}%" : code + "-%";
        var found = c.Query<long>("""
            SELECT DISTINCT k.version_id FROM version_code k JOIN version v ON v.id = k.version_id
            WHERE v.platform = @console AND (k.code = @code OR k.code LIKE @like)
            """, new { console, code = code.ToUpperInvariant(), like }).ToList();
        return found.Count == 1 ? found[0] : null;
    }

    /// <summary>
    /// El nombre del juego con ese serial en el DAT de su consola (#240): «SLUS-20062», «CTR-P-BP4J», «BKAJ». También con un sufijo
    /// de disco o revisión («SLPM-65002-0»). Null si no hay DAT bajado o no está.
    /// </summary>
    public static string? DatBySerial(Database db, string console, string serial)
    {
        using var c = db.Open();
        return c.QueryFirstOrDefault<string?>("SELECT name FROM dat_entry WHERE console = @console AND (serial = @serial OR serial LIKE @like) ORDER BY length(serial)",
            new { console, serial, like = serial + "-%" });
    }

    /// <summary>La versión de <paramref name="console"/> cuyo juego se llama así, si hay una sola (para lo verificado por hash, #179).</summary>
    public static long? MatchName(Database db, string console, string name)
    {
        var versions = new Dictionary<string, HashSet<long>>();
        using var c = db.Open();
        foreach (var (id, es, original) in c.Query<(long, string, string?)>(
                     "SELECT v.id, g.name_es, g.original_name FROM version v JOIN game g ON g.id = v.game_id WHERE v.platform = @console", new { console }))
            foreach (var n in new[] { es, original }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Normalize(x!)).Distinct())
                (versions.TryGetValue(n, out var set) ? set : versions[n] = []).Add(id);
        return Match(versions, name);
    }

    /// <summary>El texto de un alias: el nombre del archivo normalizado.</summary>
    public static string AliasText(string relativePath) => Normalize(Path.GetFileNameWithoutExtension(relativePath));

    /// <summary>Los alias vigentes (`learning` kind alias) de cada consola: nombre normalizado → la versión de su juego en esa consola.</summary>
    internal static Dictionary<string, Dictionary<string, HashSet<long>>> Aliases(Database db)
    {
        using var c = db.Open();
        var result = new Dictionary<string, Dictionary<string, HashSet<long>>>();
        foreach (var (console, text, version) in c.Query<(string, string, long)>("""
                     SELECT json_extract(l.content, '$.console'), json_extract(l.content, '$.text'), v.id FROM learning l
                     JOIN version v ON v.game_id = json_extract(l.content, '$.gameId') AND v.platform = json_extract(l.content, '$.console')
                     WHERE l.kind = 'alias' AND l.undone = 0
                     """))
        {
            var names = result.TryGetValue(console, out var n) ? n : result[console] = [];
            (names.TryGetValue(text, out var set) ? set : names[text] = []).Add(version);
        }
        return result;
    }

    /// <summary>La versión si el nombre coincide con una sola; null si no hay o hay varias (eso queda para Claude, #180).</summary>
    internal static long? Match(Dictionary<string, HashSet<long>> versions, string name) =>
        versions.TryGetValue(Normalize(name), out var set) && set.Count == 1 ? set.First() : null;

    /// <summary>
    /// El nombre sin región, etiquetas ni signos: «The Legend of Zelda: Link’s Awakening (Europe) [BASE]» → «the legend of zelda links awakening».
    /// </summary>
    public static string Normalize(string name)
    {
        name = Article().Replace(Tags().Replace(name, " ").Trim(), "$2 $1$3").Replace("'", "").Replace("’", "");
        var plain = new StringBuilder();
        foreach (var ch in name.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                plain.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        return string.Join(' ', plain.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>`…000` es BASE, `…800` UPD y el resto DLC.</summary>
    public static string ComponentOf(string titleId) => titleId[^3..] switch { "000" => "BASE", "800" => "UPD", _ => "DLC" };

    /// <summary>El TitleID de la BASE: el de un UPD sin el 800; el de un DLC, con el dígito 13 un paso atrás y los tres últimos en 0.</summary>
    public static string BaseOf(string titleId)
    {
        var id = Convert.ToUInt64(titleId, 16);
        return (ComponentOf(titleId) == "DLC" ? (id - 0x1000) & ~0xFFFUL : id & ~0xFFFUL).ToString("X16");
    }

    /// <summary>El serial de un disco de GameCube o Wii (ISO, GCM o RVZ), como «GZ2P01»; null en lo demás.</summary>
    internal static string? Serial(string file)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext is not (".iso" or ".gcm" or ".rvz")) return null;
        try
        {
            using var s = File.OpenRead(file);
            var head = new byte[0x60];
            if (s.Read(head) < head.Length) return null;
            var at = ext == ".rvz" ? (Encoding.ASCII.GetString(head, 0, 3) == "RVZ" ? 0x58 : -1) : 0;
            if (at < 0 || (ext != ".rvz" && !IsNintendoDisc(head))) return null;
            var id = Encoding.ASCII.GetString(head, at, 6);
            return id.All(char.IsAsciiLetterOrDigit) ? id : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>La marca de GameCube (0x1C) o Wii (0x18) en la cabecera del disco.</summary>
    static bool IsNintendoDisc(byte[] head) =>
        head.AsSpan(0x1C, 4).SequenceEqual(new byte[] { 0xC2, 0x33, 0x9F, 0x3D }) || head.AsSpan(0x18, 4).SequenceEqual(new byte[] { 0x5D, 0x1C, 0x9E, 0xA3 });

    [GeneratedRegex(@"\[(0[14]00[0-9A-Fa-f]{12})\]")]
    internal static partial Regex TitleIdInName();

    /// <summary>Como lo escriben No-Intro y Redump: «Legend of Zelda, The - Twilight Princess».</summary>
    [GeneratedRegex(@"^(.+?), (The|A|An)\b(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex Article();

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]")]
    private static partial Regex Tags();
}
