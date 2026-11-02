using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Otteretro.Api;
using Otteretro.App;

namespace Otteretro.Libraries;

/// <summary>
/// MediaFire (#206): sus enlaces públicos bajan sin cuenta, y el directo sale de su página de descarga. Es el conector frágil (no hay librería ni
/// API para esto): si la página cambia o pide un captcha, no se salta: se avisa, y lo abres en el navegador para resolverlo tú (#228).
/// </summary>
public sealed partial class MediaFireSite : IDownloadSite
{
    /// <summary>Para las pruebas: responde en lugar del sitio.</summary>
    public static HttpMessageHandler? Handler { get; set; }
    static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromSeconds(30) };

    public string Id => "mediafire";

    /// <summary>Sin cuenta: pocas a la vez, para no parecer un abuso.</summary>
    public int MaxParallel(Credentials credentials) => 4;
    public int MaxConnections => 2;
    public bool Handles(Uri url) => url.Host is "mediafire.com" or "www.mediafire.com";

    public async Task<ResolvedLink> Resolve(Uri url, Credentials credentials, CancellationToken ct)
    {
        var http = Handler is null ? Shared : new HttpClient(Handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0");
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            return new(url.ToString(), Blocked: ApiError.Of("download.unavailable", ("file", url.ToString()), ("status", ((int)response.StatusCode).ToString())));
        if ((int)response.StatusCode >= 500) throw new HttpRequestException($"MediaFire respondió {(int)response.StatusCode}", null, response.StatusCode);
        var page = await response.Content.ReadAsStringAsync(ct);
        var direct = Direct().Match(page) is { Success: true } plain ? plain.Groups[1].Value
            : Scrambled().Match(page) is { Success: true } scrambled ? Decode(scrambled.Groups[1].Value)
            : null;
        return direct is not null ? new(WebUtility.HtmlDecode(direct)) : new(url.ToString(), Blocked: ApiError.Of("download.openInBrowser", ("site", Id)));
    }

    /// <summary>A veces el enlace viene en base64 en el botón.</summary>
    static string? Decode(string value)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)) is var text && text.StartsWith("http", StringComparison.Ordinal) ? text : null; }
        catch (FormatException) { return null; }
    }

    [GeneratedRegex(@"href=""(https?://download\d*\.mediafire\.com/[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex Direct();

    [GeneratedRegex(@"data-scrambled-url=""([^""]+)""")]
    private static partial Regex Scrambled();
}
