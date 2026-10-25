using Microsoft.AspNetCore.Http.HttpResults;
using Otteretro.App;

namespace Otteretro.Api;

public record AppState(string Version, string? DataFolder, bool Configured, string Language);
public record FolderRequest(string Path);
public record LanguageRequest(string Language);

/// <summary>
/// Error o aviso para la persona, como código y parámetros: el front lo traduce con su catálogo (#142).
/// Los códigos son las claves de `errors.*` en web/messages.
/// </summary>
public record ApiError(string Code, Dictionary<string, string>? Params = null)
{
    public static ApiError Of(string code, params (string Key, string Value)[] values) =>
        new(code, values.Length == 0 ? null : values.ToDictionary(v => v.Key, v => v.Value));
}
public record Drive(string Name, long Free, long Total);
public record FolderInfo(string? Path, long Used, long FreeOnDrive);

/// <summary>Endpoints base de F0. Cada módulo añadirá los suyos.</summary>
public static class Endpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/state", (AppSession s) => State(s));

        api.MapPost("/data-folder", Results<Ok<AppState>, BadRequest<ApiError>, Conflict<ApiError>> (FolderRequest request, AppSession s, FolderMoveService m) =>
        {
            if (m.State.Running) return TypedResults.Conflict(ApiError.Of("move.inProgress"));
            var error = DataFolder.Prepare(request.Path);
            if (error is not null) return TypedResults.BadRequest(error);
            s.Config.DataFolder = request.Path;
            s.Config.Save(s.ConfigPath);
            Startup.PrepareData(s.Config);
            return TypedResults.Ok(State(s));
        });

        api.MapGet("/data-folder", (AppSession s) =>
        {
            var folder = s.Config.DataFolder;
            var root = folder is null ? null : Mounts.Of(folder);
            return new FolderInfo(folder, folder is null ? 0 : FolderMove.Size(folder),
                root is not null && Directory.Exists(root) ? new DriveInfo(root).AvailableFreeSpace : 0);
        });

        api.MapGet("/data-folder/move", (FolderMoveService m) => m.State);

        api.MapPost("/data-folder/move", Results<Accepted<MoveState>, BadRequest<ApiError>> (FolderRequest request, FolderMoveService m) =>
        {
            var error = m.Start(request.Path);
            return error is null ? TypedResults.Accepted("/api/data-folder/move", m.State) : TypedResults.BadRequest(error);
        });

        api.MapPut("/language", Results<Ok<AppState>, BadRequest<ApiError>> (LanguageRequest request, AppSession s) =>
        {
            if (!AppConfig.Languages.Contains(request.Language)) return TypedResults.BadRequest(ApiError.Of("language.unsupported", ("language", request.Language)));
            s.Config.Language = request.Language;
            s.Config.Save(s.ConfigPath);
            return TypedResults.Ok(State(s));
        });

        api.MapGet("/drives", () => DriveInfo.GetDrives()
            .Where(d => d.IsReady && (OperatingSystem.IsWindows() ? d.DriveType == DriveType.Fixed : Mounts.IsUserMount(d.Name)))
            .Select(d => new Drive(d.Name, d.TotalFreeSpace, d.TotalSize))
            .ToList());

        api.MapGet("/update", (Updater u) => u.State);

        // Responde cuando termina la comprobación; si hay descarga, su progreso llega por el evento "update".
        api.MapPost("/update/check", async (Updater u) =>
        {
            await u.Check();
            return u.State;
        });

        api.MapPost("/update/restart", Results<Ok, Conflict<ApiError>> (Updater u) =>
            u.RestartNow() ? TypedResults.Ok() : TypedResults.Conflict(ApiError.Of("update.nothingDownloaded")));

        api.MapGet("/events", (HttpContext http, Events events) => events.Serve(http)).ExcludeFromDescription();
    }

    static AppState State(AppSession s) =>
        new(Startup.Version, s.Config.DataFolder, DataFolder.IsReady(s.Config.DataFolder), s.Config.EffectiveLanguage);
}
