using Dapper;
using Otteretro.Api;
using Otteretro.Data;

namespace Otteretro.Catalog;

/// <param name="RefreshedAt">Hasta cuándo trajo IGDB sus juegos (F2.14); null = nunca se cargó.</param>
/// <param name="Loading">El avance (0 a 100) de su carga desde IGDB, si está en la cola o corriendo (#109).</param>
public record ConsoleInfo(string Id, string Name, string? Company, bool Enabled, string? RefreshedAt, int Games, int? Loading);

/// <param name="RefreshDays">Cada cuántos días se actualiza el catálogo (F2.14).</param>
public record ConsolesPage(List<ConsoleInfo> Consoles);

/// <param name="ConsoleId">La consola donde ya está, si está.</param>
public record PlatformInfo(long IgdbId, string Name, string? Abbreviation, string? Company, string? ConsoleId);

public record EnableConsoleRequest(long IgdbId);

public record ConsoleUpdateRequest(bool Enabled);

public record RefreshSettingsRequest(int Days);

/// <summary>Las consolas que habilitas (F2.13, #83) y cada cuánto se actualiza el catálogo (F2.14, #84).</summary>
public static class Consoles
{
    public static ConsolesPage Page(Database db)
    {
        using var c = db.Open();
        var consoles = c.Query<(string Id, string Name, string? Company, bool Enabled, string? RefreshedAt, long Games, long? Loading)>("""
            SELECT k.id, k.name, k.company, k.enabled, k.refreshed_at, (SELECT count(DISTINCT game_id) FROM version v WHERE v.platform = k.id),
                   NULL
            FROM console k ORDER BY k.rowid
            """).Select(r => new ConsoleInfo(r.Id, r.Name, r.Company, r.Enabled, r.RefreshedAt, (int)r.Games, (int?)r.Loading)).ToList();
        return new ConsolesPage(consoles);
    }
}
