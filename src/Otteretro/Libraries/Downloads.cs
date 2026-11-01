using System.ComponentModel;
using System.Net;
using System.Text.Json;
using Dapper;
using Downloader;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>El enlace listo para bajar. <paramref name="Blocked"/> dice qué falta: esperar, un captcha o una cuenta.</summary>
public record ResolvedLink(string Url, string? FileName = null, long? Size = null, ApiError? Blocked = null, TimeSpan? Wait = null, Dictionary<string, string>? Headers = null);

/// <summary>Un conector por sitio (#225): sabe convertir un enlace de ese sitio en uno que se puede bajar.</summary>
public interface IDownloadSite
{
    string Id { get; }
    bool Handles(Uri url);
    Task<ResolvedLink> Resolve(Uri url, TaskRun run, CancellationToken ct);
}

/// <summary>Un enlace directo: el archivo está en esa misma dirección.</summary>
public sealed class DirectSite : IDownloadSite
{
    public string Id => "direct";
    public bool Handles(Uri url) => url.Scheme is "http" or "https";
    public Task<ResolvedLink> Resolve(Uri url, TaskRun run, CancellationToken ct) => Task.FromResult(new ResolvedLink(url.ToString()));
}

/// <param name="Name">El paquete: la carpeta donde quedan sus archivos.</param>
/// <param name="DiskId">El disco de destino.</param>
/// <param name="Done">Punto de control: cuántos enlaces ya terminaron.</param>
public record DownloadJob(string Name, string DiskId, List<string> Links, int Done = 0);

/// <param name="Links">Cuántos enlaces trae.</param>
public record DownloadInfo(long Id, string Name, string Disk, int Links, int Done, string Status, int Progress, ApiError? Detail, string CreatedAt);

/// <summary>
/// Descargas (§9.2, #202): cada paquete es una tarea de la cola, así que se pausa, se reanuda y se cancela como cualquiera. Baja a una carpeta
/// temporal del disco y, al terminar, pasa a su `Entrada\`, que descomprime, identifica y ordena.
/// </summary>
public static class Downloads
{
    public const string Kind = "download.package";
    const string Partial = ".descargando";

    /// <summary>Los conectores, del más específico al más general.</summary>
    public static List<IDownloadSite> Sites { get; } = [new DirectSite()];

    /// <summary>Conexiones por archivo (#226): pocas por defecto, para no arriesgar una cuenta.</summary>
    public static int Connections { get; set; } = 2;

    public static IDownloadSite? SiteOf(Uri url) => Sites.FirstOrDefault(s => s.Handles(url));

    public static (long? Task, ApiError? Error) Enqueue(Database db, string name, string? diskId, List<string> links)
    {
        if (links.Count == 0) return (null, ApiError.Of("download.noLinks"));
        if (links.Any(l => !Uri.TryCreate(l, UriKind.Absolute, out var url) || SiteOf(url) is null)) return (null, ApiError.Of("download.badLink"));
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return (null, ApiError.Of("download.badName"));
        var disk = diskId is null ? Disk.DefaultDisk(db) : Disk.Get(db, diskId);
        if (disk is not { Connected: true, Type: "disk" }) return (null, ApiError.Of("library.notConnected"));
        return (TaskQueue.Enqueue(db, Kind, JsonSerializer.Serialize(new DownloadJob(name.Trim(), disk.Id, links), JsonSerializerOptions.Web)), null);
    }

    record Row(long Id, string? Data, string Status, long Progress, string? Detail, string CreatedAt);

    public static List<DownloadInfo> List(Database db)
    {
        using var c = db.Open();
        var disks = Disk.List(db).ToDictionary(d => d.Id, d => d.Name);
        return c.Query<Row>("""
                SELECT id, data, status, progress, detail, created_at AS CreatedAt FROM task WHERE kind = @Kind
                ORDER BY CASE status WHEN 'running' THEN 0 WHEN 'queued' THEN 1 WHEN 'paused' THEN 2 ELSE 3 END, id DESC LIMIT 200
                """, new { Kind })
            .Select(r =>
            {
                var job = JsonSerializer.Deserialize<DownloadJob>(r.Data!, JsonSerializerOptions.Web)!;
                var detail = string.IsNullOrEmpty(r.Detail) ? null : JsonSerializer.Deserialize<ApiError>(r.Detail, JsonSerializerOptions.Web);
                return new DownloadInfo(r.Id, job.Name, disks.GetValueOrDefault(job.DiskId, job.DiskId), job.Links.Count, job.Done, r.Status, (int)r.Progress, detail, r.CreatedAt);
            }).ToList();
    }

    public static async Task Run(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<DownloadJob>(run.Data!, JsonSerializerOptions.Web)!;
        if (Disk.Get(run.Db, job.DiskId) is not { Connected: true } disk) throw new TaskException(ApiError.Of("library.notConnected"));
        RemoveAbandoned(run.Db, disk, run.Id);
        var folder = Directory.CreateDirectory(Path.Combine(disk.Path, Partial, run.Id.ToString())).FullName;
        for (var i = job.Done; i < job.Links.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var url = new Uri(job.Links[i]);
            var site = SiteOf(url) ?? throw new TaskException(ApiError.Of("download.badLink"));
            var link = await site.Resolve(url, run, ct);
            if (link.Wait is { } wait)
            {
                run.Report(i * 100 / job.Links.Count, ApiError.Of("download.waiting", ("seconds", ((int)wait.TotalSeconds).ToString())));
                await Task.Delay(wait, ct);
                link = await site.Resolve(url, run, ct);
            }
            if (link.Blocked is { } blocked) throw new TaskException(blocked);
            await Fetch(run, link, folder, i, job.Links.Count, ct);
            job = job with { Done = i + 1 };
            run.Report((i + 1) * 100 / job.Links.Count, null, JsonSerializer.Serialize(job, JsonSerializerOptions.Web));
        }

        // Terminó: a la Entrada de su disco, que lo ordena (#221).
        var target = Path.Combine(disk.Path, Disk.EntryFolder, job.Name);
        for (var n = 2; Directory.Exists(target); n++) target = Path.Combine(disk.Path, Disk.EntryFolder, $"{job.Name} ({n})");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.Move(folder, target);
        var parent = Path.Combine(disk.Path, Partial);
        if (!Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
        run.Log(ApiError.Of("download.log.done", ("name", job.Name), ("path", target)));
        Import.EnqueueEntry(run.Db, disk);
    }

    /// <summary>Baja un archivo por partes. Con la tarea pausada o cortada, su avance queda en el archivo `.download` y sigue de ahí.</summary>
    static async Task Fetch(TaskRun run, ResolvedLink link, string folder, int index, int total, CancellationToken ct)
    {
        var config = new DownloadConfiguration
        {
            ChunkCount = Connections,
            ParallelDownload = Connections > 1,
            MaxTryAgainOnFailure = 5,
            EnableAutoResumeDownload = true,
            FileExistPolicy = FileExistPolicy.Exception,
            CheckDiskSizeBeforeDownload = true,
            RequestConfiguration = { UserAgent = "Otteretro" },
        };
        foreach (var (key, value) in link.Headers ?? []) config.RequestConfiguration.Headers[key] = value;
        await using var service = new DownloadService(config);

        var name = link.FileName ?? FileNameOf(new Uri(link.Url));
        var last = DateTime.MinValue;
        service.DownloadProgressChanged += (_, e) =>
        {
            if (DateTime.UtcNow - last < TimeSpan.FromSeconds(1)) return;
            last = DateTime.UtcNow;
            run.Report((int)((index + e.ProgressPercentage / 100) * 100 / total),
                ApiError.Of("download.progress", ("file", name), ("speed", ((long)e.AverageBytesPerSecondSpeed).ToString()), ("done", e.ReceivedBytesSize.ToString()), ("total", e.TotalBytesToReceive.ToString())));
        };
        Exception? failure = null;
        service.DownloadFileCompleted += (_, e) => failure = e.Cancelled ? new OperationCanceledException(ct) : e.Error;
        var path = Path.Combine(folder, name);
        // Si ya terminó en una vuelta anterior, no se repite.
        if (File.Exists(path)) return;
        await CheckAvailable(link, name, ct);
        await service.DownloadFileTaskAsync(link.Url, path, ct);
        ct.ThrowIfCancellationRequested();
        if (failure is OperationCanceledException) throw failure;
        if (failure is not null) throw new HttpRequestException(failure.Message, failure);
        if (!File.Exists(path)) throw new TaskException(ApiError.Of("download.failed", ("file", name)));
        if (link.Size is { } size && new FileInfo(path).Length != size) throw new TaskException(ApiError.Of("download.failed", ("file", name)));
    }

    static readonly HttpClient Probe = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Un enlace muerto o prohibido falla enseguida y con su motivo; un corte de red, en cambio, lo reintenta la cola.</summary>
    static async Task CheckAvailable(ResolvedLink link, string name, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, link.Url);
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
        request.Headers.UserAgent.ParseAdd("Otteretro");
        foreach (var (key, value) in link.Headers ?? []) request.Headers.TryAddWithoutValidation(key, value);
        using var response = await Probe.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var code = (int)response.StatusCode;
        if (code is >= 400 and < 500 and not (408 or 416 or 429))
            throw new TaskException(ApiError.Of("download.unavailable", ("file", name), ("status", code.ToString())));
    }

    static string FileNameOf(Uri url)
    {
        var name = WebUtility.UrlDecode(Path.GetFileName(url.LocalPath));
        foreach (var bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
        return name.Length > 0 ? name : "descarga";
    }

    /// <summary>Lo que quedó de descargas canceladas o que fallaron para siempre: no vuelven a usarse.</summary>
    static void RemoveAbandoned(Database db, DiskInfo disk, long current)
    {
        var parent = Path.Combine(disk.Path, Partial);
        if (!Directory.Exists(parent)) return;
        using var c = db.Open();
        var alive = c.Query<long>("SELECT id FROM task WHERE kind = @Kind AND status IN ('queued', 'running', 'paused')", new { Kind }).ToHashSet();
        foreach (var folder in Directory.GetDirectories(parent))
            if (long.TryParse(Path.GetFileName(folder), out var id) && id != current && !alive.Contains(id))
                Directory.Delete(folder, recursive: true);
    }
}
