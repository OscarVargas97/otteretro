using System.Net;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Catalog;
using Otteretro.Data;

namespace Otteretro.Tests;

/// <summary>F1.5 con un IGDB falso: la saga Zelda resumida, con lo que el importador tiene que resolver.</summary>
[Collection("FakeHttp")]
public class IgdbImportTests
{
    const string Zelda = """{ "id": 596, "name": "The Legend of Zelda" }""";

    const string OcarinaOfTime = """
        { "id": 1029, "name": "The Legend of Zelda: Ocarina of Time", "first_release_date": 911606400, "game_type": 0, "platforms": [4],
          "remakes": [1030], "franchises": [ZELDA],
          "alternative_names": [ { "id": 1, "name": "La leyenda de Zelda: Ocarina of Time", "comment": "Spanish title" } ],
          "release_dates": [ { "platform": 4, "date": 911606400 } ] }
        """;

    /// <summary>El remake de 3DS: no viene en la página de la franquicia y hay que pedirlo aparte.</summary>
    const string OcarinaOfTime3D = """
        { "id": 1030, "name": "The Legend of Zelda: Ocarina of Time 3D", "first_release_date": 1308873600, "game_type": 8, "platforms": [37],
          "franchises": [ZELDA], "release_dates": [ { "platform": 37, "date": 1308873600 } ] }
        """;

    const string BreathOfTheWild = """
        { "id": 7346, "name": "The Legend of Zelda: Breath of the Wild", "first_release_date": 1488499200, "game_type": 0, "platforms": [41, 130],
          "franchises": [ZELDA], "release_dates": [ { "platform": 41, "date": 1488499200 }, { "platform": 130, "date": 1488499200 } ] }
        """;

    /// <summary>De dos franquicias: al cargar Zelda queda en Zelda.</summary>
    const string HyruleWarriors = """
        { "id": 2909, "name": "Hyrule Warriors", "first_release_date": 1407974400, "game_type": 0, "platforms": [41],
          "franchises": [ { "id": 1, "name": "Dynasty Warriors" }, ZELDA ] }
        """;

    const string PcGame = """
        { "id": 5000, "name": "Zelda PC", "first_release_date": 1500000000, "game_type": 0, "platforms": [6], "franchises": [ZELDA],
          "external_games": [ { "id": 1, "uid": "70", "external_game_source": 1 } ] }
        """;

    const string Dlc = """{ "id": 9999, "name": "BotW: The Champions' Ballad", "game_type": 1, "platforms": [130], "franchises": [ZELDA] }""";
    const string Edition = """{ "id": 8888, "name": "BotW Edición especial", "game_type": 0, "version_parent": 7346, "platforms": [130] }""";

    static string Games(params string[] games) => "[" + string.Join(",", games).Replace("ZELDA", Zelda) + "]";

    /// <summary>Responde como Twitch e IGDB según la URL y la consulta, y cuenta las llamadas.</summary>
    sealed class FakeIgdb : HttpMessageHandler
    {
        public int Calls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            var url = request.RequestUri!.ToString();
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var json = url switch
            {
                Igdb.TokenUrl => """{ "access_token": "token", "expires_in": 3600 }""",
                _ when url.EndsWith("/franchises") => body.Contains("zelda", StringComparison.OrdinalIgnoreCase) ? $"[{Zelda}]" : "[]",
                _ when url.EndsWith("/collections") => "[]",
                _ when url.EndsWith("/games/count") => """{ "count": 6 }""",
                _ when body.Contains("franchises = (596)") => Games(OcarinaOfTime, BreathOfTheWild, HyruleWarriors, PcGame, Dlc, Edition),
                _ when body.Contains("platforms = (37,137)") => Games(OcarinaOfTime3D),
                _ when body.Contains("where id = (1030)") => Games(OcarinaOfTime3D),
                _ => null,
            };
            return json is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent($"sin respuesta para {url} {body}") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }

    static readonly (string, string)[] IgdbKeys = [("igdb.clientId", "id"), ("igdb.clientSecret", "secreto")];

    public IgdbImportTests() => IgdbImport.Handler = new FakeIgdb();

    /// <summary>Listo cuando de F1.5: cargar la saga Zelda la deja completa, sin duplicar al recargar.</summary>
    [Fact]
    public async Task LoadingZeldaLeavesTheSagaComplete()
    {
        await using var f = new Fixture(IgdbKeys);

        // Antes, una carga por consola trajo el remake de 3DS como juego suelto: al cargar la saga pasa a su original.
        Assert.Equal("done", (await f.Run(IgdbImport.EnqueuePlatform(f.Db, "3ds"))).Status);
        var task = await f.Run(IgdbImport.EnqueueSaga(f.Db, "zelda"));
        Assert.Equal("done", task.Status);
        Assert.Equal(100, task.Progress);

        using var c = f.Db.Open();
        Assert.Equal(["The Legend of Zelda"], c.Query<string>("SELECT name FROM saga"));
        Assert.Equal(
            ["Hyrule Warriors", "La leyenda de Zelda: Ocarina of Time", "The Legend of Zelda: Breath of the Wild", "Zelda PC"],
            c.Query<string>("SELECT name_es FROM game WHERE saga_id IS NOT NULL ORDER BY name_es"));
        Assert.Equal(4, c.ExecuteScalar<int>("SELECT count(*) FROM game"));

        var oot = c.ExecuteScalar<long>("SELECT id FROM game WHERE igdb_id = 1029");
        Assert.Equal("The Legend of Zelda: Ocarina of Time", c.ExecuteScalar<string>("SELECT original_name FROM game WHERE id = @oot", new { oot }));
        Assert.Equal(["3ds remake 2011", "n64 original 1998"],
            c.Query<string>("SELECT platform || ' ' || kind || ' ' || year FROM version WHERE game_id = @oot ORDER BY platform", new { oot }));
        Assert.Equal(["switch", "wiiu"], c.Query<string>("SELECT platform FROM version v JOIN game g ON g.id = v.game_id WHERE g.igdb_id = 7346 ORDER BY platform"));
        Assert.Equal(70, c.ExecuteScalar<long>("SELECT steam_app_id FROM version WHERE platform = 'pc'"));

        // Lo corregido a mano gana, y recargar no duplica nada.
        var botw = c.ExecuteScalar<long>("SELECT id FROM game WHERE igdb_id = 7346");
        FieldValues.Set(c, "game", botw, "name_es", "Zelda: Aliento de lo salvaje", FieldValues.Manual);
        var versions = c.ExecuteScalar<int>("SELECT count(*) FROM version");

        Assert.Equal("done", (await f.Run(IgdbImport.EnqueueSaga(f.Db, "zelda"))).Status);
        Assert.Equal("Zelda: Aliento de lo salvaje", c.ExecuteScalar<string>("SELECT name_es FROM game WHERE id = @botw", new { botw }));
        Assert.Equal(4, c.ExecuteScalar<int>("SELECT count(*) FROM game"));
        Assert.Equal(versions, c.ExecuteScalar<int>("SELECT count(*) FROM version"));
        Assert.Equal(1, c.ExecuteScalar<int>("SELECT count(*) FROM saga"));
    }

    [Theory]
    [InlineData("pc", "tasks.igdb.pcBySaga")]
    [InlineData("dreamcast", "tasks.igdb.unknownPlatform")]
    public async Task PlatformLoadRefusesPcAndUnknownPlatforms(string platform, string code)
    {
        await using var f = new Fixture(IgdbKeys);
        var task = await f.Run(IgdbImport.EnqueuePlatform(f.Db, platform));
        Assert.Equal(("error", code), (task.Status, task.Detail!.Code));
    }

    [Fact]
    public async Task WithoutKeysOrSagaTheTaskSaysWhy()
    {
        await using (var f = new Fixture())
            Assert.Equal("tasks.igdb.noKey", (await f.Run(IgdbImport.EnqueueSaga(f.Db, "zelda"))).Detail!.Code);
        await using (var f = new Fixture(IgdbKeys))
        {
            var task = await f.Run(IgdbImport.EnqueueSaga(f.Db, "no existe"));
            Assert.Equal("tasks.igdb.sagaNotFound", task.Detail!.Code);
            Assert.Equal("no existe", task.Detail.Params!["query"]);
        }
    }
}
