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
        server.Close();
        var delays = App.TaskQueue.RetryDelays;
        App.TaskQueue.RetryDelays = [];
        try
        {
            var done = await f.Run(Downloads.Enqueue(f.Db, "x", disk, [baseUrl + "a.bin"]).Task!.Value);
            Assert.NotEqual("done", done.Status);
        }
        finally { App.TaskQueue.RetryDelays = delays; }
        Assert.False(Directory.Exists(f.Tmp[@"Otteretro\Entrada\x"]));
    }
}
