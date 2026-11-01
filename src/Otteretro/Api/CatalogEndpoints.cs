using Dapper;
using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Otteretro.App;
using Otteretro.Catalog;
using Otteretro.Data;

namespace Otteretro.Api;

public record SagaFlagsRequest(bool? Favorite, bool? Following);
public record FieldRequest(string? Value);

/// <summary>Una consola o un juego (#179).</summary>
public record VerifyRequest(string? Console, long? GameId);

/// <summary>El ejecutable de un juego de PC suelto, relativo a su carpeta (#182).</summary>
public record ExecutableRequest(string Path);
/// <param name="Skip">Lo que se desmarcó en la vista previa, relativo a la carpeta.</param>
public record DownloadRequest(string Name, string? DiskId, List<string> Links, long? GameId = null);
public record DownloadPreviewRequest(string Text);
public record ConnectionsRequest(int Connections);
public record ImportRequest(string Path, List<string>? Skip = null);
public record PreviewRequest(string Path);
public record RegionsRequest(List<string> Order);
public record ConvertRequest(string Console);
/// <param name="Ids">Las filas de <c>source</c> que se quitan del disco (#224).</param>
public record TrashRequest(List<long> Ids);

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
        api.MapGet("/sagas", Results<Ok<SagaPage>, Conflict<ApiError>> (AppSession s, string? filter, string? platform, string? company, string? sort,
            int offset = 0, int limit = CatalogQueries.PageSize) =>
            Db(s) is { } db ? TypedResults.Ok(CatalogQueries.SagasPage(db, filter, platform, company, sort, Math.Max(0, offset), Math.Clamp(limit, 1, 500))) : NoData());

        api.MapGet("/sagas/{id:long}", Results<Ok<SagaDetail>, NotFound, Conflict<ApiError>> (long id, AppSession s) =>
            Db(s) is not { } db ? NoData() : CatalogQueries.Saga(db, id) is { } saga ? TypedResults.Ok(saga) : TypedResults.NotFound());

        api.MapGet("/sagas/{id:long}/games", Results<Ok<GamePage>, Conflict<ApiError>> (long id, AppSession s, int offset = 0, int limit = CatalogQueries.PageSize) =>
            Db(s) is { } db ? TypedResults.Ok(CatalogQueries.SagaGames(db, id, Math.Max(0, offset), Math.Clamp(limit, 1, 500))) : NoData());

        api.MapPut("/sagas/{id:long}", Results<Ok<SagaDetail>, NotFound, Conflict<ApiError>> (long id, SagaFlagsRequest request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            return CatalogQueries.SetSagaFlags(db, id, request.Favorite, request.Following) && CatalogQueries.Saga(db, id) is { } saga
                ? TypedResults.Ok(saga) : TypedResults.NotFound();
        });

        api.MapGet("/consoles", Results<Ok<List<ConsoleCount>>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(CatalogQueries.Consoles(db)) : NoData());

        // Ajustes › Consolas (F2.13, F2.14): habilitar consolas desde IGDB y el catálogo al día.
        api.MapGet("/consoles/settings", Results<Ok<ConsolesPage>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(Consoles.Page(db)) : NoData());
        api.MapGet("/consoles/igdb", async Task<Results<Ok<List<PlatformInfo>>, Conflict<ApiError>>> (AppSession s, SecretBox box, CancellationToken ct) =>
        {
            if (Db(s) is not { } db) return NoData();
            try { return TypedResults.Ok(await Consoles.Available(db, IgdbFor(db, s, box), ct)); }
            catch (TaskException e) { return TypedResults.Conflict(e.Error); }
        });
        api.MapPost("/consoles", async Task<Results<Ok<ConsolesPage>, NotFound, Conflict<ApiError>>> (EnableConsoleRequest request, AppSession s, SecretBox box, CancellationToken ct) =>
        {
            if (Db(s) is not { } db) return NoData();
            try { return await Consoles.Enable(db, IgdbFor(db, s, box), request.IgdbId, ct) is null ? TypedResults.NotFound() : TypedResults.Ok(Consoles.Page(db)); }
            catch (TaskException e) { return TypedResults.Conflict(e.Error); }
        });
        api.MapPut("/consoles/{id}", Results<Ok<ConsolesPage>, NotFound, Conflict<ApiError>> (string id, ConsoleUpdateRequest request, AppSession s) =>
            Db(s) is not { } db ? NoData() : Consoles.SetEnabled(db, id, request.Enabled) ? TypedResults.Ok(Consoles.Page(db)) : TypedResults.NotFound());
        api.MapPut("/catalog/refresh", Results<Ok<ConsolesPage>, BadRequest<ApiError>, Conflict<ApiError>> (RefreshSettingsRequest request, AppSession s) =>
            Db(s) is not { } db ? NoData() : Consoles.SaveRefreshDays(db, request.Days) is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok(Consoles.Page(db)));
        api.MapPost("/catalog/refresh", Results<Ok<long>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(IgdbImport.EnqueueRefresh(db)) : NoData());

        // Portadas a pedido (#132).
        api.MapGet("/covers", Results<Ok<SteamGridDb.CoversInfo>, Conflict<ApiError>> (AppSession s, SecretBox box) =>
            Db(s) is { } db ? TypedResults.Ok(SteamGridDb.Status(db, HasSgdbKey(db, s, box))) : NoData());
        api.MapPost("/covers", Results<Ok<SteamGridDb.CoversInfo>, Conflict<ApiError>> (AppSession s, SecretBox box) =>
            Db(s) is { } db ? TypedResults.Ok(SteamGridDb.Request(db, HasSgdbKey(db, s, box))) : NoData());

        api.MapGet("/collection/{platform}", Results<Ok<CollectionPage>, NotFound, Conflict<ApiError>> (string platform, AppSession s, string? filter, string? sort, int offset = 0, int limit = CatalogQueries.PageSize) =>
        {
            if (Db(s) is not { } db) return NoData();
            using (var c = db.Open())
                if (c.ExecuteScalar<long>("SELECT count(*) FROM console WHERE id = @platform", new { platform }) == 0) return TypedResults.NotFound();
            return TypedResults.Ok(CatalogQueries.Collection(db, platform, filter, sort, Math.Max(0, offset), Math.Clamp(limit, 1, 500)));
        });

        // «Escanear» en Colección (#178): la tarea que reconoce los archivos de cada disco.
        api.MapPost("/collection/scan", Results<Ok<long>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(Libraries.Scan.Enqueue(db)) : NoData());

        // «Importar carpeta» (#193): cada juego de esa carpeta a su lugar, movido con copia y hash.
        api.MapPost("/collection/import", Results<Ok<long>, BadRequest<ApiError>, Conflict<ApiError>> (ImportRequest request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            var (task, error) = Libraries.Import.Enqueue(db, request.Path, request.Skip);
            return error is null ? TypedResults.Ok(task!.Value) : TypedResults.BadRequest(error);
        });

        // La vista previa de esa carpeta (#197): qué hay, una versión por juego. No mueve nada.
        api.MapPost("/collection/import/preview", Results<Ok<Libraries.MegapackPreview>, BadRequest<ApiError>, Conflict<ApiError>> (PreviewRequest request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            if (Libraries.Import.CheckFolder(db, request.Path) is { } error) return TypedResults.BadRequest(error);
            return TypedResults.Ok(Libraries.Megapack.Preview(db, request.Path, s.Config.RegionOrder ?? [.. Libraries.Megapack.DefaultRegions]));
        });

        api.MapPut("/collection/regions", Results<Ok, BadRequest<ApiError>> (RegionsRequest request, AppSession s) =>
        {
            if (request.Order.Count != Libraries.Megapack.DefaultRegions.Length || request.Order.Except(Libraries.Megapack.DefaultRegions).Any())
                return TypedResults.BadRequest(ApiError.Of("import.badRegions"));
            s.Config.RegionOrder = request.Order;
            s.Config.Save(s.ConfigPath);
            return TypedResults.Ok();
        });

        // Descargas (#202): la cola y agregar un paquete. Pausar, reanudar y cancelar son los de cualquier tarea.
        api.MapGet("/downloads", Results<Ok<List<Libraries.DownloadInfo>>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(Libraries.Downloads.List(db)) : NoData());

        api.MapPost("/downloads", Results<Ok<long>, BadRequest<ApiError>, Conflict<ApiError>> (DownloadRequest request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            var (task, error) = Libraries.Downloads.Enqueue(db, request.Name, request.DiskId, request.Links, request.GameId, s.Config.DownloadConnections ?? Libraries.Downloads.DefaultConnections);
            return error is null ? TypedResults.Ok(task!.Value) : TypedResults.BadRequest(error);
        });

        // Conexiones por archivo (#226): se ajustan, y cada sitio tiene su tope.
        api.MapGet("/downloads/connections", (AppSession s) => s.Config.DownloadConnections ?? Libraries.Downloads.DefaultConnections);
        api.MapPut("/downloads/connections", Results<Ok, BadRequest<ApiError>> (ConnectionsRequest request, AppSession s) =>
        {
            if (request.Connections is < 1 or > 8) return TypedResults.BadRequest(ApiError.Of("download.badConnections"));
            s.Config.DownloadConnections = request.Connections;
            s.Config.Save(s.ConfigPath);
            return TypedResults.Ok();
        });

        // Capturador (#203): los paquetes de un texto pegado, comprobando cada enlace. No baja nada.
        api.MapPost("/downloads/preview", async Task<Results<Ok<List<Libraries.PackagePreview>>, Conflict<ApiError>>> (DownloadPreviewRequest request, AppSession s, SecretBox box, CancellationToken ct) =>
        {
            if (Db(s) is not { } db) return NoData();
            return TypedResults.Ok(await Libraries.Downloads.Preview(request.Text, new Credentials(db, s.Config.MachineId, box), ct));
        });

        // «Convertir» (#233): lo que ya tienes de una consola al formato liviano.
        api.MapPost("/collection/convert", Results<Ok<long>, BadRequest<ApiError>, Conflict<ApiError>> (ConvertRequest request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            var (task, error) = Libraries.Converter.Enqueue(db, request.Console);
            return error is null ? TypedResults.Ok(task!.Value) : TypedResults.BadRequest(error);
        });

        // «Verificar» (#179): el SHA-1 contra No-Intro o Redump, de una consola o de un juego.
        api.MapPost("/collection/verify", Results<Ok<long>, BadRequest<ApiError>, Conflict<ApiError>> (VerifyRequest request, AppSession s) =>
            Db(s) is not { } db ? NoData()
            : request.Console is null == request.GameId is null ? TypedResults.BadRequest(ApiError.Of("dat.verifyWhat"))
            : TypedResults.Ok(Libraries.Dats.EnqueueVerify(db, request.Console, request.GameId)));

        // Cambiar el ejecutable de un juego de PC suelto (#182): uno de los .exe de su carpeta.
        // Quitar un juego del disco (#224): lo marcado va a la papelera y deja de contar como «lo tengo».
        api.MapPost("/sources/trash", Results<Ok, BadRequest<ApiError>, Conflict<ApiError>> (TrashRequest request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            return Libraries.Disk.TrashSources(db, request.Ids.Distinct().ToList()) is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok();
        });

        api.MapPut("/sources/{id:long}/executable", Results<Ok, BadRequest<ApiError>, NotFound, Conflict<ApiError>> (long id, ExecutableRequest request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            using var c = db.Open();
            var folder = c.QuerySingleOrDefault<(string Root, string Relative)>(
                "SELECT l.path, o.relative_path FROM source o JOIN library l ON l.id = o.library_id WHERE o.id = @id AND o.executable IS NOT NULL", new { id });
            if (folder.Root is null) return TypedResults.NotFound();
            if (!Libraries.Scan.Executables(Path.Combine(folder.Root, folder.Relative)).Contains(request.Path)) return TypedResults.BadRequest(ApiError.Of("source.badExecutable"));
            c.Execute("UPDATE source SET executable = @Path WHERE id = @id", new { id, request.Path });
            return TypedResults.Ok();
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
            var notices = NeededKeys.Where(k => !k.Ids.All(saved.Contains)).Select(k => ApiError.Of("notices.missingKey", ("service", k.Service))).ToList();
            // Archivos que ni el escaneo ni Claude reconocieron (#180): se eligen en Revisar › Sin identificar.
            using (var c = db.Open())
                if (c.ExecuteScalar<long>("SELECT count(*) FROM review_item WHERE kind = 'unidentified' AND status = 'open'") is > 0 and var n)
                    notices.Add(ApiError.Of("notices.unidentified", ("count", n.ToString())));
            return TypedResults.Ok(notices);
        });
    }

    static Igdb IgdbFor(Database db, AppSession s, SecretBox box) =>
        new(new ApiClient(db, IgdbImport.Handler), new Credentials(db, s.Config.MachineId, box), IgdbImport.Handler);

    static bool HasSgdbKey(Database db, AppSession s, SecretBox box) =>
        new Credentials(db, s.Config.MachineId, box).List().Any(c => c.Id == "steamgriddb.apiKey" && c.Saved);

    internal static Database? Db(AppSession s) =>
        DataFolder.IsReady(s.Config.DataFolder) ? DataFolder.Open(s.Config.DataFolder!) : null;

    internal static Conflict<ApiError> NoData() => TypedResults.Conflict(ApiError.Of("move.noDataFolder"));

    static bool IsDate(string? value) =>
        value is null || DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
