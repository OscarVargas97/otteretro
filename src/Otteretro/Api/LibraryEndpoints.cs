using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Otteretro.App;
using Otteretro.Data;
using Otteretro.Libraries;

namespace Otteretro.Api;

/// <param name="Type">game, emulator, port, complement o system (#198).</param>
public record LibraryRequest(string Path, string? Name = null, string Type = "game");
public record RenameRequest(string Name);
public record ReconnectRequest(string? Path = null);
/// <param name="Relative">Carpeta dentro de la biblioteca (una consola o un juego).</param>
/// <param name="ToLibrary">Biblioteca de destino (sin ella, la misma).</param>
/// <param name="ToRelative">Ruta dentro del destino (sin ella, la misma).</param>
public record MoveFolderRequest(string Relative, string? ToLibrary = null, string? ToRelative = null);
/// <param name="Path">La instalación (una carpeta de versión de lo instalado).</param>
/// <param name="ToLocation">Ubicación de su tipo; sin ella, de vuelta a `Herramientas` de la carpeta de datos.</param>
public record MoveToolRequest(string Path, string? ToLocation = null);
public record DestinationRequest(string? LocationId);
/// <param name="Path">La carpeta Otteretro del disco (la `folder` de /drives).</param>
/// <param name="Name">Lo que ve la persona: la unidad (E:).</param>
public record DiskRequest(string Path, string? Name = null);
/// <param name="Items">Rutas dentro del disco, como las da /contents.</param>
public record MoveItemsRequest(List<string> Items, string ToLocation);
/// <summary>Una cosa de un disco (#160): un juego (carpeta o archivo), una instalación o la carpeta de sistema de una consola.</summary>
/// <param name="Group">La consola, o el emulador o port de un complemento.</param>
public record DiskItem(string Type, string Relative, string Name, string Group, long Size);
/// <summary>Todo lo de una consola junto (#192): juegos, sistema, emuladores, ports con su juego y complementos.</summary>
/// <param name="Destination">La ubicación de juegos de lo nuevo (#195).</param>
public record ConsoleDetail(string Id, string Name, string? Destination, List<ConsoleFolder> Games, List<ConsoleFolder> System,
    List<ToolInfo> Emulators, List<ToolInfo> Ports, List<ToolInfo> Complements);
/// <param name="Task">La migración de la estructura antigua que quedó en la cola, si hacía falta.</param>
public record AdoptResult(string Id, long? Task, List<LibraryInfo> Libraries);

/// <summary>Ubicaciones por tipo (§5, F3, #198), Herramientas instaladas y el detalle de cada consola (#192).</summary>
public static class LibraryEndpoints
{
    static DateTime lastSearch;
    static readonly Lock searchGate = new();

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/locations", Results<Ok<List<LibraryInfo>>, Conflict<ApiError>> (AppSession s) =>
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

        api.MapPost("/locations", Results<Ok<List<LibraryInfo>>, BadRequest<ApiError>, Conflict<ApiError>> (LibraryRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            var (_, error) = Library.Create(db, request.Path, request.Name, request.Type);
            return error is null ? TypedResults.Ok(Library.List(db)) : TypedResults.BadRequest(error);
        });

        // Adoptar (§5.3): una carpeta con marcador, una cualquiera o una con la estructura antigua, que se migra en una tarea.
        api.MapPost("/locations/adopt", Results<Ok<AdoptResult>, BadRequest<ApiError>, Conflict<ApiError>> (LibraryRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            var (id, legacy, error) = Library.Adopt(db, request.Path, request.Name, request.Type);
            if (error is not null) return TypedResults.BadRequest(error);
            // Una carpeta de antes (como E:\Consolas) pasa enseguida a la carpeta fija de su disco (#202).
            long? task = request.Type != "disk" ? LibraryTasks.EnqueueMigrate(db) : null;
            return TypedResults.Ok(new AdoptResult(id!, task, Library.List(db)));
        });

        // Agregar un disco (#158): su carpeta Otteretro con las cinco fijas. Si ya tenía marcador, vuelve con el mismo id.
        api.MapPost("/disks", Results<Ok<List<LibraryInfo>>, BadRequest<ApiError>, Conflict<ApiError>> (DiskRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            if (!Path.IsPathFullyQualified(request.Path)) return TypedResults.BadRequest(ApiError.Of("path.notAbsolute"));
            try { Directory.CreateDirectory(request.Path); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return TypedResults.BadRequest(ApiError.Of("folder.createFailed", ("detail", e.Message)));
            }
            var (_, _, error) = Library.Adopt(db, request.Path, request.Name, "disk");
            return error is null ? TypedResults.Ok(Library.List(db)) : TypedResults.BadRequest(error);
        });

        api.MapPut("/disks/{id}/default", Results<Ok<List<LibraryInfo>>, BadRequest<ApiError>, Conflict<ApiError>> (string id, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            return Library.SetDefault(db, id) is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok(Library.List(db));
        });

        // Lo que falta pasar al estándar (#159) y la tarea que lo hace.
        api.MapGet("/disks/migration", Results<Ok<LibraryTasks.MigrationPlan>, Conflict<ApiError>> (AppSession s) =>
            CatalogEndpoints.Db(s) is { } db ? TypedResults.Ok(LibraryTasks.Pending(db)) : CatalogEndpoints.NoData());

        api.MapPost("/disks/migrate", Results<Ok<long>, Conflict<ApiError>> (AppSession s) =>
            CatalogEndpoints.Db(s) is { } db ? TypedResults.Ok(LibraryTasks.EnqueueMigrate(db)) : CatalogEndpoints.NoData());

        // Lo que tiene un disco, con su tamaño (#160).
        api.MapGet("/disks/{id}/contents", Results<Ok<List<DiskItem>>, NotFound, Conflict<ApiError>> (string id, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            if (Library.Get(db, id) is not { Type: "disk" } disk) return TypedResults.NotFound();
            if (!disk.Connected) return TypedResults.Conflict(ApiError.Of("library.notConnected"));
            return TypedResults.Ok(Contents(db, disk));
        });

        // Mover lo marcado a otro disco (#160, #205): una tarea por cosa, a la misma ruta dentro del otro.
        api.MapPost("/disks/{id}/move", Results<Ok<List<long>>, BadRequest<ApiError>, NotFound, Conflict<ApiError>> (string id, MoveItemsRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            if (Library.Get(db, id) is not { Type: "disk" } from || Library.Get(db, request.ToLocation) is not { Type: "disk" } to) return TypedResults.NotFound();
            if (!from.Connected || !to.Connected) return TypedResults.Conflict(ApiError.Of("library.notConnected"));
            var (tasks, error) = LibraryTasks.EnqueueMoveItems(db, from, to, request.Items);
            return error is null ? TypedResults.Ok(tasks!) : TypedResults.BadRequest(error);
        });

        // Las que quedaron con la estructura antigua (adoptadas antes, o la migración se cortó con un error) se pasan al estándar.
        api.MapPost("/locations/{id}/migrate", Results<Ok<long>, NotFound, Conflict<ApiError>> (string id, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            if (Library.Get(db, id) is not { } library) return TypedResults.NotFound();
            if (!library.Connected) return TypedResults.Conflict(ApiError.Of("library.notConnected"));
            return TypedResults.Ok(LibraryTasks.EnqueueMigrate(db));
        });

        api.MapPut("/locations/{id}", Results<Ok<List<LibraryInfo>>, BadRequest<ApiError>, Conflict<ApiError>> (string id, RenameRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            return Library.Rename(db, id, request.Name) is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok(Library.List(db));
        });

        // Quitar saca del registro y nunca borra archivos (§5.3).
        api.MapDelete("/locations/{id}", Results<Ok<List<LibraryInfo>>, NotFound, Conflict<ApiError>> (string id, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            return Library.Remove(db, id) ? TypedResults.Ok(Library.List(db)) : TypedResults.NotFound();
        });

        // Reconectar: en la ruta que indicas (su marcador tiene que ser el de esta biblioteca) o buscando en los discos.
        api.MapPost("/locations/{id}/reconnect", Results<Ok<List<LibraryInfo>>, BadRequest<ApiError>, NotFound, Conflict<ApiError>> (string id, ReconnectRequest request, AppSession s) =>
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
        api.MapPost("/locations/{id}/move", Results<Ok<long>, BadRequest<ApiError>, NotFound, Conflict<ApiError>> (string id, FolderRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            if (Library.Get(db, id) is not { } library) return TypedResults.NotFound();
            if (!library.Connected) return TypedResults.Conflict(ApiError.Of("library.notConnected"));
            var (task, error) = LibraryTasks.EnqueueMove(db, new(library.Path, request.Path, LibraryId: id));
            return error is null ? TypedResults.Ok(task!.Value) : TypedResults.BadRequest(error);
        });

        api.MapPost("/locations/{id}/move-folder", Results<Ok<long>, BadRequest<ApiError>, NotFound, Conflict<ApiError>> (string id, MoveFolderRequest request, AppSession s) =>
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
        api.MapGet("/locations/detect", Results<Ok<List<string>>, Conflict<ApiError>> (AppSession s) =>
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

        // Ajustes › Consolas (#192): todo lo de una consola junto.
        api.MapGet("/consoles/{id}/detail", Results<Ok<ConsoleDetail>, NotFound, Conflict<ApiError>> (string id, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            return Detail(db, id) is { } detail ? TypedResults.Ok(detail) : TypedResults.NotFound();
        });

        // A qué ubicación de juegos van los juegos nuevos de la consola (#195); null la quita.
        api.MapPut("/consoles/{id}/destination", Results<Ok<ConsoleDetail>, BadRequest<ApiError>, NotFound, Conflict<ApiError>> (string id, DestinationRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            if (request.LocationId is { } l && Library.Get(db, l)?.Type is not ("game" or "disk")) return TypedResults.BadRequest(ApiError.Of("library.notGames"));
            if (Detail(db, id) is null) return TypedResults.NotFound();
            using (var c = db.Open())
                c.Execute(request.LocationId is null ? "DELETE FROM console_destination WHERE console_id = @id"
                    : "INSERT INTO console_destination (console_id, library_id) VALUES (@id, @LocationId) ON CONFLICT (console_id) DO UPDATE SET library_id = excluded.library_id",
                    new { id, request.LocationId });
            return TypedResults.Ok(Detail(db, id)!);
        });

        api.MapGet("/tools", Results<Ok<List<ToolInfo>>, Conflict<ApiError>> (AppSession s) =>
            CatalogEndpoints.Db(s) is { } db ? TypedResults.Ok(Recipes.Installed(Library.ToolRoots(db))) : CatalogEndpoints.NoData());

        // Mover un emulador, port o complemento a otra ubicación de su tipo, o de vuelta a Herramientas (#198), en una tarea.
        api.MapPost("/tools/move", Results<Ok<long>, BadRequest<ApiError>, NotFound, Conflict<ApiError>> (MoveToolRequest request, AppSession s) =>
        {
            if (CatalogEndpoints.Db(s) is not { } db) return CatalogEndpoints.NoData();
            if (Recipes.Installed(Library.ToolRoots(db)).FirstOrDefault(t => Library.Same(t.Path, request.Path)) is not { } tool) return TypedResults.NotFound();
            string root;
            if (request.ToLocation is { } to)
            {
                if (Library.Get(db, to) is not { } location) return TypedResults.NotFound();
                if (Library.FolderOf(location, tool.Kind) is not { } folder) return TypedResults.BadRequest(ApiError.Of("library.otherType"));
                if (!location.Connected) return TypedResults.Conflict(ApiError.Of("library.notConnected"));
                root = folder;
            }
            else root = Recipes.DefaultRoots(LibraryTasks.ToolsOf(db)).First(r => r.Kind == tool.Kind).Path;
            var (task, error) = LibraryTasks.EnqueueMove(db, new(tool.Path, Path.Combine(root, tool.Folder, tool.Name, tool.Version)));
            return error is null ? TypedResults.Ok(task!.Value) : TypedResults.BadRequest(error);
        });
    }

    public static List<DiskItem> Contents(Database db, LibraryInfo disk)
    {
        var items = new List<DiskItem>();
        // Sin seguir enlaces: un complemento enlazado dentro de un emulador no se cuenta dos veces.
        static long Size(string path) => File.Exists(path) ? new FileInfo(path).Length
            : Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }).Sum(f => new FileInfo(f).Length);
        var games = Path.Combine(disk.Path, Library.DiskFolders["game"]);
        if (Directory.Exists(games))
            foreach (var console in Directory.EnumerateDirectories(games))
                foreach (var entry in Directory.EnumerateFileSystemEntries(console))
                    items.Add(new("game", Path.GetRelativePath(disk.Path, entry), Path.GetFileName(entry), Path.GetFileName(console), Size(entry)));
        foreach (var tool in Recipes.Installed(Recipes.DefaultRoots(disk.Path).Select(r => r with { LocationId = disk.Id })))
            items.Add(new(tool.Kind, Path.GetRelativePath(disk.Path, tool.Path), $"{tool.Name} {tool.Version}", tool.Folder, Size(tool.Path)));
        var system = Path.Combine(disk.Path, Library.DiskFolders["system"]);
        if (Directory.Exists(system))
            foreach (var console in Directory.EnumerateDirectories(system))
                items.Add(new("system", Path.GetRelativePath(disk.Path, console), Path.GetFileName(console), Path.GetFileName(console), Size(console)));
        return items;
    }

    public static ConsoleDetail? Detail(Database db, string id)
    {
        using var c = db.Open();
        if (c.QuerySingleOrDefault<(string Id, string Name, string? Destination)>("SELECT k.id, k.name, d.library_id FROM console k LEFT JOIN console_destination d ON d.console_id = k.id WHERE k.id = @id", new { id }) is not { Id: not null } console)
            return null;
        bool Mine(string folder) => string.Equals(folder, console.Name, StringComparison.OrdinalIgnoreCase) || string.Equals(folder, console.Id, StringComparison.OrdinalIgnoreCase);
        var tools = Recipes.Installed(Library.ToolRoots(db));
        var emulators = tools.Where(t => t.Kind == "emulator" && Mine(t.Folder)).ToList();
        var ports = tools.Where(t => t.Kind == "port" && Mine(t.Folder)).ToList();
        var parents = emulators.Concat(ports).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new(console.Id, console.Name, console.Destination,
            Library.ConsoleFolders(db).Where(f => f.Console == id).ToList(), Library.ConsoleFolders(db, "system").Where(f => f.Console == id).ToList(),
            emulators, ports, tools.Where(t => t.Kind == "complement" && parents.Contains(t.Folder)).ToList());
    }
}
