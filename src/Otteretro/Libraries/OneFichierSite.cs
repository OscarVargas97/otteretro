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
/// <remarks>El formato sale de la API real (responde 401 sin clave) y del cliente de rclone. Sin clave no se probó si un enlace aguanta varias conexiones.</remarks>
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
    public bool Handles(Uri url) => url.Host.Equals("1fichier.com", StringComparison.OrdinalIgnoreCase) || url.Host.EndsWith(".1fichier.com", StringComparison.OrdinalIgnoreCase);

    public async Task<ResolvedLink> Resolve(Uri url, Credentials credentials, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(credentials.Read("onefichier.apiKey"))) return new(url.ToString(), Blocked: ApiError.Of("download.needsAccount", ("site", Id)));

        using var request = new HttpRequestMessage(HttpMethod.Post, Api)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { url = url.ToString(), single = 0 }), Encoding.UTF8, "application/json"),
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

    [GeneratedRegex(@"Flood|#374\b|#412\b", RegexOptions.IgnoreCase)]
    private static partial Regex Flood();
}
