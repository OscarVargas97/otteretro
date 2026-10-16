namespace Otteretro.Inicio;

/// <summary>Registro de errores en archivo: uno por día, se guardan 30 días (A5).</summary>
public sealed class RegistroArchivo : ILoggerProvider
{
    /// <summary>Hasta elegir la carpeta de datos, los logs van junto a config.json.</summary>
    public static string Directorio { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Otteretro", "logs");

    static readonly Lock Cerrojo = new();

    public ILogger CreateLogger(string categoria) => new Registrador(categoria);
    public void Dispose() { }

    public static void Escribir(LogLevel nivel, string categoria, string mensaje)
    {
        lock (Cerrojo)
        {
            Directory.CreateDirectory(Directorio);
            File.AppendAllText(Path.Combine(Directorio, $"otteretro-{DateTime.Now:yyyy-MM-dd}.log"),
                $"{DateTime.Now:HH:mm:ss.fff} [{nivel}] {categoria}: {mensaje}{Environment.NewLine}");
        }
    }

    public static void Limpiar(int dias = 30)
    {
        if (!Directory.Exists(Directorio)) return;
        foreach (var archivo in Directory.EnumerateFiles(Directorio, "otteretro-*.log"))
            if (File.GetLastWriteTime(archivo) < DateTime.Now.AddDays(-dias))
                File.Delete(archivo);
    }

    sealed class Registrador(string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Lo de ASP.NET solo a partir de avisos; lo nuestro desde información.
        public bool IsEnabled(LogLevel nivel) =>
            nivel >= (categoria.StartsWith("Microsoft", StringComparison.Ordinal) ? LogLevel.Warning : LogLevel.Information);

        public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? error, Func<TState, Exception?, string> formato)
        {
            if (!IsEnabled(nivel)) return;
            Escribir(nivel, categoria, formato(estado, error) + (error is null ? "" : Environment.NewLine + error));
        }
    }
}
