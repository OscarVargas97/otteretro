using Dapper;
using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Otteretro.App;
using Otteretro.Catalog;
using Otteretro.Data;

namespace Otteretro.Api;

public record SagaFlagsRequest(bool? Favorite, bool? Following);
public record CatalogSource(string Repo, bool Official);
public record CatalogSourceRequest(string? Repo);
/// <summary>La versión aplicada y, si hay una esperando, qué cambia con ella (#222, #223).</summary>
public record CatalogVersion(string? Current, CatalogChanges? Pending);
public record FieldRequest(string? Value);

/// <summary>Una consola o un juego (#179).</summary>
public record VerifyRequest(string? Console, long? GameId);

/// <summary>El ejecutable de un juego de PC suelto, relativo a su carpeta (#182).</summary>
public record ExecutableRequest(string Path);
/// <param name="Skip">Lo que se desmarcó en la vista previa, relativo a la carpeta.</param>
public record DownloadRequest(string Name, string? DiskId, List<string> Links, long? GameId = null);
public record CaptureRequest(string Text, long? GameId = null);
public record QueueLinksRequest(List<string> Packages, string? DiskId);
public record ClipboardRequest(bool Enabled);
public record SiteLimitRequest(string Site, int Value);
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

        api.MapGet("/consoles/settings", Results<Ok<ConsolesPage>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(Consoles.Page(db)) : NoData());
        api.MapPut("/consoles/{id}/visible", Results<Ok<ConsolesPage>, NotFound, Conflict<ApiError>> (string id, ConsoleVisibleRequest request, AppSession s) =>
            Db(s) is not { } db ? NoData() : Consoles.SetVisible(db, id, request.Visible) ? TypedResults.Ok(Consoles.Page(db)) : TypedResults.NotFound());

        // De qué repo se actualiza el catálogo: el oficial o un fork (#217).
        api.MapGet("/catalog/source", Results<Ok<CatalogSource>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(SourceOf(db)) : NoData());
        api.MapPut("/catalog/source", async Task<Results<Ok<CatalogSource>, BadRequest<ApiError>, Conflict<ApiError>>> (CatalogSourceRequest request, AppSession s, CancellationToken ct) =>
        {
            if (Db(s) is not { } db) return NoData();
            return await CatalogUpdate.SetSource(db, request.Repo, ct) is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok(SourceOf(db));
        });

        // Ajustes › General › Catálogo (#222, #223): buscar a mano, ver qué cambia y aceptar o rechazar la versión entera (259).
        api.MapGet("/catalog/version", Results<Ok<CatalogVersion>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(VersionOf(db)) : NoData());
        api.MapPost("/catalog/version/check", Results<Ok<long>, Conflict<ApiError>> (AppSession s, string? tag) =>
            Db(s) is { } db ? TypedResults.Ok(CatalogUpdate.Enqueue(db, string.IsNullOrWhiteSpace(tag) ? null : tag)) : NoData());
        // #228: las versiones publicadas con los cambios de cada una, para elegir a cuál ir.
        api.MapGet("/catalog/versions", async Task<Results<Ok<CatalogUpdate.VersionsPage>, Conflict<ApiError>>> (AppSession s, CancellationToken ct) =>
        {
            if (Db(s) is not { } db) return NoData();
            try { return TypedResults.Ok(await CatalogUpdate.Versions(db, ct)); }
            catch (HttpRequestException) { return TypedResults.Conflict(ApiError.Of("catalog.sourceUnreachable", ("repo", CatalogUpdate.Source(db)))); }
        });
        api.MapPost("/catalog/version/accept", Results<Ok<CatalogVersion>, BadRequest<ApiError>, Conflict<ApiError>> (AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            try { return CatalogUpdate.Accept(db) is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok(VersionOf(db)); }
            catch (TaskException e) { return TypedResults.BadRequest(e.Error); }
        });
        api.MapPost("/catalog/version/reject", Results<Ok<CatalogVersion>, Conflict<ApiError>> (AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            CatalogUpdate.Reject(db);
            return TypedResults.Ok(VersionOf(db));
        });

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

        // Capturador (#203, #210): lo que pegas o copias queda guardado. Agregar no consulta los sitios; comprobar, solo cuando lo pides.
        api.MapGet("/downloads/links", Results<Ok<List<Libraries.LinkPackage>>, Conflict<ApiError>> (AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(Libraries.DownloadLinks.Captured(db)) : NoData());

        api.MapPost("/downloads/links", Results<Ok<int>, Conflict<ApiError>> (CaptureRequest request, AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(Libraries.DownloadLinks.Add(db, request.Text, request.GameId).Found) : NoData());

        api.MapPost("/downloads/links/check", async Task<Results<Ok<List<Libraries.LinkPackage>>, Conflict<ApiError>>> (AppSession s, SecretBox box, CancellationToken ct) =>
        {
            if (Db(s) is not { } db) return NoData();
            await Libraries.DownloadLinks.Check(db, new Credentials(db, s.Config.MachineId, box), ct);
            return TypedResults.Ok(Libraries.DownloadLinks.Captured(db));
        });

        api.MapPost("/downloads/links/queue", Results<Ok, BadRequest<ApiError>, Conflict<ApiError>> (QueueLinksRequest request, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            var error = Libraries.DownloadLinks.Queue(db, request.Packages, request.DiskId, s.Config.DownloadConnections ?? Libraries.Downloads.DefaultConnections);
            return error is null ? TypedResults.Ok() : TypedResults.BadRequest(error);
        });

        api.MapDelete("/downloads/links/{id:long}", Results<Ok, Conflict<ApiError>> (long id, AppSession s) =>
        {
            if (Db(s) is not { } db) return NoData();
            Libraries.DownloadLinks.Remove(db, id);
            return TypedResults.Ok();
        });

        api.MapGet("/games/{id:long}/links", Results<Ok<List<Libraries.CapturedLink>>, Conflict<ApiError>> (long id, AppSession s) =>
            Db(s) is { } db ? TypedResults.Ok(Libraries.DownloadLinks.OfGame(db, id)) : NoData());

        // Descargas a la vez por sitio (#202, decisión 248), con el tope de tu cuenta.
        api.MapGet("/downloads/sites", Results<Ok<List<Libraries.SiteLimit>>, Conflict<ApiError>> (AppSession s, SecretBox box) =>
            Db(s) is { } db ? TypedResults.Ok(Libraries.Downloads.Limits(s.Config, new Credentials(db, s.Config.MachineId, box))) : NoData());
        api.MapPut("/downloads/sites", Results<Ok<List<Libraries.SiteLimit>>, BadRequest<ApiError>, Conflict<ApiError>> (SiteLimitRequest request, AppSession s, SecretBox box) =>
        {
            if (Db(s) is not { } db) return NoData();
            var credentials = new Credentials(db, s.Config.MachineId, box);
            if (Libraries.Downloads.Limits(s.Config, credentials).FirstOrDefault(l => l.Site == request.Site) is not { } site || request.Value < 1 || request.Value > site.Max)
                return TypedResults.BadRequest(ApiError.Of("download.badParallel"));
            (s.Config.DownloadsPerSite ??= [])[request.Site] = request.Value;
            s.Config.Save(s.ConfigPath);
            return TypedResults.Ok(Libraries.Downloads.Limits(s.Config, credentials));
        });

        api.MapGet("/downloads/clipboard", (AppSession s) => s.Config.ClipboardCapture ?? true);
        api.MapPut("/downloads/clipboard", (ClipboardRequest request, AppSession s) =>
        {
            s.Config.ClipboardCapture = request.Enabled;
            s.Config.Save(s.ConfigPath);
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

    static CatalogSource SourceOf(Database db)
    {
        var repo = CatalogUpdate.Source(db);
        return new(repo, string.Equals(repo, CatalogUpdate.Official, StringComparison.OrdinalIgnoreCase));
    }

    static CatalogVersion VersionOf(Database db) => new(CatalogUpdate.Version(db), CatalogDiff.Of(db));

    internal static Database? Db(AppSession s) =>
        DataFolder.IsReady(s.Config.DataFolder) ? DataFolder.Open(s.Config.DataFolder!) : null;

    internal static Conflict<ApiError> NoData() => TypedResults.Conflict(ApiError.Of("move.noDataFolder"));

    static bool IsDate(string? value) =>
        value is null || DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
