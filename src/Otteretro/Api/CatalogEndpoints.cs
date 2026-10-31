using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Otteretro.App;
using Otteretro.Catalog;
using Otteretro.Data;

namespace Otteretro.Api;

public record SagaFlagsRequest(bool? Favorite, bool? Following);
public record FieldRequest(string? Value);

/// <summary>Pantallas del catálogo (F1.9): sagas, colección, ficha, seguimiento, búsqueda y avisos de Inicio.</summary>
public static class CatalogEndpoints
{
    /// <summary>Claves sin las que una parte del catálogo no funciona: van como aviso en Inicio (§2, #44).</summary>
    static readonly (string Service, string[] Ids)[] NeededKeys =
    [
        ("igdb", ["igdb.clientId", "igdb.clientSecret"]),
        ("steamgriddb", ["steamgriddb.apiKey"]),
        ("steam", ["steam.apiKey"]),
    ];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/sagas", Results<Ok<List<SagaCard>>, Conflict<ApiError>> (AppSession s, string? filter, string? platform, string? company, string? sort) =>
            Db(s) is { } db ? TypedResults.Ok(CatalogQueries.Sagas(db, filter, platform, company, sort)) : NoData());

        api.MapGet("/sagas/{id:long}", Results<Ok<SagaDetail>, NotFound, Conflict<ApiError>> (long id, AppSession s) =>
            Db(s) is not { } db ? NoData() : CatalogQueries.Saga(db, id) is { } saga ? TypedResults.Ok(saga) : TypedResults.NotFound());

        api.MapPut("/sagas/{id:long}", Results<Ok<SagaDetail>, NotFound, Conflict<ApiError>> (long id, SagaFlagsRequest request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            return CatalogQueries.SetSagaFlags(db, id, request.Favorite, request.Following) && CatalogQueries.Saga(db, id) is { } saga
                ? TypedResults.Ok(saga) : TypedResults.NotFound();
        });

        api.MapGet("/consoles", Results<Ok<List<ConsoleCount>>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(CatalogQueries.Consoles(db)) : NoData());

        api.MapGet("/collection/{platform}", Results<Ok<CollectionPage>, NotFound, Conflict<ApiError>> (string platform, AppSession s, string? filter, string? sort) =>
        {
            if (Db(s) is not { } db) return NoData();
            return CatalogQueries.Consoles(db).Any(c => c.Id == platform) ? TypedResults.Ok(CatalogQueries.Collection(db, platform, filter, sort)) : TypedResults.NotFound();
        });

        api.MapGet("/games/{id:long}", Results<Ok<GameDetail>, NotFound, Conflict<ApiError>> (long id, AppSession s) =>
            Db(s) is not { } db ? NoData() : CatalogQueries.Game(db, id) is { } game ? TypedResults.Ok(game) : TypedResults.NotFound());

        // Seguimiento (#133): estado, fechas (AAAA-MM-DD), nota de 1 a 10, notas y favorito.
        api.MapPut("/games/{id:long}/progress", Results<Ok<GameDetail>, BadRequest<ApiError>, NotFound, Conflict<ApiError>> (long id, ProgressInfo request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            if (!CatalogQueries.Statuses.Contains(request.Status)) return TypedResults.BadRequest(ApiError.Of("progress.badStatus"));
            if (!IsDate(request.StartedAt) || !IsDate(request.FinishedAt)) return TypedResults.BadRequest(ApiError.Of("progress.badDate"));
            if (request.Rating is < 1 or > 10) return TypedResults.BadRequest(ApiError.Of("progress.badRating"));
            if (request.Notes?.Length > 10_000) return TypedResults.BadRequest(ApiError.Of("progress.notesTooLong"));
            return CatalogQueries.SaveProgress(db, id, request) ? TypedResults.Ok(CatalogQueries.Game(db, id)!) : TypedResults.NotFound();
        });

        // Corregir a mano un dato (§6.8): gana siempre y ninguna carga lo pisa. DELETE vuelve al de las fuentes.
        api.MapPut("/games/{id:long}/fields/{field}", Results<Ok<GameDetail>, BadRequest<ApiError>, NotFound, Conflict<ApiError>> (long id, string field, FieldRequest request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            if (!CatalogQueries.EditableFields.Contains(field)) return TypedResults.NotFound();
            var value = request.Value?.Trim() ?? "";
            if (value.Length == 0 && field == "name_es") return TypedResults.BadRequest(ApiError.Of("fields.empty"));
            if (field == "year" && !(int.TryParse(value, out var year) && year is >= 1970 and <= 2100)) return TypedResults.BadRequest(ApiError.Of("fields.badYear"));
            if (CatalogQueries.Game(db, id) is null) return TypedResults.NotFound();
            using (var c = db.Open()) FieldValues.Set(c, "game", id, field, value.Length == 0 ? null : value, FieldValues.Manual);
            return TypedResults.Ok(CatalogQueries.Game(db, id)!);
        });

        api.MapDelete("/games/{id:long}/fields/{field}", Results<Ok<GameDetail>, NotFound, Conflict<ApiError>> (long id, string field, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            if (!CatalogQueries.EditableFields.Contains(field) || CatalogQueries.Game(db, id) is null) return TypedResults.NotFound();
            using (var c = db.Open()) FieldValues.Clear(c, "game", id, field, FieldValues.Manual);
            return TypedResults.Ok(CatalogQueries.Game(db, id)!);
        });

        api.MapGet("/search", Results<Ok<SearchResults>, Conflict<ApiError>> (AppSession s, string? q) =>
        {
            if (Db(s) is not { } db) return NoData();
            var text = q?.Trim() ?? "";
            return TypedResults.Ok(text.Length < 2 ? new SearchResults([], []) : CatalogQueries.Search(db, text));
        });

        // Avisos de Inicio: por ahora, las claves que faltan (el código lo traduce el front: notices.*).
        api.MapGet("/notices", Results<Ok<List<ApiError>>, Conflict<ApiError>> (AppSession s, SecretBox box) =>
        {
            if (Db(s) is not { } db) return NoData();
            var saved = new Credentials(db, s.Config.MachineId, box).List().Where(c => c.Saved).Select(c => c.Id).ToHashSet();
            return TypedResults.Ok(NeededKeys.Where(k => !k.Ids.All(saved.Contains))
                .Select(k => ApiError.Of("notices.missingKey", ("service", k.Service))).ToList());
        });
    }

    internal static Database? Db(AppSession s) =>
        DataFolder.IsReady(s.Config.DataFolder) ? new Database(DataFolder.DatabasePath(s.Config.DataFolder!)) : null;

    internal static Conflict<ApiError> NoData() => TypedResults.Conflict(ApiError.Of("move.noDataFolder"));

    static bool IsDate(string? value) =>
        value is null || DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
