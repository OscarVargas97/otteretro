using System.IO.Pipes;

namespace Otteretro.Inicio;

/// <summary>
/// Solo una ventana de Otteretro a la vez. Si se abre otra, avisa a la que ya existe
/// (por una tubería con nombre) para que se traiga al frente, y termina.
/// </summary>
public sealed class UnaInstancia : IDisposable
{
    const string Nombre = "Otteretro-instancia";
    readonly Mutex mutex;

    /// <summary>Se dispara (en un hilo de fondo) cuando otra instancia pide traer la ventana al frente.</summary>
    public event Action? Activar;

    UnaInstancia(Mutex mutex) => this.mutex = mutex;

    public static UnaInstancia? Tomar()
    {
        var mutex = new Mutex(true, @"Local\" + Nombre, out var esNueva);
        if (!esNueva)
        {
            mutex.Dispose();
            return null;
        }
        var instancia = new UnaInstancia(mutex);
        instancia.Escuchar();
        return instancia;
    }

    void Escuchar() => Task.Run(async () =>
    {
        while (true)
        {
            await using var tuberia = new NamedPipeServerStream(Nombre, PipeDirection.In);
            await tuberia.WaitForConnectionAsync();
            Activar?.Invoke();
        }
    });

    public static void AvisarALaQueExiste()
    {
        try
        {
            using var tuberia = new NamedPipeClientStream(".", Nombre, PipeDirection.Out);
            tuberia.Connect(2000);
        }
        catch (TimeoutException) { }
    }

    public void Dispose()
    {
        mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
