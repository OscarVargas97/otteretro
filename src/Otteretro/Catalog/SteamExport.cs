using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;
using Otteretro.Libraries;

namespace Otteretro.Catalog;

/// <summary>Un acceso de Steam: la versión, su nombre «Juego (Consola)», su categoría y el id corto del acceso no-Steam.</summary>
public record SteamEntry(long VersionId, string Title, string Category, long AppId)
{
    /// <summary>El id de `steam://rungameid/…` (#130). Texto: no cabe en un número de JavaScript.</summary>
    public string RunGameId => SteamExport.RunGameId(AppId).ToString(CultureInfo.InvariantCulture);
}

/// <summary>Lo que se exportaría ahora y lo que cambia respecto de la última exportación de este equipo.</summary>
public record SteamChanges(List<SteamEntry> Entries, List<SteamEntry> Added, List<SteamEntry> Changed, List<SteamEntry> Removed);

/// <summary>
/// Exportar a Steam (§10, #401) con Steam ROM Manager: un manifiesto «Manual» por categoría (la saga, o «Emulación») cuyos accesos
/// abren `otteretro jugar &lt;id&gt;`, con el arte del catálogo. Lo que ya está en Steam no se exporta. Cada equipo guarda su última
/// exportación: de ahí salen las diferencias y el `steam://rungameid/…` con el que Jugar abre lo exportado.
/// </summary>
public static class SteamExport
{
    public const string Kind = "steam.export";
    public const string NoSaga = "Emulación";
    const string RecipeId = "steam-rom-manager";
    const string ParserPrefix = "otteretro-";
    /// <summary>La versión del formato de userConfigurations.json de SRM 2.6 (su modificador la sube si es más nuevo).</summary>
    const int SrmConfigVersion = 30;
    /// <summary>Imagen del catálogo → tipo de arte de SRM.</summary>
    static readonly (string Catalog, string Srm)[] Art = [("cover", "tall"), ("hero", "hero"), ("logo", "logo")];

    /// <summary>Para las pruebas: el Steam-ROM-Manager.exe y cómo se corre (`add`; devuelve el código de salida).</summary>
    public static string? SrmPath { get; set; }
    public static Func<string, CancellationToken, Task<int>> RunSrm { get; set; } = async (exe, ct) =>
    {
        using var p = Process.Start(new ProcessStartInfo(exe, ["add"]) { WorkingDirectory = Path.GetDirectoryName(exe)! })!;
        await p.WaitForExitAsync(ct);
        return p.ExitCode;
    };

    public static void Mark(Database db, long versionId, bool export)
    {
        using var c = db.Open();
        c.Execute(export ? "INSERT OR IGNORE INTO steam_mark (version_id) VALUES (@versionId)" : "DELETE FROM steam_mark WHERE version_id = @versionId", new { versionId });
    }

    /// <summary>Lo marcado que no está en Steam, como se exportaría con este ejecutable de Otteretro.</summary>
    public static List<SteamEntry> Entries(Database db, string exe)
    {
        using var c = db.Open();
        return c.Query<(long VersionId, string Title, string Category)>($"""
            SELECT v.id, g.name_es || ' (' || co.name || ')', COALESCE(s.name, '{NoSaga}')
            FROM steam_mark m
            JOIN catalog.version v ON v.id = m.version_id
            JOIN catalog.game g ON g.id = v.game_id
            JOIN catalog.console co ON co.id = v.platform
            LEFT JOIN catalog.saga s ON s.id = g.saga_id
            WHERE NOT EXISTS (SELECT 1 FROM source x WHERE x.version_id = v.id AND x.kind = 'store' AND x.store = 'steam')
            ORDER BY 3, 2
            """).Select(e => new SteamEntry(e.VersionId, e.Title, e.Category, AppId(Quote(exe), e.Title))).ToList();
    }

    public static List<SteamEntry> LastExport(Database db, string machine)
    {
        using var c = db.Open();
        return c.Query<(long VersionId, string Title, string Category, long AppId)>(
            "SELECT version_id, title, category, app_id FROM steam_export WHERE machine_id = @machine ORDER BY category, title", new { machine })
            .Select(e => new SteamEntry(e.VersionId, e.Title, e.Category, e.AppId)).ToList();
    }

    public static SteamChanges Changes(Database db, string machine, string exe) => Diff(Entries(db, exe), LastExport(db, machine));

    static SteamChanges Diff(List<SteamEntry> now, List<SteamEntry> last)
    {
        var before = last.ToDictionary(e => e.VersionId);
        var ids = now.Select(e => e.VersionId).ToHashSet();
        return new(now,
            now.Where(e => !before.ContainsKey(e.VersionId)).ToList(),
            now.Where(e => before.TryGetValue(e.VersionId, out var b) && b != e).ToList(),
            last.Where(e => !ids.Contains(e.VersionId)).ToList());
    }

    /// <summary>El acceso exportado en este equipo para esa versión, para abrirlo por Steam; null si no se exportó aquí.</summary>
    public static ulong? Exported(Database db, string machine, long versionId)
    {
        using var c = db.Open();
        return c.ExecuteScalar<long?>("SELECT app_id FROM steam_export WHERE machine_id = @machine AND version_id = @versionId", new { machine, versionId })
            is { } id ? RunGameId(id) : null;
    }

    record Job(string Machine, string Exe);

    public static long Enqueue(Database db, string machine, string exe) =>
        TaskQueue.Enqueue(db, Kind, JsonSerializer.Serialize(new Job(machine, exe), JsonSerializerOptions.Web));

    /// <summary>
    /// Tarea `steam.export`: escribe los manifiestos, el arte y la configuración de SRM en `steam\&lt;equipo&gt;` de la carpeta de datos. Con
    /// SRM instalado, suma sus analizadores a los de SRM, corre `add` y anota la exportación; sin él, deja todo listo y avisa.
    /// </summary>
    public static async Task Run(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<Job>(run.Data!, JsonSerializerOptions.Web)!;
        var db = run.Db;
        var vdf = Steam.LoginUsers();
        if (Steam.DetectAccount(vdf) is not { } account || Steam.AccountName(vdf!, account) is not { } accountName)
            throw new TaskException(ApiError.Of("tasks.steam.noAccount"));
        var steamRoot = Path.GetDirectoryName(Path.GetDirectoryName(vdf))!;

        var last = LastExport(db, job.Machine);
        var changes = Diff(Entries(db, job.Exe), last);
        var folder = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(db.FilePath)!)!, "steam", job.Machine);
        // Las categorías que se vaciaron quedan con su manifiesto vacío: así SRM quita sus accesos.
        var categories = changes.Entries.Select(e => e.Category).Concat(last.Select(e => e.Category)).Distinct().Order(StringComparer.Ordinal).ToList();
        var parsers = await Write(db, folder, job.Exe, changes.Entries, categories, steamRoot, accountName, ct);

        if ((SrmPath ?? Srm(db)) is not { } srm)
        {
            run.Log(ApiError.Of("tasks.steam.noSrm", ("folder", folder)), "warn");
            run.Report(100, ApiError.Of("tasks.steam.noSrm", ("folder", folder)));
            return;
        }
        MergeSrmConfig(Path.Combine(Path.GetDirectoryName(srm)!, "userData", "userConfigurations.json"), parsers);
        run.Report(50, ApiError.Of("tasks.steam.running"));
        if (await RunSrm(srm, ct) is var code and not 0) throw new TaskException(ApiError.Of("tasks.steam.srmFailed", ("code", code.ToString())));

        Save(db, job.Machine, changes.Entries);
        run.Report(100, ApiError.Of("tasks.steam.exported", ("added", changes.Added.Count.ToString()),
            ("changed", changes.Changed.Count.ToString()), ("removed", changes.Removed.Count.ToString())));
    }

    /// <summary>Escribe manifiestos, arte y `userConfigurations.json` (solo los de Otteretro); devuelve esos analizadores.</summary>
    public static async Task<JsonArray> Write(Database db, string folder, string exe, List<SteamEntry> entries, List<string> categories,
        string steamRoot, string accountName, CancellationToken ct = default)
    {
        // Todo lo de esta carpeta lo genera Otteretro: se rehace entero.
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        var parsers = new JsonArray();
        foreach (var category in categories)
        {
            var id = ParserId(category);
            var manifests = Path.Combine(folder, "manifiestos", id);
            Directory.CreateDirectory(manifests);
            var items = new JsonArray();
            foreach (var e in entries.Where(e => e.Category == category))
            {
                // El arte va en la carpeta de inicio de cada acceso: SRM no tiene otra forma de ligar una imagen a una entrada de un
                // manifiesto (el título puede tener «:», que no va en un nombre de archivo de Windows). `jugar` no depende de ella.
                var art = Path.Combine(folder, "arte", e.VersionId.ToString(CultureInfo.InvariantCulture));
                Directory.CreateDirectory(art);
                if (GameOf(db, e.VersionId) is { } gameId)
                    foreach (var (kind, srmKind) in Art)
                        if (await ImageRefs.File(db, gameId, kind, ct) is { } image)
                            File.Copy(image, Path.Combine(art, srmKind + Path.GetExtension(image)), overwrite: true);
                items.Add(new JsonObject
                {
                    ["title"] = e.Title,
                    ["target"] = exe,
                    ["startIn"] = art,
                    ["launchOptions"] = $"jugar {e.VersionId}",
                    ["appendArgsToExecutable"] = false,
                });
            }
            File.WriteAllText(Path.Combine(manifests, "otteretro.json"), items.ToJsonString(Indented));
            parsers.Add(new JsonObject
            {
                ["version"] = SrmConfigVersion,
                ["parserType"] = "Manual",
                ["configTitle"] = $"Otteretro · {category}",
                ["parserId"] = id,
                ["steamCategories"] = new JsonArray(category),
                ["steamDirectory"] = steamRoot,
                ["userAccounts"] = new JsonObject { ["specifiedAccounts"] = new JsonArray(accountName) },
                ["parserInputs"] = new JsonObject { ["manualManifests"] = manifests },
                // El título extraído tal cual: el id del acceso sale de él, y el emparejamiento difuso lo cambiaría.
                ["titleModifier"] = "${title}",
                ["localImages"] = new JsonObject(Art.Select(a => KeyValuePair.Create(a.Srm, (JsonNode?)("${startInDir}/" + a.Srm + ".*")))),
                ["disabled"] = false,
            });
        }
        File.WriteAllText(Path.Combine(folder, "userConfigurations.json"), parsers.ToJsonString(Indented));
        return parsers;
    }

    /// <summary>Reemplaza los analizadores de Otteretro en la configuración de SRM y deja los de la persona como estaban.</summary>
    public static void MergeSrmConfig(string file, JsonArray ours)
    {
        JsonArray all;
        try { all = File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonArray ?? throw new JsonException() : []; }
        catch (JsonException) { throw new TaskException(ApiError.Of("tasks.steam.srmConfig", ("file", file))); }
        foreach (var p in all.Where(p => p?["parserId"]?.GetValue<string>().StartsWith(ParserPrefix, StringComparison.Ordinal) == true).ToList()) all.Remove(p);
        foreach (var p in ours) all.Add(p!.DeepClone());
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temp = file + ".part";
        File.WriteAllText(temp, all.ToJsonString(Indented));
        File.Move(temp, file, overwrite: true);
    }

    public static void Save(Database db, string machine, List<SteamEntry> entries)
    {
        using var c = db.Open();
        using var t = c.BeginTransaction();
        c.Execute("DELETE FROM steam_export WHERE machine_id = @machine", new { machine }, t);
        c.Execute("""
            INSERT INTO steam_export (machine_id, version_id, title, category, app_id, exported_at)
            VALUES (@machine, @VersionId, @Title, @Category, @AppId, @date)
            """, entries.Select(e => new { machine, e.VersionId, e.Title, e.Category, e.AppId, date = DateTime.UtcNow.ToString("O") }), t);
        t.Commit();
    }

    /// <summary>
    /// El id corto del acceso no-Steam, como SRM (generate-app-id.ts): crc32(ejecutable entre comillas + título) con el bit alto.
    /// </summary>
    public static uint AppId(string exe, string title) => Crc32(Encoding.UTF8.GetBytes(exe + title)) | 0x80000000;

    /// <summary>El id largo (64 bits) de `steam://rungameid/…`.</summary>
    public static ulong RunGameId(long appId) => (ulong)(uint)appId << 32 | 0x02000000;

    static string Quote(string exe) => $"\"{exe}\"";

    static string ParserId(string category) => ParserPrefix + Crc32(Encoding.UTF8.GetBytes(category)).ToString("x8", CultureInfo.InvariantCulture);

    static long? GameOf(Database db, long versionId)
    {
        using var c = db.Open();
        return c.ExecuteScalar<long?>("SELECT game_id FROM catalog.version WHERE id = @versionId", new { versionId });
    }

    /// <summary>El Steam-ROM-Manager.exe instalado en Herramientas (#274), el de la versión más nueva; null si no está.</summary>
    static string? Srm(Database db) =>
        Recipes.Find(RecipeId) is { Detect: { } exe }
        && Recipes.Installed(Disk.ToolRoots(db)).Where(t => t.RecipeId == RecipeId).OrderBy(t => t.Version, StringComparer.Ordinal).LastOrDefault() is { } tool
        && File.Exists(Path.Combine(tool.Path, exe)) ? Path.Combine(tool.Path, exe) : null;

    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    static readonly uint[] Table = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    /// <summary>CRC-32 (IEEE), el de SRM.</summary>
    static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFF;
        foreach (var b in bytes) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }
}
