using System.Text.Json;
using Dapper;
using Otteretro.Data;

namespace Otteretro.Claude;

/// <param name="Result">El resultado de Claude tal cual (JSON); su forma depende de <paramref name="Prompt"/>.</param>
/// <param name="Title">De qué es: la saga, el juego o «juego · consola».</param>
public record ReviewItem(long Id, string Prompt, string Entity, long EntityId, string Title, string Result, string CreatedAt);

/// <param name="Change">move, merge o rename.</param>
/// <param name="Detail">El cambio tal cual (JSON).</param>
public record LearningItem(long Id, string Change, string? Reason, string Detail, string CreatedAt, bool Undone);
public record ReviewPage(List<ReviewItem> Pending, List<LearningItem> Learnings);

public record SourceRef(string Title, string Url);
public record RouteStepInfo(int Position, long GameId, string Name, int? Year, long? VersionId, string? VersionPlatform, string? Reason,
    string? Status, bool Owned, bool HasCover);
/// <param name="Kind">release, story o community.</param>
public record RouteInfo(long Id, string Kind, string? Rationale, List<SourceRef> Sources, List<RouteStepInfo> Steps);
public record RoutesPage(long? ChosenRoute, List<RouteInfo> Routes);

/// <param name="Criteria">content, gameplay, technical y original: Value es la nota de 1 a 5.</param>
public record CriterionInfo(string Criterion, string? Value, string? Text);
public record VersionCompare(long VersionId, string Platform, string Kind, int? Year, bool Best, List<CriterionInfo> Criteria);

/// <param name="Step">El primer juego de la ruta que no terminaste ni abandonaste.</param>
public record NextGame(long SagaId, string SagaName, string RouteKind, int Done, int Total, RouteStepInfo Step);

/// <summary>
/// Revisar › Resultados por aprobar y Aprendizajes (F2.6): aprobar aplica lo que preparó Claude (F2.5) a sus tablas; las
/// rutas aprobadas se leen en la saga, la comparativa en la ficha y el siguiente juego en Inicio.
/// </summary>
public static class ClaudeReview
{
    public static readonly string[] Criteria = ["content", "gameplay", "technical", "original"];
    static readonly JsonSerializerOptions Web = JsonSerializerOptions.Web;

    public static ReviewPage Page(Database db)
    {
        using var c = db.Open();
        var pending = c.Query<(long Id, string Entity, long EntityId, string Data, string CreatedAt, string? Title)>("""
            SELECT r.id, r.entity, r.entity_id, r.data, r.created_at,
                   CASE r.entity WHEN 'saga' THEN (SELECT name FROM saga WHERE id = r.entity_id)
                                 WHEN 'game' THEN (SELECT name_es FROM game WHERE id = r.entity_id)
                                 ELSE (SELECT g.name_es || ' · ' || v.platform FROM version v JOIN game g ON g.id = v.game_id WHERE v.id = r.entity_id) END
            FROM review_item r WHERE r.kind = 'pending_approval' AND r.status = 'open' ORDER BY r.id
            """).Select(r =>
        {
            using var doc = JsonDocument.Parse(r.Data);
            return new ReviewItem(r.Id, doc.RootElement.GetProperty("prompt").GetString()!, r.Entity, r.EntityId, r.Title ?? "?",
                doc.RootElement.GetProperty("result").GetRawText(), r.CreatedAt);
        }).Where(r => r.Title != "?").ToList(); // lo que ya no existe (una saga unida) no se muestra
        var learnings = c.Query<(long Id, string Content, string CreatedAt, bool Undone)>(
            "SELECT id, content, created_at, undone FROM learning ORDER BY id DESC LIMIT 200").Select(l =>
        {
            using var doc = JsonDocument.Parse(l.Content);
            var change = doc.RootElement.TryGetProperty("change", out var ch) ? ch : doc.RootElement;
            return new LearningItem(l.Id, change.TryGetProperty("kind", out var k) ? k.GetString() ?? "?" : "?",
                change.TryGetProperty("reason", out var why) ? why.GetString() : null, change.GetRawText(), l.CreatedAt, l.Undone);
        }).ToList();
        return new ReviewPage(pending, learnings);
    }

    /// <summary>Aplica un resultado por aprobar. False si no existe o ya se resolvió.</summary>
    public static bool Approve(Database db, long id)
    {
        using var c = db.Open();
        using var t = c.BeginTransaction();
        var item = c.QuerySingleOrDefault<(string Entity, long EntityId, string Data)?>(
            "SELECT entity, entity_id, data FROM review_item WHERE id = @id AND kind = 'pending_approval' AND status = 'open'", new { id }, t);
        if (item is not { } r) return false;
        using var doc = JsonDocument.Parse(r.Data);
        var result = doc.RootElement.GetProperty("result");
        switch (doc.RootElement.GetProperty("prompt").GetString())
        {
            case SagaAnalysis.Routes:
                var chosen = c.ExecuteScalar<string?>("SELECT kind FROM route WHERE id = (SELECT chosen_route FROM saga WHERE id = @EntityId)", new { r.EntityId }, t);
                foreach (var route in result.Deserialize<List<SagaAnalysis.Route>>(Web)!)
                {
                    c.Execute("DELETE FROM route WHERE saga_id = @EntityId AND kind = @Kind", new { r.EntityId, route.Kind }, t);
                    var routeId = c.ExecuteScalar<long>("""
                        INSERT INTO route (saga_id, kind, rationale, sources, approved) VALUES (@EntityId, @Kind, @Rationale, @sources, 1) RETURNING id
                        """, new { r.EntityId, route.Kind, route.Rationale, sources = JsonSerializer.Serialize(route.Sources, Web) }, t);
                    var position = 0;
                    foreach (var step in route.Steps)
                        c.Execute("""
                            INSERT INTO route_step (route_id, position, game_id, recommended_version_id, reason)
                            SELECT @routeId, @position, @GameId, (SELECT id FROM version WHERE id = @VersionId AND game_id = @GameId), @Reason
                            WHERE EXISTS (SELECT 1 FROM game WHERE id = @GameId)
                            """, new { routeId, position = ++position, step.GameId, step.VersionId, step.Reason }, t);
                    // La ruta que seguías se renueva; sin ninguna elegida, se sigue la de la comunidad.
                    if (route.Kind == (chosen ?? "community"))
                        c.Execute("UPDATE saga SET chosen_route = @routeId WHERE id = @EntityId", new { routeId, r.EntityId }, t);
                }
                break;
            case SagaAnalysis.Iconic:
                c.Execute("UPDATE saga SET iconic = @iconic WHERE id = @EntityId", new { iconic = result.GetProperty("iconic").GetBoolean(), r.EntityId }, t);
                break;
            case SagaAnalysis.BestVersion:
                var best = result.Deserialize<SagaAnalysis.GameBest>(Web)!;
                foreach (var v in best.Versions)
                    foreach (var (criterion, value) in Criteria.Zip(new[] { v.Content, v.Gameplay, v.Technical, v.Original }))
                        c.Execute("""
                            INSERT INTO version_criterion (version_id, criterion, value, text)
                            SELECT @VersionId, @criterion, @score, @Text WHERE EXISTS (SELECT 1 FROM version WHERE id = @VersionId)
                            ON CONFLICT (version_id, criterion) DO UPDATE SET value = excluded.value, text = excluded.text
                            """, new { v.VersionId, criterion, score = value.Score.ToString(), value.Text }, t);
                Store(c, t, r.EntityId, "best_version", best.BestVersionId.ToString());
                break;
            case SagaAnalysis.Classify:
                c.Execute("UPDATE version SET kind = @kind WHERE id = @EntityId", new { kind = result.GetProperty("kind").GetString(), r.EntityId }, t);
                break;
            case SagaAnalysis.Translate:
                Store(c, t, r.EntityId, "summary_es", result.GetProperty("text").GetString());
                break;
            default:
                return false;
        }
        c.Execute("UPDATE review_item SET status = 'approved' WHERE id = @id", new { id }, t);
        t.Commit();
        return true;
    }

    public static bool Reject(Database db, long id)
    {
        using var c = db.Open();
        return c.Execute("UPDATE review_item SET status = 'rejected' WHERE id = @id AND kind = 'pending_approval' AND status = 'open'", new { id }) > 0;
    }

    /// <summary>Un dato de Claude sin columna propia (descripción, mejor versión): queda en `field_value`.</summary>
    static void Store(System.Data.IDbConnection c, System.Data.IDbTransaction t, long gameId, string field, string? value) =>
        c.Execute("""
            INSERT INTO field_value (entity, entity_id, field, value, source) VALUES ('game', @gameId, @field, @value, 'claude')
            ON CONFLICT (entity, entity_id, field, source) DO UPDATE SET value = excluded.value
            """, new { gameId, field, value }, t);

    public static RoutesPage Routes(Database db, long sagaId)
    {
        using var c = db.Open();
        var chosen = c.ExecuteScalar<long?>("SELECT chosen_route FROM saga WHERE id = @sagaId", new { sagaId });
        var covers = Catalog.CatalogQueries.Covers(db);
        var routes = c.Query<(long Id, string Kind, string? Rationale, string? Sources)>(
            "SELECT id, kind, rationale, sources FROM route WHERE saga_id = @sagaId AND approved = 1 ORDER BY CASE kind WHEN 'release' THEN 0 WHEN 'story' THEN 1 ELSE 2 END",
            new { sagaId }).ToList();
        return new RoutesPage(chosen, routes.Select(r => new RouteInfo(r.Id, r.Kind, r.Rationale,
            r.Sources is null ? [] : JsonSerializer.Deserialize<List<SourceRef>>(r.Sources, Web) ?? [], Steps(c, r.Id, covers))).ToList());
    }

    static List<RouteStepInfo> Steps(System.Data.IDbConnection c, long routeId, HashSet<long> covers) =>
        c.Query<(long Position, long GameId, string Name, long? Year, long? VersionId, string? Platform, string? Reason, string? Status, bool Owned)>("""
            SELECT st.position, g.id, g.name_es, g.year, st.recommended_version_id, v.platform, st.reason, p.status,
                   EXISTS (SELECT 1 FROM source o JOIN version x ON x.id = o.version_id WHERE x.game_id = g.id)
            FROM route_step st JOIN game g ON g.id = st.game_id
            LEFT JOIN version v ON v.id = st.recommended_version_id LEFT JOIN progress p ON p.game_id = g.id
            WHERE st.route_id = @routeId ORDER BY st.position
            """, new { routeId }).Select(s => new RouteStepInfo((int)s.Position, s.GameId, s.Name, (int?)s.Year, s.VersionId, s.Platform, s.Reason,
            s.Status, s.Owned, covers.Contains(s.GameId))).ToList();

    /// <summary>Elige qué ruta sigues en una saga (null = ninguna). False si la ruta no es de esa saga.</summary>
    public static bool Choose(Database db, long sagaId, long? routeId)
    {
        using var c = db.Open();
        if (routeId is not null && c.ExecuteScalar<long>("SELECT count(*) FROM route WHERE id = @routeId AND saga_id = @sagaId", new { routeId, sagaId }) == 0)
            return false;
        return c.Execute("UPDATE saga SET chosen_route = @routeId, following = CASE WHEN @routeId IS NULL THEN following ELSE 1 END WHERE id = @sagaId",
            new { routeId, sagaId }) > 0;
    }

    public static List<VersionCompare> Compare(Database db, long gameId)
    {
        using var c = db.Open();
        var best = c.ExecuteScalar<string?>("SELECT value FROM field_value WHERE entity = 'game' AND entity_id = @gameId AND field = 'best_version' AND source = 'claude'", new { gameId });
        var criteria = c.Query<(long VersionId, string Criterion, string? Value, string? Text)>(
            "SELECT k.version_id, k.criterion, k.value, k.text FROM version_criterion k JOIN version v ON v.id = k.version_id WHERE v.game_id = @gameId", new { gameId })
            .ToLookup(k => k.VersionId);
        return c.Query<(long Id, string Platform, string Kind, long? Year)>("SELECT id, platform, kind, year FROM version WHERE game_id = @gameId ORDER BY year, id", new { gameId })
            .Select(v => new VersionCompare(v.Id, v.Platform, v.Kind, (int?)v.Year, best == v.Id.ToString(),
                Criteria.Select(k => criteria[v.Id].FirstOrDefault(x => x.Criterion == k) is { Criterion: not null } x ? new CriterionInfo(k, x.Value, x.Text) : new CriterionInfo(k, null, null)).ToList()))
            .ToList();
    }

    /// <summary>Inicio › Siguiente en tus rutas: en cada saga que sigues con una ruta elegida, el primer juego sin terminar.</summary>
    public static List<NextGame> Next(Database db)
    {
        using var c = db.Open();
        var routes = c.Query<(long SagaId, string SagaName, long RouteId, string Kind)>("""
            SELECT s.id, s.name, r.id, r.kind FROM saga s JOIN route r ON r.id = s.chosen_route WHERE s.following = 1 ORDER BY s.favorite DESC, s.name
            """).ToList();
        var next = new List<NextGame>();
        var covers = Catalog.CatalogQueries.Covers(db);
        foreach (var r in routes)
        {
            var steps = Steps(c, r.RouteId, covers);
            var done = steps.Count(s => s.Status is "finished" or "completed" or "dropped");
            if (steps.FirstOrDefault(s => s.Status is not ("finished" or "completed" or "dropped")) is { } step)
                next.Add(new NextGame(r.SagaId, r.SagaName, r.Kind, done, steps.Count, step));
        }
        return next;
    }
}
