using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Otteretro.Api;
using Otteretro.App;

namespace Otteretro.Libraries;

/// <summary>
/// 1fichier por su API oficial para cuentas premium (#225): con tu clave pide el enlace directo. Va despacio y con una sola conexión: la API
/// bloquea por flood, y una cuenta se puede usar desde una sola IP a la vez.
/// </summary>
/// <remarks>El formato sale de la documentación (1fichier.com/api.html) y de la API real, que responde 401 sin clave. Falta probarlo con una clave de verdad.</remarks>
public sealed partial class OneFichierSite : IDownloadSite
{
    const string Api = "https://api.1fichier.com/v1/download/get_token.cgi";

    /// <summary>Para las pruebas: responde en lugar del servicio.</summary>
    public static HttpMessageHandler? Handler { get; set; }
    static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>La API limita mucho: una llamada a la vez y separadas (el mismo ritmo que usa rclone).</summary>
    static readonly SemaphoreSlim Gate = new(1, 1);
    static readonly TimeSpan Spacing = TimeSpan.FromMilliseconds(400);

    public string Id => "1fichier";
    public int MaxConnections => 1;

    /// <summary>Como JDownloader: 1 descarga a la vez sin premium y 10 con premium (con clave de API).</summary>
    public int MaxParallel(Credentials credentials) => string.IsNullOrEmpty(credentials.Read("onefichier.apiKey")) ? 1 : 10;

    /// <summary>
    /// Comprueba con `check_links.pl`, como JDownloader: nombre y tamaño sin pedir un enlace de descarga ni usar tu clave, así un enlace muerto
    /// no suma pedidos fallidos a tu cuenta (#202, decisión 249). Sin clave no se consulta: sin premium no se puede bajar.
    /// </summary>
    public async Task<LinkCheck?> Inspect(Uri url, Credentials credentials, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(credentials.Read("onefichier.apiKey"))) return new(url.ToString(), "", null, Id, "blocked", ApiError.Of("download.needsAccount", ("site", Id)));
        if (LinkId().Match(url.Query) is not { Success: true } id) return null;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://1fichier.com/check_links.pl")
        {
            Content = new FormUrlEncodedContent([new("links[]", Canonical(url))]),
        };
        var http = Handler is null ? Shared : new HttpClient(Handler);
        string text;
        await Gate.WaitAsync(ct);
        try
        {
            using var response = await http.SendAsync(request, ct);
            text = await response.Content.ReadAsStringAsync(ct);
            await Task.Delay(Spacing, ct);
        }
        finally { Gate.Release(); }
        // Una línea por enlace: `enlace;nombre;tamaño`, o `enlace;;;NOT FOUND`.
        var line = text.Split('\n').FirstOrDefault(l => Regex.IsMatch(l, $@"\?{id.Groups[1].Value}[;&]"));
        if (line?.Split(';') is not [_, var name, var size, ..] parts) return null;
        if (parts.Length > 3 && parts[3].Contains("NOT FOUND") || parts.Length > 3 && parts[3].Contains("BAD LINK")) return new(url.ToString(), "", null, Id, "unavailable");
        return new(url.ToString(), WebUtility.HtmlDecode(name), long.TryParse(size, out var bytes) ? bytes : null, Id, "ok");
    }
    public bool Handles(Uri url) => url.Host.Equals("1fichier.com", StringComparison.OrdinalIgnoreCase) || url.Host.EndsWith(".1fichier.com", StringComparison.OrdinalIgnoreCase);

    public async Task<ResolvedLink> Resolve(Uri url, Credentials credentials, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(credentials.Read("onefichier.apiKey"))) return new(url.ToString(), Blocked: ApiError.Of("download.needsAccount", ("site", Id)));

        using var request = new HttpRequestMessage(HttpMethod.Post, Api)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { url = Canonical(url), single = 0 }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Read("onefichier.apiKey"));
        var http = Handler is null ? Shared : new HttpClient(Handler);
        await Gate.WaitAsync(ct);
        string text;
        HttpStatusCode status;
        try
        {
            using var response = await http.SendAsync(request, ct);
            status = response.StatusCode;
            text = await response.Content.ReadAsStringAsync(ct);
            await Task.Delay(Spacing, ct);
        }
        finally { Gate.Release(); }

        // Flood: se espera lo que rclone espera y la cola reintenta; nunca se insiste.
        if (Flood().IsMatch(text))
        {
            var flood = new HttpRequestException("1fichier: flood", null, HttpStatusCode.TooManyRequests);
            flood.Data[TaskQueue.RetryAfterKey] = TimeSpan.FromSeconds(30);
            throw flood;
        }
        if (status is HttpStatusCode.Unauthorized) return new(url.ToString(), Blocked: ApiError.Of("download.badAccount", ("site", Id)));
        if ((int)status >= 500) throw new HttpRequestException($"1fichier respondió {(int)status}", null, status);
        string? direct = null, message = null;
        try
        {
            using var json = JsonDocument.Parse(text);
            direct = json.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
            message = json.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
        }
        catch (JsonException) { }
        // Lo demás es del enlace: borrado, con contraseña o sin permiso. Se muestra lo que dijo el sitio.
        return direct is not null ? new(direct)
            : new(url.ToString(), Blocked: ApiError.Of("download.siteSaid", ("site", Id), ("message", message ?? ((int)status).ToString())));
    }

    /// <summary>La API solo acepta `https://dominio/?id` y rechaza lo demás: los enlaces compartidos suelen traer `&amp;af=` de afiliado.</summary>
    internal static string Canonical(Uri url) => LinkId().Match(url.Query) is { Success: true } m ? $"https://{url.Host}/?{m.Groups[1].Value}" : url.ToString();

    [GeneratedRegex(@"^\?([a-z0-9]{5,20})(?:&|$)")]
    private static partial Regex LinkId();

    [GeneratedRegex(@"Flood|#374\b|#412\b", RegexOptions.IgnoreCase)]
    private static partial Regex Flood();
}
