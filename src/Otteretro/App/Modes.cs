using System.Runtime.InteropServices;

namespace Otteretro.App;

/// <summary>Modo APP: servidor local + ventana.</summary>
public static class AppMode
{
    public static int Run(bool fullscreen)
    {
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            SingleInstance.NotifyExisting();
            return 0;
        }

        var configPath = AppConfig.DefaultPath;
        var config = AppConfig.Load(configPath);
        if (DataFolder.IsReady(config.DataFolder))
            Startup.PrepareData(config);

        var session = new AppSession(config, configPath, LocalServer.NewKey(), fullscreen);
        var app = LocalServer.Create(session);
        app.StartAsync().GetAwaiter().GetResult();
        var api = LocalServer.Address(app);

        // En desarrollo la interfaz viene de `next dev` y se le dice dónde está la API.
        var web = Environment.GetEnvironmentVariable("OTTERETRO_DEV_WEB");
        var url = web is null
            ? $"{api}/?key={session.Key}"
            : $"{web}/?key={session.Key}&api={Uri.EscapeDataString(api)}";

        _ = app.Services.GetRequiredService<Updater>().Check();
        MainWindow.Show(url, instance, fullscreen);
        app.StopAsync().GetAwaiter().GetResult();
        return 0;
    }
}

/// <summary>Modo JUGAR: `otteretro jugar &lt;id&gt;`. Llega en la fase 4.</summary>
public static class PlayMode
{
    public const int NotImplemented = 2;

    public static int Run(string[] args)
    {
        FileLog.Write(LogLevel.Warning, "Play", $"`jugar {string.Join(' ', args)}` todavía no está implementado (fase 4).");
        return NotImplemented;
    }
}

/// <summary>Modo CLI: `otteretro &lt;tarea&gt;`. Los comandos son la interfaz de la CLI y siguen en español.</summary>
public static class CliMode
{
    public const string Help = """
        Uso: otteretro [comando]

          (sin comando)          Abre la app.
          --fullscreen           Abre la app a pantalla completa (en la sesión Play es automático).
          jugar <id>             Lanza un juego (fase 4).
          version                Muestra la versión.
          catalogo actualizar    Busca una versión publicada del catálogo; lo hace la app abierta.
          openapi <archivo>      Escribe el contrato OpenAPI de la API (para generar los tipos del front).
          steam sincronizar      Lee lo que tienes en Steam (con sus horas) y lo enlaza con el catálogo; lo hace la app abierta.
          claude ahora           Lanza ya la carga de Claude, fuera de su horario (00:00 a 08:00), hasta que empiece el siguiente.
                                 y después Claude prepara rutas, icónico y mejor versión para que las apruebes. Dura días.
            [--parte X]            Solo una parte (nintendo, sony, microsoft o pc).
          tareas prueba          Encola una tarea de prueba (10 pasos de 3 s) para ver el panel de tareas.
          servidor               Levanta la API y la interfaz sin ventana, para abrirlas en un navegador (desarrollo).
          ayuda                  Muestra esta ayuda.
        """;

    public static int Run(string[] args, TextWriter? output = null)
    {
        if (output is null)
        {
            AttachToConsole();
            output = Console.Out;
        }
        switch (args)
        {
            case ["version"]:
                output.WriteLine(Startup.Version);
                return 0;
            case ["ayuda"] or ["--help"] or ["-h"]:
                output.WriteLine(Help);
                return 0;
            case ["catalogo", "actualizar"]:
                return Enqueue(output, db => [Catalog.CatalogUpdate.Enqueue(db)]);
            case ["openapi", var file]:
                WriteOpenApi(file).GetAwaiter().GetResult();
                output.WriteLine($"Contrato escrito en {file}");
                return 0;
            case ["tareas", "prueba"]:
                return Enqueue(output, db => [TaskQueue.Enqueue(db, "demo")]);
            case ["steam", "sincronizar"]:
                return Enqueue(output, db => [Catalog.Steam.EnqueueSync(db)]);
            case ["claude", "ahora"]:
                return WithData(output, db =>
                    output.WriteLine($"Claude corre ya (tarea {Claude.ClaudeBudget.RunNow(db)}) hasta que empiece su horario; el límite de la suscripción y el tope, si está marcado, siguen valiendo."));
            case ["servidor"]:
                Serve(output);
                return 0;
            default:
                output.WriteLine($"Comando desconocido: {string.Join(' ', args)}");
                output.WriteLine(Help);
                return 1;
        }
    }

    /// <summary>Encola tareas en la base de la carpeta de datos; las toma la app abierta.</summary>
    static int Enqueue(TextWriter output, Func<Data.Database, long[]> enqueue) => WithData(output, db =>
    {
        var ids = enqueue(db);
        output.WriteLine(ids.Length == 1
            ? $"Tarea {ids[0]} encolada; la app abierta la toma en unos segundos."
            : $"{ids.Length} tareas encoladas; la app abierta las hace de a una y se ven en el panel de tareas.");
    });

    static int WithData(TextWriter output, Action<Data.Database> action)
    {
        var config = AppConfig.Load(AppConfig.DefaultPath);
        if (!DataFolder.IsReady(config.DataFolder))
        {
            output.WriteLine("Todavía no hay carpeta de datos: abre la app y elige una.");
            return 1;
        }
        action(Startup.PrepareData(config));
        return 0;
    }

    static void Serve(TextWriter output)
    {
        var configPath = AppConfig.DefaultPath;
        var config = AppConfig.Load(configPath);
        if (DataFolder.IsReady(config.DataFolder))
            Startup.PrepareData(config);
        var session = new AppSession(config, configPath, LocalServer.NewKey());
        var app = LocalServer.Create(session);
        app.StartAsync().GetAwaiter().GetResult();
        output.WriteLine($"Configuración: {configPath}");
        output.WriteLine($"Abre {LocalServer.Address(app)}/?key={session.Key}");
        output.WriteLine("Ctrl+C para terminar.");
        app.WaitForShutdownAsync().GetAwaiter().GetResult();
    }

    static async Task WriteOpenApi(string file)
    {
        var session = new AppSession(new AppConfig(), Path.GetTempFileName(), LocalServer.NewKey());
        await using var app = LocalServer.Create(session);
        await app.StartAsync();
        using var http = new HttpClient();
        var json = await http.GetStringAsync($"{LocalServer.Address(app)}/openapi/v1.json");
        await File.WriteAllTextAsync(file, json);
        await app.StopAsync();
    }

    // El ejecutable es WinExe (sin consola); al usarlo desde una terminal se engancha a ella.
    static void AttachToConsole()
    {
        if (OperatingSystem.IsWindows()) AttachConsole(-1);
        Console.OutputEncoding = System.Text.Encoding.UTF8;
    }

    [DllImport("kernel32.dll")]
    static extern bool AttachConsole(int processId);
}
