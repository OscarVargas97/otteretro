using Microsoft.AspNetCore.Http.HttpResults;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Api;

public record AppState(string Version, string? DataFolder, bool Configured, string Language, bool Fullscreen);
public record FolderRequest(string Path);
public record LanguageRequest(string Language);
public record CredentialRequest(string Value);

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

        // Ajustes › Cuentas y claves (§5.6): nunca se devuelve un valor, solo si está guardado.
        api.MapGet("/credentials", Results<Ok<List<CredentialStatus>>, Conflict<ApiError>> (AppSession s, SecretBox box) =>
            CredentialsOf(s, box) is { } c ? TypedResults.Ok(c.List()) : TypedResults.Conflict(ApiError.Of("move.noDataFolder")));

        api.MapPut("/credentials/{id}", Results<Ok<List<CredentialStatus>>, BadRequest<ApiError>, Conflict<ApiError>, NotFound> (string id, CredentialRequest request, AppSession s, SecretBox box) =>
        {
            if (!Credentials.IsKnown(id)) return TypedResults.NotFound();
            if (string.IsNullOrWhiteSpace(request.Value)) return TypedResults.BadRequest(ApiError.Of("credentials.empty"));
            if (CredentialsOf(s, box) is not { } c) return TypedResults.Conflict(ApiError.Of("move.noDataFolder"));
            try { c.Save(id, request.Value.Trim()); }
            catch (KeyringUnavailableException e)
            {
                FileLog.Write(LogLevel.Warning, "Credentials", $"Sin llavero: {e.Message}");
                return TypedResults.Conflict(ApiError.Of("credentials.noKeyring"));
            }
            return TypedResults.Ok(c.List());
        });

        api.MapDelete("/credentials/{id}", Results<Ok<List<CredentialStatus>>, Conflict<ApiError>, NotFound> (string id, AppSession s, SecretBox box) =>
        {
            if (!Credentials.IsKnown(id)) return TypedResults.NotFound();
            if (CredentialsOf(s, box) is not { } c) return TypedResults.Conflict(ApiError.Of("move.noDataFolder"));
            c.Delete(id);
            return TypedResults.Ok(c.List());
        });

        // Abre en el navegador del sistema dónde se consigue la clave; solo URLs del catálogo.
        api.MapPost("/credentials/{id}/open", Results<Ok, NotFound> (string id) =>
        {
            if (Credentials.Kinds.FirstOrDefault(k => k.Id == id) is not { } kind) return TypedResults.NotFound();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(kind.Url) { UseShellExecute = true })?.Dispose();
            return TypedResults.Ok();
        });

        // «Ir a Steam» (#12): Steam pasa a Big Picture; Otteretro sigue abierto detrás.
        api.MapPost("/steam/big-picture", () =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("steam://open/bigpicture") { UseShellExecute = true })?.Dispose();
            return TypedResults.Ok();
        });

        api.MapGet("/events", (HttpContext http, Events events) => events.Serve(http)).ExcludeFromDescription();
    }

    static Credentials? CredentialsOf(AppSession s, SecretBox box) =>
        DataFolder.IsReady(s.Config.DataFolder) ? new(new Database(DataFolder.DatabasePath(s.Config.DataFolder!)), s.Config.MachineId, box) : null;

    static AppState State(AppSession s) =>
        new(Startup.Version, s.Config.DataFolder, DataFolder.IsReady(s.Config.DataFolder), s.Config.EffectiveLanguage, s.Fullscreen);
}
