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

    /// <summary>De dos franquicias, sin principal, pero en la colección Zelda: queda en Zelda.</summary>
    const string HyruleWarriors = """
        { "id": 2909, "name": "Hyrule Warriors", "first_release_date": 1407974400, "game_type": 0, "platforms": [41],
          "franchises": [ { "id": 1, "name": "Musou" }, ZELDA ], "collections": [ { "id": 7, "name": "The Legend of Zelda" } ] }
        """;

    /// <summary>Edición ampliada de Hyrule Warriors, sin la colección Zelda: entra porque entra su original.</summary>
    const string HyruleWarriorsLegends = """
        { "id": 11193, "name": "Hyrule Warriors: Legends", "first_release_date": 1453334400, "game_type": 10, "parent_game": 2909, "platforms": [37],
          "franchises": [ { "id": 1, "name": "Musou" }, ZELDA ], "collections": [ { "id": 8, "name": "Hyrule Warriors" } ] }
        """;

    /// <summary>Invitado sin franquicia principal ni colección Zelda (#58).</summary>
    const string AmiiboTap = """
        { "id": 60431, "name": "Amiibo Tap: Nintendo's Greatest Bits", "first_release_date": 1428969600, "game_type": 0, "platforms": [41],
          "franchises": [ { "id": 3, "name": "Mario" }, ZELDA ] }
        """;

    /// <summary>Invitado (#58): está en la franquicia Zelda, pero la principal es Smash.</summary>
    const string Smash = """
        { "id": 1626, "name": "Super Smash Bros.", "first_release_date": 916963200, "game_type": 0, "platforms": [4],
          "franchise": { "id": 2, "name": "Super Smash Bros." }, "franchises": [ { "id": 2, "name": "Super Smash Bros." }, ZELDA ] }
        """;

    /// <summary>Sin ninguna fecha: cancelado o prototipo (#58).</summary>
    const string Cancelled = """{ "id": 206539, "name": "Project X", "game_type": 0, "platforms": [19], "franchises": [ZELDA] }""";

    /// <summary>Edición ampliada (game_type 10) de BotW: una versión más, no un juego (#58).</summary>
    const string Switch2Edition = """
        { "id": 338072, "name": "The Legend of Zelda: Breath of the Wild – Nintendo Switch 2 Edition", "first_release_date": 1749168000,
          "game_type": 10, "parent_game": 7346, "platforms": [130], "franchises": [ZELDA] }
        """;

    /// <summary>Mismo nombre en la misma saga (SNES y GBA): el de GBA es una versión del de SNES (#58).</summary>
    const string LinkToThePast = """
        { "id": 1026, "name": "The Legend of Zelda: A Link to the Past", "first_release_date": 690076800, "game_type": 0, "platforms": [19], "franchises": [ZELDA] }
        """;
    const string LinkToThePastGba = """
        { "id": 229416, "name": "The Legend of Zelda:  A Link to the Past", "first_release_date": 1038873600, "game_type": 0, "platforms": [24], "franchises": [ZELDA] }
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
                _ when body.Contains("franchises = (596)") => Games(OcarinaOfTime, BreathOfTheWild, HyruleWarriors, PcGame, Dlc, Edition,
                    Smash, Cancelled, Switch2Edition, LinkToThePastGba, LinkToThePast, HyruleWarriorsLegends, AmiiboTap),
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
        // Sin Smash (invitado) ni Project X (sin fecha); la Switch 2 Edition y el de GBA son versiones (#58).
        Assert.Equal(
            ["Hyrule Warriors", "La leyenda de Zelda: Ocarina of Time", "The Legend of Zelda: A Link to the Past", "The Legend of Zelda: Breath of the Wild", "Zelda PC"],
            c.Query<string>("SELECT name_es FROM game WHERE saga_id IS NOT NULL ORDER BY name_es"));
        Assert.Equal(5, c.ExecuteScalar<int>("SELECT count(*) FROM game"));
        Assert.Equal(["gba 2002", "snes 1991"],
            c.Query<string>("SELECT platform || ' ' || v.year FROM version v JOIN game g ON g.id = v.game_id WHERE g.igdb_id = 1026 ORDER BY platform"));

        var oot = c.ExecuteScalar<long>("SELECT id FROM game WHERE igdb_id = 1029");
        Assert.Equal("The Legend of Zelda: Ocarina of Time", c.ExecuteScalar<string>("SELECT original_name FROM game WHERE id = @oot", new { oot }));
        Assert.Equal(["3ds remake 2011", "n64 original 1998"],
            c.Query<string>("SELECT platform || ' ' || kind || ' ' || year FROM version WHERE game_id = @oot ORDER BY platform", new { oot }));
        Assert.Equal(["3ds", "wiiu"], c.Query<string>("SELECT platform FROM version v JOIN game g ON g.id = v.game_id WHERE g.igdb_id = 2909 ORDER BY platform"));
        Assert.Equal(["switch", "switch", "wiiu"], c.Query<string>("SELECT platform FROM version v JOIN game g ON g.id = v.game_id WHERE g.igdb_id = 7346 ORDER BY platform"));
        Assert.Equal(70, c.ExecuteScalar<long>("SELECT steam_app_id FROM version WHERE platform = 'pc'"));

        // Lo corregido a mano gana, y recargar no duplica nada.
        var botw = c.ExecuteScalar<long>("SELECT id FROM game WHERE igdb_id = 7346");
        FieldValues.Set(c, "game", botw, "name_es", "Zelda: Aliento de lo salvaje", FieldValues.Manual);
        var versions = c.ExecuteScalar<int>("SELECT count(*) FROM version");

        Assert.Equal("done", (await f.Run(IgdbImport.EnqueueSaga(f.Db, "zelda"))).Status);
        Assert.Equal("Zelda: Aliento de lo salvaje", c.ExecuteScalar<string>("SELECT name_es FROM game WHERE id = @botw", new { botw }));
        Assert.Equal(5, c.ExecuteScalar<int>("SELECT count(*) FROM game"));
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
