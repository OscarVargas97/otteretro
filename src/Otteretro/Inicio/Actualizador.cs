using Velopack;
using Velopack.Sources;

namespace Otteretro.Inicio;

/// <summary>
/// Busca una versión nueva y la descarga; se aplica al cerrar la app.
/// Mientras no haya releases en GitHub (#101) el origen es una carpeta local (config.json → origenActualizaciones).
/// </summary>
public static class Actualizador
{
    public static async Task BuscarEnSegundoPlano(Configuracion config)
    {
        if (string.IsNullOrEmpty(config.OrigenActualizaciones)) return;
        try
        {
            var gestor = new UpdateManager(new SimpleFileSource(new DirectoryInfo(config.OrigenActualizaciones)));
            if (!gestor.IsInstalled) return;
            var info = await gestor.CheckForUpdatesAsync();
            if (info is null) return;
            await gestor.DownloadUpdatesAsync(info);
            gestor.WaitExitThenApplyUpdates(info.TargetFullRelease, silent: true, restart: false);
            RegistroArchivo.Escribir(LogLevel.Information, "Actualizador",
                $"Versión {info.TargetFullRelease.Version} descargada; se aplica al cerrar la app.");
        }
        catch (Exception e)
        {
            RegistroArchivo.Escribir(LogLevel.Error, "Actualizador", e.ToString());
        }
    }
}
