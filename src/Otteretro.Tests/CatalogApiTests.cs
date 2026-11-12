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
                INSERT INTO saga (id, name, iconic) VALUES (1, 'The Legend of Zelda', 1), (2, 'Half-Life', 0);
                INSERT INTO saga_user (saga_id, favorite) VALUES (1, 1);
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
            c.Execute("INSERT INTO field_value (entity, entity_id, field, value, source) VALUES ('game', 11, 'name_es', 'Breath of the Wild', 'igdb'); UPDATE game SET name_es = 'Breath of the Wild' WHERE id = 11");
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

    /// <summary>
    /// #225: Sagas se arma una vez y queda guardado (también en `cache/sagas.json`); no vuelve a leer el catálogo mientras nada tuyo cambie.
    /// Lo que haces tú se ve al instante; lo que cambia el escaneo se rehace aparte y llega en el pedido siguiente.
    /// </summary>
    [Fact]
    public async Task SagasAreBuiltOnceAndRenewedWhenSomethingOfYoursChanges()
    {
        Assert.Equal(3, (await Get<SagaPage>("/api/sagas"))!.Items.Count);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(f.Db.FilePath)!, "cache", "sagas.json")));
        using (var c = f.Db.Open()) c.Execute("UPDATE catalog.saga SET name = 'Otro nombre' WHERE id = 2"); // el catálogo solo cambia al aceptar una versión
        Assert.Equal("Half-Life", (await Get<SagaPage>("/api/sagas"))!.Items[0].Name); // sale de lo guardado

        Assert.Equal(HttpStatusCode.OK, (await http.PutAsJsonAsync("/api/sagas/1", new SagaFlagsRequest(null, true))).StatusCode);
        var sagas = (await Get<SagaPage>("/api/sagas"))!.Items;
        Assert.True(sagas.Single(s => s.Id == 1).Following); // lo tuyo, al instante
        Assert.Contains(sagas, s => s.Name == "Otro nombre");

        // Un juego que encuentra el escaneo: se rehace aparte.
        using (var c = f.Db.Open()) c.Execute("INSERT INTO source (kind, library_id, relative_path, console, version_id) VALUES ('file', NULL, 'oot.z64', 'n64', 100)");
        for (var i = 0; i < 50 && (await Get<SagaPage>("/api/sagas"))!.Items.Single(s => s.Id == 1).Owned == 0; i++) await Task.Delay(100);
        Assert.Equal(1, (await Get<SagaPage>("/api/sagas"))!.Items.Single(s => s.Id == 1).Owned);
    }

    /// <summary>#224: desactivar una consola la saca de Sagas, juegos de una saga, búsqueda y Colección; activarla la devuelve. No borra nada.</summary>
    [Fact]
    public async Task AHiddenConsoleIsLeftOutOfWhatIsShown()
    {
        async Task Show(string id, bool visible) =>
            Assert.Equal(HttpStatusCode.OK, (await http.PutAsJsonAsync($"/api/consoles/{id}/visible", new ConsoleVisibleRequest(visible))).StatusCode);

        await Show("gb", false);
        await Show("switch", false);
        var settings = (await Get<ConsolesPage>("/api/consoles/settings"))!.Consoles;
        Assert.False(settings.Single(c => c.Id == "gb").Visible);
        Assert.True(settings.Single(c => c.Id == "n64").Visible);

        var sagas = (await Get<SagaPage>("/api/sagas"))!.Items;
        Assert.Equal([2L, 1L], sagas.Select(s => s.Id)); // «Emulación» solo tenía Tetris de Game Boy
        var zelda = sagas.Single(s => s.Id == 1);
        Assert.Equal(["n64", "3ds"], zelda.Platforms);
        Assert.Equal(1, zelda.Games); // Breath of the Wild solo está en Switch
        Assert.Equal(["Ocarina of Time"], (await Get<GamePage>("/api/sagas/1/games"))!.Items.Select(g => g.Name));
        Assert.Empty((await Get<SearchResults>("/api/search?q=tetris"))!.Games);
        Assert.DoesNotContain((await Get<List<ConsoleCount>>("/api/consoles"))!, c => c.Id == "gb");

        await Show("gb", true);
        await Show("switch", true);
        Assert.Equal(3, (await Get<SagaPage>("/api/sagas"))!.Items.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PutAsJsonAsync("/api/consoles/nada/visible", new ConsoleVisibleRequest(false))).StatusCode);
    }

    [Fact]
    public async Task SagasHaveTheirPlatformsFiltersAndEmulationLast()
    {
        var all = (await Get<SagaPage>("/api/sagas"))!.Items;
        Assert.Equal([2L, 1L, 0L], all.Select(s => s.Id)); // por nombre; «Emulación» al final
        var zelda = all.Single(s => s.Id == 1);
        Assert.Equal(["n64", "3ds", "switch"], zelda.Platforms);
        Assert.Equal((2, 0), (zelda.Games, zelda.Owned));

        Assert.Equal([2L], (await Get<SagaPage>("/api/sagas?filter=pc"))!.Items.Select(s => s.Id));
        Assert.Equal([1L], (await Get<SagaPage>("/api/sagas?filter=favorite"))!.Items.Select(s => s.Id));
        Assert.Equal([2L], (await Get<SagaPage>("/api/sagas?filter=owned"))!.Items.Select(s => s.Id));
        Assert.Equal([1L, 0L], (await Get<SagaPage>("/api/sagas?company=Nintendo"))!.Items.Select(s => s.Id));
        Assert.Equal(2L, (await Get<SagaPage>("/api/sagas?sort=played"))!.Items[0].Id);
        Assert.Equal(1L, (await Get<SagaPage>("/api/sagas?sort=recognized"))!.Items[0].Id);

        var follow = await http.PutAsJsonAsync("/api/sagas/2", new SagaFlagsRequest(null, true));
        Assert.True((await follow.Content.ReadFromJsonAsync<SagaDetail>())!.Saga.Following);
        Assert.Equal(["Tetris"], (await Get<SagaDetail>("/api/sagas/0"))!.Games.Select(g => g.Name));
    }

    /// <summary>#177: Sagas, los juegos de una saga y Colección se piden por tramos, con el total para seguir bajando.</summary>
    [Fact]
    public async Task ListsComeInPagesWithTheirTotal()
    {
        var first = (await Get<SagaPage>("/api/sagas?limit=2"))!;
        Assert.Equal((2, 3), (first.Items.Count, first.Total));
        var rest = (await Get<SagaPage>("/api/sagas?offset=2&limit=2"))!;
        Assert.Equal([0L], rest.Items.Select(s => s.Id)); // «Emulación», al final
        Assert.Equal(10L, first.Items.Single(s => s.Id == 1).CoverGameId ?? 10L); // las del tramo llevan su portada (aquí, sin archivos)

        var zelda = (await Get<GamePage>("/api/sagas/1/games?offset=1&limit=1"))!;
        Assert.Equal((2, "Breath of the Wild"), (zelda.Total, Assert.Single(zelda.Items).Name));

        var page = (await Get<CollectionPage>("/api/collection/pc?limit=1"))!;
        Assert.Equal((1, 1), (page.Total, page.Games.Count));
        Assert.Empty((await Get<CollectionPage>("/api/collection/pc?offset=1"))!.Unlinked); // lo de Steam sin IGDB, solo en el primer tramo
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/collection/nada")).StatusCode);
    }

    /// <summary>F2.16 y F2.17: exclusivas de una empresa (PC no cuenta) y «más reconocidas» (icónico, después popularidad en IGDB).</summary>
    [Fact]
    public async Task ExclusivesAndMostRecognized()
    {
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO saga (id, name) VALUES (3, 'Persona'), (4, 'Halo');
                INSERT INTO game (id, saga_id, name_es, popularity) VALUES (40, 3, 'Persona 5', 50), (50, 4, 'Halo', 2000);
                INSERT INTO version (game_id, platform) VALUES (40, 'ps2'), (40, 'switch'), (50, 'xbox'), (50, 'pc');
                UPDATE game SET popularity = 900 WHERE id = 20;
                """);

        Assert.Equal([4L, 1L], (await Get<SagaPage>("/api/sagas?filter=exclusive"))!.Items.Select(s => s.Id)); // Persona no; Half-Life es solo PC
        Assert.Equal([1L], (await Get<SagaPage>("/api/sagas?filter=exclusive&company=Nintendo"))!.Items.Select(s => s.Id));
        Assert.Equal("Microsoft", (await Get<SagaDetail>("/api/sagas/4"))!.Saga.Exclusive);
        Assert.Equal([1L, 4L, 2L, 3L, 0L], (await Get<SagaPage>("/api/sagas?sort=recognized"))!.Items.Select(s => s.Id));
        Assert.Equal([1L, 3L], (await Get<SagaPage>("/api/sagas?sort=recognized&platform=switch"))!.Items.Select(s => s.Id));
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
    }

    [Fact]
    public async Task SearchFindsSagasAndGamesAndNoticesListMissingKeys()
    {
        var results = (await Get<SearchResults>("/api/search?q=zelda"))!;
        Assert.Equal(["The Legend of Zelda"], results.Sagas.Select(s => s.Name));
        Assert.Equal(["Half-Life"], (await Get<SearchResults>("/api/search?q=half"))!.Games.Select(g => g.Name));
        Assert.Empty((await Get<SearchResults>("/api/search?q=z"))!.Games);

        var notices = (await Get<List<ApiError>>("/api/notices"))!;
        Assert.Equal(["steam"], notices.Select(n => n.Params!["service"])); // #221: las imágenes ya no piden clave
    }

    /// <summary>#428: Inicio › Continuar jugando junta las sesiones de Otteretro y lo jugado en Steam, el último primero.</summary>
    [Fact]
    public async Task RecentGamesJoinSessionsAndStorePlaytime()
    {
        Assert.Equal([20L], (await Get<List<Otteretro.Play.RecentGame>>("/api/home/recent"))!.Select(g => g.GameId)); // solo lo de Steam
        var now = DateTime.UtcNow.ToString("O");
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO play_session (version_id, machine_id, launcher, started_at, ended_at, duration) VALUES
                    (100, @machine, 'otteretro', '2026-10-05T10:00:00.0000000Z', '2026-10-05T11:00:00.0000000Z', 3600),
                    (101, @machine, 'otteretro', @now, NULL, NULL),
                    (200, @machine, 'steam', '2026-09-01T10:00:00.0000000Z', '2026-09-01T12:00:00.0000000Z', 7200);
                """, new { machine = f.Config.MachineId, now });
        var recent = (await Get<List<Otteretro.Play.RecentGame>>("/api/home/recent"))!;
        Assert.Equal(
            [(10L, "Ocarina of Time", "3ds", "otteretro", 60L, true), (20L, "Half-Life", "pc", "steam", 600L, false)],
            recent.Select(g => (g.GameId, g.Name, g.Platform, g.Launcher, g.Minutes, g.Playing)));
        Assert.Equal(now, recent[0].LastPlayed);
    }
}
