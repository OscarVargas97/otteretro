using Otteretro.Data;
using SkiaSharp;

namespace Otteretro.Catalog;

/// <summary>
/// Las imágenes guardadas en `catalogo/imagenes/{juego}-{tipo}.{ext}` (#167), en WebP y al tamaño que muestra la app (#64).
/// Las baja <see cref="ImageRefs"/> desde la dirección que publica el catálogo (#215): Otteretro no busca ni baja portadas por su cuenta (decisión 258).
/// </summary>
public static class SteamGridDb
{
    public static readonly string[] Kinds = ["cover", "hero", "logo"];

    /// <summary>Tope de cada tipo (#64): lo que muestra la app. Se reduce sin agrandar y sin cambiar la proporción.</summary>
    static readonly Dictionary<string, (int Width, int Height)> MaxSize = new()
    {
        ["cover"] = (600, 900),
        ["hero"] = (1920, 620),
        ["logo"] = (800, int.MaxValue),
    };

    public static string Folder(Database db) => Path.Combine(Path.GetDirectoryName(db.CatalogPath)!, "imagenes"); // van con el catálogo (#167)

    /// <summary>La imagen guardada de un juego, o null si todavía no hay.</summary>
    public static string? Find(Database db, long gameId, string kind) =>
        Directory.Exists(Folder(db)) ? Directory.EnumerateFiles(Folder(db), $"{gameId}-{kind}.*").FirstOrDefault() : null;

    /// <summary>Escribe a un temporal y lo renombra al final: un corte no deja una imagen rota.</summary>
    internal static void Write(string path, byte[] bytes)
    {
        var temp = Path.Combine(Path.GetDirectoryName(path)!, "." + Path.GetFileName(path) + ".part");
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>La imagen leída, o null si no es una imagen (SKBitmap.Decode lanza en vez de devolver null).</summary>
    static SKBitmap? Read(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        return codec is null ? null : SKBitmap.Decode(codec);
    }

    /// <summary>La imagen en WebP (calidad 80) y dentro del tope de su tipo, o null si no es una imagen que se pueda leer.</summary>
    public static byte[]? ToWebp(byte[] bytes, string kind)
    {
        using var source = Read(bytes);
        if (source is null) return null;
        var (width, height) = MaxSize[kind];
        var scale = Math.Min(1, Math.Min((double)width / source.Width, (double)height / source.Height));
        using var resized = scale < 1
            ? source.Resize(new SKImageInfo(Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)), SKColorType.Rgba8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
            : null;
        using var image = SKImage.FromBitmap(resized ?? source);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 80);
        return data?.ToArray();
    }
}
