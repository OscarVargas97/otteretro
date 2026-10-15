using System.Runtime.InteropServices;

namespace Otteretro.Inicio;

/// <summary>Modo APP: servidor local + ventana.</summary>
public static class ModoApp
{
    public static int Ejecutar()
    {
        using var instancia = UnaInstancia.Tomar();
        if (instancia is null)
        {
            UnaInstancia.AvisarALaQueExiste();
            return 0;
        }

        var rutaConfig = Configuracion.RutaPorDefecto;
        var config = Configuracion.Cargar(rutaConfig);
        if (CarpetaDatos.EstaLista(config.CarpetaDatos))
            Arranque.PrepararDatos(config);

        var contexto = new ContextoApp(config, rutaConfig, Servidor.NuevaClave());
        var app = Servidor.Crear(contexto);
        app.StartAsync().GetAwaiter().GetResult();
        var api = Servidor.Direccion(app);

        // En desarrollo la interfaz viene de `next dev` y se le dice dónde está la API.
        var web = Environment.GetEnvironmentVariable("OTTERETRO_DEV_WEB");
        var url = web is null
            ? $"{api}/?clave={contexto.Clave}"
            : $"{web}/?clave={contexto.Clave}&api={Uri.EscapeDataString(api)}";

        _ = Actualizador.BuscarEnSegundoPlano(config);
        Ventana.Mostrar(url, instancia);
        app.StopAsync().GetAwaiter().GetResult();
        return 0;
    }
}

/// <summary>Modo JUGAR: `otteretro jugar &lt;id&gt;`. Llega en la fase 4.</summary>
public static class ModoJugar
{
    public const int NoImplementado = 2;

    public static int Ejecutar(string[] args)
    {
        RegistroArchivo.Escribir(LogLevel.Warning, "Jugar", $"`jugar {string.Join(' ', args)}` todavía no está implementado (fase 4).");
        return NoImplementado;
    }
}

/// <summary>Modo CLI: `otteretro &lt;tarea&gt;`.</summary>
public static class ModoCli
{
    public const string Ayuda = """
        Uso: otteretro [comando]

          (sin comando)          Abre la app.
          jugar <id>             Lanza un juego (fase 4).
          version                Muestra la versión.
          openapi <archivo>      Escribe el contrato OpenAPI de la API (para generar los tipos del front).
          servidor               Levanta la API y la interfaz sin ventana, para abrirlas en un navegador (desarrollo).
          ayuda                  Muestra esta ayuda.
        """;

    public static int Ejecutar(string[] args, TextWriter? salida = null)
    {
        if (salida is null)
        {
            EngancharConsola();
            salida = Console.Out;
        }
        switch (args)
        {
            case ["version"]:
                salida.WriteLine(Arranque.Version);
                return 0;
            case ["ayuda"] or ["--help"] or ["-h"]:
                salida.WriteLine(Ayuda);
                return 0;
            case ["openapi", var archivo]:
                EscribirOpenApi(archivo).GetAwaiter().GetResult();
                salida.WriteLine($"Contrato escrito en {archivo}");
                return 0;
            case ["servidor"]:
                Servir(salida);
                return 0;
            default:
                salida.WriteLine($"Comando desconocido: {string.Join(' ', args)}");
                salida.WriteLine(Ayuda);
                return 1;
        }
    }

    static void Servir(TextWriter salida)
    {
        var rutaConfig = Configuracion.RutaPorDefecto;
        var config = Configuracion.Cargar(rutaConfig);
        if (CarpetaDatos.EstaLista(config.CarpetaDatos))
            Arranque.PrepararDatos(config);
        var contexto = new ContextoApp(config, rutaConfig, Servidor.NuevaClave());
        var app = Servidor.Crear(contexto);
        app.StartAsync().GetAwaiter().GetResult();
        salida.WriteLine($"Configuración: {rutaConfig}");
        salida.WriteLine($"Abre {Servidor.Direccion(app)}/?clave={contexto.Clave}");
        salida.WriteLine("Ctrl+C para terminar.");
        app.WaitForShutdownAsync().GetAwaiter().GetResult();
    }

    static async Task EscribirOpenApi(string archivo)
    {
        var contexto = new ContextoApp(new Configuracion(), Path.GetTempFileName(), Servidor.NuevaClave());
        await using var app = Servidor.Crear(contexto);
        await app.StartAsync();
        using var http = new HttpClient();
        var json = await http.GetStringAsync($"{Servidor.Direccion(app)}/openapi/v1.json");
        await File.WriteAllTextAsync(archivo, json);
        await app.StopAsync();
    }

    // El ejecutable es WinExe (sin consola); al usarlo desde una terminal se engancha a ella.
    static void EngancharConsola()
    {
        if (OperatingSystem.IsWindows()) AttachConsole(-1);
        Console.OutputEncoding = System.Text.Encoding.UTF8;
    }

    [DllImport("kernel32.dll")]
    static extern bool AttachConsole(int idProceso);
}
