using Otteretro.Api;

namespace Otteretro.App;

/// <summary>La carpeta de datos que elige el usuario: base de datos, Herramientas, caché y logs (P1, S7).</summary>
public static class DataFolder
{
    // Los nombres de las subcarpetas son datos del usuario: no se traducen.
    public static readonly string[] Subfolders = ["data", Path.Combine("cache", "imagenes"), "Herramientas", "logs"];

    /// <summary>Crea la carpeta y sus subcarpetas. Devuelve el error o null si quedó lista.</summary>
    public static ApiError? Prepare(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            return ApiError.Of("path.notAbsolute");
        if (File.Exists(path))
            return ApiError.Of("path.isFile");
        try
        {
            foreach (var sub in Subfolders)
                Directory.CreateDirectory(Path.Combine(path, sub));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ApiError.Of("folder.createFailed", ("detail", e.Message));
        }
        return null;
    }

    public static bool IsReady(string? path) =>
        path is not null && Subfolders.All(sub => Directory.Exists(Path.Combine(path, sub)));

    public static string DatabasePath(string folder) => Path.Combine(folder, "data", "otteretro.db");
    public static string LogsPath(string folder) => Path.Combine(folder, "logs");
}
