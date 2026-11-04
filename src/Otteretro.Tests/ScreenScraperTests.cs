using System.Net;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#198 con el servicio simulado: identifica lo que no reconoce el nombre, y sin cuentas o con cuota agotada no estorba.</summary>
[Collection("Dats.Handler")]
public class ScreenScraperTests
{
    static readonly (string, string)[] Keys = [("screenscraper.devId", "dev"), ("screenscraper.devPassword", "clave de dev"), ("screenscraper.user", "yo"), ("screenscraper.password", "secreta")];

    sealed class Fake(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public readonly List<Uri> Calls = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new HttpRequestException("sin red");
    }

    static void Prepare(Fixture f)
    {
        Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO game (id, name_es) VALUES (10, 'Metroid Fusion'); INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'gba');");
        Directory.CreateDirectory(f.Tmp["pack"]);
        File.WriteAllText(f.Tmp[@"pack\mf-eur.gba"], "rom");
    }

    static async Task Import_(Fixture f, HttpMessageHandler handler)
    {
        ScreenScraper.Handler = handler;
        Dats.Handler = new NoNetwork();
        try { Assert.Equal("done", (await f.Run(Import.Enqueue(f.Db, f.Tmp["pack"]).Task!.Value)).Status); }
        finally { ScreenScraper.Handler = null; Dats.Handler = null; }
    }

    [Fact]
    public async Task IdentifiesWhatTheNameDoesNotSay()
    {
        await using var f = new Fixture(Keys);
        Prepare(f);
        // Con una barra suelta en un texto, como las que a veces manda.
        var fake = new Fake(HttpStatusCode.OK, """{ "response": { "jeu": { "synopsis": "C:\mal", "noms": [ { "region": "ss", "text": "Metroid Fusion" } ] } } }""");
        await Import_(f, fake);

        var url = Assert.Single(fake.Calls).ToString();
        Assert.Contains("systemeid=12", url);
        Assert.Contains("romnom=mf-eur.gba", url);
        Assert.Contains("sspassword=secreta", url);
        Assert.True(File.Exists(f.Tmp[@"Otteretro\Juegos\Game Boy Advance\Metroid Fusion\mf-eur.gba"]));
        using var c = f.Db.Open();
        Assert.Equal(20, c.ExecuteScalar<long>("SELECT version_id FROM source"));
        // Las claves no quedan en la caché.
        Assert.Equal(0, c.ExecuteScalar<long>("SELECT count(*) FROM api_cache WHERE key LIKE '%secreta%' OR key LIKE '%clave%'"));
    }

    [Fact]
    public async Task WithoutAccountsItDoesNotCall()
    {
        await using var f = new Fixture();
        Prepare(f);
        var fake = new Fake(HttpStatusCode.OK, "{}");
        await Import_(f, fake);
        Assert.Empty(fake.Calls);
        Assert.True(File.Exists(f.Tmp[@"Otteretro\Juegos\Game Boy Advance\mf-eur\mf-eur.gba"])); // igual se importa
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "Erreur : Rom/Iso/Dossier non trouvée !", 2)] // no lo conoce: sigue preguntando por los demás
    [InlineData((HttpStatusCode)430, "cuota", 1)] // cuota agotada: no insiste
    [InlineData(HttpStatusCode.OK, "Erreur de login : Vérifier vos identifiants !", 1)] // cuentas malas, con un 200
    public async Task AQuotaOrBadLoginStopsAskingButNotTheImport(HttpStatusCode status, string body, int calls)
    {
        await using var f = new Fixture(Keys);
        Prepare(f);
        File.WriteAllText(f.Tmp[@"pack\otro.gba"], "rom2");
        var fake = new Fake(status, body);
        await Import_(f, fake);
        Assert.Equal(calls, fake.Calls.Count);
        Assert.Empty(Directory.GetFiles(f.Tmp["pack"]));
    }
}
