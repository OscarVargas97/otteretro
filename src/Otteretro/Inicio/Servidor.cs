using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Otteretro.Api;

namespace Otteretro.Inicio;

/// <summary>Lo que comparten la ventana y la API durante una sesión.</summary>
public sealed record ContextoApp(Configuracion Config, string RutaConfig, string Clave);

/// <summary>
/// Servidor local: solo 127.0.0.1, puerto aleatorio y una clave por sesión que solo conoce la ventana.
/// Así ningún otro programa ni página web del PC puede usar la API.
/// </summary>
public static class Servidor
{
    public const string CabeceraClave = "X-Otteretro-Clave";

    public static string NuevaClave() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static WebApplication Crear(ContextoApp contexto, Action<WebApplicationBuilder>? ajustar = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new RegistroArchivo());
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddOpenApi();
        builder.Services.AddSingleton(contexto);
        builder.Services.AddSingleton<Eventos>();
        builder.Services.AddSingleton<Actualizador>();

        // Solo en desarrollo: la interfaz corre en `next dev` en otro origen.
        var origenDev = Environment.GetEnvironmentVariable("OTTERETRO_DEV_WEB");
        if (origenDev is not null)
            builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origenDev).AllowAnyHeader().AllowAnyMethod()));

        ajustar?.Invoke(builder);
        var app = builder.Build();

        if (origenDev is not null) app.UseCors();
        app.Use(async (http, siguiente) =>
        {
            // Un Host distinto delata una página web que intenta llegar a la API (DNS rebinding).
            if (http.Request.Host.Host != "127.0.0.1")
            {
                http.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            if (http.Request.Path.StartsWithSegments("/api") && !http.Request.Method.Equals("OPTIONS", StringComparison.Ordinal)
                && !ClaveValida(http, contexto.Clave))
            {
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            await siguiente();
        });
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapOpenApi();
        Endpoints.Mapear(app);
        return app;
    }

    /// <summary>La clave llega en la cabecera o, para EventSource que no admite cabeceras, en ?clave=.</summary>
    static bool ClaveValida(HttpContext http, string clave)
    {
        var dada = http.Request.Headers[CabeceraClave].FirstOrDefault() ?? http.Request.Query["clave"].FirstOrDefault();
        return dada is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(dada), Encoding.UTF8.GetBytes(clave));
    }

    /// <summary>La dirección real (con el puerto elegido por el sistema) después de StartAsync.</summary>
    public static string Direccion(WebApplication app) =>
        app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
}
