using System.Net;
using System.Text;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#206 con páginas de ejemplo: el formato de la página real no se pudo comprobar sin un enlace público de MediaFire.</summary>
[Collection("Dats.Handler")]
public class MediaFireTests
{
    static readonly Uri Link = new("https://www.mediafire.com/file/abc/juego.zip/file");

    sealed class Fake(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    static async Task<ResolvedLink> Resolve(Fixture f, HttpStatusCode status, string body)
    {
        MediaFireSite.Handler = new Fake(status, body);
        try { return await new MediaFireSite().Resolve(Link, f.Credentials, default); }
        finally { MediaFireSite.Handler = null; }
    }

    [Fact]
    public async Task FindsTheDirectLinkInItsPage()
    {
        await using var f = new Fixture();
        var plain = await Resolve(f, HttpStatusCode.OK, """<a id="downloadButton" href="https://download123.mediafire.com/x/y/juego.zip?a=1&amp;b=2">Download</a>""");
        Assert.Equal("https://download123.mediafire.com/x/y/juego.zip?a=1&b=2", plain.Url);

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("https://download5.mediafire.com/z/juego.zip"));
        Assert.Equal("https://download5.mediafire.com/z/juego.zip", (await Resolve(f, HttpStatusCode.OK, $"""<a id="downloadButton" data-scrambled-url="{encoded}" href="#">""")).Url);
    }

    /// <summary>Si la página cambió o pide un captcha, no se salta nada: te manda al navegador. Un enlace borrado lo dice.</summary>
    [Fact]
    public async Task IfItCannotFindItItDoesNotGetAround()
    {
        await using var f = new Fixture();
        Assert.Equal("download.openInBrowser", (await Resolve(f, HttpStatusCode.OK, "<html>captcha</html>")).Blocked?.Code);
        Assert.Equal("download.unavailable", (await Resolve(f, HttpStatusCode.NotFound, "")).Blocked?.Code);
    }

    [Fact]
    public void RecognizesItsLinks()
    {
        Assert.Equal("mediafire", Downloads.SiteOf(Link)!.Id);
        Assert.True(Downloads.SiteOf(Link)!.MaxConnections <= 2);
    }
}
