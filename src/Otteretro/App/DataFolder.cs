namespace Otteretro.Inicio;

/// <summary>La carpeta de datos que elige el usuario: base de datos, Herramientas, caché y logs (P1, S7).</summary>
public static class CarpetaDatos
{
    public static readonly string[] Subcarpetas = ["data", Path.Combine("cache", "imagenes"), "Herramientas", "logs"];

    /// <summary>Crea la carpeta y sus subcarpetas. Devuelve un mensaje de error o null si quedó lista.</summary>
    public static string? Preparar(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta) || !Path.IsPathFullyQualified(ruta))
            return "La ruta tiene que ser completa, por ejemplo E:\\Otteretro.";
        if (File.Exists(ruta))
            return "Esa ruta es un archivo, no una carpeta.";
        try
        {
            foreach (var sub in Subcarpetas)
                Directory.CreateDirectory(Path.Combine(ruta, sub));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"No se pudo crear la carpeta: {e.Message}";
        }
        return null;
    }

    public static bool EstaLista(string? ruta) =>
        ruta is not null && Subcarpetas.All(sub => Directory.Exists(Path.Combine(ruta, sub)));

    public static string RutaBd(string carpeta) => Path.Combine(carpeta, "data", "otteretro.db");
    public static string RutaLogs(string carpeta) => Path.Combine(carpeta, "logs");
}
