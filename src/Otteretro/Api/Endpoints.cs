using Microsoft.AspNetCore.Http.HttpResults;
using Otteretro.Inicio;

namespace Otteretro.Api;

public record EstadoApp(string Version, string? CarpetaDatos, bool Configurada);
public record PedidoCarpeta(string Ruta);
public record Problema(string Mensaje);
public record Disco(string Nombre, long Libre, long Total);
public record InfoCarpeta(string? Ruta, long Ocupado, long LibreEnDisco);

/// <summary>Endpoints base de F0. Cada módulo añadirá los suyos.</summary>
public static class Endpoints
{
    public static void Mapear(WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/estado", (ContextoApp c) => Estado(c));

        api.MapPost("/carpeta-datos", Results<Ok<EstadoApp>, BadRequest<Problema>, Conflict<Problema>> (PedidoCarpeta pedido, ContextoApp c, ServicioMudanza m) =>
        {
            if (m.Estado.EnCurso) return TypedResults.Conflict(new Problema("Espera a que termine la mudanza de la carpeta de datos."));
            var error = CarpetaDatos.Preparar(pedido.Ruta);
            if (error is not null) return TypedResults.BadRequest(new Problema(error));
            c.Config.CarpetaDatos = pedido.Ruta;
            c.Config.Guardar(c.RutaConfig);
            Arranque.PrepararDatos(c.Config);
            return TypedResults.Ok(Estado(c));
        });

        api.MapGet("/carpeta-datos", (ContextoApp c) =>
        {
            var ruta = c.Config.CarpetaDatos;
            var raiz = ruta is null ? null : Path.GetPathRoot(ruta);
            return new InfoCarpeta(ruta, ruta is null ? 0 : Mudanza.Tamano(ruta),
                raiz is not null && Directory.Exists(raiz) ? new DriveInfo(raiz).AvailableFreeSpace : 0);
        });

        api.MapGet("/carpeta-datos/mudanza", (ServicioMudanza m) => m.Estado);

        api.MapPost("/carpeta-datos/mover", Results<Accepted<EstadoMudanza>, BadRequest<Problema>> (PedidoCarpeta pedido, ServicioMudanza m) =>
        {
            var error = m.Iniciar(pedido.Ruta);
            return error is null ? TypedResults.Accepted("/api/carpeta-datos/mudanza", m.Estado) : TypedResults.BadRequest(new Problema(error));
        });

        api.MapGet("/discos", () => DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => new Disco(d.Name, d.TotalFreeSpace, d.TotalSize))
            .ToList());

        api.MapGet("/actualizacion", (Actualizador a) => a.Estado);

        // Responde cuando termina la comprobación; si hay descarga, su progreso llega por el evento "actualizacion".
        api.MapPost("/actualizacion/buscar", async (Actualizador a) =>
        {
            await a.Buscar();
            return a.Estado;
        });

        api.MapPost("/actualizacion/reiniciar", Results<Ok, Conflict<Problema>> (Actualizador a) =>
            a.ReiniciarAhora() ? TypedResults.Ok() : TypedResults.Conflict(new Problema("No hay ninguna actualización descargada.")));

        api.MapGet("/eventos", (HttpContext http, Eventos eventos) => eventos.Atender(http)).ExcludeFromDescription();
    }

    static EstadoApp Estado(ContextoApp c) =>
        new(Arranque.Version, c.Config.CarpetaDatos, CarpetaDatos.EstaLista(c.Config.CarpetaDatos));
}
