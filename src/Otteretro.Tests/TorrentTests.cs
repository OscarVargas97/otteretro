using System.Net;
using MonoTorrent;
using MonoTorrent.Client;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#207 con torrents de verdad entre dos clientes en este equipo, sin salir a internet.</summary>
[Collection("Dats.Handler")]
public class TorrentTests
{
    [Fact]
    public void RecognizesMagnetsAndTorrentFiles()
    {
        Assert.Equal("torrent", Downloads.SiteOf(new Uri("magnet:?xt=urn:btih:0123456789012345678901234567890123456789&dn=Juego"))!.Id);
        Assert.Equal("torrent", Downloads.SiteOf(new Uri("https://sitio.com/juego.torrent"))!.Id);
        Assert.Equal("direct", Downloads.SiteOf(new Uri("https://sitio.com/juego.zip"))!.Id);
    }

    [Fact]
    public async Task AMagnetIsDescribedWithoutDownloading()
    {
        await using var f = new Fixture();
        var (name, _) = await new TorrentSite().Describe(new Uri("magnet:?xt=urn:btih:0123456789012345678901234567890123456789&dn=Juego"), f.Credentials, default);
        Assert.Equal("Juego", name);
        var preview = Assert.Single(await Downloads.Preview("magnet:?xt=urn:btih:0123456789012345678901234567890123456789&dn=Juego", f.Credentials, default));
        Assert.Equal("ok", preview.Links[0].State);
    }

    [Fact]
    public async Task DownloadsAMagnetFromAnotherClientAndEndsInTheEntryFolder()
    {
        await using var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");

        // El que comparte: un archivo real, en su propio puerto.
        var content = Enumerable.Range(0, 2_000_000).Select(i => (byte)(i * 13 % 251)).ToArray();
        var seedFolder = Directory.CreateDirectory(f.Tmp["semilla"]).FullName;
        await File.WriteAllBytesAsync(Path.Combine(seedFolder, "juego.gba"), content);
        var creator = new TorrentCreator();
        var torrent = Torrent.Load(await creator.CreateAsync(new TorrentFileSource(Path.Combine(seedFolder, "juego.gba"))));
        var port = Random.Shared.Next(42000, 50000);
        using var seeder = new ClientEngine(Local(new EngineSettingsBuilder { CacheDirectory = f.Tmp["cache-semilla"] }, port).ToSettings());
        var seeding = await seeder.AddAsync(torrent, seedFolder);
        await seeding.StartAsync();
        await seeding.WaitForMetadataAsync();
        for (var i = 0; i < 100 && seeding.State != TorrentState.Seeding; i++) await Task.Delay(100);
        Assert.Equal(TorrentState.Seeding, seeding.State);

        // El que baja: Otteretro, que se conecta directo a ese cliente.
        TorrentSite.Configure = s => Local(s, port + 1);
        TorrentSite.OnStarted = m => m.AddPeerAsync(new PeerInfo(new Uri($"ipv4://127.0.0.1:{port}")));
        try
        {
            var magnet = $"magnet:?xt=urn:btih:{torrent.InfoHashes.V1!.ToHex()}&dn=juego.gba";
            var done = await f.Run(Downloads.Enqueue(f.Db, "Torrent", disk, [magnet]).Task!.Value);
            Assert.True(done.Status == "done", System.Text.Json.JsonSerializer.Serialize(done.Detail));
        }
        finally
        {
            TorrentSite.Configure = null;
            TorrentSite.OnStarted = null;
            await seeding.StopAsync(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(content, await File.ReadAllBytesAsync(f.Tmp[@"Otteretro\Entrada\Torrent\juego.gba"]));
        Assert.False(Directory.Exists(f.Tmp[@"Otteretro\Entrada\Torrent\.torrent"]));
    }

    /// <summary>Sin rastreadores ni DHT ni UPnP: solo en este equipo.</summary>
    static EngineSettingsBuilder Local(EngineSettingsBuilder s, int port)
    {
        s.AllowLocalPeerDiscovery = false;
        s.AllowPortForwarding = false;
        s.DhtEndPoint = null;
        s.ListenEndPoints = new() { ["ipv4"] = new IPEndPoint(IPAddress.Loopback, port) };
        return s;
    }
}
