using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>
/// TitleID → nombre del juego, para reconocer sin IA (decisión 276): Switch 2 con la tabla pública de Switchbrew (#237) y Switch con
/// `titledb` (#241). Cada tabla se baja, se guarda compacta en `cache\` y se renueva sola; sin conexión vale lo guardado.
/// </summary>
public static partial class SwitchTitles
{
    sealed record Source(string Url, string File, TimeSpan MaxAge, Func<Stream, CancellationToken, Task<Dictionary<string, string>>> Read);

    /// <summary>≈475 juegos; la tabla de la wiki es chica y cambia seguido.</summary>
    static readonly Source Switch2 = new("https://switchbrew.org/w/index.php?title=Switch_2:_Title_list/Games&action=raw", "switch2-titulos.json",
        TimeSpan.FromDays(7), async (stream, ct) => Parse(await new StreamReader(stream).ReadToEndAsync(ct)));

    /// <summary>`US.en.json` de titledb pesa unos 90 MB: se baja una vez al mes y se guarda solo TitleID → nombre.</summary>
    static readonly Source Switch1 = new("https://raw.githubusercontent.com/blawar/titledb/master/US.en.json", "switch-titulos.json",
        TimeSpan.FromDays(30), ReadTitledb);

    /// <summary>Para las pruebas: responde en lugar de Switchbrew y de titledb.</summary>
    public static HttpMessageHandler? Handler { get; set; }

    /// <summary>Las tablas ya leídas, por archivo y fecha: se leen del disco una vez y no en cada juego.</summary>
    static readonly Dictionary<string, (DateTime Stamp, Dictionary<string, string> Table)> Loaded = [];

    /// <summary>El nombre del juego con ese TitleID (o el de su base); null si no está o la consola no tiene tabla.</summary>
    public static async Task<string?> NameOf(Database db, string titleId, CancellationToken ct = default)
    {
        var source = titleId[..2] switch { "04" => Switch2, "01" => Switch1, _ => null };
        if (source is null) return null;
        var table = await Table(db, source, ct);
        return table.GetValueOrDefault(titleId.ToUpperInvariant()) ?? table.GetValueOrDefault(Scan.BaseOf(titleId));
    }

    static async Task<Dictionary<string, string>> Table(Database db, Source source, CancellationToken ct)
    {
        var file = Path.Combine(Path.GetDirectoryName(db.FilePath)!, "cache", source.File);
        if (!File.Exists(file) || DateTime.UtcNow - File.GetLastWriteTimeUtc(file) >= source.MaxAge)
            try
            {
                using var http = Handler is null ? new HttpClient() : new HttpClient(Handler);
                http.Timeout = TimeSpan.FromMinutes(10);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Otteretro");
                await using var stream = await http.GetStreamAsync(source.Url, ct);
                var fresh = await source.Read(stream, ct);
                if (fresh.Count > 0)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                    await File.WriteAllTextAsync(file + ".part", JsonSerializer.Serialize(fresh), ct);
                    File.Move(file + ".part", file, overwrite: true);
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or JsonException) { } // vale lo guardado
        if (!File.Exists(file)) return [];
        var stamp = File.GetLastWriteTimeUtc(file);
        lock (Loaded)
            if (Loaded.TryGetValue(file, out var hit) && hit.Stamp == stamp) return hit.Table;
        Dictionary<string, string> table;
        try { table = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(file, ct)) ?? []; }
        catch (JsonException) { table = []; }
        lock (Loaded) Loaded[file] = (stamp, table);
        return table;
    }

    record TitledbEntry([property: JsonPropertyName("id")] string? Id, [property: JsonPropertyName("name")] string? Name);

    /// <summary>titledb: un objeto por juego (nsuId → {id, name, …}); se lee de a poco y solo se guarda id → nombre.</summary>
    static async Task<Dictionary<string, string>> ReadTitledb(Stream stream, CancellationToken ct)
    {
        var all = await JsonSerializer.DeserializeAsync<Dictionary<string, TitledbEntry>>(stream, cancellationToken: ct) ?? [];
        var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in all.Values)
            if (e.Id is { Length: 16 } id && !string.IsNullOrWhiteSpace(e.Name)) table.TryAdd(id.ToUpperInvariant(), Clean(e.Name));
        return table;
    }

    /// <summary>Las filas `| TitleID || Nombre || …` de la tabla de Switchbrew.</summary>
    public static Dictionary<string, string> Parse(string wiki)
    {
        var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Row().Matches(wiki)) table.TryAdd(m.Groups[1].Value.ToUpperInvariant(), Clean(m.Groups[2].Value));
        return table;
    }

    static string Clean(string name) => Edition().Replace(name.Replace("™", "").Replace("®", ""), "").Trim();

    [GeneratedRegex(@"^\|\s*(0[14][0-9A-Fa-f]{14})\s*\|\|\s*([^|\n]+?)\s*\|\|", RegexOptions.Multiline)]
    private static partial Regex Row();

    /// <summary>«… – Nintendo Switch 2 Edition» y «… for Nintendo Switch 2»: el juego del catálogo es el mismo.</summary>
    [GeneratedRegex(@"\s*([-–:]\s*)?(Nintendo Switch 2 Edition|for Nintendo Switch 2)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Edition();
}
