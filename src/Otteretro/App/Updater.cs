using System.Text.Json.Serialization;
using Otteretro.Api;
using Velopack;
using Velopack.Sources;

namespace Otteretro.App;

[JsonConverter(typeof(JsonStringEnumConverter<UpdatePhase>))]
public enum UpdatePhase { None, Unavailable, Checking, UpToDate, Downloading, Ready, Error }

/// <param name="CheckedAt">Cuándo terminó la última comprobación (para mostrar "comprobado a las …").</param>
/// <param name="Message">Código del motivo (clave `update.*` del catálogo del front, #142).</param>
public record UpdateState(UpdatePhase Phase, string? Version, int Progress, string? Message, DateTimeOffset? CheckedAt);

/// <summary>
/// Busca una versión nueva, la descarga y avisa a la interfaz en cada paso (panel de tareas y aviso de "lista").
/// Se instala al cerrar la app o al pulsar "Reiniciar ahora".
/// Mientras no haya releases en GitHub (#101) el origen es una carpeta local (config.json → origenActualizaciones).
/// </summary>
public sealed class Updater(AppSession session, Events events)
{
    UpdateManager? manager;
    VelopackAsset? ready;
    int busy;

    public UpdateState State { get; private set; } = new(UpdatePhase.None, null, 0, null, null);
    DateTimeOffset? checkedAt;

    /// <summary>
    /// Se llama al abrir la app y desde el botón "Buscar actualización". Termina cuando acaba la comprobación;
    /// si hay versión nueva, la descarga sigue en segundo plano. No hace nada si ya está buscando o hay una lista.
    /// </summary>
    public async Task Check()
    {
        // Una sola búsqueda o descarga a la vez (el arranque y el botón pueden coincidir).
        if (State.Phase == UpdatePhase.Ready || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
        var downloading = false;
        try
        {
            var source = session.Config.UpdateSource;
            if (string.IsNullOrEmpty(source))
            {
                Set(UpdatePhase.Unavailable, null, 0, "noSource");
                return;
            }
            manager = new UpdateManager(new SimpleFileSource(new DirectoryInfo(source)));
            if (!manager.IsInstalled)
            {
                Set(UpdatePhase.Unavailable, null, 0, "notInstalled");
                return;
            }
            Set(UpdatePhase.Checking, null, 0);
            // Un origen en una unidad de red caída no debe dejar el botón colgado en "Buscando".
            var info = await manager.CheckForUpdatesAsync().WaitAsync(TimeSpan.FromMinutes(1));
            checkedAt = DateTimeOffset.Now;
            if (info is null)
            {
                Set(UpdatePhase.UpToDate, null, 0);
                return;
            }
            Set(UpdatePhase.Downloading, info.TargetFullRelease.Version.ToString(), 0);
            downloading = true;
            _ = Download(manager, info);
        }
        catch (Exception e)
        {
            Fail(e);
        }
        finally
        {
            if (!downloading) Volatile.Write(ref busy, 0);
        }
    }

    async Task Download(UpdateManager manager, UpdateInfo info)
    {
        var version = info.TargetFullRelease.Version.ToString();
        try
        {
            await manager.DownloadUpdatesAsync(info, p => Set(UpdatePhase.Downloading, version, p));
            ready = info.TargetFullRelease;
            // Si no se reinicia antes, se instala al cerrar la app.
            manager.WaitExitThenApplyUpdates(ready, silent: true, restart: false);
            Set(UpdatePhase.Ready, version, 100);
            FileLog.Write(LogLevel.Information, "Updater", $"Versión {version} descargada; se instala al cerrar la app.");
        }
        catch (Exception e)
        {
            Fail(e);
        }
        finally
        {
            Volatile.Write(ref busy, 0);
        }
    }

    void Fail(Exception e)
    {
        Set(UpdatePhase.Error, State.Version, 0, "failed");
        FileLog.Write(LogLevel.Error, "Updater", e.ToString());
    }

    /// <summary>Instala la versión descargada y vuelve a abrir la app. Devuelve false si no hay ninguna lista.</summary>
    public bool RestartNow()
    {
        if (manager is null || ready is null) return false;
        manager.ApplyUpdatesAndRestart(ready);
        return true;
    }

    void Set(UpdatePhase phase, string? version, int progress, string? message = null)
    {
        State = new(phase, version, progress, message, checkedAt);
        events.Publish("update", State);
    }
}
