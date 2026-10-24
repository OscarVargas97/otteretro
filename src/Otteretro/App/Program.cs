using Velopack;

namespace Otteretro.App;

/// <summary>Punto de entrada: el mismo ejecutable es la app, el lanzador de juegos y la CLI.</summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        VelopackApp.Build().Run();
        return args switch
        {
            [] => AppMode.Run(),
            ["jugar", .. var rest] => PlayMode.Run(rest),
            _ => CliMode.Run(args),
        };
    }
}
