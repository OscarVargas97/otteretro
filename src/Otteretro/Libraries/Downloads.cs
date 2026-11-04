using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Downloader;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>El enlace listo para bajar. <paramref name="Blocked"/> dice qué falta: esperar, un captcha o una cuenta.</summary>
/// <param name="Probe">False si el sitio ya dijo nombre y tamaño, o si el enlace sirve una sola vez: no se gasta pidiéndolo antes.</param>
public record ResolvedLink(string Url, string? FileName = null, long? Size = null, ApiError? Blocked = null, TimeSpan? Wait = null, Dictionary<string, string>? Headers = null, bool Probe = true);

/// <summary>Un conector por sitio (#225): sabe convertir un enlace de ese sitio en uno que se puede bajar.</summary>
public interface IDownloadSite
{
    string Id { get; }
    /// <summary>El tope de conexiones por archivo de este sitio (#226), por encima de lo que elijas.</summary>
    int MaxConnections => 8;

    /// <summary>Cuántas descargas de este sitio pueden ir a la vez con tu cuenta (#202, decisión 248). Lo que elijas queda por debajo.</summary>
    int MaxParallel(Credentials credentials) => 8;

    /// <summary>Nombre (vacío si no se sabe), tamaño y estado, sin pedir un enlace de descarga. Null: el sitio no tiene cómo, y se comprueba resolviendo.</summary>
    Task<LinkCheck?> Inspect(Uri url, Credentials credentials, CancellationToken ct) => Task.FromResult<LinkCheck?>(null);

    bool Handles(Uri url);
    Task<ResolvedLink> Resolve(Uri url, Credentials credentials, CancellationToken ct);
}

/// <summary>Un sitio que transfiere por su cuenta, sin un enlace directo: Mega descifra, y un torrent habla con otros equipos.</summary>
public interface IOwnTransfer
{
    /// <summary>Nombre y tamaño, si se saben sin bajar nada.</summary>
    Task<(string? Name, long? Size)> Describe(Uri url, Credentials credentials, CancellationToken ct);

    /// <param name="progress">Porcentaje de este enlace y archivo en curso.</param>
    Task Transfer(Uri url, string folder, Credentials credentials, Action<double, string> progress, CancellationToken ct);
}

/// <summary>Un enlace directo: el archivo está en esa misma dirección.</summary>
public sealed class DirectSite : IDownloadSite
{
    public string Id => "direct";
    public bool Handles(Uri url) => url.Scheme is "http" or "https";
    public Task<ResolvedLink> Resolve(Uri url, Credentials credentials, CancellationToken ct) => Task.FromResult(new ResolvedLink(url.ToString()));
}

/// <param name="Name">El paquete: la carpeta donde quedan sus archivos.</param>
/// <param name="DiskId">El disco de destino.</param>
/// <param name="Done">Punto de control: cuántos enlaces ya terminaron.</param>
/// <param name="GameId">El juego desde cuya ficha se pegó: es lo primero que identifica lo que baja (§9.6).</param>
/// <param name="Connections">Las que elegiste; cada sitio tiene su tope.</param>
/// <param name="Files">Por enlace: el archivo que dejó en la carpeta temporal.</param>
/// <param name="Sizes">Por enlace: el tamaño esperado, si el sitio lo dijo.</param>
/// <param name="Checks">Por enlace: cómo se verificó (crc32, md5, sha1, sha256, sha512, size o site); null, sin verificar.</param>
/// <param name="Retries">Por enlace: cuántas veces llegó dañado.</param>
public record DownloadJob(string Name, string DiskId, List<string> Links, int Done = 0, long? GameId = null, int Connections = Downloads.DefaultConnections,
    List<string?>? Files = null, List<long?>? Sizes = null, List<string?>? Checks = null, List<int>? Retries = null);

/// <param name="State">ok, unavailable, blocked (necesita una cuenta o un captcha), noConnector o unknown.</param>
/// <param name="Detail">Por qué está bloqueado: lo que falta o lo que respondió el sitio.</param>
public record LinkCheck(string Url, string Name, long? Size, string? Site, string State, ApiError? Detail = null);

public record PackagePreview(string Name, List<LinkCheck> Links, long Size);

/// <summary>Un enlace de una descarga, en orden: los primeros `Done` ya bajaron y el siguiente es el que está en curso.</summary>
/// <param name="Check">Cómo se verificó; null, sin verificar.</param>
public record DownloadItem(string Url, string Name, long? Size, string? Check);

/// <param name="Max">El tope del sitio con tu cuenta.</param>
/// <param name="Value">Las que elegiste, dentro del tope.</param>
public record SiteLimit(string Site, int Max, int Value);

/// <param name="Links">Cuántos enlaces trae.</param>
public record DownloadInfo(long Id, string Name, string Disk, int Links, int Done, string Status, int Progress, ApiError? Detail, string CreatedAt, List<DownloadItem> Items);

/// <summary>
/// Descargas (§9.2, #202): cada paquete es una tarea de la cola, así que se pausa, se reanuda y se cancela como cualquiera. Baja a una carpeta
/// temporal del disco y, al terminar, pasa a su `Entrada\`, que descomprime, identifica y ordena.
/// </summary>
public static partial class Downloads
{
    public const string Kind = "download.package";
    const string Partial = ".descargando";

    /// <summary>Los conectores, del más específico al más general.</summary>
    public static List<IDownloadSite> Sites { get; } = [new OneFichierSite(), new MegaSite(), new MediaFireSite(), new TorrentSite(), new DirectSite()];

    /// <summary>Conexiones por archivo (#226): pocas por defecto, para no arriesgar una cuenta.</summary>
    public const int DefaultConnections = 2;

    public static IDownloadSite? SiteOf(Uri url) => Sites.FirstOrDefault(s => s.Handles(url));

    public static (long? Task, ApiError? Error) Enqueue(Database db, string name, string? diskId, List<string> links, long? gameId = null, int connections = DefaultConnections)
    {
        if (links.Count == 0) return (null, ApiError.Of("download.noLinks"));
        if (links.Any(l => !Uri.TryCreate(l, UriKind.Absolute, out var url) || SiteOf(url) is null)) return (null, ApiError.Of("download.badLink"));
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return (null, ApiError.Of("download.badName"));
        var disk = diskId is null ? Disk.DefaultDisk(db) : Disk.Get(db, diskId);
        if (disk is not { Connected: true, Type: "disk" }) return (null, ApiError.Of("library.notConnected"));
        return (TaskQueue.Enqueue(db, Kind, JsonSerializer.Serialize(new DownloadJob(name.Trim(), disk.Id, links, 0, gameId, connections), JsonSerializerOptions.Web)), null);
    }

    record Row(long Id, string? Data, string Status, long Progress, string? Detail, string CreatedAt);

    public static List<DownloadInfo> List(Database db)
    {
        using var c = db.Open();
        var disks = Disk.List(db).ToDictionary(d => d.Id, d => d.Name);
        // El capturador ya sabe cómo se llama y cuánto pesa cada enlace; uno que no pasó por él se nombra por su dirección.
        var known = c.Query<(string Url, string Name, long? Size)>("SELECT url, name, size FROM download_link").ToDictionary(l => l.Url);
        return c.Query<Row>("""
                SELECT id, data, status, progress, detail, created_at AS CreatedAt FROM task WHERE kind = @Kind
                ORDER BY CASE status WHEN 'running' THEN 0 WHEN 'queued' THEN 1 WHEN 'paused' THEN 2 ELSE 3 END, id DESC LIMIT 200
                """, new { Kind })
            .Select(r =>
            {
                var job = JsonSerializer.Deserialize<DownloadJob>(r.Data!, JsonSerializerOptions.Web)!;
                var detail = string.IsNullOrEmpty(r.Detail) ? null : JsonSerializer.Deserialize<ApiError>(r.Detail, JsonSerializerOptions.Web);
                return new DownloadInfo(r.Id, job.Name, disks.GetValueOrDefault(job.DiskId, job.DiskId), job.Links.Count, job.Done, r.Status, (int)r.Progress, detail, r.CreatedAt,
                    job.Links.Select((l, i) =>
                    {
                        var check = job.Checks?.ElementAtOrDefault(i);
                        return known.TryGetValue(l, out var link) ? new DownloadItem(l, link.Name, link.Size, check) : new DownloadItem(l, job.Files?.ElementAtOrDefault(i) ?? FileNameOf(new Uri(l)), null, check);
                    }).ToList());
            }).ToList();
    }

    /// <summary>Cuántas veces se vuelve a bajar un archivo que llegó dañado, como pyLoad.</summary>
    public const int MaxRetries = 2;

    /// <summary>Las descargas simultáneas de cada sitio: las que elegiste, dentro del tope de tu cuenta (decisión 248).</summary>
    public static List<SiteLimit> Limits(AppConfig config, Credentials credentials) => Sites.Select(s =>
    {
        var max = Math.Max(1, s.MaxParallel(credentials));
        var chosen = config.DownloadsPerSite?.TryGetValue(s.Id, out var v) == true ? v : 1;
        return new SiteLimit(s.Id, max, Math.Clamp(chosen, 1, max));
    }).ToList();

    /// <summary>A qué sitio cuenta una descarga en la cola: el del enlace que le toca.</summary>
    public static string SiteOfJob(string? data)
    {
        var job = data is null ? null : JsonSerializer.Deserialize<DownloadJob>(data, JsonSerializerOptions.Web);
        if (job is null || job.Links.Count == 0) return "direct";
        return SiteOf(new Uri(job.Links[Math.Min(job.Done, job.Links.Count - 1)]))?.Id ?? "direct";
    }

    public static async Task Run(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<DownloadJob>(run.Data!, JsonSerializerOptions.Web)!;
        if (Disk.Get(run.Db, job.DiskId) is not { Connected: true } disk) throw new TaskException(ApiError.Of("library.notConnected"));
        RemoveAbandoned(run.Db, disk, run.Id);
        var folder = Directory.CreateDirectory(Path.Combine(disk.Path, Partial, run.Id.ToString())).FullName;
        var n = job.Links.Count;
        List<T> Fill<T>(List<T>? list, T empty) => list is { } l && l.Count == n ? l : [.. Enumerable.Repeat(empty, n)];
        var (files, sizes, checks, retries) = (Fill(job.Files, null), Fill(job.Sizes, null), Fill(job.Checks, null), Fill(job.Retries, 0));
        job = job with { Files = files, Sizes = sizes, Checks = checks, Retries = retries };
        void Save(int done)
        {
            job = job with { Done = done };
            run.Report(done * 100 / n, null, JsonSerializer.Serialize(job, JsonSerializerOptions.Web));
        }

        while (true)
        {
            for (var i = job.Done; i < n; i++)
            {
                ct.ThrowIfCancellationRequested();
                // Ya bajó en una vuelta anterior: no se pide otro enlace.
                if (files[i] is { } had && File.Exists(Path.Combine(folder, had)))
                {
                    Save(i + 1);
                    continue;
                }
                var url = new Uri(job.Links[i]);
                var site = SiteOf(url) ?? throw new TaskException(ApiError.Of("download.badLink"));
                if (site is IOwnTransfer own)
                {
                    var index = i;
                    await own.Transfer(url, folder, run.Credentials, (percent, file) =>
                        run.Report((int)((index + percent / 100) * 100 / n), ApiError.Of("download.running", ("file", file))), ct);
                    checks[i] = "site"; // Mega y BitTorrent comprueban cada parte por su cuenta
                    Save(i + 1);
                    continue;
                }
                var link = await site.Resolve(url, run.Credentials, ct);
                if (link.Wait is { } wait)
                {
                    run.Report(i * 100 / n, ApiError.Of("download.waiting", ("seconds", ((int)wait.TotalSeconds).ToString())));
                    await Task.Delay(wait, ct);
                    link = await site.Resolve(url, run.Credentials, ct);
                }
                if (link.Blocked is { } blocked) throw new TaskException(blocked);
                sizes[i] ??= link.Size ?? KnownSize(run.Db, job.Links[i]);
                files[i] = await Fetch(run, link, folder, i, n, Math.Min(job.Connections, site.MaxConnections), ct);
                Save(i + 1);
            }

            // Lo dañado se borra y se vuelve a bajar; lo demás queda.
            var bad = Verify(folder, files, sizes, checks);
            if (bad.Count == 0) break;
            foreach (var i in bad)
            {
                File.Delete(Path.Combine(folder, files[i]!));
                if (++retries[i] > MaxRetries) throw new TaskException(ApiError.Of("download.corrupt", ("file", files[i]!)));
                run.Log(ApiError.Of("download.log.corrupt", ("file", files[i]!)), "warn");
            }
            Save(bad.Min());
        }
        Save(n); // con cómo se verificó cada uno

        // Terminó: a la Entrada de su disco, que lo ordena (#221).
        var target = Path.Combine(disk.Path, Disk.EntryFolder, job.Name);
        for (var k = 2; Directory.Exists(target); k++) target = Path.Combine(disk.Path, Disk.EntryFolder, $"{job.Name} ({k})");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.Move(folder, target);
        run.Log(ApiError.Of("download.log.done", ("name", job.Name), ("path", target)));
        Import.EnqueueEntry(run.Db, disk, target, job.GameId, run.Id);
        // Otra descarga en paralelo puede estar creando la suya aquí: si no se pudo borrar, queda para la próxima.
        var parent = Path.Combine(disk.Path, Partial);
        try
        {
            if (!Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
        }
        catch (IOException) { }
    }

    /// <summary>El tamaño que dijo el sitio al comprobar el enlace en el capturador.</summary>
    static long? KnownSize(Database db, string url)
    {
        using var c = db.Open();
        return c.ExecuteScalar<long?>("SELECT size FROM download_link WHERE url = @url", new { url });
    }

    /// <summary>
    /// Compara cada archivo con su tamaño esperado y con el hash que traiga el paquete (`.sfv`, `.md5`, `.sha*`) o su nombre (`[ABCD1234]`), como
    /// JDownloader (#202, decisión 249). Anota en <paramref name="checks"/> cómo se verificó y devuelve los que no coinciden.
    /// </summary>
    public static List<int> Verify(string folder, List<string?> files, List<long?> sizes, List<string?> checks)
    {
        var hashes = Checksums.In(folder);
        var bad = new List<int>();
        for (var i = 0; i < files.Count; i++)
        {
            if (files[i] is not { } name || checks[i] is not (null or "size")) continue; // sin archivo propio, o ya verificado
            var path = Path.Combine(folder, name);
            if (!File.Exists(path)) continue;
            if (sizes[i] is { } size && new FileInfo(path).Length != size)
            {
                checks[i] = null;
                bad.Add(i);
                continue;
            }
            if ((hashes.GetValueOrDefault(name) ?? Checksums.InName(name)) is { } expected)
            {
                if (Checksums.Matches(path, expected)) checks[i] = expected.Type;
                else
                {
                    checks[i] = null;
                    bad.Add(i);
                }
            }
            else checks[i] = sizes[i] is null ? null : "size";
        }
        return bad;
    }

    /// <summary>Un comprimido de esta descarga no pasó su CRC al abrirlo: la descarga queda con error, para volver a bajarla. No se borra nada.</summary>
    public static void MarkBroken(Database db, long task, string file)
    {
        using var c = db.Open();
        c.Execute("UPDATE task SET status = 'error', detail = @detail WHERE id = @task AND kind = @Kind AND status = 'done'",
            new { task, Kind, detail = JsonSerializer.Serialize(ApiError.Of("download.archiveBroken", ("file", Path.GetFileName(file))), JsonSerializerOptions.Web) });
    }

    /// <summary>
    /// Lo que puede quedar en memoria por descarga, esperando al disco. Sin tope, un disco lento acumula gigas en RAM: el avance parece rápido, pero lo
    /// recibido todavía no está escrito y un corte lo pierde. Con tope, la velocidad que se ve es la que el disco aguanta.
    /// </summary>
    public const long MemoryCap = 64 * 1024 * 1024;

    public static DownloadConfiguration Configuration(int connections) => new()
    {
        ChunkCount = connections,
        ParallelDownload = connections > 1,
        MaxTryAgainOnFailure = 5,
        EnableAutoResumeDownload = true,
        FileExistPolicy = FileExistPolicy.Exception,
        CheckDiskSizeBeforeDownload = true,
        MaximumMemoryBufferBytes = MemoryCap,
        RequestConfiguration = { UserAgent = "Otteretro" },
    };

    /// <summary>
    /// Si una descarga está escribiendo en el disco de esta importación. Descomprimir mientras tanto hace pelear a las dos por el mismo disco, y en uno
    /// mecánico ambas se vuelven lentas: la importación espera.
    /// </summary>
    /// <remarks>ponytail: también espera en un SSD, donde no haría falta; distinguirlos es propio de cada sistema y aquí solo retrasa un poco.</remarks>
    public static bool WritingOn(Database db, string? importData)
    {
        var disk = importData is null ? null : JsonSerializer.Deserialize<Import.Job>(importData, JsonSerializerOptions.Web)?.EntryDisk;
        if (disk is null) return false;
        using var c = db.Open();
        return c.ExecuteScalar<long>("SELECT count(*) FROM task WHERE kind = @Kind AND status = 'running' AND json_extract(data, '$.diskId') = @disk", new { Kind, disk }) > 0;
    }

    /// <summary>Baja un archivo por partes. Con la tarea pausada o cortada, su avance queda en el archivo `.download` y sigue de ahí. Devuelve su nombre.</summary>
    static async Task<string> Fetch(TaskRun run, ResolvedLink link, string folder, int index, int total, int connections, CancellationToken ct)
    {
        var config = Configuration(connections);
        foreach (var (key, value) in link.Headers ?? []) config.RequestConfiguration.Headers[key] = value;
        await using var service = new DownloadService(config);

        var probe = link.Probe is false ? null : await Probe(link, ct);
        if (probe is not null && Unavailable(probe.Code)) throw new TaskException(ApiError.Of("download.unavailable", ("file", FileNameOf(new Uri(link.Url))), ("status", probe.Code.ToString())));
        // Una página no es un archivo: pide un conector para ese sitio (#226).
        if (probe is { IsPage: true }) throw new TaskException(ApiError.Of("download.notDirect", ("url", link.Url)));
        var name = link.FileName ?? probe?.Name ?? FileNameOf(new Uri(link.Url));
        var last = DateTime.MinValue;
        service.DownloadProgressChanged += (_, e) =>
        {
            if (DateTime.UtcNow - last < TimeSpan.FromSeconds(1)) return;
            last = DateTime.UtcNow;
            run.Report((int)((index + e.ProgressPercentage / 100) * 100 / total),
                ApiError.Of("download.progress", ("file", name), ("speed", ((long)e.BytesPerSecondSpeed).ToString()), ("done", e.ReceivedBytesSize.ToString()), ("total", e.TotalBytesToReceive.ToString())));
        };
        Exception? failure = null;
        service.DownloadFileCompleted += (_, e) => failure = e.Cancelled ? new OperationCanceledException(ct) : e.Error;
        var path = Path.Combine(folder, name);
        // Si ya terminó en una vuelta anterior, no se repite.
        if (File.Exists(path)) return name;
        await service.DownloadFileTaskAsync(link.Url, path, ct);
        ct.ThrowIfCancellationRequested();
        if (failure is OperationCanceledException) throw failure;
        if (failure is not null) throw new HttpRequestException(failure.Message, failure);
        if (!File.Exists(path)) throw new TaskException(ApiError.Of("download.failed", ("file", name)));
        return name;
    }

    static readonly HttpClient Prober = new() { Timeout = TimeSpan.FromSeconds(30) };

    record ProbeResult(int Code, long? Size, string? Name, bool IsPage);

    /// <summary>Pide solo el primer byte, para saber si está, cuánto pesa y cómo se llama.</summary>
    static async Task<ProbeResult> Probe(ResolvedLink link, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, link.Url);
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
        request.Headers.UserAgent.ParseAdd("Otteretro");
        foreach (var (key, value) in link.Headers ?? []) request.Headers.TryAddWithoutValidation(key, value);
        using var response = await Prober.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var content = response.Content.Headers;
        var name = content.ContentDisposition?.FileNameStar ?? content.ContentDisposition?.FileName?.Trim('"');
        var page = content.ContentType?.MediaType is "text/html" or "application/xhtml+xml";
        return new((int)response.StatusCode, content.ContentRange?.Length ?? content.ContentLength, name is null ? null : Clean(name), page);
    }

    /// <summary>Un 4xx falla enseguida, no como un corte de red que la cola reintenta (salvo los que se pasan solos).</summary>
    static bool Unavailable(int code) => code is >= 400 and < 500 and not (408 or 416 or 429);

    /// <summary>Saca los enlaces de un texto pegado o arrastrado, sin repetirlos.</summary>
    public static List<string> LinksIn(string text) => Link().Matches(text).Select(m => m.Value.TrimEnd('.', ',', ';', ')')).Distinct().ToList();

    /// <summary>La clave de su paquete: las partes de un mismo comprimido van juntas; lo demás, cada uno por su cuenta.</summary>
    public static string PackageOf(string file) => Parts().Replace(file, "") is var key && key != file ? key : Path.GetFileNameWithoutExtension(file);

    /// <summary>
    /// Los paquetes de un texto (#203): reconoce el sitio de cada enlace y comprueba si está en línea y cuánto pesa. No baja nada.
    /// </summary>
    public static async Task<List<PackagePreview>> Preview(string text, Credentials credentials, CancellationToken ct)
    {
        var links = LinksIn(text);
        var checks = new LinkCheck[links.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, links.Count), new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (i, token) =>
            checks[i] = await Check(links[i], credentials, token));
        return checks.GroupBy(c => PackageOf(c.Name), StringComparer.OrdinalIgnoreCase)
            .Select(g => new PackagePreview(g.Key, g.ToList(), g.Sum(c => c.Size ?? 0))).ToList();
    }

    static async Task<LinkCheck> Check(string text, Credentials credentials, CancellationToken ct)
    {
        var url = new Uri(text);
        var fallback = FileNameOf(url);
        if (SiteOf(url) is not { } site) return new(text, fallback, null, null, "noConnector");
        try
        {
            if (site is IOwnTransfer own)
            {
                var (ownName, ownSize) = await own.Describe(url, credentials, ct);
                return new(text, ownName ?? fallback, ownSize, site.Id, "ok");
            }
            if (await site.Inspect(url, credentials, ct) is { } seen) return seen with { Url = text, Name = seen.Name.Length > 0 ? seen.Name : fallback };
            var link = await site.Resolve(url, credentials, ct);
            if (link.Blocked is not null) return new(text, link.FileName ?? fallback, link.Size, site.Id, "blocked", link.Blocked);
            if (link.Probe is false) return new(text, link.FileName ?? fallback, link.Size, site.Id, "ok");
            var probe = await Probe(link, ct);
            var state = Unavailable(probe.Code) ? "unavailable" : probe.IsPage && site is DirectSite ? "noConnector" : "ok";
            return new(text, link.FileName ?? probe.Name ?? fallback, link.Size ?? probe.Size, site.Id, state);
        }
        // Sin respuesta no se sabe: se deja intentar, y la cola reintenta si es un corte.
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return new(text, fallback, null, site.Id, "unknown"); }
        catch (CG.Web.MegaApiClient.ApiException e) when (e.ApiResultCode == CG.Web.MegaApiClient.ApiResultCode.ResourceNotExists) { return new(text, fallback, null, site.Id, "unavailable"); }
        catch (CG.Web.MegaApiClient.ApiException) { return new(text, fallback, null, site.Id, "unknown"); }
    }

    /// <summary>Un enlace del que no se saca ningún nombre.</summary>
    internal const string NoName = "descarga";

    internal static string FileNameOf(Uri url)
    {
        var name = Clean(WebUtility.UrlDecode(Path.GetFileName(url.LocalPath)));
        if (name.Length > 0) return name;
        // `https://sitio/?id`: sin esto, los enlaces distintos de un sitio se juntarían en un solo paquete.
        var id = Clean(url.Query.TrimStart('?').Split('&')[0]);
        return id.Length > 0 && !id.Contains('=') ? id : NoName;
    }

    /// <summary>Un nombre que llega de afuera no puede salirse de la carpeta.</summary>
    static string Clean(string name)
    {
        name = Path.GetFileName(name.Replace('\\', '/'));
        foreach (var bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
        return name.Trim(' ', '.');
    }

    [GeneratedRegex("""(?:https?://|magnet:\?)[^\s"'<>]+""", RegexOptions.IgnoreCase)]
    private static partial Regex Link();

    /// <summary>`x.part02.rar`, `x.r00`, `x.rar`, `x.7z.001`, `x.z01`: lo que sobra para quedarse con el nombre del conjunto.</summary>
    [GeneratedRegex(@"(\.part\d+\.rar|\.r\d{2}|\.rar|\.7z\.\d{3}|\.z\d{2}|\.zip|\.7z)$", RegexOptions.IgnoreCase)]
    private static partial Regex Parts();

    /// <summary>Lo que quedó de descargas canceladas o que fallaron para siempre: no vuelven a usarse.</summary>
    static void RemoveAbandoned(Database db, DiskInfo disk, long current)
    {
        var parent = Path.Combine(disk.Path, Partial);
        using var c = db.Open();
        var alive = c.Query<long>("SELECT id FROM task WHERE kind = @Kind AND status IN ('queued', 'running', 'paused')", new { Kind }).ToHashSet();
        try
        {
            foreach (var folder in Directory.GetDirectories(parent))
                if (long.TryParse(Path.GetFileName(folder), out var id) && id != current && !alive.Contains(id))
                    Directory.Delete(folder, recursive: true);
        }
        catch (DirectoryNotFoundException) { } // no hay, o la borró otra descarga que terminó recién
    }
}
