using System.Reflection;
using Otteretro.Datos;

namespace Otteretro.Inicio;

/// <summary>Lo que se hace cada vez que hay una carpeta de datos lista: logs, migraciones y equipo.</summary>
public static class Arranque
{
    public static string Version =>
        typeof(Arranque).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    public static Bd PrepararDatos(Configuracion config)
    {
        var carpeta = config.CarpetaDatos ?? throw new InvalidOperationException("No hay carpeta de datos.");
        RegistroArchivo.Directorio = CarpetaDatos.RutaLogs(carpeta);
        RegistroArchivo.Limpiar();
        var bd = new Bd(CarpetaDatos.RutaBd(carpeta));
        var aplicadas = Migraciones.Aplicar(bd);
        Migraciones.RegistrarEquipo(bd, config.EquipoId);
        RegistroArchivo.Escribir(LogLevel.Information, "Arranque", $"Otteretro {Version}; carpeta de datos {carpeta}; {aplicadas} migraciones nuevas.");
        return bd;
    }
}
