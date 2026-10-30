using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Catalog;

/// <summary>
/// Portadas 2:3, héroes y logos de SteamGridDB (F1.7) en `cache/imagenes/{juego}-{tipo}.{ext}`.
/// La tarea `sgdb.images` recorre los juegos sin portada, con punto de control; la encola cada carga de IGDB.
/// </summary>
public static class SteamGridDb
{
    public const string ApiUrl = "https://www.steamgriddb.com/api/v2/";
    public static readonly string[] Kinds = ["cover", "hero", "logo"];

    /// <summary>Qué se pide de cada tipo: portada vertical 2:3 (como la de Steam), héroe y logo.</summary>
    static readonly Dictionary<string, string> Queries = new()
    {
        ["cover"] = "grids/game/{0}?dimensions=600x900,342x482,660x930",
        ["hero"] = "heroes/game/{0}",
        ["logo"] = "logos/game/{0}",
    };

    static readonly TimeSpan Ttl = TimeSpan.FromDays(30);
    static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromSeconds(60) };

    /// <summary>Para las pruebas: el manejador HTTP falso de SteamGridDB y su CDN.</summary>
    public static HttpMessageHandler? Handler { get; set; }

    public record Job(long After = 0);
    record Found(long Id);
    record Image(string Url);
    record Response<T>(bool Success, T? Data);

    public static long Enqueue(Database db) => TaskQueue.Enqueue(db, "sgdb.images", JsonSerializer.Serialize(new Job(), JsonSerializerOptions.Web));

    public static string Folder(Database db) => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(db.FilePath)!)!, "cache", "imagenes");

    /// <summary>La imagen guardada de un juego, o null si todavía no hay.</summary>
    /// <summary>Borra las imágenes de un juego que ya no existe.</summary>
    public static void Forget(Database db, long gameId)
    {
        foreach (var kind in Kinds)
            while (Find(db, gameId, kind) is { } file) File.Delete(file);
    }

    public static string? Find(Database db, long gameId, string kind) =>
        Directory.Exists(Folder(db)) ? Directory.EnumerateFiles(Folder(db), $"{gameId}-{kind}.*").FirstOrDefault() : null;

    public static async Task Images(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<Job>(run.Data!, JsonSerializerOptions.Web)!;
        var key = run.Credentials.Read("steamgriddb.apiKey");
        if (string.IsNullOrEmpty(key)) throw new TaskException(ApiError.Of("tasks.sgdb.noKey"));

        var api = new ApiClient(run.Db, Handler);
        var http = Handler is null ? Shared : new HttpClient(Handler);
        var folder = Folder(run.Db);
        Directory.CreateDirectory(folder);

        List<(long Id, string Name, long? Steam)> games;
        using (var c = run.Db.Open())
            games = c.Query<(long, string, long?)>("""
                SELECT g.id, COALESCE(g.original_name, g.name_es), (SELECT max(steam_app_id) FROM version v WHERE v.game_id = g.id)
                FROM game g WHERE g.id > @After ORDER BY g.id
                """, job).ToList();
        games = games.Where(g => Find(run.Db, g.Id, "cover") is null).ToList();

        for (var i = 0; i < games.Count; i++)
        {
            var (id, name, steam) = games[i];
            var sgdbId = await GameId(api, key, name, steam, ct);
            if (sgdbId is not null)
                foreach (var kind in Kinds)
                    if (Find(run.Db, id, kind) is null && await First(api, key, string.Format(Queries[kind], sgdbId), ct) is { } url)
                        await Download(http, url, Path.Combine(folder, $"{id}-{kind}{Path.GetExtension(new Uri(url).AbsolutePath)}"), ct);
            run.Report((i + 1) * 100 / games.Count, ApiError.Of("tasks.sgdb.images", ("done", (i + 1).ToString()), ("total", games.Count.ToString())),
                JsonSerializer.Serialize(new Job(id), JsonSerializerOptions.Web));
        }
    }

    /// <summary>El juego en SteamGridDB: por el id de Steam si lo hay, si no por el nombre (el primer resultado).</summary>
    static async Task<long?> GameId(ApiClient api, string key, string name, long? steam, CancellationToken ct)
    {
        if (steam is not null && await Get<Found>(api, key, $"games/steam/{steam}", ct) is { } bySteam) return bySteam.Id;
        var list = await Get<Found[]>(api, key, $"search/autocomplete/{Uri.EscapeDataString(name)}", ct);
        return list?.FirstOrDefault()?.Id;
    }

    static async Task<string?> First(ApiClient api, string key, string path, CancellationToken ct) =>
        (await Get<Image[]>(api, key, path, ct))?.FirstOrDefault()?.Url;

    /// <summary>Un 404 (juego sin ficha o sin imágenes) es «no hay», no un error.</summary>
    static async Task<T?> Get<T>(ApiClient api, string key, string path, CancellationToken ct)
    {
        try
        {
            var text = await api.Send("steamgriddb", HttpMethod.Get, ApiUrl + path, null, Ttl,
                r => r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key), ct);
            return JsonSerializer.Deserialize<Response<T>>(text, JsonSerializerOptions.Web) is { Success: true } r ? r.Data : default;
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new TaskException(ApiError.Of("tasks.sgdb.badKey"));
        }
    }

    /// <summary>Baja a un temporal y lo renombra al final: una descarga cortada no deja una imagen rota.</summary>
    static async Task Download(HttpClient http, string url, string path, CancellationToken ct)
    {
        var temp = Path.Combine(Path.GetDirectoryName(path)!, "." + Path.GetFileName(path) + ".part");
        using (var response = await http.GetAsync(url, ct))
        {
            response.EnsureSuccessStatusCode();
            await using var file = File.Create(temp);
            await response.Content.CopyToAsync(file, ct);
        }
        File.Move(temp, path, overwrite: true);
    }
}
