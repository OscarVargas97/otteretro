using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Catalog;

/// <summary>
/// La app toma el catálogo de lo publicado en `otteretro-catalogo` (#213, decisión 253), sin claves y solo cuando la persona lo pide (259).
/// Buscar (#222) baja la versión a un temporal, verifica el hash y la deja esperando; nada cambia hasta aceptarla. Aceptar la aplica entera y
/// rechazar la descarta. Lo de cada persona vive en otra base y apunta por id; lo fusionado o quitado se corrige con `replaced`.
/// </summary>
public static class CatalogUpdate
{
    public const string Kind = "catalog.check";
    /// <summary>De dónde sale el catálogo si la persona no eligió un fork (#217).</summary>
    public const string Official = "OscarVargas97/otteretro-catalogo";
    const string TagPrefix = "catalogo-", DataAsset = "catalogo.db.gz", ManifestAsset = "catalogo.db.gz.json", ChangesAsset = "cambios.json";
    /// <summary>El hash y la versión de lo último aplicado. Se escriben al final: un corte a mitad hace que se repita.</summary>
    const string Applied = "catalog.published", AppliedVersion = "catalog.version", SourceSetting = "catalog.source", PendingSetting = "catalog.pending";
    static readonly Regex RepoName = new(@"^[A-Za-z0-9-]{1,39}/[A-Za-z0-9._-]{1,100}$");

    /// <summary>Lo que se bajó y espera que la persona lo acepte o lo rechace.</summary>
    public record Pending(string Repo, string Tag, string Sha256);

    /// <summary>Una versión publicada, como en el buscador de actualizaciones (#228).</summary>
    /// <param name="Available">Se puede bajar: `publicar.yml` conserva solo las últimas; las demás aparecen por su historial de cambios.</param>
    /// <param name="Counts">Cuántos hay de cada cambio en esa versión (renamed, merged, removed, added, changed), según su `cambios.json`.</param>
    /// <param name="Changes">Los cambios de esa versión, hasta <see cref="ChangesShown"/>.</param>
    public record VersionInfo(string Tag, string? PublishedAt, bool Available, bool Current, Dictionary<string, int> Counts, List<VersionChange> Changes);
    public record VersionChange(string Kind, string Change, long Id, string? Name, string? From);
    public record VersionsPage(string Repo, string? Current, List<VersionInfo> Versions);
    record HistoryEntry(string Version, string Kind, string Change, long Id, string? Name = null, string? From = null);
    record History(List<string> Versions, List<HistoryEntry> Entries);
    const int ChangesShown = 100;

    static string Releases(string repo) => $"https://api.github.com/repos/{repo}/releases?per_page=30";

    /// <summary>El repo elegido, o el oficial.</summary>
    public static string Source(Database db)
    {
        using var c = db.Open();
        return c.ExecuteScalar<string?>("SELECT value FROM setting WHERE machine_id = '' AND name = @SourceSetting", new { SourceSetting }) ?? Official;
    }

    /// <summary>
    /// Cambia el origen solo si `repo` tiene una versión publicada completa; si no, devuelve el error y sigue el anterior. Solo guarda el origen (259):
    /// su versión se busca y se confirma como cualquier otra, aunque sea más antigua que la actual. Lo que esperaba de otro origen se descarta.
    /// </summary>
    public static async Task<ApiError?> SetSource(Database db, string? repo, CancellationToken ct = default)
    {
        repo = string.IsNullOrWhiteSpace(repo) ? Official : repo.Trim().Trim('/');
        if (repo.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) repo = repo["https://github.com/".Length..].TrimEnd('/');
        if (!RepoName.IsMatch(repo)) return ApiError.Of("catalog.sourceName");
        try
        {
            if (Latest(await ReadReleases(Handler is null ? Shared : NewClient(Handler), repo, ct)) is not { } latest
                || !latest.Assets.Any(a => a.Name == DataAsset) || !latest.Assets.Any(a => a.Name == ManifestAsset))
                return ApiError.Of("catalog.sourceEmpty", ("repo", repo));
        }
        catch (HttpRequestException) { return ApiError.Of("catalog.sourceUnreachable", ("repo", repo)); }

        using (var c = db.Open())
            if (string.Equals(repo, Official, StringComparison.OrdinalIgnoreCase)) c.Execute("DELETE FROM setting WHERE machine_id = '' AND name = @SourceSetting", new { SourceSetting });
            else c.Execute("INSERT INTO setting (machine_id, name, value) VALUES ('', @SourceSetting, @repo) ON CONFLICT (name, machine_id) DO UPDATE SET value = excluded.value",
                new { SourceSetting, repo });
        Reject(db);
        return null;
    }

    static async Task<List<Release>> ReadReleases(HttpClient http, string repo, CancellationToken ct) =>
        JsonSerializer.Deserialize<List<Release>>(await http.GetStringAsync(Releases(repo), ct), Json)!;

    // Las versiones del catálogo conviven con otras en el repo (imágenes, por ejemplo): solo cuentan las de este prefijo.
    static Release? Latest(List<Release> releases) => releases.FirstOrDefault(r => !r.Draft && r.TagName.StartsWith(TagPrefix, StringComparison.Ordinal));

    /// <summary>Para las pruebas: responde en lugar de GitHub.</summary>
    public static HttpMessageHandler? Handler { get; set; }
    static readonly HttpClient Shared = NewClient(null);

    static HttpClient NewClient(HttpMessageHandler? handler)
    {
        var http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromMinutes(10);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Otteretro");
        return http;
    }

    /// <summary>Con cada tabla antes de las que apuntan a ella. `claude_fingerprint` y lo de Claude no: esos quedaron en el generador.</summary>
    static readonly string[] Published = ["saga", "game", "version", "version_criterion", "field_value", "route", "route_step", "image"];

    record Asset(string Name, string BrowserDownloadUrl);
    record Release(string TagName, bool Draft, List<Asset> Assets, string? PublishedAt = null);
    record Manifest(string Sha256);

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    record Job(string? Tag);

    /// <summary>Bajar una versión para verla (#222); sin <paramref name="tag"/>, la última. Salvo que ya haya una búsqueda esperando.</summary>
    public static long Enqueue(Database db, string? tag = null)
    {
        using var c = db.Open();
        return c.ExecuteScalar<long?>("SELECT id FROM task WHERE kind = @Kind AND status IN ('queued', 'running') LIMIT 1", new { Kind })
            ?? TaskQueue.Enqueue(db, Kind, tag is null ? null : JsonSerializer.Serialize(new Job(tag), JsonSerializerOptions.Web));
    }

    /// <summary>
    /// Las versiones del origen elegido, de la más nueva a la más vieja, con los cambios de cada una (#228). Las que se pueden bajar salen de
    /// las releases; las anteriores, del `cambios.json` de la última, que tiene todo el historial (#31).
    /// </summary>
    public static async Task<VersionsPage> Versions(Database db, CancellationToken ct = default)
    {
        var http = Handler is null ? Shared : NewClient(Handler);
        var repo = Source(db);
        var releases = (await ReadReleases(http, repo, ct)).Where(r => !r.Draft && r.TagName.StartsWith(TagPrefix, StringComparison.Ordinal)).ToList();
        History? history = null;
        if (releases.FirstOrDefault()?.Assets.FirstOrDefault(a => a.Name == ChangesAsset) is { } asset)
            try { history = JsonSerializer.Deserialize<History>(await http.GetStringAsync(asset.BrowserDownloadUrl, ct), JsonSerializerOptions.Web); }
            catch (Exception e) when (e is HttpRequestException or JsonException) { } // sin historial, cada versión va sin su detalle

        var current = Version(db);
        var tags = releases.Select(r => r.TagName).Union(history?.Versions ?? []).Distinct().OrderDescending(StringComparer.Ordinal);
        var byVersion = history?.Entries.GroupBy(e => e.Version).ToDictionary(g => g.Key, g => g.ToList()) ?? [];
        return new(repo, current, tags.Select(tag =>
        {
            var release = releases.FirstOrDefault(r => r.TagName == tag);
            var entries = byVersion.GetValueOrDefault(tag) ?? [];
            return new VersionInfo(tag, release?.PublishedAt, release?.Assets.Any(a => a.Name == DataAsset) == true, tag == current,
                entries.GroupBy(e => e.Change).ToDictionary(g => g.Key, g => g.Count()),
                entries.Take(ChangesShown).Select(e => new VersionChange(e.Kind, e.Change, e.Id, e.Name, e.From)).ToList());
        }).ToList());
    }

    static string Folder(Database db) => Path.GetDirectoryName(db.CatalogPath)!;
    /// <summary>La versión bajada que espera confirmación, y su `cambios.json` si el catálogo lo publica.</summary>
    public static string PendingFile(Database db) => Path.Combine(Folder(db), "catalogo-pendiente.db");
    internal static string PendingChanges(Database db) => Path.Combine(Folder(db), "catalogo-pendiente.cambios.json");

    static string? Setting(Database db, string name)
    {
        using var c = db.Open();
        return c.ExecuteScalar<string?>("SELECT value FROM setting WHERE machine_id = '' AND name = @name", new { name });
    }

    static void Save(SqliteConnection c, string name, string? value)
    {
        if (value is null) c.Execute("DELETE FROM setting WHERE machine_id = '' AND name = @name", new { name });
        else c.Execute("INSERT INTO setting (machine_id, name, value) VALUES ('', @name, @value) ON CONFLICT (name, machine_id) DO UPDATE SET value = excluded.value",
            new { name, value });
    }

    /// <summary>La versión aplicada; null si todavía no se aplicó ninguna desde que se guarda.</summary>
    public static string? Version(Database db) => Setting(db, AppliedVersion);

    /// <summary>Lo que espera confirmación, si su archivo sigue ahí.</summary>
    public static Pending? Waiting(Database db) =>
        Setting(db, PendingSetting) is { } json && File.Exists(PendingFile(db)) ? JsonSerializer.Deserialize<Pending>(json, JsonSerializerOptions.Web) : null;

    /// <summary>Buscar (#222): baja la versión elegida (#228) o la última, verifica el hash y la deja esperando. No aplica nada.</summary>
    public static async Task Run(TaskRun run, CancellationToken ct)
    {
        var http = Handler is null ? Shared : NewClient(Handler);
        var repo = Source(run.Db);
        var wanted = run.Data is { } data ? JsonSerializer.Deserialize<Job>(data, JsonSerializerOptions.Web)?.Tag : null;
        var releases = await ReadReleases(http, repo, ct);
        var latest = wanted is null ? Latest(releases) : releases.FirstOrDefault(r => !r.Draft && r.TagName == wanted)
            ?? throw new TaskException(ApiError.Of("catalog.versionGone", ("version", wanted)));
        if (latest is null) return;
        string Url(string name) => latest.Assets.FirstOrDefault(a => a.Name == name)?.BrowserDownloadUrl ?? throw new TaskException(ApiError.Of("catalog.badRelease"));

        var manifest = JsonSerializer.Deserialize<Manifest>(await http.GetStringAsync(Url(ManifestAsset), ct), JsonSerializerOptions.Web)!;
        if (Setting(run.Db, Applied) == manifest.Sha256) { Reject(run.Db); return; }
        if (Waiting(run.Db) is { } waiting && waiting.Sha256 == manifest.Sha256 && waiting.Repo == repo) return;

        Reject(run.Db);
        var (gz, plain) = (Path.Combine(Folder(run.Db), "catalogo-nuevo.db.gz"), Path.Combine(Folder(run.Db), "catalogo-nuevo.tmp"));
        try
        {
            run.Report(0, ApiError.Of("catalog.downloading", ("version", latest.TagName)));
            using (var response = await http.GetAsync(Url(DataAsset), HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                await using var file = File.Create(gz);
                await response.Content.CopyToAsync(file, ct);
            }
            await using (var stream = File.OpenRead(gz))
                if (Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct)) != manifest.Sha256) throw new TaskException(ApiError.Of("catalog.badHash"));
            await using (var input = new GZipStream(File.OpenRead(gz), CompressionMode.Decompress))
            await using (var output = File.Create(plain))
                await input.CopyToAsync(output, ct);
            CheckFile(plain);
            // El diff publicado (#223, decisión 260). Sin él, la app usa solo su propia comparación.
            if (latest.Assets.Any(a => a.Name == ChangesAsset))
                await File.WriteAllBytesAsync(PendingChanges(run.Db), await http.GetByteArrayAsync(Url(ChangesAsset), ct), ct);
            File.Move(plain, PendingFile(run.Db), overwrite: true);
            using var c = run.Db.Open();
            Save(c, PendingSetting, JsonSerializer.Serialize(new Pending(repo, latest.TagName, manifest.Sha256), JsonSerializerOptions.Web));
        }
        finally
        {
            File.Delete(gz);
            File.Delete(plain);
        }
    }

    /// <summary>Aceptar (#222): aplica entero lo que espera. Sin nada esperando, no hace nada.</summary>
    public static ApiError? Accept(Database db)
    {
        if (Waiting(db) is not { } pending) return ApiError.Of("catalog.nothingPending");
        Apply(db, PendingFile(db));
        using (var c = db.Open())
        {
            Save(c, Applied, pending.Sha256);
            Save(c, AppliedVersion, pending.Tag);
        }
        Reject(db);
        SagaCache.Refresh(db); // la versión aceptada se ve al instante (#225)
        return null;
    }

    /// <summary>Rechazar: descarta lo bajado; el catálogo queda como estaba.</summary>
    public static void Reject(Database db)
    {
        SqliteConnection.ClearAllPools(); // la comparación pudo dejar el archivo abierto
        File.Delete(PendingFile(db));
        File.Delete(PendingChanges(db));
        using var c = db.Open();
        Save(c, PendingSetting, null);
    }

    /// <summary>Reemplaza el catálogo por el de <paramref name="file"/>, y pasa lo de la persona a lo que reemplazó a cada juego y versión. Se puede repetir sin daño.</summary>
    public static void Apply(Database db, string file)
    {
        CheckFile(file);
        using var c = db.Open();
        c.Execute("ATTACH DATABASE @file AS initial", new { file });
        try
        {
            // Lo bajado cuya referencia cambió o ya no está se borra después, para que se baje la nueva (#215). Un catálogo sin imágenes no borra nada.
            var changed = c.ExecuteScalar<long>("SELECT count(*) FROM initial.sqlite_master WHERE name = 'image'") == 0 ? []
                : c.Query<(long GameId, string Kind)>("""
                    SELECT o.game_id, o.kind FROM catalog.image o
                    WHERE NOT EXISTS (SELECT 1 FROM initial.image n WHERE n.game_id = o.game_id AND n.kind = o.kind AND n.url = o.url)
                    """).ToList();
            using (var t = c.BeginTransaction())
            {
                foreach (var table in Published.Reverse()) c.Execute($"DELETE FROM catalog.{table}", transaction: t);
                // Las consolas no se borran: tus discos y destinos apuntan a ellas.
                Copy(c, t, "console", "INSERT OR IGNORE");
                Copy(c, t, "console_platform", "INSERT OR REPLACE");
                foreach (var table in Published) Copy(c, t, table, "INSERT");
                t.Commit();
            }
            ImageRefs.Forget(db, changed);
            MoveWhatIsYours(c);
            Steam.Link(c); // lo de Steam que recién ahora está en el catálogo
            c.Execute("UPDATE cache_generation SET value = value + 1"); // Sagas guardado ya no vale (#225)
        }
        finally { c.Execute("DETACH DATABASE initial"); }
    }

    static void Copy(SqliteConnection c, SqliteTransaction t, string table, string verb)
    {
        var columns = string.Join(", ", c.Query<string>($"SELECT name FROM pragma_table_info('{table}', 'catalog') WHERE name IN (SELECT name FROM pragma_table_info('{table}', 'initial'))", transaction: t));
        if (columns.Length > 0) c.Execute($"{verb} INTO catalog.{table} ({columns}) SELECT {columns} FROM initial.{table}", transaction: t);
    }

    /// <summary>Un archivo que no se puede abrir o viene vacío no toca nada: el hash dice que llegó bien, no que sirve.</summary>
    static void CheckFile(string file)
    {
        try
        {
            using var check = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            check.Open();
            if (check.ExecuteScalar<string>("PRAGMA quick_check") != "ok" || check.ExecuteScalar<long>("SELECT count(*) FROM game") == 0)
                throw new TaskException(ApiError.Of("catalog.badFile"));
        }
        catch (SqliteException) { throw new TaskException(ApiError.Of("catalog.badFile")); }
    }

    /// <summary>
    /// Progreso, colección, enlaces y sesiones apuntan por id. Si un juego se fusionó, pasan al que queda; si ya tenías datos en ese, mandan los del que queda.
    /// Un archivo cuya versión se quitó sin reemplazo vuelve a quedar sin identificar: nunca se borra nada tuyo.
    /// </summary>
    static void MoveWhatIsYours(SqliteConnection c)
    {
        var replaced = c.ExecuteScalar<long>("SELECT count(*) FROM initial.sqlite_master WHERE name = 'replaced'") == 0 ? []
            : c.Query<(string Kind, long OldId, long? NewId)>("SELECT kind, old_id, new_id FROM initial.replaced").ToList();
        using var t = c.BeginTransaction();
        foreach (var (kind, oldId, newId) in replaced)
            if (kind == "game")
            {
                if (newId is null) continue; // sin reemplazo: tu progreso queda guardado por si el juego vuelve
                foreach (var table in new[] { "progress", "price_watch", "dismissal" }.Where(x => HasColumn(c, t, x, "game_id")))
                {
                    c.Execute($"UPDATE OR IGNORE main.{table} SET game_id = @newId WHERE game_id = @oldId", new { newId, oldId }, t);
                    c.Execute($"DELETE FROM main.{table} WHERE game_id = @oldId", new { oldId }, t);
                }
                c.Execute("UPDATE main.download_link SET game_id = @newId WHERE game_id = @oldId", new { newId, oldId }, t);
            }
            else
            {
                c.Execute("UPDATE main.source SET version_id = @newId WHERE version_id = @oldId", new { newId, oldId }, t);
                c.Execute("UPDATE main.download_link SET version_id = @newId WHERE version_id = @oldId", new { newId, oldId }, t);
                if (newId is not null && HasColumn(c, t, "play_session", "version_id"))
                    c.Execute("UPDATE main.play_session SET version_id = @newId WHERE version_id = @oldId", new { newId, oldId }, t);
            }
        // Una versión que ya no existe, aunque no figure en `replaced`: el archivo vuelve a quedar sin identificar, recordando a cuál apuntaba (#217).
        c.Execute("UPDATE main.source SET lost_version_id = version_id, version_id = NULL WHERE version_id IS NOT NULL AND version_id NOT IN (SELECT id FROM catalog.version)", transaction: t);
        // Y si un catálogo la vuelve a tener (volver al oficial desde un fork), se recupera sola.
        c.Execute("UPDATE main.source SET version_id = lost_version_id, lost_version_id = NULL WHERE version_id IS NULL AND lost_version_id IN (SELECT id FROM catalog.version)", transaction: t);
        t.Commit();
    }

    static bool HasColumn(SqliteConnection c, SqliteTransaction t, string table, string column) =>
        c.ExecuteScalar<long>($"SELECT count(*) FROM pragma_table_info('{table}', 'main') WHERE name = @column", new { column }, t) > 0;
}
