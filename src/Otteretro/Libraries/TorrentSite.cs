using MonoTorrent;
using MonoTorrent.Client;
using Otteretro.Api;
using Otteretro.App;

namespace Otteretro.Libraries;

/// <summary>
/// Torrents y magnets (#207) con MonoTorrent, en la misma cola. Mientras bajan también comparten, desde tu IP: es como funciona BitTorrent. Al
/// terminar deja de compartir. Pausar lo detiene y al reanudar sigue, porque compara lo que ya está en la carpeta.
/// </summary>
public sealed class TorrentSite : IDownloadSite, IOwnTransfer
{
    /// <summary>Para las pruebas: ajusta el motor y lo que pasa al arrancar, sin salir a internet.</summary>
    public static Action<EngineSettingsBuilder>? Configure { get; set; }
    public static Func<TorrentManager, Task>? OnStarted { get; set; }

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public string Id => "torrent";
    public bool Handles(Uri url) => url.Scheme == "magnet" || (url.Scheme is "http" or "https" && url.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase));

    public Task<ResolvedLink> Resolve(Uri url, Credentials credentials, CancellationToken ct) => Task.FromResult(new ResolvedLink(url.ToString(), Probe: false));

    public async Task<(string? Name, long? Size)> Describe(Uri url, Credentials credentials, CancellationToken ct)
    {
        if (url.Scheme == "magnet")
        {
            var magnet = Magnet(url);
            return (magnet.Name, magnet.Size);
        }
        var torrent = await Torrent.LoadAsync(await Http.GetByteArrayAsync(url, ct));
        return (torrent.Name, torrent.Size);
    }

    static MagnetLink Magnet(Uri url) =>
        MagnetLink.TryParse(url.ToString(), out var magnet) ? magnet! : throw new TaskException(ApiError.Of("download.badLink"));

    public async Task Transfer(Uri url, string folder, Credentials credentials, Action<double, string> progress, CancellationToken ct)
    {
        var cache = Path.Combine(folder, ".torrent");
        var settings = new EngineSettingsBuilder { CacheDirectory = cache, AutoSaveLoadFastResume = true };
        Configure?.Invoke(settings);
        using var engine = new ClientEngine(settings.ToSettings());
        var manager = url.Scheme == "magnet"
            ? await engine.AddAsync(Magnet(url), folder)
            : await engine.AddAsync(await Torrent.LoadAsync(await Http.GetByteArrayAsync(url, ct)), folder);
        await manager.StartAsync();
        if (OnStarted is { } started) await started(manager);
        try
        {
            while (!manager.Complete)
            {
                ct.ThrowIfCancellationRequested();
                if (manager.State == TorrentState.Error) throw new TaskException(ApiError.Of("download.failed", ("file", manager.Name ?? url.ToString())));
                progress(manager.Progress, manager.Name ?? "torrent");
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            // No espera eternamente a los rastreadores.
            await manager.StopAsync(TimeSpan.FromSeconds(10));
        }
        await engine.RemoveAsync(manager);
        await engine.StopAllAsync(TimeSpan.FromSeconds(5));
        if (Directory.Exists(cache)) Directory.Delete(cache, recursive: true);
    }
}
