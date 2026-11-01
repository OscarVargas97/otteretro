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
    static readonly HashSet<string> GameExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nsp", ".nsz", ".xci", ".xcz", ".iso", ".rvz", ".gcm", ".gcz", ".wbfs", ".wia", ".ciso", ".chd", ".cue", ".cso", ".pbp", ".m3u",
        ".nes", ".fds", ".sfc", ".smc", ".n64", ".z64", ".v64", ".gb", ".gbc", ".gba", ".vb", ".nds", ".3ds", ".cia", ".cci", ".wua", ".wux",
        ".zip", ".7z",
    };

    /// <summary>Encola un escaneo, salvo que ya haya uno esperando.</summary>
    public static long Enqueue(Database db)
    {
        using (var c = db.Open())
            if (c.ExecuteScalar<long?>("SELECT id FROM task WHERE kind = @Kind AND status = 'queued' ORDER BY id LIMIT 1", new { Kind }) is { } waiting)
                return waiting;
        return TaskQueue.Enqueue(db, Kind);
    }

    record Found(string LibraryId, string Relative, string Console, long Size, string? Component, string? TitleId, string Name, string? Folder);

    public static Task Run(TaskRun run, CancellationToken ct)
    {
        var folders = Library.ConsoleFolders(run.Db).Where(f => f.Console != "pc").ToList();
        var libraries = Library.List(run.Db).ToDictionary(l => l.Id);
        byte[]? keys = null;
        var keysRead = false;
        var catalog = Catalog(run.Db);
        int files = 0, matched = 0;

        for (var i = 0; i < folders.Count; i++)
        {
            var (console, libraryId, relative) = folders[i];
            var root = libraries[libraryId].Path;
            run.Report(i * 100 / Math.Max(1, folders.Count), ApiError.Of("scan.folder", ("path", Path.Combine(root, relative))));
            var found = new List<Found>();
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, relative), "*", new EnumerationOptions { RecurseSubdirectories = true }))
            {
                ct.ThrowIfCancellationRequested();
                if (!GameExtensions.Contains(Path.GetExtension(file))) continue;
                var name = Path.GetFileNameWithoutExtension(file);
                var parent = Path.GetDirectoryName(file)!;
                var folder = Library.Same(parent, Path.Combine(root, relative)) ? null : Path.GetFileName(parent);
                string? titleId = null, component = null;
                if (console == "switch")
                {
                    titleId = TitleIdInName().Match(name) is { Success: true } m ? m.Groups[1].Value.ToUpperInvariant() : null;
                    if (titleId is null)
                    {
                        if (!keysRead) (keys, keysRead) = (SwitchTitle.HeaderKey(SystemFolders(run.Db, libraries.Values)), true);
                        titleId = SwitchTitle.Read(file, keys);
                    }
                    component = titleId is null ? null : ComponentOf(titleId);
                }
                else titleId = Serial(file);
                found.Add(new(libraryId, Path.GetRelativePath(root, file), console, new FileInfo(file).Length, component, titleId, name, folder));
            }

            // Cada archivo con su versión: por nombre, o por el TitleID base (UPD y DLC van con su BASE).
            var versions = catalog.GetValueOrDefault(console) ?? [];
            using var c = run.Db.Open();
            var hashes = c.Query<(string Path, string Sha1, long Size)>(
                    "SELECT relative_path, sha1, size FROM source WHERE kind = 'file' AND library_id = @libraryId AND sha1 IS NOT NULL", new { libraryId })
                .ToDictionary(r => r.Path, r => r, StringComparer.OrdinalIgnoreCase);
            // Por nombre, por el de su carpeta y, si no, por el nombre del DAT: con el hash ya calculado (#179) o con el serial.
            var byName = found.ToDictionary(f => f, f => Match(versions, f.Name) ?? (f.Folder is null ? null : Match(versions, f.Folder))
                ?? (DatName(c, f, hashes.TryGetValue(f.Relative, out var h) && h.Size == f.Size ? h.Sha1 : null) is { } dat ? Match(versions, dat) : null));
            var byBase = found.Where(f => f.Component is not null && byName[f] is not null)
                .GroupBy(f => BaseOf(f.TitleId!)).ToDictionary(g => g.Key, g => byName[g.First()]);
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
                    INSERT INTO source (kind, library_id, relative_path, console, size, component, title_id, version_id)
                    VALUES ('file', @LibraryId, @Relative, @Console, @Size, @Component, @TitleId, @version)
                    ON CONFLICT (library_id, relative_path) WHERE kind = 'file' DO UPDATE SET
                        console = excluded.console, size = excluded.size, component = excluded.component, title_id = excluded.title_id,
                        sha1 = CASE WHEN source.size = excluded.size THEN source.sha1 END,
                        verified = CASE WHEN source.size = excluded.size THEN source.verified ELSE 0 END,
                        version_id = coalesce(source.version_id, excluded.version_id)
                    """, new { f.LibraryId, f.Relative, f.Console, f.Size, f.Component, f.TitleId, version }, t);
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
        return Task.CompletedTask;
    }

    /// <summary>Las versiones de cada consola por nombre normalizado (español y original). Un nombre con varias versiones queda dudoso.</summary>
    static Dictionary<string, Dictionary<string, HashSet<long>>> Catalog(Database db)
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

    /// <summary>La versión si el nombre coincide con una sola; null si no hay o hay varias (eso queda para Claude, #180).</summary>
    static long? Match(Dictionary<string, HashSet<long>> versions, string name) =>
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
    static string? Serial(string file)
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

    /// <summary>Dónde buscar `prod.keys`: Sistema de cada disco o ubicación de sistema conectada, y la de Herramientas de antes.</summary>
    static IEnumerable<string> SystemFolders(Database db, IEnumerable<LibraryInfo> libraries) =>
        libraries.Where(l => l.Connected).Select(l => Library.FolderOf(l, "system")).OfType<string>()
            .Append(Path.Combine(LibraryTasks.ToolsOf(db), "Sistema"));

    [GeneratedRegex(@"\[(0100[0-9A-Fa-f]{12})\]")]
    private static partial Regex TitleIdInName();

    /// <summary>Como lo escriben No-Intro y Redump: «Legend of Zelda, The - Twilight Princess».</summary>
    [GeneratedRegex(@"^(.+?), (The|A|An)\b(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex Article();

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]")]
    private static partial Regex Tags();
}
