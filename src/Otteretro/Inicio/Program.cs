using Velopack;

namespace Otteretro.Inicio;

/// <summary>Punto de entrada: el mismo ejecutable es la app, el lanzador de juegos y la CLI.</summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        VelopackApp.Build().Run();
        return args switch
        {
            [] => ModoApp.Ejecutar(),
            ["jugar", .. var resto] => ModoJugar.Ejecutar(resto),
            _ => ModoCli.Ejecutar(args),
        };
    }
}
