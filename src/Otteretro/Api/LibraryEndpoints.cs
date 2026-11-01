using Microsoft.AspNetCore.Http.HttpResults;
using Otteretro.App;
using Otteretro.Data;
using Otteretro.Libraries;

namespace Otteretro.Api;

public record LibraryRequest(string Path, string? Name = null);
public record RenameRequest(string Name);
public record ReconnectRequest(string? Path = null);
/// <param name="Relative">Carpeta dentro de la biblioteca (una consola o un juego).</param>
/// <param name="ToLibrary">Biblioteca de destino (sin ella, la misma).</param>
/// <param name="ToRelative">Ruta dentro del destino (sin ella, la misma).</param>
public record MoveFolderRequest(string Relative, string? ToLibrary = null, string? ToRelative = null);
/// <param name="Task">La migración de la estructura antigua que quedó en la cola, si hacía falta.</param>
public record AdoptResult(string Id, long? Task, List<LibraryInfo> Libraries);

/// <summary>Bibliotecas (§5, F3) y Herramientas instaladas.</summary>
public static class LibraryEndpoints
{
    static DateTime lastSearch;
    static readonly Lock searchGate = new();

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/libraries", Results<Ok<List<LibraryInfo>>, Conflict<ApiError>> (AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            var list = Library.List(db);
            // Una desconectada se busca sola (§5.3), como mucho cada 30 s y por detrás: la lista no espera a recorrer discos.
            if (list.Any(l => !l.Connected))
                lock (searchGate)
                    if (DateTime.UtcNow - lastSearch > TimeSpan.FromSeconds(30))
                    {
                        lastSearch = DateTime.UtcNow;
                        _ = Task.Run(() => { try { Library.Reconnect(db); } catch (Exception e) { FileLog.Write(LogLevel.Warning, "Library", e.Message); } });
                    }
            return TypedResults.Ok(list);
        });

        api.MapPost("/libraries", Results<Ok<List<LibraryInfo>>, BadRequest<ApiError>, Conflict<ApiError>> (LibraryRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            var (_, error) = Library.Create(db, request.Path, request.Name);
            return error is null ? TypedResults.Ok(Library.List(db)) : TypedResults.BadRequest(error);
        });

        // Adoptar (§5.3): una carpeta con marcador, una cualquiera o una con la estructura antigua, que se migra en una tarea.
        api.MapPost("/libraries/adopt", Results<Ok<AdoptResult>, BadRequest<ApiError>, Conflict<ApiError>> (LibraryRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            var (id, legacy, error) = Library.Adopt(db, request.Path, request.Name);
            if (error is not null) return TypedResults.BadRequest(error);
            long? task = legacy ? LibraryTasks.EnqueueAdopt(db, id!) : null;
            return TypedResults.Ok(new AdoptResult(id!, task, Library.List(db)));
        });

        // Las que quedaron con la estructura antigua (adoptadas antes, o la migración se cortó con un error) se migran desde acá.
        api.MapPost("/libraries/{id}/migrate", Results<Ok<long>, NotFound, Conflict<ApiError>> (string id, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            if (Library.Get(db, id) is not { } library) return TypedResults.NotFound();
            if (!library.Connected) return TypedResults.Conflict(ApiError.Of("library.notConnected"));
            return TypedResults.Ok(LibraryTasks.EnqueueAdopt(db, id));
        });

        api.MapPut("/libraries/{id}", Results<Ok<List<LibraryInfo>>, BadRequest<ApiError>, Conflict<ApiError>> (string id, RenameRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            return Library.Rename(db, id, request.Name) is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok(Library.List(db));
        });

        // Quitar saca del registro y nunca borra archivos (§5.3).
        api.MapDelete("/libraries/{id}", Results<Ok<List<LibraryInfo>>, NotFound, Conflict<ApiError>> (string id, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            return Library.Remove(db, id) ? TypedResults.Ok(Library.List(db)) : TypedResults.NotFound();
        });

        // Reconectar: en la ruta que indicas (su marcador tiene que ser el de esta biblioteca) o buscando en los discos.
        api.MapPost("/libraries/{id}/reconnect", Results<Ok<List<LibraryInfo>>, BadRequest<ApiError>, NotFound, Conflict<ApiError>> (string id, ReconnectRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            if (Library.Get(db, id) is null) return TypedResults.NotFound();
            if (request.Path is { } path)
            {
                if (Library.ReadMarker(path)?.Id != id) return TypedResults.BadRequest(ApiError.Of("library.otherMarker"));
                Library.Adopt(db, path);
            }
            else Library.Reconnect(db);
            return Library.Get(db, id)!.Connected ? TypedResults.Ok(Library.List(db)) : TypedResults.BadRequest(ApiError.Of("library.notFoundOnDrives"));
        });

        // Mover o migrar (§5.3): la biblioteca entera, o una consola o un juego (a otra biblioteca o a otra ruta), en una tarea.
        api.MapPost("/libraries/{id}/move", Results<Ok<long>, BadRequest<ApiError>, NotFound, Conflict<ApiError>> (string id, FolderRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            if (Library.Get(db, id) is not { } library) return TypedResults.NotFound();
            if (!library.Connected) return TypedResults.Conflict(ApiError.Of("library.notConnected"));
            var (task, error) = LibraryTasks.EnqueueMove(db, new(library.Path, request.Path, LibraryId: id));
            return error is null ? TypedResults.Ok(task!.Value) : TypedResults.BadRequest(error);
        });

        api.MapPost("/libraries/{id}/move-folder", Results<Ok<long>, BadRequest<ApiError>, NotFound, Conflict<ApiError>> (string id, MoveFolderRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            if (Library.Get(db, id) is not { } from || (request.ToLibrary is { } t ? Library.Get(db, t) : from) is not { } to) return TypedResults.NotFound();
            if (!from.Connected || !to.Connected) return TypedResults.Conflict(ApiError.Of("library.notConnected"));
            var relative = request.Relative.Trim('\\', '/');
            var toRelative = (request.ToRelative ?? relative).Trim('\\', '/');
            var source = Path.GetFullPath(Path.Combine(from.Path, relative));
            var target = Path.GetFullPath(Path.Combine(to.Path, toRelative));
            // Nada de salir de la biblioteca con «..».
            if (relative.Length == 0 || !Library.Inside(source, from.Path) || !Library.Inside(target, to.Path))
                return TypedResults.BadRequest(ApiError.Of("library.outside"));
            var (task, error) = LibraryTasks.EnqueueMove(db, new(source, target, FromLibrary: from.Id, FromRelative: relative, ToLibrary: to.Id, ToRelative: toRelative));
            return error is null ? TypedResults.Ok(task!.Value) : TypedResults.BadRequest(error);
        });

        // Paso 4 del asistente: carpetas con la estructura antigua en la raíz de los discos que todavía no son bibliotecas.
        api.MapGet("/libraries/detect", Results<Ok<List<string>>, Conflict<ApiError>> (AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            var known = Library.List(db).Select(l => l.Path).ToList();
            var found = DriveInfo.GetDrives().Where(d => d.IsReady && (!OperatingSystem.IsWindows() || d.DriveType == DriveType.Fixed))
                .SelectMany(d => { try { return Directory.EnumerateDirectories(d.Name); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; } })
                .Where(f => { try { return Library.IsLegacy(f); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; } })
                .Where(f => !known.Any(k => Library.Same(k, f)))
                .ToList();
            return TypedResults.Ok(found);
        });

        // Ajustes › Consolas (#190): en qué biblioteca están los juegos de cada consola.
        api.MapGet("/libraries/consoles", Results<Ok<List<ConsoleFolder>>, Conflict<ApiError>> (AppSession s) =>
            CatalogEndpoints.Db(s) is { } db ? TypedResults.Ok(Library.ConsoleFolders(db)) : CatalogEndpoints.NoData());

        api.MapGet("/tools", Results<Ok<List<ToolInfo>>, Conflict<ApiError>> (AppSession s) =>
            CatalogEndpoints.Db(s) is { } db ? TypedResults.Ok(Recipes.Installed(LibraryTasks.ToolsOf(db))) : CatalogEndpoints.NoData());
    }
}
