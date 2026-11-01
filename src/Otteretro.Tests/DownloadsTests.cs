using System.Net;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#202 contra un servidor HTTP real en este equipo: por partes, retoma después de un corte y termina en `Entrada\`.</summary>
[Collection("Dats.Handler")]
public class DownloadsTests : IAsyncDisposable
{
    readonly HttpListener server = new();
    readonly CancellationTokenSource stop = new();
    readonly byte[] content = Enumerable.Range(0, 5_000_000).Select(i => (byte)(i * 31 % 251)).ToArray();
    readonly string baseUrl;
    int requests;
    volatile int slowUntil;

    public DownloadsTests()
    {
        var port = Random.Shared.Next(41000, 49000);
        baseUrl = $"http://localhost:{port}/";
        server.Prefixes.Add(baseUrl);
        server.Start();
        _ = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try { context = await server.GetContextAsync(); }
                catch { return; }
                _ = Task.Run(() => Serve(context));
            }
        });
    }

    async Task Serve(HttpListenerContext c)
    {
        Interlocked.Increment(ref requests);
        try
        {
            if (c.Request.Url!.AbsolutePath.Contains("pagina"))
            {
                c.Response.ContentType = "text/html";
                await c.Response.OutputStream.WriteAsync("<html></html>"u8.ToArray());
                c.Response.Close();
                return;
            }
            if (c.Request.Url!.AbsolutePath.Contains("no-existe"))
            {
                c.Response.StatusCode = 404;
                c.Response.Close();
                return;
            }
            long from = 0, to = content.Length - 1;
            c.Response.Headers["Accept-Ranges"] = "bytes";
            if (c.Request.Headers["Range"] is { } range)
            {
                var parts = range.Replace("bytes=", "").Split('-');
                from = long.Parse(parts[0]);
                if (parts[1].Length > 0) to = long.Parse(parts[1]);
                c.Response.StatusCode = 206;
                c.Response.Headers["Content-Range"] = $"bytes {from}-{to}/{content.Length}";
            }
            c.Response.ContentLength64 = to - from + 1;
            if (c.Request.HttpMethod != "HEAD")
                for (var at = from; at <= to; at += 64 * 1024)
                {
                    await c.Response.OutputStream.WriteAsync(content.AsMemory((int)at, (int)Math.Min(64 * 1024, to - at + 1)));
                    if (slowUntil > 0) await Task.Delay(slowUntil);
                }
            c.Response.Close();
        }
        catch { try { c.Response.Abort(); } catch { } }
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        server.Close();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task DownloadsAndHandsItToTheEntryFolder()
    {
        await using var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        var (task, error) = Downloads.Enqueue(f.Db, "Mi paquete", disk, [baseUrl + "uno.bin", baseUrl + "dos%20con%20espacio.bin"]);
        Assert.Null(error);

        var done = await f.Run(task!.Value);
        Assert.True(done.Status == "done", System.Text.Json.JsonSerializer.Serialize(done.Detail));

        Assert.Equal(content, File.ReadAllBytes(f.Tmp[@"Otteretro\Entrada\Mi paquete\uno.bin"]));
        Assert.Equal(content, File.ReadAllBytes(f.Tmp[@"Otteretro\Entrada\Mi paquete\dos con espacio.bin"]));
        Assert.False(Directory.Exists(f.Tmp[@"Otteretro\.descargando"]));
        var info = Assert.Single(Downloads.List(f.Db));
        Assert.Equal(("Mi paquete", 2, 2, "done"), (info.Name, info.Links, info.Done, info.Status));
        // Queda encolada la importación de esa Entrada.
        using var c = f.Db.Open();
        Assert.Equal(1, c.ExecuteScalar<long>("SELECT count(*) FROM task WHERE kind = @Kind", new { Import.Kind }));
    }

    /// <summary>Pausar corta la conexión; al reanudar sigue de donde iba y el archivo queda igual.</summary>
    [Fact]
    public async Task ResumesAfterAPause()
    {
        await using var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        slowUntil = 30;
        var id = Downloads.Enqueue(f.Db, "Paquete", disk, [baseUrl + "grande.bin"]).Task!.Value;
        await f.Queue.StartAsync(default);
        for (var i = 0; i < 200 && !Directory.EnumerateFiles(f.Tmp["Otteretro"], "*.download", SearchOption.AllDirectories).Any(); i++) await Task.Delay(50);
        await Task.Delay(300);
        Assert.True(f.Queue.Pause(f.Db, id));
        for (var i = 0; i < 100 && Status(f, id) != "paused"; i++) await Task.Delay(50);
        Assert.Equal("paused", Status(f, id));
        Assert.False(File.Exists(f.Tmp[$@"Otteretro\Entrada\Paquete\grande.bin"]));

        slowUntil = 0;
        var before = requests;
        Assert.True(f.Queue.Resume(f.Db, id));
        for (var i = 0; i < 400 && Status(f, id) != "done"; i++) await Task.Delay(50);
        Assert.Equal("done", Status(f, id));
        Assert.True(requests > before);
        Assert.Equal(content, File.ReadAllBytes(f.Tmp[@"Otteretro\Entrada\Paquete\grande.bin"]));
    }

    static string Status(Fixture f, long id)
    {
        using var c = f.Db.Open();
        return c.ExecuteScalar<string>("SELECT status FROM task WHERE id = @id", new { id })!;
    }

    [Fact]
    public void FindsLinksAndGroupsParts()
    {
        Assert.Equal(["https://a.com/x.part1.rar", "magnet:?xt=urn:btih:abc", "http://b.org/y.zip"],
            Downloads.LinksIn("mira (https://a.com/x.part1.rar), magnet:?xt=urn:btih:abc\nhttps://a.com/x.part1.rar y http://b.org/y.zip."));
        Assert.Equal("Juego", Downloads.PackageOf("Juego.part01.rar"));
        Assert.Equal("Juego", Downloads.PackageOf("Juego.part02.rar"));
        Assert.Equal("Juego", Downloads.PackageOf("Juego.r00"));
        Assert.Equal("Juego", Downloads.PackageOf("Juego.7z.002"));
        Assert.Equal("Otro (USA)", Downloads.PackageOf("Otro (USA).gba"));
    }

    /// <summary>#203: comprueba cada enlace sin bajar nada: en línea y tamaño, enlace muerto, y una página que no es un archivo.</summary>
    [Fact]
    public async Task PreviewChecksEachLink()
    {
        await using var f = new Fixture();
        var text = $"{baseUrl}J.part1.rar {baseUrl}J.part2.rar {baseUrl}no-existe.bin {baseUrl}pagina";
        var packages = await Downloads.Preview(text, f.Credentials, default);
        var parts = packages.Single(p => p.Name == "J");
        Assert.Equal(2, parts.Links.Count);
        Assert.Equal(2L * content.Length, parts.Size);
        Assert.All(parts.Links, l => Assert.Equal("ok", l.State));
        Assert.Equal("unavailable", packages.Single(p => p.Name == "no-existe").Links[0].State);
        Assert.Equal("noConnector", packages.Single(p => p.Name == "pagina").Links[0].State);
        Assert.False(Directory.Exists(f.Tmp[@"Otteretro"]));
    }

    /// <summary>#226: si lo que llega es una página, no se guarda como si fuera el juego.</summary>
    [Fact]
    public async Task APageIsNotDownloadedAsAFile()
    {
        await using var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        var done = await f.Run(Downloads.Enqueue(f.Db, "x", disk, [baseUrl + "pagina"]).Task!.Value);
        Assert.Equal(("error", "download.notDirect"), (done.Status, done.Detail?.Code));
    }

    /// <summary>§9.6: lo bajado desde la ficha de un juego se identifica por ese juego, aunque su nombre no lo diga.</summary>
    [Fact]
    public async Task WhatYouDownloadFromAGameIsThatGame()
    {
        await using var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO game (id, name_es) VALUES (10, 'Metroid Fusion'); INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'gba');");
        Dats.Handler = new NoNetwork();
        try
        {
            Assert.Equal("done", (await f.Run(Downloads.Enqueue(f.Db, "paquete raro", disk, [baseUrl + "cualquiera.gba"], 10).Task!.Value)).Status);
            long import;
            using (var c = f.Db.Open()) import = c.ExecuteScalar<long>("SELECT id FROM task WHERE kind = @Kind", new { Import.Kind });
            Assert.Equal("done", (await f.Run(import)).Status);
        }
        finally { Dats.Handler = null; }
        Assert.True(File.Exists(f.Tmp[@"Otteretro\Juegos\Game Boy Advance\Metroid Fusion\cualquiera.gba"]));
        Assert.False(Directory.Exists(f.Tmp[@"Otteretro\Entrada\paquete raro"]));
    }

    sealed class OwnSite : IDownloadSite, IOwnTransfer
    {
        public string Id => "propio";
        public bool Handles(Uri url) => url.Host == "propio.test";
        public Task<ResolvedLink> Resolve(Uri url, App.Credentials c, CancellationToken ct) => throw new InvalidOperationException();
        public Task<(string? Name, long? Size)> Describe(Uri url, App.Credentials c, CancellationToken ct) => Task.FromResult<(string?, long?)>(("juego.gba", 3));
        public Task Transfer(Uri url, string folder, App.Credentials c, Action<double, string> progress, CancellationToken ct)
        {
            progress(50, "juego.gba");
            File.WriteAllText(Path.Combine(folder, "juego.gba"), "rom");
            return Task.CompletedTask;
        }
    }

    /// <summary>#205, #207: los sitios que transfieren por su cuenta pasan por la misma cola y terminan igual en `Entrada\\`.</summary>
    [Fact]
    public async Task ASiteWithItsOwnTransferEndsTheSameWay()
    {
        await using var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        var site = new OwnSite();
        Downloads.Sites.Insert(0, site);
        try
        {
            var preview = Assert.Single(await Downloads.Preview("https://propio.test/x", f.Credentials, default));
            Assert.Equal(("juego", 3L), (preview.Name, preview.Size));
            var done = await f.Run(Downloads.Enqueue(f.Db, "P", disk, ["https://propio.test/x"]).Task!.Value);
            Assert.Equal("done", done.Status);
        }
        finally { Downloads.Sites.Remove(site); }
        Assert.Equal("rom", File.ReadAllText(f.Tmp[@"Otteretro\Entrada\P\juego.gba"]));
    }

    sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new HttpRequestException("sin red");
    }

    [Fact]
    public async Task RejectsWhatItCannotDownload()
    {
        await using var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        Assert.Equal("download.noLinks", Downloads.Enqueue(f.Db, "x", disk, []).Error?.Code);
        Assert.Equal("download.badLink", Downloads.Enqueue(f.Db, "x", disk, ["ftp://sitio/archivo"]).Error?.Code);
        Assert.Equal("download.badName", Downloads.Enqueue(f.Db, "a/b", disk, [baseUrl]).Error?.Code);
    }

    [Fact]
    public async Task AFailedLinkEndsInError()
    {
        await using var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        var done = await f.Run(Downloads.Enqueue(f.Db, "x", disk, [baseUrl + "no-existe.bin"]).Task!.Value);
        Assert.Equal("error", done.Status);
        Assert.False(Directory.Exists(f.Tmp[@"Otteretro\Entrada\x"]));
    }
}
