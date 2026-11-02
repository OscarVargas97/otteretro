using System.Text.Json;
using Dapper;
using Otteretro.Data;

namespace Otteretro.Claude;

/// <param name="Files">Archivos en Dudosas y Sin identificar (#181).</param>
public record ReviewPage(List<FileReview> Files);

/// <param name="Kind">doubtful (Claude lo asignó) o unidentified.</param>
/// <param name="Proposed">El nombre que dio Claude, si dio uno.</param>
/// <param name="GameId">El juego que tiene ahora (en Dudosas).</param>
public record FileReview(long Id, string Kind, long SourceId, string Path, string Console, string? Component, string? TitleId, string? Proposed,
    string? Reason, long? GameId, string? GameName);

/// <param name="GameId">null confirma lo que propuso Claude; un juego lo cambia (o lo elige, en Sin identificar).</param>
public record ResolveFileRequest(long? GameId);

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
        var files = c.Query<(long Id, string Kind, long SourceId, string Root, string Relative, string Console, string? Component, string? TitleId, string Data, long? GameId, string? GameName)>("""
            SELECT r.id, r.kind, o.id, coalesce(l.path, ''), o.relative_path, o.console, o.component, o.title_id, r.data, g.id, g.name_es
            FROM review_item r JOIN source o ON o.id = r.entity_id LEFT JOIN library l ON l.id = o.library_id
            LEFT JOIN version v ON v.id = o.version_id LEFT JOIN game g ON g.id = v.game_id
            WHERE r.entity = 'source' AND r.kind IN ('doubtful', 'unidentified') AND r.status = 'open' ORDER BY o.console, o.relative_path
            """).Select(r =>
        {
            using var doc = JsonDocument.Parse(r.Data);
            string? Text(string name) => doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            return new FileReview(r.Id, r.Kind, r.SourceId, Path.Combine(r.Root, r.Relative), r.Console, r.Component, r.TitleId, Text("name"), Text("reason"),
                r.GameId, r.GameName);
        }).ToList();
        return new ReviewPage(files);
    }

    /// <summary>Cuántos resultados esperan aprobación: el contador de Revisar en la barra lateral (D3).</summary>
    public static int PendingCount(Database db)
    {
        using var c = db.Open();
        // Lo de Claude por aprobar y los archivos en Dudosas y Sin identificar (#181).
        return c.ExecuteScalar<int>("SELECT count(*) FROM review_item WHERE status = 'open' AND entity = 'source'");
    }

    public static RoutesPage Routes(Database db, long sagaId)
    {
        using var c = db.Open();
        var chosen = c.ExecuteScalar<long?>("SELECT chosen_route FROM saga_user WHERE saga_id = @sagaId", new { sagaId });
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
        if (c.ExecuteScalar<long>("SELECT count(*) FROM saga WHERE id = @sagaId", new { sagaId }) == 0) return false;
        c.Execute("""
            INSERT INTO saga_user (saga_id, chosen_route, following) VALUES (@sagaId, @routeId, @routeId IS NOT NULL)
            ON CONFLICT (saga_id) DO UPDATE SET chosen_route = excluded.chosen_route,
                following = CASE WHEN excluded.chosen_route IS NULL THEN following ELSE 1 END
            """, new { routeId, sagaId });
        return true;
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
            SELECT s.id, s.name, r.id, r.kind FROM saga_user u JOIN saga s ON s.id = u.saga_id JOIN route r ON r.id = u.chosen_route WHERE u.following = 1 ORDER BY u.favorite DESC, s.name
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
