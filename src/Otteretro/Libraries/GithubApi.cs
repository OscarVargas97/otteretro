using System.Net;
using System.Security.Cryptography;
using Otteretro.Api;
using Otteretro.App;

namespace Otteretro.Libraries;

/// <summary>GitHub respondió 403 o 429 por límite de consultas; <see cref="Reset"/> es cuándo se libera.</summary>
public sealed class GithubRateLimitException(DateTimeOffset? reset) : HttpRequestException("GitHub rate limit")
{
    public DateTimeOffset? Reset { get; } = reset;
    public ApiError Error => Reset is { } r
        ? ApiError.Of("github.rateLimited", ("time", r.ToLocalTime().ToString("HH:mm")))
        : ApiError.Of("github.rateLimitedNoTime");
}

/// <summary>Las consultas a api.github.com: con el token de «Cuentas y claves» (#320) tienen 5000 por hora en vez de 60.</summary>
public static class GithubApi
{
    /// <summary>El token guardado, o null si no hay (o el llavero no responde: sigue sin token).</summary>
    public static string? Token(Credentials? credentials)
    {
        try { return credentials?.Read("github.token") is { Length: > 0 } t ? t : null; }
        catch (Exception e) when (e is KeyringUnavailableException or CryptographicException) { return null; }
    }

    public static async Task<string> GetString(HttpClient http, string url, string? token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null && new Uri(url).Host == "api.github.com") request.Headers.Authorization = new("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests && IsLimit(response)) throw new GithubRateLimitException(Reset(response));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    static bool IsLimit(HttpResponseMessage r) =>
        r.StatusCode == HttpStatusCode.TooManyRequests || r.Headers.TryGetValues("X-RateLimit-Remaining", out var v) && v.FirstOrDefault() == "0";

    static DateTimeOffset? Reset(HttpResponseMessage r) =>
        r.Headers.TryGetValues("X-RateLimit-Reset", out var v) && long.TryParse(v.FirstOrDefault(), out var s) ? DateTimeOffset.FromUnixTimeSeconds(s) : null;

    /// <summary>Cuándo se libera el límite (la consulta no cuenta); null si no se sabe. Para los errores sin cabeceras, como los de Velopack.</summary>
    public static async Task<DateTimeOffset?> ResetAt(string? token)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Otteretro");
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/rate_limit");
            if (token is not null) request.Headers.Authorization = new("Bearer", token);
            using var response = await http.SendAsync(request);
            return Reset(response);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return null; }
    }
}
