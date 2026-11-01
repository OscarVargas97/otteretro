using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <param name="State">unchecked, ok, unavailable, blocked, noConnector o unknown.</param>
/// <param name="Status">captured (en el capturador) o queued (ya pasó a la cola).</param>
public record CapturedLink(long Id, string Url, string Name, long? Size, string? Site, string State, ApiError? Detail, long? GameId, string Status);

public record LinkPackage(string Name, List<CapturedLink> Links, long Size);

/// <summary>
/// El capturador de enlaces (#210, decisión 245), como en JDownloader: lo que pegas o copias queda guardado hasta que lo mandas a la cola o lo quitas.
/// Son datos privados: van en `otteretro.db`, no en `catalogo.db`, que es lo que se puede compartir.
/// </summary>
public static class DownloadLinks
{
    record Row(long Id, string Url, string Name, string Package, string? Site, long? Size, string State, string? Detail, long? GameId, string Status);

    const string Columns = "id, url, name, package, site, size, state, detail, game_id AS GameId, status";

    static CapturedLink ToLink(Row r) => new(r.Id, r.Url, r.Name, r.Size, r.Site, r.State,
        string.IsNullOrEmpty(r.Detail) ? null : JsonSerializer.Deserialize<ApiError>(r.Detail, JsonSerializerOptions.Web), r.GameId, r.Status);

    /// <summary>Guarda los enlaces de un texto, sin consultar los sitios.</summary>
    /// <param name="revive">Un enlace que ya bajaste vuelve al capturador: lo pediste tú. Lo copiado (<paramref name="only"/>) no.</param>
    /// <param name="only">Qué sitios entran; sin esto, todos.</param>
    /// <returns>Cuántos enlaces reconoció y cuántos no estaban.</returns>
    public static (int Found, int New) Add(Database db, string text, long? gameId = null, bool revive = true, Func<IDownloadSite, bool>? only = null)
    {
        var count = 0;
        using var c = db.Open();
        var before = c.ExecuteScalar<int>("SELECT count(*) FROM download_link");
        foreach (var raw in Downloads.LinksIn(text))
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var url)) continue;
            var site = Downloads.SiteOf(url);
            if (only is not null && (site is null || !only(site))) continue;
            var clean = site is OneFichierSite ? OneFichierSite.Canonical(url) : raw;
            var name = Downloads.FileNameOf(url);
            // Sin nombre (un magnet, por ejemplo): cada uno en su paquete hasta comprobarlo.
            if (name == Downloads.NoName) name += "-" + Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(clean)))[..8].ToLowerInvariant();
            c.Execute("""
                INSERT INTO download_link (url, name, package, site, game_id, created_at) VALUES (@clean, @name, @package, @Id, @gameId, @now)
                ON CONFLICT (url) DO UPDATE SET game_id = COALESCE(excluded.game_id, game_id),
                    status = CASE WHEN @revive THEN 'captured' ELSE status END
                """, new { clean, name, package = Downloads.PackageOf(name), site?.Id, gameId, now = DateTime.UtcNow.ToString("O"), revive });
            count++;
        }
        return (count, c.ExecuteScalar<int>("SELECT count(*) FROM download_link") - before);
    }

    /// <summary>Lo que espera en el capturador, por paquete.</summary>
    public static List<LinkPackage> Captured(Database db)
    {
        using var c = db.Open();
        return c.Query<Row>($"SELECT {Columns} FROM download_link WHERE status = 'captured' ORDER BY id").GroupBy(r => r.Package, StringComparer.OrdinalIgnoreCase)
            .Select(g => new LinkPackage(g.Key, g.Select(ToLink).ToList(), g.Sum(r => r.Size ?? 0))).ToList();
    }

    /// <summary>Los enlaces de un juego, para su ficha.</summary>
    public static List<CapturedLink> OfGame(Database db, long gameId)
    {
        using var c = db.Open();
        return c.Query<Row>($"SELECT {Columns} FROM download_link WHERE game_id = @gameId OR version_id IN (SELECT id FROM catalog.version WHERE game_id = @gameId) ORDER BY package, name", new { gameId }).Select(ToLink).ToList();
    }

    /// <summary>
    /// Comprueba lo que todavía no está bien. Solo cuando lo pides: repetir peticiones que fallan puede hacer que un sitio bloquee tu cuenta.
    /// </summary>
    public static async Task Check(Database db, Credentials credentials, CancellationToken ct)
    {
        List<string> urls;
        using (var c = db.Open()) urls = c.Query<string>("SELECT url FROM download_link WHERE status = 'captured' AND state <> 'ok'").ToList();
        if (urls.Count == 0) return;
        var packages = await Downloads.Preview(string.Join('\n', urls), credentials, ct);
        using var write = db.Open();
        foreach (var link in packages.SelectMany(p => p.Links))
        {
            var known = link.Name != Downloads.FileNameOf(new Uri(link.Url));
            write.Execute("""
                UPDATE download_link SET state = @State, detail = @detail, site = @Site, size = COALESCE(@Size, size),
                    name = CASE WHEN @known THEN @Name ELSE name END, package = CASE WHEN @known THEN @package ELSE package END
                WHERE url = @Url
                """, new { link.State, detail = link.Detail is null ? null : JsonSerializer.Serialize(link.Detail, JsonSerializerOptions.Web), link.Site, link.Size, known, link.Name, package = Downloads.PackageOf(link.Name), link.Url });
        }
    }

    /// <summary>Pasa paquetes a la cola. Los enlaces siguen registrados: salen del capturador, no de la base.</summary>
    public static ApiError? Queue(Database db, List<string> packages, string? diskId, int connections)
    {
        var wanted = packages.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var package in Captured(db).Where(p => wanted.Contains(p.Name)))
        {
            var gameId = package.Links.Select(l => l.GameId).FirstOrDefault(g => g is not null);
            var (task, error) = Downloads.Enqueue(db, package.Name, diskId, package.Links.Select(l => l.Url).ToList(), gameId, connections);
            if (error is not null) return error;
            using var c = db.Open();
            c.Execute("UPDATE download_link SET status = 'queued', task_id = @task WHERE id IN @ids", new { task, ids = package.Links.Select(l => l.Id) });
        }
        return null;
    }

    public static void Remove(Database db, long id)
    {
        using var c = db.Open();
        c.Execute("DELETE FROM download_link WHERE id = @id", new { id });
    }
}
