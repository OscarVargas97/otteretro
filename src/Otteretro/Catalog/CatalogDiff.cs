using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Data;

namespace Otteretro.Catalog;

/// <summary>Un cambio de una saga o un juego. <paramref name="Yours"/>: cuántos datos tuyos toca (progreso, archivos, enlaces, favoritos).</summary>
/// <param name="Change">renamed, merged, removed, added o changed (el juego pasó a otra saga).</param>
public record CatalogChange(string Kind, string Change, long Id, string Name, string? From, long? Into, string? IntoName, int Yours);

/// <param name="FromHistory">Si el `cambios.json` publicado cubre el salto desde tu versión; si no, solo vale la comparación propia (262).</param>
/// <param name="Mismatch">Lo que el catálogo publicó distinto de lo que la app ve al comparar (260): cada línea, «tipo id: publicado → propio».</param>
/// <param name="Counts">Cuántos hay de cada cambio, por si la lista viene recortada.</param>
public record CatalogChanges(string Repo, string? From, string To, bool FromHistory, List<CatalogChange> Items, Dictionary<string, int> Counts, List<string> Mismatch);

/// <summary>
/// Qué cambia al aceptar la versión que espera (#223, decisiones 260 y 262). La lista es la comparación propia entre las dos bases, porque es lo que
/// de verdad va a pasar con tus datos; el `cambios.json` publicado se usa para verificarla.
/// </summary>
public static class CatalogDiff
{
    /// <summary>Tope de la lista para lo que no toca tus datos: lo que sí, va siempre entero y primero.</summary>
    const int Shown = 300;

    record Entry(string Version, string Kind, string Change, long Id, string? Name = null, string? From = null, long? Into = null);
    record Published(List<string> Versions, List<Entry> Entries);

    public static CatalogChanges? Of(Database db)
    {
        if (CatalogUpdate.Waiting(db) is not { } pending) return null;
        var from = CatalogUpdate.Version(db);
        List<CatalogChange> own;
        using (var c = db.Open())
        {
            c.Execute("ATTACH DATABASE @file AS pending", new { file = CatalogUpdate.PendingFile(db) });
            try { own = Compare(c); }
            finally { c.Execute("DETACH DATABASE pending"); }
        }
        SqliteConnection.ClearAllPools();

        var published = ReadPublished(db);
        var fromHistory = published is not null && from is not null && published.Versions.Contains(from);
        var mismatch = fromHistory ? Verify(own, Collapse(published!.Entries.Where(e => string.CompareOrdinal(e.Version, from) > 0
            && string.CompareOrdinal(e.Version, pending.Tag) <= 0))) : [];

        var counts = own.GroupBy(x => x.Change).ToDictionary(g => g.Key, g => g.Count());
        var items = own.Where(x => x.Yours > 0).OrderByDescending(x => x.Yours)
            .Concat(own.Where(x => x.Yours == 0).OrderBy(x => x.Change).ThenBy(x => x.Name).Take(Shown)).ToList();
        return new(pending.Repo, from, pending.Tag, fromHistory, items, counts, mismatch);
    }

    static Published? ReadPublished(Database db)
    {
        try { return JsonSerializer.Deserialize<Published>(File.ReadAllText(CatalogUpdate.PendingChanges(db)), JsonSerializerOptions.Web); }
        catch (Exception e) when (e is IOException or JsonException) { return null; }
    }

    /// <summary>Lo de antes (`catalog`) contra lo que espera (`pending`), por id, con `replaced` para saber a dónde va lo fusionado.</summary>
    static List<CatalogChange> Compare(SqliteConnection c)
    {
        var hasReplaced = c.ExecuteScalar<long>("SELECT count(*) FROM pending.sqlite_master WHERE name = 'replaced'") > 0;
        string Into(string kind) => hasReplaced
            ? $"(SELECT r.new_id FROM pending.replaced r WHERE r.kind = '{kind}' AND r.old_id = o.id AND r.new_id IN (SELECT id FROM pending.{kind}))" : "NULL";

        // Lo tuyo de cada juego: progreso, enlaces y archivos de sus versiones; de cada saga, favoritos y «no me interesa».
        const string GameYours = """
            (SELECT count(*) FROM main.progress p WHERE p.game_id = o.id)
            + (SELECT count(*) FROM main.download_link d WHERE d.game_id = o.id)
            + (SELECT count(*) FROM main.source s JOIN catalog.version v ON v.id = s.version_id WHERE v.game_id = o.id)
            """;
        const string SagaYours = """
            (SELECT count(*) FROM main.saga_user u WHERE u.saga_id = o.id AND (u.favorite OR u.following))
            + (SELECT count(*) FROM main.dismissal d WHERE d.saga_id = o.id)
            """;

        var list = new List<CatalogChange>();
        static string Name(string kind, string alias) => kind == "saga" ? $"{alias}.name" : $"coalesce({alias}.name_es, {alias}.original_name)";
        foreach (var (kind, yours) in new[] { ("saga", SagaYours), ("game", GameYours) })
        {
            var (oldName, newName, into) = (Name(kind, "o"), Name(kind, "n"), Into(kind));
            list.AddRange(Rows(c, $"""
                SELECT '{kind}' AS Kind, CASE WHEN {into} IS NULL THEN 'removed' ELSE 'merged' END AS Change, o.id AS Id, {oldName} AS Name, NULL AS "From",
                       {into} AS "Into", (SELECT {Name(kind, "x")} FROM pending.{kind} x WHERE x.id = {into}) AS IntoName, {yours} AS Yours
                FROM catalog.{kind} o WHERE o.id NOT IN (SELECT id FROM pending.{kind})
                """));
            list.AddRange(Rows(c, $"""
                SELECT '{kind}' AS Kind, 'renamed' AS Change, o.id AS Id, {newName} AS Name, {oldName} AS "From", NULL AS "Into", NULL AS IntoName, {yours} AS Yours
                FROM catalog.{kind} o JOIN pending.{kind} n ON n.id = o.id WHERE {oldName} IS NOT {newName}
                """));
            list.AddRange(Rows(c, $"""
                SELECT '{kind}' AS Kind, 'added' AS Change, n.id AS Id, {newName} AS Name, NULL AS "From", NULL AS "Into", NULL AS IntoName, 0 AS Yours
                FROM pending.{kind} n WHERE n.id NOT IN (SELECT id FROM catalog.{kind})
                """));
        }
        // Un juego que pasa a otra saga: se ve en otro lado, y lo tuyo va con él.
        list.AddRange(Rows(c, """
            SELECT 'game' AS Kind, 'changed' AS Change, o.id AS Id, coalesce(n.name_es, n.original_name) AS Name,
                   (SELECT s.name FROM catalog.saga s WHERE s.id = o.saga_id) AS "From", n.saga_id AS "Into",
                   (SELECT s.name FROM pending.saga s WHERE s.id = n.saga_id) AS IntoName,
                   (SELECT count(*) FROM main.progress p WHERE p.game_id = o.id)
                   + (SELECT count(*) FROM main.download_link d WHERE d.game_id = o.id)
                   + (SELECT count(*) FROM main.source s JOIN catalog.version v ON v.id = s.version_id WHERE v.game_id = o.id) AS Yours
            FROM catalog.game o JOIN pending.game n ON n.id = o.id WHERE o.saga_id IS NOT n.saga_id
            """));
        return list;
    }

    /// <summary>Por posición: SQLite no dice el tipo de una columna sin filas, y Dapper no sabría armar el record.</summary>
    static IEnumerable<CatalogChange> Rows(SqliteConnection c, string sql) =>
        c.Query<(string Kind, string Change, long Id, string Name, string? From, long? Into, string? IntoName, long Yours)>(sql)
            .Select(r => new CatalogChange(r.Kind, r.Change, r.Id, r.Name, r.From, r.Into, r.IntoName, (int)r.Yours));

    /// <summary>
    /// Lo publicado, una entrada por saga o juego entre tu versión y la nueva: algo que entró y después se quitó no cuenta; algo que entró
    /// cuenta como nuevo aunque después cambie; en lo demás manda lo último.
    /// </summary>
    static Dictionary<(string Kind, long Id), Entry> Collapse(IEnumerable<Entry> entries)
    {
        var result = new Dictionary<(string, long), Entry>();
        foreach (var group in entries.Where(e => e.Kind is "saga" or "game").GroupBy(e => (e.Kind, e.Id)))
        {
            var ordered = group.OrderBy(e => e.Version, StringComparer.Ordinal).ToList();
            var added = ordered.Any(e => e.Change == "added");
            var last = ordered[^1];
            if (added && last.Change is "removed" or "merged") continue;
            result[group.Key] = added ? ordered.First(e => e.Change == "added") : last;
        }
        return result;
    }

    /// <summary>Lo que no coincide entre lo publicado y la comparación propia. «changed» no se compara: el catálogo puede publicar otros cambios.</summary>
    static List<string> Verify(List<CatalogChange> own, Dictionary<(string Kind, long Id), Entry> published)
    {
        static bool Compared(string change) => change is "renamed" or "merged" or "removed" or "added";
        var mine = own.Where(x => Compared(x.Change)).ToDictionary(x => (x.Kind, x.Id), x => x.Change);
        var theirs = published.Where(p => Compared(p.Value.Change)).ToDictionary(p => p.Key, p => p.Value.Change);
        return mine.Keys.Union(theirs.Keys).Order()
            .Where(k => mine.GetValueOrDefault(k) != theirs.GetValueOrDefault(k))
            .Select(k => $"{k.Kind} {k.Id}: {theirs.GetValueOrDefault(k) ?? "—"} → {mine.GetValueOrDefault(k) ?? "—"}")
            .ToList();
    }
}
