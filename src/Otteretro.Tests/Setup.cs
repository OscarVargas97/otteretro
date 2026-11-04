using System.Runtime.CompilerServices;
using Otteretro.App;

namespace Otteretro.Tests;

static class Setup
{
    // Ninguna cola de las pruebas lanza la carga de Claude por su cuenta: depende de la hora y del reloj falso compartido.
    [ModuleInitializer]
    internal static void Init()
    {
        TaskQueue.OnOpen = false; // cada prueba lanza la migración, el escaneo o el vigilante cuando los quiere
        Libraries.SwitchTitles.Download = false; // las tablas de TitleID no se bajan en las pruebas: cada una pone su respuesta (#240)
        Claude.Identify.AfterScan = false;
        Catalog.InitialCatalog.BundledFile = null; // cada prueba empieza con el catálogo vacío
        Libraries.Converter.BundledFolder = ""; // sin herramientas reales: la conversión se prueba con una falsa
    }
}
