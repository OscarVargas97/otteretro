using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>
/// Verificación por hash (§7.2, #179): los DAT de No-Intro (cartuchos) y Redump (discos) de `libretro-database` en
/// `dat_entry`, y la tarea que calcula el SHA-1 de tus archivos sin conexión y los marca verificados si coinciden.
/// </summary>
public static partial class Dats
{
    public const string UpdateKind = "dat.update", VerifyKind = "library.verify";
    const string Base = "https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/";

    /// <summary>Los DAT de cada consola. Switch, Wii U, PS4 y lo más nuevo no tienen.</summary>
    public static readonly Dictionary<string, (string Source, string File)[]> Files = new()
    {
        ["nes"] = [("no-intro", "Nintendo - Nintendo Entertainment System")],
        ["snes"] = [("no-intro", "Nintendo - Super Nintendo Entertainment System")],
        ["n64"] = [("no-intro", "Nintendo - Nintendo 64")],
        ["gb"] = [("no-intro", "Nintendo - Game Boy"), ("no-intro", "Nintendo - Game Boy Color")],
        ["gba"] = [("no-intro", "Nintendo - Game Boy Advance")],
        ["vb"] = [("no-intro", "Nintendo - Virtual Boy")],
        ["ds"] = [("no-intro", "Nintendo - Nintendo DS")],
        ["3ds"] = [("no-intro", "Nintendo - Nintendo 3DS")],
        ["vita"] = [("no-intro", "Sony - PlayStation Vita")],
        ["gc"] = [("redump", "Nintendo - GameCube")],
        ["wii"] = [("redump", "Nintendo - Wii")],
        ["ps1"] = [("redump", "Sony - PlayStation")],
        ["ps2"] = [("redump", "Sony - PlayStation 2")],
        ["ps3"] = [("redump", "Sony - PlayStation 3")],
        ["psp"] = [("redump", "Sony - PlayStation Portable")],
        ["xbox"] = [("redump", "Microsoft - Xbox")],
        ["x360"] = [("redump", "Microsoft - Xbox 360")],
    };

    /// <summary>Para las pruebas: responde en lugar de GitHub.</summary>
    public static HttpMessageHandler? Handler { get; set; }
    static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromMinutes(5) };

    public record Entry(string Name, string? Sha1, string? Crc, long? Size, string? Serial, string? Region);

    /// <summary>Lee un DAT de clrmamepro: un `game ( … )` por juego y una entrada por cada `rom ( … )`.</summary>
    public static IEnumerable<Entry> Parse(TextReader reader)
    {
        string? name = null, region = null, serial = null;
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.StartsWith("game (", StringComparison.Ordinal)) (name, region, serial) = (null, null, null);
            else if (line.StartsWith("rom (", StringComparison.Ordinal) && name is not null)
            {
                var v = Pairs(line[5..]);
                yield return new(name, v.GetValueOrDefault("sha1")?.ToUpperInvariant(), v.GetValueOrDefault("crc")?.ToUpperInvariant(),
                    long.TryParse(v.GetValueOrDefault("size"), out var size) ? size : null, v.GetValueOrDefault("serial") ?? serial, region);
            }
            else if (Pairs(line) is { Count: 1 } one)
            {
                if (one.TryGetValue("name", out var n)) name = n;
                else if (one.TryGetValue("region", out var r)) region = r;
                else if (one.TryGetValue("serial", out var s)) serial = s;
            }
        }
    }

    static Dictionary<string, string> Pairs(string text) =>
        Pair().Matches(text).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value);

    public static long EnqueueUpdate(Database db)
    {
        using (var c = db.Open())
            if (c.ExecuteScalar<long?>("SELECT id FROM task WHERE kind = @UpdateKind AND status = 'queued' LIMIT 1", new { UpdateKind }) is { } waiting) return waiting;
        return TaskQueue.Enqueue(db, UpdateKind);
    }

    /// <summary>Baja todos los DAT (con la puesta al día del catálogo). Punto de control: cuántas consolas van.</summary>
    public static async Task Update(TaskRun run, CancellationToken ct)
    {
        var consoles = Files.Keys.ToList();
        for (var i = int.TryParse(run.Data, out var done) ? done : 0; i < consoles.Count; i++)
        {
            run.Report(i * 100 / consoles.Count, ApiError.Of("dat.downloading", ("console", consoles[i])), i.ToString());
            await Download(run.Db, consoles[i], ct);
        }
    }

    /// <summary>Reemplaza los DAT de una consola por los de libretro. Devuelve cuántas entradas quedaron.</summary>
    internal static async Task<int> Download(Database db, string console, CancellationToken ct)
    {
        var http = Handler is null ? Shared : new HttpClient(Handler);
        var entries = new List<(string Source, Entry Entry)>();
        foreach (var (source, file) in Files[console])
        {
            using var response = await http.GetAsync(Base + source + "/" + Uri.EscapeDataString(file) + ".dat", HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));
            entries.AddRange(Parse(reader).Select(e => (source, e)));
        }
        using var c = db.Open();
        using var t = c.BeginTransaction();
        c.Execute("DELETE FROM dat_entry WHERE console = @console", new { console }, t);
        foreach (var (source, e) in entries)
            c.Execute("INSERT INTO dat_entry (console, source, name, sha1, crc, size, serial, region) VALUES (@console, @source, @Name, @Sha1, @Crc, @Size, @Serial, @Region)",
                new { console, source, e.Name, e.Sha1, e.Crc, e.Size, e.Serial, e.Region }, t);
        t.Commit();
        return entries.Count;
    }

    /// <param name="Console">Verifica una consola…</param>
    /// <param name="GameId">…o un juego.</param>
    /// <param name="After">Punto de control: el último archivo (`source.id`) ya verificado.</param>
    public record VerifyJob(string? Console, long? GameId, long After = 0);

    public static long EnqueueVerify(Database db, string? console, long? gameId) =>
        TaskQueue.Enqueue(db, VerifyKind, JsonSerializer.Serialize(new VerifyJob(console, gameId), JsonSerializerOptions.Web));

    record FileRow(long Id, string LibraryId, string RelativePath, string Console, long Size, long? VersionId);

    /// <summary>
    /// Calcula el SHA-1 de cada archivo y lo busca en el DAT de su consola (bajándolo si falta): si coincide queda verificado, y
    /// si todavía no tenía juego, se identifica por el nombre del DAT. Sigue desde el último archivo terminado.
    /// </summary>
    public static async Task Verify(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<VerifyJob>(run.Data!, JsonSerializerOptions.Web)!;
        List<FileRow> files;
        using (var c = run.Db.Open())
            files = c.Query<FileRow>("""
                SELECT o.id, o.library_id AS LibraryId, o.relative_path AS RelativePath, o.console, coalesce(o.size, 0) AS Size, o.version_id AS VersionId
                FROM source o WHERE o.kind = 'file' AND o.id > @After AND (@Console IS NULL OR o.console = @Console)
                  AND (@GameId IS NULL OR o.version_id IN (SELECT id FROM version WHERE game_id = @GameId))
                ORDER BY o.id
                """, job).ToList();
        var libraries = Disk.List(run.Db).Where(l => l.Connected).ToDictionary(l => l.Id, l => l.Path);
        var total = Math.Max(1, files.Sum(f => f.Size));
        long done = 0;
        int verified = 0, missing = 0;
        foreach (var f in files)
        {
            // Sin DAT no hay contra qué comparar (Switch, PC…): no se gasta tiempo en el hash.
            if (!Files.ContainsKey(f.Console) || !libraries.TryGetValue(f.LibraryId, out var root)) continue;
            using (var c = run.Db.Open())
                if (c.ExecuteScalar<long>("SELECT count(*) FROM dat_entry WHERE console = @Console", f) == 0)
                    await Download(run.Db, f.Console, ct);
            var path = Path.Combine(root, f.RelativePath);
            // Borrado o movido desde el último escaneo: se avisa y se sigue con el resto.
            if (!File.Exists(path))
            {
                run.Log(ApiError.Of("dat.log.missingFile", ("path", path)), "warn");
                continue;
            }
            run.Report((int)(done * 100 / total), ApiError.Of("dat.hashing", ("path", path)));
            var sha1 = await Sha1(path, ct);
            done += f.Size;
            using var db = run.Db.Open();
            var match = db.QueryFirstOrDefault<string?>("SELECT name FROM dat_entry WHERE console = @Console AND sha1 = @sha1", new { f.Console, sha1 });
            var version = f.VersionId ?? (match is null ? null : Scan.MatchName(run.Db, f.Console, match));
            db.Execute("UPDATE source SET sha1 = @sha1, verified = @ok, version_id = @version WHERE id = @Id",
                new { f.Id, sha1, ok = match is not null, version });
            if (match is not null) verified++;
            else missing++;
            run.Log(match is null ? ApiError.Of("dat.log.notInDat", ("path", path)) : ApiError.Of("dat.log.verified", ("path", path), ("name", match)),
                match is null ? "warn" : "info");
            run.Report((int)(done * 100 / total), null, JsonSerializer.Serialize(job with { After = f.Id }, JsonSerializerOptions.Web));
        }
        run.Log(ApiError.Of("dat.log.summary", ("verified", verified.ToString()), ("missing", missing.ToString())));
    }

    internal static async Task<string> Sha1(string path, CancellationToken ct)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan | FileOptions.Asynchronous);
        return Convert.ToHexString(await SHA1.HashDataAsync(file, ct));
    }

    [GeneratedRegex("""(\w+) (?:"([^"]*)"|([^\s()]+))""")]
    private static partial Regex Pair();
}
