using Dapper;
using Otteretro.Data;

namespace Otteretro.Catalog;

/// <param name="Visible">Si la app la muestra (#224): desactivarla la saca de Sagas, Colección y la búsqueda, sin borrar nada.</param>
public record ConsoleInfo(string Id, string Name, string? Company, bool Visible, int Games);

public record ConsolesPage(List<ConsoleInfo> Consoles);

public record ConsoleVisibleRequest(bool Visible);

/// <summary>Las consolas del catálogo y cuáles muestra la app (#224, decisión 263).</summary>
public static class Consoles
{
    public static ConsolesPage Page(Database db)
    {
        using var c = db.Open();
        var consoles = c.Query<(string Id, string Name, string? Company, bool Visible, long Games)>("""
            SELECT k.id, k.name, k.company, k.id NOT IN (SELECT console_id FROM console_hidden), (SELECT count(DISTINCT game_id) FROM version v WHERE v.platform = k.id)
            FROM console k ORDER BY k.rowid
            """).Select(r => new ConsoleInfo(r.Id, r.Name, r.Company, r.Visible, (int)r.Games)).ToList();
        return new ConsolesPage(consoles);
    }

    /// <returns>false si la consola no existe.</returns>
    public static bool SetVisible(Database db, string id, bool visible)
    {
        using var c = db.Open();
        if (c.ExecuteScalar<long>("SELECT count(*) FROM console WHERE id = @id", new { id }) == 0) return false;
        c.Execute(visible ? "DELETE FROM console_hidden WHERE console_id = @id" : "INSERT OR IGNORE INTO console_hidden (console_id) VALUES (@id)", new { id });
        SagaCache.Refresh(db); // se ve al instante (#225)
        return true;
    }
}
