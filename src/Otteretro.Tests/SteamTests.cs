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
            else if (url == Igdb.TokenUrl) json = """{ "access_token": "token", "expires_in": 3600 }""";
            else if (url.EndsWith("/external_games") && body.Contains("\"70\""))
                json = """[ { "id": 1, "game": 231, "uid": "70" }, { "id": 2, "game": 233, "uid": "220" } ]""";
            else if (url.EndsWith("/games") && body.Contains("where id = (231,233)"))
                json = """
                    [ { "id": 231, "name": "Half-Life", "first_release_date": 910000000, "game_type": 0, "platforms": [6],
                        "franchises": [ { "id": 50, "name": "Half-Life" } ], "external_games": [ { "id": 1, "uid": "70", "external_game_source": 1 } ] },
                      { "id": 233, "name": "Half-Life 2", "first_release_date": 1100000000, "game_type": 0, "platforms": [6],
                        "franchises": [ { "id": 50, "name": "Half-Life" } ], "external_games": [ { "id": 3, "uid": "220", "external_game_source": 1 } ] } ]
                    """;
            return json is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("[]") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }

    readonly FakeServers servers = new();

    public SteamTests()
    {
        Steam.Handler = servers;
        IgdbImport.Handler = servers;
    }

    static readonly (string, string)[] Keys = [("steam.apiKey", "clave"), ("igdb.clientId", "id"), ("igdb.clientSecret", "secreto")];

    /// <summary>Listo cuando de F1.8: después de sincronizar, Half-Life sale como «lo tengo» con sus horas.</summary>
    [Fact]
    public async Task AfterSyncHalfLifeIsOwnedWithItsHours()
    {
        await using var f = new Fixture(Keys);
        Steam.LoginUsersPath = f.Tmp["loginusers.vdf"];
        File.WriteAllText(Steam.LoginUsersPath, LoginUsers);

        Assert.Equal("done", (await f.Run(Steam.EnqueueSync(f.Db))).Status);
        Assert.Contains("steamid=76561190000000002", servers.SteamQuery);

        long link;
        using (var c = f.Db.Open())
        {
            Assert.Equal(3, c.ExecuteScalar<int>("SELECT count(*) FROM source WHERE store = 'steam'"));
            link = c.ExecuteScalar<long>("SELECT id FROM task WHERE kind = 'igdb.steam'");
        }
        Assert.Equal("done", (await f.Run(link)).Status);

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
