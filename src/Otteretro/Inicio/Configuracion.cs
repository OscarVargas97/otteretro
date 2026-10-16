using System.Text.Json;

namespace Otteretro.Inicio;

/// <summary>
/// Lo único que vive fuera de la carpeta de datos (en %LocalAppData%\Otteretro\config.json):
/// dónde está esa carpeta, de dónde llegan las actualizaciones y la identidad de este equipo.
/// </summary>
public sealed class Configuracion
{
    public string? CarpetaDatos { get; set; }
    public string? OrigenActualizaciones { get; set; }
    public string EquipoId { get; set; } = Guid.NewGuid().ToString();

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>OTTERETRO_CONFIG permite usar otra configuración (desarrollo y pruebas) sin tocar la real.</summary>
    public static string RutaPorDefecto => Environment.GetEnvironmentVariable("OTTERETRO_CONFIG") ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Otteretro", "config.json");

    public static Configuracion Cargar(string ruta)
    {
        if (File.Exists(ruta))
            return JsonSerializer.Deserialize<Configuracion>(File.ReadAllText(ruta), Json) ?? new();
        var nueva = new Configuracion();
        nueva.Guardar(ruta);
        return nueva;
    }

    public void Guardar(string ruta)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        // Escribir aparte y reemplazar: un corte a mitad nunca deja config.json roto.
        var temporal = ruta + ".tmp";
        File.WriteAllText(temporal, JsonSerializer.Serialize(this, Json));
        File.Move(temporal, ruta, overwrite: true);
    }
}
