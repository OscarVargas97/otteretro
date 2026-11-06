using Microsoft.AspNetCore.Http.HttpResults;
using Otteretro.App;
using Otteretro.Catalog;
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

        api.MapGet("/review/count", Results<Ok<int>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(ClaudeReview.PendingCount(db)) : NoData());

        // Dudosas y Sin identificar (#181): confirmar lo de Claude o elegir el juego, que queda como alias.
        api.MapPost("/review/{id:long}/file", Results<Ok<ReviewPage>, BadRequest<ApiError>, Conflict<ApiError>> (long id, ResolveFileRequest request, AppSession s) =>
            Db(s) is not { } db ? NoData() : Identify.Resolve(db, id, request.GameId) is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok(ClaudeReview.Page(db)));

        // Reintentar un paquete sin juegos (#310, decisión 322): se vuelve a importar y se resuelve si ahora se reconoce.
        api.MapPost("/review/{id:long}/retry", Results<Ok<ReviewPage>, BadRequest<ApiError>, Conflict<ApiError>> (long id, AppSession s) =>
            Db(s) is not { } db ? NoData() : Libraries.Import.Retry(db, id) is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok(ClaudeReview.Page(db)));

        api.MapGet("/sagas/{id:long}/routes", Results<Ok<RoutesPage>, Conflict<ApiError>> (long id, AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(ClaudeReview.Routes(db, id)) : NoData());

        api.MapPut("/sagas/{id:long}/route", Results<Ok<RoutesPage>, NotFound, Conflict<ApiError>> (long id, ChooseRouteRequest request, AppSession s) =>
            Db(s) is not { } db ? NoData() : ClaudeReview.Choose(db, id, request.RouteId) ? TypedResults.Ok(ClaudeReview.Routes(db, id)) : TypedResults.NotFound());

        api.MapGet("/games/{id:long}/compare", Results<Ok<List<VersionCompare>>, Conflict<ApiError>> (long id, AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(ClaudeReview.Compare(db, id)) : NoData());

        api.MapGet("/home/next", Results<Ok<List<NextGame>>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(ClaudeReview.Next(db)) : NoData());

        // Almacenamiento › Reconocer juegos (#251).
        api.MapGet("/claude", Results<Ok<RecognizeStatus>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(ClaudeBudget.Status(db)) : NoData());

        api.MapPut("/claude", Results<Ok<RecognizeStatus>, Conflict<ApiError>> (RecognizeRequest request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            ClaudeBudget.Save(db, request);
            return TypedResults.Ok(ClaudeBudget.Status(db));
        });
    }
}
