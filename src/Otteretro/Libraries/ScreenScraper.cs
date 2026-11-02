using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Otteretro.Api;
using Otteretro.App;

namespace Otteretro.Libraries;

/// <summary>
/// ScreenScraper (§9.6, #198): identifica lo que no coincide con los DAT, por hash, nombre y tamaño. Es una ayuda: sin tus cuentas se salta,
/// y si el servicio se niega o se acaba tu cuota, avisa una vez y la tarea sigue sin él.
/// </summary>
public sealed partial class ScreenScraper(TaskRun run)
{
    /// <summary>Para las pruebas: responde en lugar del servicio.</summary>
    public static HttpMessageHandler? Handler { get; set; }

    const string Base = "https://api.screenscraper.fr/api2/jeuInfos.php?softname=Otteretro&output=json&romtype=rom";

    /// <summary>Sus números de consola.</summary>
    static readonly Dictionary<string, int> Systems = new()
    {
        ["nes"] = 3,
        ["snes"] = 4,
        ["gb"] = 9,
        ["vb"] = 11,
        ["gba"] = 12,
        ["gc"] = 13,
        ["n64"] = 14,
        ["ds"] = 15,
        ["wii"] = 16,
        ["3ds"] = 17,
        ["wiiu"] = 18,
        ["xbox"] = 32,
        ["x360"] = 33,
        ["ps1"] = 57,
        ["ps2"] = 58,
        ["ps3"] = 59,
        ["ps4"] = 60,
        ["psp"] = 61,
        ["vita"] = 62,
        ["switch"] = 225,
    };

    bool stopped;

    /// <summary>Los nombres del juego según ScreenScraper; vacío si no lo conoce o no se puede preguntar.</summary>
    public async Task<List<string>> NamesOf(string console, string file, string? sha1, CancellationToken ct)
    {
        if (stopped || !Systems.TryGetValue(console, out var system)) return [];
        string?[] keys = [run.Credentials.Read("screenscraper.devId"), run.Credentials.Read("screenscraper.devPassword"),
            run.Credentials.Read("screenscraper.user"), run.Credentials.Read("screenscraper.password")];
        if (keys.Any(string.IsNullOrEmpty)) return [];

        var url = $"{Base}&systemeid={system}&romnom={Uri.EscapeDataString(Path.GetFileName(file))}&romtaille={new FileInfo(file).Length}"
            + (sha1 is null ? "" : $"&sha1={sha1}");
        var api = new ApiClient(run.Db, Handler);
        try
        {
            // Las claves van aparte para que no entren en la clave de la caché. Poco tiempo: una negativa llega con un 200 y no debe quedar guardada.
            var text = await api.Send("screenscraper", HttpMethod.Get, url, null, TimeSpan.FromHours(1), r => r.RequestUri = new Uri(
                $"{url}&devid={Uri.EscapeDataString(keys[0]!)}&devpassword={Uri.EscapeDataString(keys[1]!)}&ssid={Uri.EscapeDataString(keys[2]!)}&sspassword={Uri.EscapeDataString(keys[3]!)}"), ct);
            if (text.Contains("Erreur de login", StringComparison.OrdinalIgnoreCase)) return Stop();
            // A veces trae barras sueltas en los textos, que no son JSON válido.
            using var json = JsonDocument.Parse(BadEscape().Replace(text, @"\\"));
            return json.RootElement.TryGetProperty("response", out var response) && response.TryGetProperty("jeu", out var game) && game.TryGetProperty("noms", out var names)
                ? names.EnumerateArray().Select(n => n.TryGetProperty("text", out var t) ? t.GetString() : null).OfType<string>().Distinct().ToList()
                : [];
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound) { return []; }
        catch (Exception e) when (e is HttpRequestException or JsonException) { return Stop(); }
    }

    List<string> Stop()
    {
        stopped = true;
        run.Log(ApiError.Of("import.log.screenscraper"), "warn");
        return [];
    }

    [GeneratedRegex("""\\(?!["\\/bfnrt]|u[0-9a-fA-F]{4})""")]
    private static partial Regex BadEscape();
}
