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
