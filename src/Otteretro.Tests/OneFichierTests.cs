using System.Net;
using System.Text.Json;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#204 con la API simulada: el formato real se comprobó contra rclone y el 401 de la API, pero falta probarlo con una clave.</summary>
[Collection("Dats.Handler")]
public class OneFichierTests
{
    static readonly (string, string)[] Key = [("onefichier.apiKey", "mi-clave")];
    static readonly Uri Link = new("https://1fichier.com/?abc123");

    sealed class Fake(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? Auth, Sent;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Auth = request.Headers.Authorization?.ToString();
            Sent = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    static async Task<(ResolvedLink Link, Fake Fake)> Resolve(Fixture f, HttpStatusCode status, string body)
    {
        var fake = new Fake(status, body);
        OneFichierSite.Handler = fake;
        try { return (await new OneFichierSite().Resolve(Link, f.Credentials, default), fake); }
        finally { OneFichierSite.Handler = null; }
    }

    [Fact]
    public void RecognizesItsLinks()
    {
        var site = new OneFichierSite();
        Assert.True(site.Handles(Link));
        Assert.False(site.Handles(new Uri("https://no1fichier.com/a")));
        // El conector gana sobre el de enlaces directos.
        Assert.Equal("1fichier", Downloads.SiteOf(Link)!.Id);
        Assert.Equal("direct", Downloads.SiteOf(new Uri("https://otro.com/a.zip"))!.Id);
        Assert.Equal("mega", Downloads.SiteOf(new Uri("https://mega.nz/file/abc#clave"))!.Id);
    }

    [Fact]
    public async Task AsksForTheDirectLinkWithYourKey()
    {
        await using var f = new Fixture(Key);
        var (link, fake) = await Resolve(f, HttpStatusCode.OK, """{ "url": "https://a-1.1fichier.com/token", "status": "OK" }""");
        Assert.Equal("https://a-1.1fichier.com/token", link.Url);
        Assert.Null(link.Blocked);
        Assert.Equal("Bearer mi-clave", fake.Auth);
        Assert.Equal(Link.ToString(), JsonDocument.Parse(fake.Sent!).RootElement.GetProperty("url").GetString());
    }

    /// <summary>La API rechaza lo que no sea `https://dominio/?id`, y los enlaces compartidos suelen traer `&amp;af=`.</summary>
    [Fact]
    public async Task SendsOnlyTheFormTheApiAccepts()
    {
        await using var f = new Fixture(Key);
        var fake = new Fake(HttpStatusCode.OK, """{ "url": "https://a-1.1fichier.com/token" }""");
        OneFichierSite.Handler = fake;
        try { await new OneFichierSite().Resolve(new Uri("https://1fichier.com/?abc123&af=327151"), f.Credentials, default); }
        finally { OneFichierSite.Handler = null; }
        Assert.Equal("https://1fichier.com/?abc123", JsonDocument.Parse(fake.Sent!).RootElement.GetProperty("url").GetString());
    }

    /// <summary>
    /// Decisión 249: comprobar usa `check_links.pl`, como JDownloader: nombre y tamaño sin pedir tokens ni mandar tu clave, así un enlace
    /// muerto no suma pedidos fallidos a tu cuenta. Sin clave ni se consulta.
    /// </summary>
    [Fact]
    public async Task CheckingUsesTheLinkCheckerWithoutYourKey()
    {
        await using var f = new Fixture(Key);
        var fake = new Fake(HttpStatusCode.OK, "https://1fichier.com/?abc123;;;NOT FOUND\nhttps://1fichier.com/?def456;Juego.part1.rar;1234\n");
        OneFichierSite.Handler = fake;
        List<PackagePreview> preview;
        try { preview = await Downloads.Preview("https://1fichier.com/?abc123\nhttps://1fichier.com/?def456&af=9", f.Credentials, default); }
        finally { OneFichierSite.Handler = null; }
        Assert.Null(fake.Auth);
        Assert.Contains("links%5B%5D=https%3A%2F%2F1fichier.com%2F%3F", fake.Sent);
        var links = preview.SelectMany(p => p.Links).ToDictionary(l => l.Url);
        Assert.Equal("unavailable", links["https://1fichier.com/?abc123"].State);
        var found = links["https://1fichier.com/?def456&af=9"];
        Assert.Equal(("ok", "Juego.part1.rar", (long?)1234), (found.State, found.Name, found.Size));

        await using var free = new Fixture();
        var blocked = Assert.Single(Assert.Single(await Downloads.Preview("https://1fichier.com/?abc123", free.Credentials, default)).Links);
        Assert.Equal("download.needsAccount", blocked.Detail?.Code);
    }

    /// <summary>Decisión 248: 1fichier deja 1 descarga a la vez sin premium y 10 con premium; lo que elijas queda dentro.</summary>
    [Fact]
    public async Task ParallelDownloadsDependOnYourAccount()
    {
        await using var free = new Fixture();
        await using var premium = new Fixture(Key);
        var config = new App.AppConfig { DownloadsPerSite = new() { ["1fichier"] = 5, ["direct"] = 50 } };
        Assert.Equal((1, 1), Of(Downloads.Limits(config, free.Credentials), "1fichier"));
        Assert.Equal((10, 5), Of(Downloads.Limits(config, premium.Credentials), "1fichier"));
        Assert.Equal((8, 8), Of(Downloads.Limits(config, premium.Credentials), "direct"));
        Assert.Equal((4, 1), Of(Downloads.Limits(new App.AppConfig(), premium.Credentials), "mega"));
    }

    static (int, int) Of(List<SiteLimit> limits, string site)
    {
        var l = limits.Single(x => x.Site == site);
        return (l.Max, l.Value);
    }

    [Fact]
    public async Task WithoutAKeyItAsksForTheAccount()
    {
        await using var f = new Fixture();
        var (link, _) = await Resolve(f, HttpStatusCode.OK, "{}");
        Assert.Equal("download.needsAccount", link.Blocked?.Code);
    }

    [Fact]
    public async Task ABadKeyOrADeadLinkSaySo()
    {
        await using var f = new Fixture(Key);
        Assert.Equal("download.badAccount", (await Resolve(f, HttpStatusCode.Unauthorized, """{ "message": "Not authenticated #246", "status": "KO" }""")).Link.Blocked?.Code);
        var dead = (await Resolve(f, HttpStatusCode.Forbidden, """{ "message": "Resource not found #644", "status": "KO" }""")).Link.Blocked;
        Assert.Equal("download.siteSaid", dead?.Code);
        Assert.Contains("#644", dead!.Params!["message"]);
    }

    /// <summary>Un flood no se insiste: va a la cola, que espera lo pedido.</summary>
    [Fact]
    public async Task AFloodWaits()
    {
        await using var f = new Fixture(Key);
        var e = await Assert.ThrowsAsync<HttpRequestException>(() => Resolve(f, HttpStatusCode.Forbidden, """{ "message": "Flood detected: IP Locked #374", "status": "KO" }"""));
        Assert.Equal(HttpStatusCode.TooManyRequests, e.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(30), e.Data[App.TaskQueue.RetryAfterKey]);
    }
}
