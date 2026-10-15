using System.Text.Json.Serialization;
using Otteretro.Api;
using Velopack;
using Velopack.Sources;

namespace Otteretro.Inicio;

[JsonConverter(typeof(JsonStringEnumConverter<FaseActualizacion>))]
public enum FaseActualizacion { Ninguna, NoDisponible, Buscando, AlDia, Descargando, Lista, Error }

public record EstadoActualizacion(FaseActualizacion Fase, string? Version, int Progreso, string? Mensaje);

/// <summary>
/// Busca una versión nueva, la descarga y avisa a la interfaz en cada paso (panel de tareas y aviso de "lista").
/// Se instala al cerrar la app o al pulsar "Reiniciar ahora".
/// Mientras no haya releases en GitHub (#101) el origen es una carpeta local (config.json → origenActualizaciones).
/// </summary>
public sealed class Actualizador(ContextoApp contexto, Eventos eventos)
{
    UpdateManager? gestor;
    VelopackAsset? lista;

    public EstadoActualizacion Estado { get; private set; } = new(FaseActualizacion.Ninguna, null, 0, null);

    /// <summary>Se llama al abrir la app y desde el botón "Buscar actualización". No hace nada si ya está buscando o hay una lista.</summary>
    public async Task Buscar()
    {
        if (Estado.Fase is FaseActualizacion.Buscando or FaseActualizacion.Descargando or FaseActualizacion.Lista) return;
        var origen = contexto.Config.OrigenActualizaciones;
        if (string.IsNullOrEmpty(origen))
        {
            Cambiar(FaseActualizacion.NoDisponible, null, 0, "No hay un origen de actualizaciones configurado.");
            return;
        }
        try
        {
            gestor = new UpdateManager(new SimpleFileSource(new DirectoryInfo(origen)));
            if (!gestor.IsInstalled)
            {
                Cambiar(FaseActualizacion.NoDisponible, null, 0, "Las actualizaciones solo funcionan en la app instalada.");
                return;
            }
            Cambiar(FaseActualizacion.Buscando, null, 0);
            var info = await gestor.CheckForUpdatesAsync();
            if (info is null)
            {
                Cambiar(FaseActualizacion.AlDia, null, 0);
                return;
            }
            var version = info.TargetFullRelease.Version.ToString();
            Cambiar(FaseActualizacion.Descargando, version, 0);
            await gestor.DownloadUpdatesAsync(info, p => Cambiar(FaseActualizacion.Descargando, version, p));
            lista = info.TargetFullRelease;
            // Si no se reinicia antes, se instala al cerrar la app.
            gestor.WaitExitThenApplyUpdates(lista, silent: true, restart: false);
            Cambiar(FaseActualizacion.Lista, version, 100);
            RegistroArchivo.Escribir(LogLevel.Information, "Actualizador", $"Versión {version} descargada; se instala al cerrar la app.");
        }
        catch (Exception e)
        {
            Cambiar(FaseActualizacion.Error, Estado.Version, 0, "No se pudo buscar o descargar la actualización. El detalle está en el registro.");
            RegistroArchivo.Escribir(LogLevel.Error, "Actualizador", e.ToString());
        }
    }

    /// <summary>Instala la versión descargada y vuelve a abrir la app. Devuelve false si no hay ninguna lista.</summary>
    public bool ReiniciarAhora()
    {
        if (gestor is null || lista is null) return false;
        gestor.ApplyUpdatesAndRestart(lista);
        return true;
    }

    void Cambiar(FaseActualizacion fase, string? version, int progreso, string? mensaje = null)
    {
        Estado = new(fase, version, progreso, mensaje);
        eventos.Publicar("actualizacion", Estado);
    }
}
