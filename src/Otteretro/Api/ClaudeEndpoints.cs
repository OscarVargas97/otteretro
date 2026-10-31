using Microsoft.AspNetCore.Http.HttpResults;
using Otteretro.App;
using Otteretro.Claude;
using static Otteretro.Api.CatalogEndpoints;

namespace Otteretro.Api;

public record ChooseRouteRequest(long? RouteId);

/// <summary>Lo que preparó Claude (F2.6): Revisar, rutas de la saga, comparativa de versiones, Inicio › Siguiente y Ajustes › Claude.</summary>
public static class ClaudeEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/review", Results<Ok<ReviewPage>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(ClaudeReview.Page(db)) : NoData());

        api.MapPost("/review/{id:long}/approve", Results<Ok<ReviewPage>, NotFound, Conflict<ApiError>> (long id, AppSession s) =>
            Db(s) is not { } db ? NoData() : ClaudeReview.Approve(db, id) ? TypedResults.Ok(ClaudeReview.Page(db)) : TypedResults.NotFound());

        api.MapPost("/review/{id:long}/reject", Results<Ok<ReviewPage>, NotFound, Conflict<ApiError>> (long id, AppSession s) =>
            Db(s) is not { } db ? NoData() : ClaudeReview.Reject(db, id) ? TypedResults.Ok(ClaudeReview.Page(db)) : TypedResults.NotFound());

        api.MapPost("/learnings/{id:long}/undo", Results<Ok<ReviewPage>, NotFound, Conflict<ApiError>> (long id, AppSession s) =>
            Db(s) is not { } db ? NoData() : CatalogCleanup.Undo(db, id) ? TypedResults.Ok(ClaudeReview.Page(db)) : TypedResults.NotFound());

        api.MapGet("/sagas/{id:long}/routes", Results<Ok<RoutesPage>, Conflict<ApiError>> (long id, AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(ClaudeReview.Routes(db, id)) : NoData());

        api.MapPut("/sagas/{id:long}/route", Results<Ok<RoutesPage>, NotFound, Conflict<ApiError>> (long id, ChooseRouteRequest request, AppSession s) =>
            Db(s) is not { } db ? NoData() : ClaudeReview.Choose(db, id, request.RouteId) ? TypedResults.Ok(ClaudeReview.Routes(db, id)) : TypedResults.NotFound());

        // Pedir a Claude que recalcule la saga: olvida sus huellas y la encola.
        api.MapPost("/sagas/{id:long}/recalculate", Results<Ok<long>, Conflict<ApiError>> (long id, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            foreach (var prompt in new[] { SagaAnalysis.Routes, SagaAnalysis.Iconic }) ClaudeFingerprint.Forget(db, prompt, $"saga:{id}");
            return TypedResults.Ok(SagaAnalysis.Enqueue(db, [id]));
        });

        api.MapGet("/games/{id:long}/compare", Results<Ok<List<VersionCompare>>, Conflict<ApiError>> (long id, AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(ClaudeReview.Compare(db, id)) : NoData());

        api.MapGet("/home/next", Results<Ok<List<NextGame>>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(ClaudeReview.Next(db)) : NoData());

        api.MapGet("/claude", Results<Ok<ClaudeStatus>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(ClaudeBudget.Status(db)) : NoData());

        api.MapPut("/claude", Results<Ok<ClaudeStatus>, BadRequest<ApiError>, Conflict<ApiError>> (ClaudeSettingsRequest request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            return ClaudeBudget.Save(db, request) is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok(ClaudeBudget.Status(db));
        });

        api.MapPost("/claude/run-now", Results<Ok<ClaudeStatus>, Conflict<ApiError>> (AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            ClaudeBudget.RunNow(db);
            return TypedResults.Ok(ClaudeBudget.Status(db));
        });
    }
}
