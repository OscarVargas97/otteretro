using Otteretro.Inicio;

namespace Otteretro.Tests;

public class ModosTests
{
    [Fact]
    public void VersionDevuelveCero()
    {
        var salida = new StringWriter();
        Assert.Equal(0, ModoCli.Ejecutar(["version"], salida));
        Assert.Equal(Arranque.Version, salida.ToString().Trim());
    }

    [Fact]
    public void ComandoDesconocidoDevuelveUnoYMuestraAyuda()
    {
        var salida = new StringWriter();
        Assert.Equal(1, ModoCli.Ejecutar(["volar"], salida));
        Assert.Contains("Uso: otteretro", salida.ToString());
    }

    [Fact]
    public void JugarTodaviaNoEstaImplementado() =>
        Assert.Equal(ModoJugar.NoImplementado, ModoJugar.Ejecutar(["123"]));

    [Fact]
    public void OpenApiDescribeLaApi()
    {
        using var tmp = new Temporal();
        var archivo = tmp["openapi.json"];
        Assert.Equal(0, ModoCli.Ejecutar(["openapi", archivo], new StringWriter()));
        var json = File.ReadAllText(archivo);
        Assert.Contains("/api/estado", json);
        Assert.Contains("/api/carpeta-datos", json);
        Assert.DoesNotContain("/api/eventos", json);
    }
}
