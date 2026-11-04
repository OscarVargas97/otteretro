using System.Net;
using Dapper;
using Otteretro.Catalog;

namespace Otteretro.Tests;

/// <summary>F1.8 con Steam e IGDB falsos.</summary>
[Collection("FakeHttp")]
public class SteamTests
{
    const string LoginUsers = """
        "users"
        {
            "76561190000000001"
            {
                "AccountName"   "otra"
                "MostRecent"    "0"
            }
            "76561190000000002"
            {
                "AccountName"   "yo"
                "MostRecent"    "1"
            }
        }
        """;

    sealed class FakeServers : HttpMessageHandler
    {
        public string? SteamQuery;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.AbsoluteUri;
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            string? json = null;
            if (url.StartsWith(Steam.OwnedGamesUrl))
            {
                SteamQuery = request.RequestUri.Query;
                json = """
                    { "response": { "game_count": 3, "games": [
                        { "appid": 70, "name": "Half-Life", "playtime_forever": 600, "rtime_last_played": 1700000000 },
                        { "appid": 220, "name": "Half-Life 2", "playtime_forever": 0, "rtime_last_played": 0 },
                        { "appid": 999, "name": "Algo que IGDB no tiene", "playtime_forever": 5, "rtime_last_played": 0 } ] } }
                    """;
            }
            return json is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("[]") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }

    readonly FakeServers servers = new();

    public SteamTests()
    {
        Steam.Handler = servers;
    }

    static readonly (string, string)[] Keys = [("steam.apiKey", "clave")];

    /// <summary>Listo cuando de F1.8: después de sincronizar, Half-Life sale como «lo tengo» con sus horas.</summary>
    [Fact]
    public async Task AfterSyncHalfLifeIsOwnedWithItsHours()
    {
        await using var f = new Fixture(Keys);
        Steam.LoginUsersPath = f.Tmp["loginusers.vdf"];
        File.WriteAllText(Steam.LoginUsersPath, LoginUsers);
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO catalog.saga (id, name) VALUES (1, 'Half-Life');
                INSERT INTO catalog.game (id, saga_id, name_es) VALUES (1, 1, 'Half-Life'), (2, 1, 'Half-Life 2');
                INSERT INTO catalog.version (id, game_id, platform, steam_app_id) VALUES (1, 1, 'pc', 70), (2, 2, 'pc', 220);
                """);

        Assert.Equal("done", (await f.Run(Steam.EnqueueSync(f.Db))).Status);
        Assert.Contains("steamid=76561190000000002", servers.SteamQuery);

        using (var c = f.Db.Open())
            Assert.Equal(3, c.ExecuteScalar<int>("SELECT count(*) FROM source WHERE store = 'steam'"));

        using var db = f.Db.Open();
        var halfLife = db.QuerySingle<(string Name, string Saga, long Playtime, string LastPlayed)>("""
            SELECT g.name_es, s.name, o.playtime, o.last_played FROM source o
            JOIN version v ON v.id = o.version_id JOIN game g ON g.id = v.game_id JOIN saga s ON s.id = g.saga_id
            WHERE o.store_id = '70'
            """);
        Assert.Equal(("Half-Life", "Half-Life", 600L), (halfLife.Name, halfLife.Saga, halfLife.Playtime));
        Assert.StartsWith("2023-11-14", halfLife.LastPlayed);
        Assert.Equal("Half-Life 2", db.ExecuteScalar<string>("SELECT g.name_es FROM source o JOIN version v ON v.id = o.version_id JOIN game g ON g.id = v.game_id WHERE o.store_id = '220'"));
        // Lo que IGDB no conoce sigue apareciendo, con su nombre de Steam.
        Assert.Equal("Algo que IGDB no tiene", db.ExecuteScalar<string>("SELECT name FROM source WHERE store_id = '999' AND version_id IS NULL"));
    }

    [Fact]
    public async Task WithoutKeyOrAccountTheTaskSaysWhy()
    {
        await using (var f = new Fixture())
            Assert.Equal("tasks.steam.noKey", (await f.Run(Steam.EnqueueSync(f.Db))).Detail!.Code);
        await using (var f = new Fixture(Keys))
        {
            Steam.LoginUsersPath = f.Tmp["no-existe.vdf"];
            Assert.Equal("tasks.steam.noAccount", (await f.Run(Steam.EnqueueSync(f.Db))).Detail!.Code);
        }
    }
}
