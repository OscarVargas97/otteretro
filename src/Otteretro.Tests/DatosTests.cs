using Dapper;
using Otteretro.Datos;
using Otteretro.Inicio;

namespace Otteretro.Tests;

public class DatosTests
{
    [Fact]
    public void MigracionesSonIdempotentes()
    {
        using var tmp = new Temporal();
        var bd = new Bd(tmp["otteretro.db"]);
        Assert.True(Migraciones.Aplicar(bd) > 0);
        Assert.Equal(0, Migraciones.Aplicar(bd));

        using var c = bd.Abrir();
        Assert.Equal("wal", c.ExecuteScalar<string>("PRAGMA journal_mode"));
        Assert.Equal(1, c.ExecuteScalar<int>("SELECT count(*) FROM sqlite_master WHERE name = 'equipo'"));
    }

    [Fact]
    public void RegistrarEquipoDosVecesNoDuplica()
    {
        using var tmp = new Temporal();
        var bd = new Bd(tmp["otteretro.db"]);
        Migraciones.Aplicar(bd);
        Migraciones.RegistrarEquipo(bd, "pc-1");
        Migraciones.RegistrarEquipo(bd, "pc-1");
        using var c = bd.Abrir();
        Assert.Equal(1, c.ExecuteScalar<int>("SELECT count(*) FROM equipo"));
    }

    [Fact]
    public void CarpetaDeDatosCreaSusSubcarpetas()
    {
        using var tmp = new Temporal();
        var ruta = tmp["Otteretro"];
        Assert.Null(CarpetaDatos.Preparar(ruta));
        Assert.True(CarpetaDatos.EstaLista(ruta));
        Assert.True(Directory.Exists(Path.Combine(ruta, "cache", "imagenes")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relativa\\carpeta")]
    public void CarpetaDeDatosRechazaRutasIncompletas(string? ruta) =>
        Assert.NotNull(CarpetaDatos.Preparar(ruta));

    [Fact]
    public void CarpetaDeDatosRechazaUnArchivo()
    {
        using var tmp = new Temporal();
        var archivo = tmp["archivo.txt"];
        File.WriteAllText(archivo, "x");
        Assert.NotNull(CarpetaDatos.Preparar(archivo));
    }

    [Fact]
    public void ConfiguracionSeGuardaYSeLee()
    {
        using var tmp = new Temporal();
        var ruta = tmp["config.json"];
        var c = Configuracion.Cargar(ruta);
        c.CarpetaDatos = "E:\\Otteretro";
        c.Guardar(ruta);
        var leida = Configuracion.Cargar(ruta);
        Assert.Equal("E:\\Otteretro", leida.CarpetaDatos);
        Assert.Equal(c.EquipoId, leida.EquipoId);
    }
}
