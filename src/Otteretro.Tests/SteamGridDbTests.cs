using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Otteretro.App;
using Otteretro.Catalog;
using SkiaSharp;

namespace Otteretro.Tests;

/// <summary>Las imágenes guardadas (#64) y cómo las sirve la API. Bajarlas de su referencia se prueba en <see cref="CatalogUpdateTests"/> (#215).</summary>
public class SteamGridDbTests
{
    /// <summary>Un PNG del tamaño pedido; con `transparent`, la mitad izquierda transparente.</summary>
    static byte[] Png(int width, int height, bool transparent = false)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Red);
            if (transparent) canvas.DrawRect(0, 0, width / 2, height, new SKPaint { Color = SKColors.Transparent, BlendMode = SKBlendMode.Src });
        }
        return bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    /// <summary>#64: se reduce al tope de su tipo sin agrandar ni deformar, y el logo conserva la transparencia.</summary>
    [Fact]
    public void ImagesAreSavedAsSmallerWebp()
    {
        var big = Png(1200, 1800);
        var cover = SteamGridDb.ToWebp(big, "cover")!;
        using (var decoded = SKBitmap.Decode(cover))
            Assert.Equal((600, 900), (decoded.Width, decoded.Height));
        Assert.True(cover.Length < big.Length);

        using (var small = SKBitmap.Decode(SteamGridDb.ToWebp(Png(300, 450), "cover")))
            Assert.Equal((300, 450), (small.Width, small.Height));

        using var logo = SKBitmap.Decode(SteamGridDb.ToWebp(Png(1600, 400, transparent: true), "logo"));
        Assert.Equal((800, 200), (logo.Width, logo.Height));
        Assert.Equal(0, logo.GetPixel(10, 100).Alpha);
        Assert.Equal(255, logo.GetPixel(790, 100).Alpha);

        Assert.Null(SteamGridDb.ToWebp([1, 2, 3], "cover"));
    }

    /// <summary>Lo guardado se sirve como imagen; lo que no hay o una ruta rara, 404.</summary>
    [Fact]
    public async Task TheApiServesTheSavedImages()
    {
        await using var f = new Fixture();
        Directory.CreateDirectory(SteamGridDb.Folder(f.Db));
        File.WriteAllBytes(Path.Combine(SteamGridDb.Folder(f.Db), "1-cover.webp"), SteamGridDb.ToWebp(Png(60, 90), "cover")!);

        var app = LocalServer.Create(f.Session, b => b.WebHost.UseTestServer());
        await app.StartAsync();
        var http = app.GetTestClient();
        http.BaseAddress = new Uri("http://127.0.0.1/");
        var cover = await http.GetAsync($"/api/images/1/cover?key={f.Session.Key}");
        Assert.Equal(HttpStatusCode.OK, cover.StatusCode);
        Assert.Equal("image/webp", cover.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/images/3/cover?key={f.Session.Key}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/images/1/..%2F..%2Fdata?key={f.Session.Key}")).StatusCode);
        await app.StopAsync();
    }
}
