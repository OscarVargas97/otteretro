using Dapper;
using Otteretro.Datos;
using Otteretro.Inicio;

namespace Otteretro.Tests;

public class MudanzaTests
{
    /// <summary>Una carpeta de datos con base de datos real y un archivo en Herramientas.</summary>
    static string CrearCarpeta(Temporal tmp)
    {
        var origen = tmp["origen"];
        CarpetaDatos.Preparar(origen);
        var bd = new Bd(CarpetaDatos.RutaBd(origen));
        Migraciones.Aplicar(bd);
        Migraciones.RegistrarEquipo(bd, "pc-prueba");
        File.WriteAllBytes(Path.Combine(origen, "Herramientas", "emulador.bin"), Enumerable.Range(0, 100_000).Select(i => (byte)i).ToArray());
        return origen;
    }

    [Fact]
    public void CopiaVerificaYConservaLaBaseDeDatos()
    {
        using var tmp = new Temporal();
        var origen = CrearCarpeta(tmp);
        var destino = tmp["destino"];
        var pasos = new List<string>();

        Mudanza.CopiarYVerificar(origen, destino, p => pasos.Add(p.Paso), copiar: true);

        Assert.Contains("Copiando", pasos);
        Assert.Contains("Verificando", pasos);
        Assert.True(CarpetaDatos.EstaLista(destino));
        Assert.Equal(File.ReadAllBytes(Path.Combine(origen, "Herramientas", "emulador.bin")),
                     File.ReadAllBytes(Path.Combine(destino, "Herramientas", "emulador.bin")));
        using (var c = new Bd(CarpetaDatos.RutaBd(destino)).Abrir())
            Assert.Equal("pc-prueba", c.ExecuteScalar<string>("SELECT id FROM equipo"));
        Assert.True(Directory.Exists(origen)); // el origen no se toca hasta BorrarOrigen

        Assert.Null(Mudanza.BorrarOrigen(origen));
        Assert.False(Directory.Exists(origen));
    }

    [Fact]
    public void EnElMismoDiscoMueveSinCopiar()
    {
        using var tmp = new Temporal();
        var origen = CrearCarpeta(tmp);
        var destino = tmp["otra\\carpeta"];

        Mudanza.CopiarYVerificar(origen, destino, _ => { });

        Assert.False(Directory.Exists(origen));
        Assert.True(CarpetaDatos.EstaLista(destino));
    }

    [Fact]
    public async Task DosMudanzasALaVezSoloArrancaUna()
    {
        using var tmp = new Temporal();
        var origen = CrearCarpeta(tmp);
        var destino = tmp["destino"];
        var config = new Configuracion { CarpetaDatos = origen };
        using var eventos = new Otteretro.Api.Eventos();
        var servicio = new ServicioMudanza(new ContextoApp(config, tmp["config.json"], "clave"), eventos);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => servicio.Iniciar(destino))));
        for (var i = 0; i < 200 && servicio.Estado.EnCurso; i++) await Task.Delay(50);

        Assert.Single(respuestas, r => r is null);
        Assert.Equal("Listo", servicio.Estado.Paso);
        Assert.True(CarpetaDatos.EstaLista(destino));
        Assert.Equal(destino, Configuracion.Cargar(tmp["config.json"]).CarpetaDatos);
    }

    [Fact]
    public void DeshacerUnaCopiaSoloBorraLoCopiado()
    {
        using var tmp = new Temporal();
        var origen = CrearCarpeta(tmp);
        var destino = tmp["destino"];
        Mudanza.CopiarYVerificar(origen, destino, _ => { }, copiar: true);
        File.WriteAllText(Path.Combine(destino, "ajeno.txt"), "de otra persona");

        Mudanza.Deshacer(origen, destino);

        Assert.True(CarpetaDatos.EstaLista(origen));
        Assert.Equal(["ajeno.txt"], Directory.EnumerateFileSystemEntries(destino).Select(Path.GetFileName));
    }

    [Fact]
    public void DeshacerUnMovimientoDevuelveLaCarpeta()
    {
        using var tmp = new Temporal();
        var origen = CrearCarpeta(tmp);
        var destino = tmp["otra\\carpeta"];
        Mudanza.CopiarYVerificar(origen, destino, _ => { });

        Mudanza.Deshacer(origen, destino);

        Assert.True(CarpetaDatos.EstaLista(origen));
        Assert.False(Directory.Exists(destino));
    }

    [Fact]
    public void AceptaUnaCarpetaVaciaQueYaExiste()
    {
        using var tmp = new Temporal();
        var origen = CrearCarpeta(tmp);
        Directory.CreateDirectory(tmp["vacia"]);
        Assert.Null(Mudanza.Validar(origen, tmp["vacia"]));
    }

    [Fact]
    public void RechazaDestinosPeligrosos()
    {
        using var tmp = new Temporal();
        var origen = CrearCarpeta(tmp);
        var ocupada = tmp["ocupada"];
        Directory.CreateDirectory(ocupada);
        File.WriteAllText(Path.Combine(ocupada, "algo.txt"), "x");

        Assert.NotNull(Mudanza.Validar(origen, origen));                              // la misma
        Assert.NotNull(Mudanza.Validar(origen, origen + "\\"));                       // la misma, con barra final
        Assert.NotNull(Mudanza.Validar(origen, Path.Combine(origen, "dentro")));      // dentro de la actual
        Assert.NotNull(Mudanza.Validar(origen, ocupada));                             // con contenido
        Assert.NotNull(Mudanza.Validar(origen, "relativa"));                          // incompleta
        Assert.NotNull(Mudanza.Validar(origen, Path.Combine(ocupada, "algo.txt")));   // un archivo
    }
}
