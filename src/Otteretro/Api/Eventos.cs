using System.Text.Json;
using System.Threading.Channels;

namespace Otteretro.Api;

/// <summary>Progreso en vivo hacia la interfaz con Server-Sent Events (A2). Cualquier módulo publica aquí.</summary>
public sealed class Eventos : IDisposable
{
    readonly List<Channel<string>> suscriptores = [];
    readonly Lock cerrojo = new();
    readonly Timer latido;

    public Eventos() =>
        // Mantiene viva la conexión aunque no pase nada.
        latido = new Timer(_ => Publicar("latido", new { }), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));

    public void Publicar(string tipo, object datos)
    {
        var mensaje = $"event: {tipo}\ndata: {JsonSerializer.Serialize(datos, JsonSerializerOptions.Web)}\n\n";
        lock (cerrojo)
            foreach (var s in suscriptores)
                s.Writer.TryWrite(mensaje);
    }

    public async Task Atender(HttpContext http)
    {
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        var canal = Channel.CreateBounded<string>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (cerrojo) suscriptores.Add(canal);
        try
        {
            var fin = http.RequestAborted;
            await http.Response.WriteAsync(": conectado\n\n", fin);
            await http.Response.Body.FlushAsync(fin);
            await foreach (var mensaje in canal.Reader.ReadAllAsync(fin))
            {
                await http.Response.WriteAsync(mensaje, fin);
                await http.Response.Body.FlushAsync(fin);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            lock (cerrojo) suscriptores.Remove(canal);
        }
    }

    public void Dispose() => latido.Dispose();
}
