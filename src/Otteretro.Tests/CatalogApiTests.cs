using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Catalog;

namespace Otteretro.Tests;

/// <summary>F1.9: lo que leen y guardan las pantallas del catálogo, con un catálogo pequeño de ejemplo.</summary>
public class CatalogApiTests : IAsyncLifetime
{
    readonly Fixture f = new();
    WebApplication app = null!;
    HttpClient http = null!;

    public async Task InitializeAsync()
    {
        using (var c = f.Db.Open())
        {
            c.Execute("""
                INSERT INTO saga (id, name, favorite, iconic) VALUES (1, 'The Legend of Zelda', 1, 1), (2, 'Half-Life', 0, 0);
                INSERT INTO game (id, saga_id, name_es, original_name, year) VALUES
                    (10, 1, 'Ocarina of Time', 'Ocarina of Time', 1998), (11, 1, 'Breath of the Wild', 'Breath of the Wild', 2017),
                    (20, 2, 'Half-Life', 'Half-Life', 1998), (30, NULL, 'Tetris', 'Tetris', 1989);
                INSERT INTO version (id, game_id, platform, kind, year, steam_app_id) VALUES
                    (100, 10, 'n64', 'original', 1998, NULL), (101, 10, '3ds', 'remake', 2011, NULL), (110, 11, 'switch', 'original', 2017, NULL),
                    (200, 20, 'pc', 'original', 1998, 70), (300, 30, 'gb', 'original', 1989, NULL);
                INSERT INTO source (kind, store, store_id, name, playtime, last_played, version_id) VALUES
                    ('store', 'steam', '70', 'Half-Life', 600, '2026-10-01T00:00:00Z', 200),
                    ('store', 'steam', '999', 'Algo que IGDB no tiene', 5, NULL, NULL);
                """);
            FieldValues.Set(c, "game", 11, "name_es", "Breath of the Wild", "igdb");
        }
        app = LocalServer.Create(f.Session, b => b.WebHost.UseTestServer());
        await app.StartAsync();
        http = app.GetTestClient();
        http.BaseAddress = new Uri("http://127.0.0.1/");
        http.DefaultRequestHeaders.Add(LocalServer.KeyHeader, f.Session.Key);
    }

    public async Task DisposeAsync()
    {
        await app.StopAsync();
        await f.DisposeAsync();
    }

    Task<T?> Get<T>(string url) => http.GetFromJsonAsync<T>(url);

    [Fact]
    public async Task SagasHaveTheirPlatformsFiltersAndEmulationLast()
    {
        var all = (await Get<List<SagaCard>>("/api/sagas"))!;
        Assert.Equal([2L, 1L, 0L], all.Select(s => s.Id)); // por nombre; «Emulación» al final
        var zelda = all.Single(s => s.Id == 1);
        Assert.Equal(["n64", "3ds", "switch"], zelda.Platforms);
        Assert.Equal((2, 0), (zelda.Games, zelda.Owned));

        Assert.Equal([2L], (await Get<List<SagaCard>>("/api/sagas?filter=pc"))!.Select(s => s.Id));
        Assert.Equal([1L], (await Get<List<SagaCard>>("/api/sagas?filter=favorite"))!.Select(s => s.Id));
        Assert.Equal([2L], (await Get<List<SagaCard>>("/api/sagas?filter=owned"))!.Select(s => s.Id));
        Assert.Equal([1L, 0L], (await Get<List<SagaCard>>("/api/sagas?company=Nintendo"))!.Select(s => s.Id));
        Assert.Equal(2L, (await Get<List<SagaCard>>("/api/sagas?sort=played"))![0].Id);
        Assert.Equal(1L, (await Get<List<SagaCard>>("/api/sagas?sort=iconic"))![0].Id);

        var follow = await http.PutAsJsonAsync("/api/sagas/2", new SagaFlagsRequest(null, true));
        Assert.True((await follow.Content.ReadFromJsonAsync<SagaDetail>())!.Saga.Following);
        Assert.Equal(["Tetris"], (await Get<SagaDetail>("/api/sagas/0"))!.Games.Select(g => g.Name));
    }

    [Fact]
    public async Task CollectionByConsoleWithPcStoresAndUnlinkedGames()
    {
        var consoles = (await Get<List<ConsoleCount>>("/api/consoles"))!;
        Assert.Equal(26, consoles.Count);
        Assert.Equal((1, 1), consoles.Where(c => c.Id == "pc").Select(c => (c.Games, c.Owned)).Single());

        var pc = (await Get<CollectionPage>("/api/collection/pc"))!;
        var halfLife = Assert.Single(pc.Games);
        Assert.Equal((true, 600L), (halfLife.Owned, halfLife.Playtime));
        Assert.Equal(["steam"], halfLife.Stores);
        Assert.Equal("Algo que IGDB no tiene", Assert.Single(pc.Unlinked).Name);

        Assert.Equal(["Ocarina of Time"], (await Get<CollectionPage>("/api/collection/3ds?filter=missing"))!.Games.Select(g => g.Name));
        Assert.Empty((await Get<CollectionPage>("/api/collection/3ds?filter=owned"))!.Games);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/collection/dreamcast")).StatusCode);
    }

    [Fact]
    public async Task GameSheetSavesProgressAndManualFixes()
    {
        var game = (await Get<GameDetail>("/api/games/10"))!;
        Assert.Equal(["n64", "3ds"], game.Versions.Select(v => v.Platform));
        Assert.Equal("pending", game.Progress.Status);

        var saved = await http.PutAsJsonAsync("/api/games/10/progress", new ProgressInfo("finished", "2026-10-01", "2026-10-20", 9, "Muy bueno", true));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(new ProgressInfo("finished", "2026-10-01", "2026-10-20", 9, "Muy bueno", true), (await Get<GameDetail>("/api/games/10"))!.Progress);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync("/api/games/10/progress", new ProgressInfo("finished", null, null, 11, null, false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync("/api/games/10/progress", new ProgressInfo("finished", "20/10/2026", null, null, null, false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync("/api/games/10/progress", new ProgressInfo("perdido", null, null, null, null, false))).StatusCode);

        // A mano gana sobre IGDB; al quitarlo, vuelve el de IGDB.
        var fixedName = await (await http.PutAsJsonAsync("/api/games/11/fields/name_es", new FieldRequest("Zelda: Aliento de lo salvaje"))).Content.ReadFromJsonAsync<GameDetail>();
        Assert.Equal("Zelda: Aliento de lo salvaje", fixedName!.NameEs);
        Assert.Equal(("manual", true), fixedName.Fields.Where(x => x.Winner).Select(x => (x.Source, x.Manual)).Single());
        Assert.Equal("Breath of the Wild", (await (await http.DeleteAsync("/api/games/11/fields/name_es")).Content.ReadFromJsonAsync<GameDetail>())!.NameEs);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync("/api/games/11/fields/year", new FieldRequest("hace mucho"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync("/api/games/11/fields/name_es", new FieldRequest(null))).StatusCode); // #59
        Assert.Equal(HttpStatusCode.NotFound, (await http.PutAsJsonAsync("/api/games/11/fields/saga_id", new FieldRequest("2"))).StatusCode);
    }

    [Fact]
    public async Task SearchFindsSagasAndGamesAndNoticesListMissingKeys()
    {
        var results = (await Get<SearchResults>("/api/search?q=zelda"))!;
        Assert.Equal(["The Legend of Zelda"], results.Sagas.Select(s => s.Name));
        Assert.Equal(["Half-Life"], (await Get<SearchResults>("/api/search?q=half"))!.Games.Select(g => g.Name));
        Assert.Empty((await Get<SearchResults>("/api/search?q=z"))!.Games);

        var notices = (await Get<List<ApiError>>("/api/notices"))!;
        Assert.Equal(["igdb", "steamgriddb", "steam"], notices.Select(n => n.Params!["service"]));
    }
}
