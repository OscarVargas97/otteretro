using Dapper;
using Microsoft.Data.Sqlite;

namespace Otteretro.Catalog;

/// <summary>
/// Cada dato del catálogo con su fuente (`field_value`). La columna de la tabla guarda el ganador:
/// lo manual siempre, y si no, la fuente con más prioridad para ese campo (§6.8).
/// </summary>
public static class FieldValues
{
    /// <summary>Campos que se pueden escribir así, con su tabla. Nada fuera de esta lista llega al SQL.</summary>
    static readonly Dictionary<(string Entity, string Field), string> Columns = new()
    {
        [("game", "name_es")] = "game",
        [("game", "original_name")] = "game",
        [("game", "year")] = "game",
        [("saga", "name")] = "saga",
    };

    /// <summary>Orden de las fuentes, de más a menos confiable, por campo. Lo que no está aquí va al final.</summary>
    public static readonly Dictionary<string, string[]> Priority = new()
    {
        ["default"] = ["igdb", "wikidata", "rawg", "hltb"],
    };

    public const string Manual = "manual";

    public static void Set(SqliteConnection c, string entity, long id, string field, object? value, string source, SqliteTransaction? t = null)
    {
        var table = Columns[(entity, field)];
        c.Execute("""
            INSERT INTO field_value (entity, entity_id, field, value, source, manual) VALUES (@entity, @id, @field, @value, @source, @manual)
            ON CONFLICT (entity, entity_id, field, source) DO UPDATE SET value = excluded.value
            """, new { entity, id, field, value = value?.ToString(), source, manual = source == Manual }, t);

        var order = Priority.GetValueOrDefault(field) ?? Priority["default"];
        var winner = c.Query<(string? Value, string Source, bool Manual)>(
                "SELECT value, source, manual FROM field_value WHERE entity = @entity AND entity_id = @id AND field = @field",
                new { entity, id, field }, t)
            .OrderByDescending(r => r.Manual)
            .ThenBy(r => Array.IndexOf(order, r.Source) is var i and >= 0 ? i : int.MaxValue)
            .First();
        c.Execute($"UPDATE {table} SET {field} = @value WHERE id = @id", new { id, value = winner.Value }, t);
    }
}
