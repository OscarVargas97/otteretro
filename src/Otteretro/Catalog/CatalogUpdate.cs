using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Catalog;

/// <summary>
/// La app toma el catálogo de lo publicado en `otteretro-catalogo` (#213, decisión 253), sin claves. Baja a un temporal, verifica el hash y recién
/// entonces reemplaza los datos, todo o nada: si algo falla, sigue el catálogo anterior. Lo de cada persona vive en otra base y apunta por id; lo
/// fusionado o quitado se corrige con `replaced`.
/// </summary>
public static class CatalogUpdate
{
    public const string Kind = "catalog.update";
    const string Releases = "https://api.github.com/repos/OscarVargas97/otteretro-catalogo/releases?per_page=30";
    const string TagPrefix = "catalogo-", DataAsset = "catalogo.db.gz", ManifestAsset = "catalogo.db.gz.json";
    /// <summary>El hash de lo último aplicado. Se escribe al final: un corte a mitad hace que se repita.</summary>
    const string Applied = "catalog.published";

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
    static readonly string[] Published = ["saga", "game", "version", "version_criterion", "field_value", "route", "route_step"];

    record Asset(string Name, string BrowserDownloadUrl);
    record Release(string TagName, bool Draft, List<Asset> Assets);
    record Manifest(string Sha256);

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>Busca al abrir la app, salvo que ya haya una esperando.</summary>
    public static long Enqueue(Database db)
    {
        using var c = db.Open();
        return c.ExecuteScalar<long?>("SELECT id FROM task WHERE kind = @Kind AND status IN ('queued', 'running') LIMIT 1", new { Kind }) ?? TaskQueue.Enqueue(db, Kind);
    }

    public static async Task Run(TaskRun run, CancellationToken ct)
    {
        var http = Handler is null ? Shared : NewClient(Handler);
        // Las versiones del catálogo conviven con otras en el repo (imágenes, por ejemplo): solo cuentan las de este prefijo.
        var releases = JsonSerializer.Deserialize<List<Release>>(await http.GetStringAsync(Releases, ct), Json)!;
        if (releases.FirstOrDefault(r => !r.Draft && r.TagName.StartsWith(TagPrefix, StringComparison.Ordinal)) is not { } latest) return;
        string Url(string name) => latest.Assets.FirstOrDefault(a => a.Name == name)?.BrowserDownloadUrl ?? throw new TaskException(ApiError.Of("catalog.badRelease"));

        var manifest = JsonSerializer.Deserialize<Manifest>(await http.GetStringAsync(Url(ManifestAsset), ct), JsonSerializerOptions.Web)!;
        using (var c = run.Db.Open())
            if (c.ExecuteScalar<string?>("SELECT value FROM setting WHERE machine_id = '' AND name = @Applied", new { Applied }) == manifest.Sha256) return;

        var folder = Path.GetDirectoryName(run.Db.CatalogPath)!;
        var (gz, plain) = (Path.Combine(folder, "catalogo-nuevo.db.gz"), Path.Combine(folder, "catalogo-nuevo.tmp"));
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

            run.Report(50, ApiError.Of("catalog.applying", ("version", latest.TagName)));
            Apply(run.Db, plain);
            using var done = run.Db.Open();
            done.Execute("INSERT INTO setting (machine_id, name, value) VALUES ('', @Applied, @Sha256) ON CONFLICT (name, machine_id) DO UPDATE SET value = excluded.value",
                new { Applied, manifest.Sha256 });
        }
        finally
        {
            File.Delete(gz);
            File.Delete(plain);
        }
    }

    /// <summary>Reemplaza el catálogo por el de <paramref name="file"/>, y pasa lo de la persona a lo que reemplazó a cada juego y versión. Se puede repetir sin daño.</summary>
    public static void Apply(Database db, string file)
    {
        CheckFile(file);
        using var c = db.Open();
        c.Execute("ATTACH DATABASE @file AS initial", new { file });
        try
        {
            using (var t = c.BeginTransaction())
            {
                foreach (var table in Published.Reverse()) c.Execute($"DELETE FROM catalog.{table}", transaction: t);
                // Las consolas no se borran: tus discos y destinos apuntan a ellas.
                Copy(c, t, "console", "INSERT OR IGNORE");
                Copy(c, t, "console_platform", "INSERT OR REPLACE");
                foreach (var table in Published) Copy(c, t, table, "INSERT");
                t.Commit();
            }
            MoveWhatIsYours(c);
            Steam.Link(c); // lo de Steam que recién ahora está en el catálogo
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
        if (c.ExecuteScalar<long>("SELECT count(*) FROM initial.sqlite_master WHERE name = 'replaced'") == 0) return;
        var replaced = c.Query<(string Kind, long OldId, long? NewId)>("SELECT kind, old_id, new_id FROM initial.replaced").ToList();
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
        t.Commit();
    }

    static bool HasColumn(SqliteConnection c, SqliteTransaction t, string table, string column) =>
        c.ExecuteScalar<long>($"SELECT count(*) FROM pragma_table_info('{table}', 'main') WHERE name = @column", new { column }, t) > 0;
}
