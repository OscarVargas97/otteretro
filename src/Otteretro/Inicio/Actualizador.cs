using System.Text.Json.Serialization;
using Otteretro.Api;
using Velopack;
using Velopack.Sources;

namespace Otteretro.Inicio;

[JsonConverter(typeof(JsonStringEnumConverter<FaseActualizacion>))]
public enum FaseActualizacion { Ninguna, NoDisponible, Buscando, AlDia, Descargando, Lista, Error }

/// <param name="Comprobada">Cuándo terminó la última comprobación (para mostrar "comprobado a las …").</param>
public record EstadoActualizacion(FaseActualizacion Fase, string? Version, int Progreso, string? Mensaje, DateTimeOffset? Comprobada);

/// <summary>
/// Busca una versión nueva, la descarga y avisa a la interfaz en cada paso (panel de tareas y aviso de "lista").
/// Se instala al cerrar la app o al pulsar "Reiniciar ahora".
/// Mientras no haya releases en GitHub (#101) el origen es una carpeta local (config.json → origenActualizaciones).
/// </summary>
public sealed class Actualizador(ContextoApp contexto, Eventos eventos)
{
    UpdateManager? gestor;
    VelopackAsset? lista;
    int ocupado;

    public EstadoActualizacion Estado { get; private set; } = new(FaseActualizacion.Ninguna, null, 0, null, null);
    DateTimeOffset? comprobada;

    /// <summary>
    /// Se llama al abrir la app y desde el botón "Buscar actualización". Termina cuando acaba la comprobación;
    /// si hay versión nueva, la descarga sigue en segundo plano. No hace nada si ya está buscando o hay una lista.
    /// </summary>
    public async Task Buscar()
    {
        // Una sola búsqueda o descarga a la vez (el arranque y el botón pueden coincidir).
        if (Estado.Fase == FaseActualizacion.Lista || Interlocked.CompareExchange(ref ocupado, 1, 0) != 0) return;
        var descargando = false;
        try
        {
            var origen = contexto.Config.OrigenActualizaciones;
            if (string.IsNullOrEmpty(origen))
            {
                Cambiar(FaseActualizacion.NoDisponible, null, 0, "No hay un origen de actualizaciones configurado.");
                return;
            }
            gestor = new UpdateManager(new SimpleFileSource(new DirectoryInfo(origen)));
            if (!gestor.IsInstalled)
            {
                Cambiar(FaseActualizacion.NoDisponible, null, 0, "Las actualizaciones solo funcionan en la app instalada.");
                return;
            }
            Cambiar(FaseActualizacion.Buscando, null, 0);
            // Un origen en una unidad de red caída no debe dejar el botón colgado en "Buscando".
            var info = await gestor.CheckForUpdatesAsync().WaitAsync(TimeSpan.FromMinutes(1));
            comprobada = DateTimeOffset.Now;
            if (info is null)
            {
                Cambiar(FaseActualizacion.AlDia, null, 0);
                return;
            }
            Cambiar(FaseActualizacion.Descargando, info.TargetFullRelease.Version.ToString(), 0);
            descargando = true;
            _ = Descargar(gestor, info);
        }
        catch (Exception e)
        {
            Fallo(e);
        }
        finally
        {
            if (!descargando) Volatile.Write(ref ocupado, 0);
        }
    }

    async Task Descargar(UpdateManager gestor, UpdateInfo info)
    {
        var version = info.TargetFullRelease.Version.ToString();
        try
        {
            await gestor.DownloadUpdatesAsync(info, p => Cambiar(FaseActualizacion.Descargando, version, p));
            lista = info.TargetFullRelease;
            // Si no se reinicia antes, se instala al cerrar la app.
            gestor.WaitExitThenApplyUpdates(lista, silent: true, restart: false);
            Cambiar(FaseActualizacion.Lista, version, 100);
            RegistroArchivo.Escribir(LogLevel.Information, "Actualizador", $"Versión {version} descargada; se instala al cerrar la app.");
        }
        catch (Exception e)
        {
            Fallo(e);
        }
        finally
        {
            Volatile.Write(ref ocupado, 0);
        }
    }

    void Fallo(Exception e)
    {
        Cambiar(FaseActualizacion.Error, Estado.Version, 0, "No se pudo buscar o descargar la actualización. El detalle está en el registro.");
        RegistroArchivo.Escribir(LogLevel.Error, "Actualizador", e.ToString());
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
        Estado = new(fase, version, progreso, mensaje, comprobada);
        eventos.Publicar("actualizacion", Estado);
    }
}
