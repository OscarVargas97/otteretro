using Microsoft.AspNetCore.Http.HttpResults;
using Otteretro.Inicio;

namespace Otteretro.Api;

public record EstadoApp(string Version, string? CarpetaDatos, bool Configurada);
public record PedidoCarpeta(string Ruta);
public record Problema(string Mensaje);
public record Disco(string Nombre, long Libre, long Total);

/// <summary>Endpoints base de F0. Cada módulo añadirá los suyos.</summary>
public static class Endpoints
{
    public static void Mapear(WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/estado", (ContextoApp c) => Estado(c));

        api.MapPost("/carpeta-datos", Results<Ok<EstadoApp>, BadRequest<Problema>> (PedidoCarpeta pedido, ContextoApp c) =>
        {
            var error = CarpetaDatos.Preparar(pedido.Ruta);
            if (error is not null) return TypedResults.BadRequest(new Problema(error));
            c.Config.CarpetaDatos = pedido.Ruta;
            c.Config.Guardar(c.RutaConfig);
            Arranque.PrepararDatos(c.Config);
            return TypedResults.Ok(Estado(c));
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
