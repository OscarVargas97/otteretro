using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <param name="Path">Relativa a la carpeta elegida.</param>
/// <param name="Reason">Por qué viene sin marcar: extra, region u owned; null si viene marcado.</param>
public record PreviewItem(string Path, string? Console, string Name, long? GameId, string? Game, long? SagaId, string? Saga, bool Selected, string? Reason);

public record MegapackPreview(List<PreviewItem> Items, List<string> Regions);

/// <summary>
/// La vista previa de una carpeta o un megapack (§9.4, #197), solo con los nombres: rápida y sin mover nada. Una versión por juego (1G1R)
/// con tu orden de regiones, sin hacks, betas ni demos; las traducciones de fans sí.
/// </summary>
public static partial class Megapack
{
    public static readonly string[] DefaultRegions = ["es", "Europe", "USA", "Japan", "World"];

    /// <summary>Hack, beta, demo, sin licencia y similares. Las traducciones de fans (`[T+Spa]`) no cuentan.</summary>
    public static bool IsExtra(string name) => ExtraTag().IsMatch(name) || BadDump().IsMatch(name);

    /// <summary>Un disco de un juego de varios: no es otra versión de lo mismo.</summary>
    public static string? DiscOf(string name) => Disc().Match(name) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Menor es mejor. Español primero si está en tu orden: por idioma (`Es`), región o traducción de fans.</summary>
    public static int Rank(string name, IReadOnlyList<string> order)
    {
        var tags = Tag().Matches(name).Select(m => m.Value).ToList();
        for (var i = 0; i < order.Count; i++)
            if (order[i] == "es"
                    ? tags.Any(t => Spanish().IsMatch(t))
                    : tags.Any(t => t.Contains(order[i], StringComparison.OrdinalIgnoreCase)))
                return i;
        return order.Count;
    }

    static int Revision(string name) => Rev().Match(name) is { Success: true } m ? int.Parse(m.Groups[1].Value) : 0;

    record Row(string Path, string? Console, string Name, long? Version);

    public static MegapackPreview Preview(Database db, string root, IReadOnlyList<string> order)
    {
        List<(string Id, string Name)> consoles;
        using (var c = db.Open()) consoles = c.Query<(string, string)>("SELECT id, name FROM console").ToList();
        var catalog = Scan.Catalog(db);
        var aliases = Scan.Aliases(db);
        var rows = new List<Row>();
        foreach (var file in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true }))
        {
            string? console;
            string name;
            if (Archives.IsArchive(file))
            {
                if (!Archives.IsMain(file)) continue;
                name = Import.Label(file);
                console = Import.ConsoleOf(Archives.FirstGame(file) ?? "", Import.Folders(Path.GetDirectoryName(file)!, root), consoles);
            }
            else if (Scan.GameExtensions.Contains(Path.GetExtension(file)))
            {
                name = Path.GetFileNameWithoutExtension(file);
                console = Import.Classify(file, Import.Folders(Path.GetDirectoryName(file)!, root), consoles).Console;
            }
            else continue;
            var version = console is null ? null
                : Scan.Match(aliases.GetValueOrDefault(console) ?? [], name) ?? Scan.Match(catalog.GetValueOrDefault(console) ?? [], name);
            rows.Add(new(Path.GetRelativePath(root, file), console, name, version));
        }

        // Una versión por juego: lo que no se reconoce se agrupa por su nombre sin región ni etiquetas.
        var best = rows.Where(r => !IsExtra(r.Name) && r.Console is not null)
            .GroupBy(r => (r.Console, Game: r.Version?.ToString() ?? Scan.Normalize(r.Name), Disc: DiscOf(r.Name)))
            .Select(g => g.OrderBy(r => Rank(r.Name, order)).ThenByDescending(r => Revision(r.Name)).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).First())
            .ToHashSet();

        using var db2 = db.Open();
        var games = db2.Query<(long Version, long GameId, string Game, long? SagaId, string? Saga)>("""
            SELECT v.id, g.id, g.name_es, s.id, s.name FROM version v JOIN game g ON g.id = v.game_id LEFT JOIN saga s ON s.id = g.saga_id
            """).ToDictionary(g => g.Version);
        var verified = db2.Query<long>("SELECT DISTINCT version_id FROM source WHERE kind = 'file' AND verified = 1 AND version_id IS NOT NULL").ToHashSet();
        var items = rows.OrderBy(r => r.Console).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Select(r =>
        {
            var reason = IsExtra(r.Name) ? "extra"
                : r.Console is not null && !best.Contains(r) ? "region"
                : r.Version is { } v && verified.Contains(v) ? "owned"
                : null;
            var game = r.Version is { } id && games.TryGetValue(id, out var g) ? g : default;
            return new PreviewItem(r.Path, r.Console, r.Name, game.GameId == 0 ? null : game.GameId, game.Game, game.SagaId, game.Saga, reason is null, reason);
        }).ToList();
        return new(items, order.ToList());
    }

    [GeneratedRegex(@"\((Beta|Demo|Proto|Prototype|Sample|Unl|Pirate|Hack|Aftermarket|Kiosk|Program|Test)\b[^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ExtraTag();

    /// <summary>Las marcas de GoodTools: `[h1]` hack, `[b1]` dañado, `[p1]` pirata, `[a1]` alterno, `[o1]` de más, `[f1]` arreglado.</summary>
    [GeneratedRegex(@"\[[hbpaof]\d*(\s[^\]]*)?\]")]
    private static partial Regex BadDump();

    [GeneratedRegex(@"\((?:Disc|Disk|CD)\s*(\d+)[^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex Disc();

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\(Spain\)|\(Mexico\)|\(Latin America\)|[(,]\s*Es\s*[,)]|\[T[+\-]Spa|Spanish|Español", RegexOptions.IgnoreCase)]
    private static partial Regex Spanish();

    [GeneratedRegex(@"\(Rev\s*(\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex Rev();
}
