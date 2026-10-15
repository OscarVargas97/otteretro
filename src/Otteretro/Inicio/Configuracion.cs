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

    public static string RutaPorDefecto => Path.Combine(
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
        File.WriteAllText(ruta, JsonSerializer.Serialize(this, Json));
    }
}
