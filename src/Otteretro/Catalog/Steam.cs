using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Catalog;

/// <summary>
/// Lo comprado en Steam como «lo tengo · versión PC», con horas y última vez (F1.8, §6.6). Tarea `steam.sync`:
/// lee la cuenta detectada en loginusers.vdf, pide `GetOwnedGames` y guarda cada juego como origen de tienda.
/// Lo que no está en el catálogo se busca en IGDB por su id de Steam (tarea `igdb.steam`).
/// </summary>
public static partial class Steam
{
    public const string OwnedGamesUrl = "https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/";

    /// <summary>Para las pruebas: el manejador HTTP falso y la ruta de un loginusers.vdf de ejemplo.</summary>
    public static HttpMessageHandler? Handler { get; set; }
    public static string? LoginUsersPath { get; set; }

    static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromSeconds(30) };

    record Owned(OwnedResponse? Response);
    record OwnedResponse(OwnedGame[]? Games);
    record OwnedGame(long Appid, string? Name, long PlaytimeForever, long RtimeLastPlayed);
    /// <summary>Punto de control de `igdb.steam`: el último id de Steam ya buscado.</summary>
    public record LinkJob(long After = 0);

    public static long EnqueueSync(Database db) => TaskQueue.Enqueue(db, "steam.sync", "{}");

    public static async Task Sync(TaskRun run, CancellationToken ct)
    {
        var key = run.Credentials.Read("steam.apiKey");
        if (string.IsNullOrEmpty(key)) throw new TaskException(ApiError.Of("tasks.steam.noKey"));
        var account = DetectAccount(LoginUsersPath ?? DefaultLoginUsersPath()) ?? throw new TaskException(ApiError.Of("tasks.steam.noAccount"));

        // La clave va en la URL: por eso no pasa por api_cache y nunca se escribe la URL en un error o en el log.
        var http = Handler is null ? Shared : new HttpClient(Handler);
        using var response = await http.GetAsync($"{OwnedGamesUrl}?key={Uri.EscapeDataString(key)}&steamid={account}&include_appinfo=1&include_played_free_games=1", ct);
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new TaskException(ApiError.Of("tasks.steam.badKey"));
        if (!response.IsSuccessStatusCode) throw new TaskException(ApiError.Of("tasks.steam.failed", ("status", ((int)response.StatusCode).ToString())));
        var games = JsonSerializer.Deserialize<Owned>(await response.Content.ReadAsStringAsync(ct), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
            ?.Response?.Games ?? [];

        using (var c = run.Db.Open())
        using (var t = c.BeginTransaction())
        {
            foreach (var g in games)
                c.Execute("""
                    INSERT INTO source (kind, store, store_id, name, playtime, last_played) VALUES ('store', 'steam', @id, @Name, @PlaytimeForever, @last)
                    ON CONFLICT (store, store_id) WHERE kind = 'store'
                    DO UPDATE SET name = excluded.name, playtime = excluded.playtime, last_played = excluded.last_played
                    """, new { id = g.Appid.ToString(), g.Name, g.PlaytimeForever, last = g.RtimeLastPlayed > 0 ? DateTimeOffset.FromUnixTimeSeconds(g.RtimeLastPlayed).UtcDateTime.ToString("O") : null }, t);
            Link(c, t);
            t.Commit();
        }
        run.Report(100, ApiError.Of("tasks.steam.synced", ("count", games.Length.ToString())));

        using (var c = run.Db.Open())
            if (c.ExecuteScalar<long>("SELECT count(*) FROM source WHERE store = 'steam' AND version_id IS NULL") > 0
                && c.ExecuteScalar<long>("SELECT count(*) FROM task WHERE kind = 'igdb.steam' AND status = 'queued'") == 0)
                TaskQueue.Enqueue(run.Db, "igdb.steam", JsonSerializer.Serialize(new LinkJob(), JsonSerializerOptions.Web));
    }

    /// <summary>Enlaza los orígenes de Steam sueltos con la versión de PC que tiene ese id de Steam.</summary>
    public static void Link(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? t = null) =>
        c.Execute("""
            UPDATE source SET version_id = (SELECT v.id FROM version v WHERE v.platform IN ('pc', 'pcvr') AND v.steam_app_id = CAST(source.store_id AS INTEGER) ORDER BY v.platform LIMIT 1)
            WHERE kind = 'store' AND store = 'steam' AND version_id IS NULL
            """, transaction: t);

    /// <summary>La cuenta con la que se entró por última vez (MostRecent), o la primera; null si no hay Steam.</summary>
    public static string? DetectAccount(string? vdfPath)
    {
        if (vdfPath is null || !File.Exists(vdfPath)) return null;
        var users = UserBlock().Matches(File.ReadAllText(vdfPath)).Select(m => (Id: m.Groups[1].Value, Body: m.Groups[2].Value)).ToList();
        return users.Where(u => MostRecent().IsMatch(u.Body)).Concat(users).Select(u => u.Id).FirstOrDefault();
    }

    // ponytail: rutas por defecto de Steam; si está en otro lugar se suma un ajuste cuando aparezca el caso.
    static string? DefaultLoginUsersPath()
    {
        string? root = null;
        if (OperatingSystem.IsWindows())
            root = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                   ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            root = new[] { ".local/share/Steam", ".steam/steam" }.Select(p => Path.Combine(home, p)).FirstOrDefault(Directory.Exists);
        }
        return root is null ? null : Path.Combine(root, "config", "loginusers.vdf");
    }

    [GeneratedRegex("\"(\\d{17})\"\\s*\\{([^}]*)\\}")]
    private static partial Regex UserBlock();

    [GeneratedRegex("\"MostRecent\"\\s*\"1\"", RegexOptions.IgnoreCase)]
    private static partial Regex MostRecent();
}
