using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Otteretro.Api;
using Otteretro.Inicio;

namespace Otteretro.Tests;

public class ServidorTests : IDisposable
{
    readonly Temporal tmp = new();
    readonly WebApplication app;
    readonly HttpClient cliente;
    readonly ContextoApp contexto;

    public ServidorTests()
    {
        contexto = new ContextoApp(new Configuracion(), tmp["config.json"], Servidor.NuevaClave());
        app = Servidor.Crear(contexto, b => b.WebHost.UseTestServer());
        app.StartAsync().GetAwaiter().GetResult();
        cliente = app.GetTestClient();
        cliente.BaseAddress = new Uri("http://127.0.0.1/");
    }

    [Fact]
    public async Task SinClaveDa401()
    {
        var r = await cliente.GetAsync("/api/estado");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task ClaveIncorrectaDa401()
    {
        cliente.DefaultRequestHeaders.Add(Servidor.CabeceraClave, "otra");
        var r = await cliente.GetAsync("/api/estado");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task ConClaveResponde()
    {
        cliente.DefaultRequestHeaders.Add(Servidor.CabeceraClave, contexto.Clave);
        var estado = await cliente.GetFromJsonAsync<EstadoApp>("/api/estado");
        Assert.NotNull(estado);
        Assert.False(estado.Configurada);
    }

    [Fact]
    public async Task ClaveEnLaUrlTambienVale()
    {
        var r = await cliente.GetAsync($"/api/estado?clave={contexto.Clave}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task HostAjenoDa400()
    {
        var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/estado");
        pedido.Headers.Host = "pagina-maliciosa.com";
        pedido.Headers.Add(Servidor.CabeceraClave, contexto.Clave);
        var r = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task ElegirCarpetaDeDatosLaPreparaYGuarda()
    {
        cliente.DefaultRequestHeaders.Add(Servidor.CabeceraClave, contexto.Clave);
        var carpeta = tmp["datos"];
        var r = await cliente.PostAsJsonAsync("/api/carpeta-datos", new PedidoCarpeta(carpeta));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.True(CarpetaDatos.EstaLista(carpeta));
        Assert.True(File.Exists(CarpetaDatos.RutaBd(carpeta)));
        Assert.Equal(carpeta, Configuracion.Cargar(tmp["config.json"]).CarpetaDatos);
    }

    [Fact]
    public async Task CarpetaInvalidaDa400()
    {
        cliente.DefaultRequestHeaders.Add(Servidor.CabeceraClave, contexto.Clave);
        var r = await cliente.PostAsJsonAsync("/api/carpeta-datos", new PedidoCarpeta("relativa"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    public void Dispose()
    {
        app.StopAsync().GetAwaiter().GetResult();
        ((IDisposable)app).Dispose();
        RegistroArchivo.Directorio = Path.Combine(Path.GetTempPath(), "otteretro-pruebas", "logs");
        tmp.Dispose();
    }
}
