using System.Runtime.CompilerServices;
using Otteretro.App;

namespace Otteretro.Tests;

static class Setup
{
    // Ninguna cola de las pruebas lanza la carga de Claude por su cuenta: depende de la hora y del reloj falso compartido.
    [ModuleInitializer]
    internal static void Init()
    {
        TaskQueue.AutoClaude = false;
        Otteretro.Libraries.DiskTasks.MigrateOnOpen = false; // cada prueba lanza la migración cuando la quiere
        Claude.Identify.AfterScan = false;
    }
}
