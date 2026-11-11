using Dapper;
using Otteretro.Data;
using Otteretro.Libraries;

namespace Otteretro.Play;

/// <summary>Una forma de jugar una versión del juego.</summary>
/// <param name="Key">pc:&lt;versión de PC&gt;, port:&lt;id del port&gt; o emulator:&lt;id del emulador&gt;.</param>
/// <param name="Kind">pc, port o emulator.</param>
/// <param name="Criterion">El criterio de la regla que cumple (§8.3): pc, port (100 %), perfect (emulador perfecto) o playable.</param>
/// <param name="Level">Qué tan jugable: 3 perfecto, 2 jugable, 1 parcial, 0 no funciona; null sin dato.</param>
/// <param name="Installed">Se puede jugar ya; false: un port con receta que falta instalar.</param>
public record PlayOption(string Key, string Kind, string Name, string Criterion, int? Level, bool Installed);

/// <summary>Cómo se juega una versión del juego (#398): sus opciones y la elegida.</summary>
/// <param name="Chosen">La key de la elegida; null si no hay ninguna opción.</param>
/// <param name="Reason">Por qué: manual (elegida a mano) o el criterio de la regla que la eligió.</param>
public record VersionPlay(long VersionId, string Platform, List<PlayOption> Options, string? Chosen, string? Reason);

public record PlayChoiceRequest(string Key);
public record PlayOrder(List<string> Criteria);

/// <summary>
/// La regla de cómo jugar (§8.3, #398): PC oficial que tienes &gt; port al 100 % &gt; emulador perfecto &gt; lo más jugable, en el orden
/// que se guarde en Ajustes. La elección manual de una versión manda sobre la regla mientras siga siendo una de sus opciones.
/// </summary>
public static class PlayChoice
{
    public const string Pc = "pc", Port = "port", Perfect = "perfect", Playable = "playable";
    public static readonly string[] DefaultOrder = [Pc, Port, Perfect, Playable];
    const string OrderSetting = "play.order";

    /// <summary>El orden de los criterios: el guardado o, si no hay, el de §8.3.</summary>
    public static List<string> Order(Database db)
    {
        using var c = db.Open();
        var saved = c.ExecuteScalar<string?>("SELECT value FROM setting WHERE machine_id = '' AND name = @OrderSetting", new { OrderSetting })?.Split(',').ToList();
        return saved is not null && IsOrder(saved) ? saved : [.. DefaultOrder];
    }

    static bool IsOrder(List<string> criteria) => criteria.Count == DefaultOrder.Length && DefaultOrder.All(criteria.Contains);

    /// <summary>Guarda el orden; false si no son los cuatro criterios, cada uno una vez.</summary>
    public static bool SetOrder(Database db, List<string> criteria)
    {
        if (!IsOrder(criteria)) return false;
        using var c = db.Open();
        c.Execute("INSERT INTO setting (machine_id, name, value) VALUES ('', @OrderSetting, @value) ON CONFLICT (name, machine_id) DO UPDATE SET value = excluded.value",
            new { OrderSetting, value = string.Join(',', criteria) });
        return true;
    }

    /// <summary>
    /// Cada versión del juego que tienes (o cuya versión de PC tienes), con sus opciones y la elegida. Las opciones: la versión de PC
    /// que tienes, los ports de su consola instalados o con receta, y los emuladores de la consola (el del perfil efectivo primero).
    /// </summary>
    public static List<VersionPlay> OfGame(Database db, long gameId)
    {
        var ports = Ports.OfGame(db, gameId).Where(p => p.Status != Ports.NoPort && (p.Installed.Count > 0 || p.RecipeId is not null)).ToList();
        var order = Order(db);
        List<(long Id, string Platform)> versions;
        List<(long Id, string Platform)> pcOwned;
        Dictionary<long, string> manual;
        using (var c = db.Open())
        {
            pcOwned = c.Query<(long, string)>("""
                SELECT v.id, v.platform FROM version v WHERE v.game_id = @gameId AND v.platform IN ('pc', 'pcvr')
                  AND EXISTS (SELECT 1 FROM source s WHERE s.version_id = v.id) ORDER BY v.platform, v.id
                """, new { gameId }).ToList();
            versions = c.Query<(long, string)>("""
                SELECT v.id, v.platform FROM version v WHERE v.game_id = @gameId
                  AND (@pc OR EXISTS (SELECT 1 FROM source s WHERE s.version_id = v.id)) ORDER BY v.id
                """, new { gameId, pc = pcOwned.Count > 0 }).ToList();
            manual = c.Query<(long, string)>("SELECT version_id, option FROM play_choice WHERE version_id IN @ids", new { ids = versions.Select(v => v.Id) })
                .ToDictionary(r => r.Item1, r => r.Item2);
        }
        return versions.Select(v =>
        {
            var options = pcOwned.Select(p => new PlayOption($"pc:{p.Id}", Pc, p.Platform, Pc, null, true)).ToList();
            options.AddRange(ports.Where(p => p.Console == v.Platform).Select(p =>
                new PlayOption($"port:{p.Id}", Port, p.Project, p.FullyPlayable ? Port : Playable, PortLevel(p.Status), p.Installed.Count > 0)));
            options.AddRange(Emulators(db, v.Id, v.Platform, ports.Where(p => p.Console == v.Platform).ToList()));
            var (chosen, reason) = Choose(options, order, manual.GetValueOrDefault(v.Id));
            return new VersionPlay(v.Id, v.Platform, options, chosen, reason);
        }).ToList();
    }

    /// <summary>
    /// La regla: la manual si sigue siendo una opción; si no, la del primer criterio del orden que tenga alguna y, entre ellas, la
    /// más jugable (3, 2, 1, sin dato, 0) y la que ya está instalada. A igual nivel, la que viene antes (el emulador efectivo primero).
    /// </summary>
    public static (string? Chosen, string? Reason) Choose(List<PlayOption> options, List<string> order, string? manual)
    {
        if (manual is not null && options.Any(o => o.Key == manual)) return (manual, "manual");
        var best = options
            .OrderBy(o => order.IndexOf(o.Criterion))
            .ThenByDescending(o => o.Level switch { null => 0, 0 => -1, var l => l })
            .ThenByDescending(o => o.Installed)
            .FirstOrDefault();
        return (best?.Key, best?.Criterion);
    }

    /// <summary>El estado de un port como nivel: complete es 100 % jugable, playable con fallos, beta y experimental a medias.</summary>
    static int PortLevel(string status) => status switch { Ports.Complete => 3, "playable" => 2, _ => 1 };

    /// <summary>
    /// Los emuladores de la consola, el del perfil efectivo primero. Su nivel sale de la compatibilidad que publica el catálogo con
    /// los ports del juego (otteretro-catalogo#70): el emulador de referencia de la consola y cómo corre ahí.
    /// </summary>
    /// <remarks>shortcut: solo hay nivel para juegos con port en el catálogo y para su emulador de referencia; los demás quedan sin dato
    /// (cuentan como «lo más jugable»). Cuando el catálogo publique compatibilidad por juego y emulador, se lee de ahí.</remarks>
    static IEnumerable<PlayOption> Emulators(Database db, long version, string platform, List<PortInfo> ports)
    {
        var effective = Profiles.Effective(db, version)?.EmulatorId;
        List<(string Id, string Name)> emulators;
        using (var c = db.Open())
            emulators = c.Query<(string, string)>("""
                SELECT e.id, e.name FROM console_emulator ce JOIN emulator e ON e.id = ce.emulator_id
                WHERE ce.console_id = @platform AND e.kind = 'emulator' ORDER BY e.id = @effective DESC, ce.is_base DESC, e.name
                """, new { platform, effective }).ToList();
        var compat = ports.Where(p => p.Emulator is not null && p.EmulatorLevel is not null).ToList();
        return emulators.Select(e =>
        {
            var names = new[] { e.Name, Recipes.Find(e.Id)?.Name }.OfType<string>().ToList();
            var level = compat.Where(p => p.Emulator!.Split('/').Any(r => names.Contains(r.Trim(), StringComparer.OrdinalIgnoreCase))).Max(p => p.EmulatorLevel);
            return new PlayOption($"emulator:{e.Id}", "emulator", Recipes.Find(e.Id)?.Name ?? e.Name, level == 3 ? Perfect : Playable, level, true);
        });
    }

    /// <summary>Elige a mano (manda sobre la regla). False si la versión no existe en el juego o la key no es una de sus opciones.</summary>
    public static bool SetManual(Database db, long gameId, long versionId, string key)
    {
        if (OfGame(db, gameId).FirstOrDefault(v => v.VersionId == versionId) is not { } v || v.Options.All(o => o.Key != key)) return false;
        using var c = db.Open();
        c.Execute("""
            INSERT INTO play_choice (version_id, option, chosen_at) VALUES (@versionId, @key, @now)
            ON CONFLICT (version_id) DO UPDATE SET option = excluded.option, chosen_at = excluded.chosen_at
            """, new { versionId, key, now = DateTime.UtcNow.ToString("O") });
        return true;
    }

    /// <summary>«Volver a la automática»: borra la elección manual de la versión.</summary>
    public static void ClearManual(Database db, long versionId)
    {
        using var c = db.Open();
        c.Execute("DELETE FROM play_choice WHERE version_id = @versionId", new { versionId });
    }
}
