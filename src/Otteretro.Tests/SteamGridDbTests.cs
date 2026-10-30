using System.Net;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Otteretro.App;
using Otteretro.Catalog;

namespace Otteretro.Tests;

/// <summary>F1.7 con un SteamGridDB falso.</summary>
public class SteamGridDbTests
{
    sealed class FakeSteamGridDb : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url.StartsWith("https://cdn2.steamgriddb.com/"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
            var path = url[SteamGridDb.ApiUrl.Length..];
            string? data = path switch
            {
                "games/steam/70" => """{ "id": 11 }""",
                "search/autocomplete/Zelda%20Classic" => """[ { "id": 22 } ]""",
                "search/autocomplete/Nada" => "[]",
                _ when path.StartsWith("grids/game/11") => """[ { "url": "https://cdn2.steamgriddb.com/grid/a.png" } ]""",
                _ when path.StartsWith("grids/game/22") => """[ { "url": "https://cdn2.steamgriddb.com/grid/b.jpg" } ]""",
                "logos/game/11" => """[ { "url": "https://cdn2.steamgriddb.com/logo/c.webp" } ]""",
                "heroes/game/22" or "logos/game/22" => "[]",
                _ => null, // heroes/game/11: SteamGridDB responde 404
            };
            return Task.FromResult(data is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{ "success": false }""") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($$"""{ "success": true, "data": {{data}} }""") });
        }
    }

    public SteamGridDbTests() => SteamGridDb.Handler = new FakeSteamGridDb();

    /// <summary>Listo cuando de F1.7: las portadas 2:3 quedan en la caché y la API las sirve.</summary>
    [Fact]
    public async Task CoversGoToTheCacheAndTheApiServesThem()
    {
        await using var f = new Fixture(("steamgriddb.apiKey", "clave"));
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO game (id, name_es, original_name) VALUES (1, 'Zelda PC', 'Zelda PC'), (2, 'Zelda Classic', 'Zelda Classic'), (3, 'Nada', 'Nada');
                INSERT INTO version (game_id, platform, steam_app_id) VALUES (1, 'pc', 70);
                """);

        var task = await f.Run(SteamGridDb.Enqueue(f.Db));
        Assert.Equal(("done", 100), (task.Status, task.Progress));
        var files = Directory.GetFiles(SteamGridDb.Folder(f.Db)).Select(Path.GetFileName).Order();
        Assert.Equal(["1-cover.png", "1-logo.webp", "2-cover.jpg"], files);

        var app = LocalServer.Create(f.Session, b => b.WebHost.UseTestServer());
        await app.StartAsync();
        var http = app.GetTestClient();
        http.BaseAddress = new Uri("http://127.0.0.1/");
        var cover = await http.GetAsync($"/api/images/1/cover?key={f.Session.Key}");
        Assert.Equal(HttpStatusCode.OK, cover.StatusCode);
        Assert.Equal("image/png", cover.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/images/3/cover?key={f.Session.Key}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/images/1/..%2F..%2Fdata?key={f.Session.Key}")).StatusCode);
        await app.StopAsync();
    }

    [Fact]
    public async Task WithoutKeyTheTaskSaysWhy()
    {
        await using var f = new Fixture();
        Assert.Equal("tasks.sgdb.noKey", (await f.Run(SteamGridDb.Enqueue(f.Db))).Detail!.Code);
    }
}
