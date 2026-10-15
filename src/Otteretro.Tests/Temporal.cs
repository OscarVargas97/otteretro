namespace Otteretro.Tests;

/// <summary>Carpeta temporal que se borra al terminar la prueba.</summary>
sealed class Temporal : IDisposable
{
    public string Ruta { get; } = Path.Combine(Path.GetTempPath(), "otteretro-pruebas", Guid.NewGuid().ToString("N"));

    public Temporal() => Directory.CreateDirectory(Ruta);

    public string this[string relativa] => Path.Combine(Ruta, relativa);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(Ruta, true); } catch (IOException) { }
    }
}
