using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>
/// Qué instalar (#268): de la receta de <paramref name="Emulator"/> con <paramref name="Version"/>, de una release ya compilada
/// (<paramref name="Url"/>) o de una carpeta o un zip de este equipo (<paramref name="Path"/>; con <paramref name="Adopt"/>, uno
/// instalado a mano). Sin receta, <paramref name="Name"/> y <paramref name="Folder"/> (la consola) dicen dónde va.
/// </summary>
public record InstallJob(string Emulator, string? Version = null, string? Url = null, string? Path = null, bool Adopt = false,
    string? Name = null, string? Folder = null, string Kind = "emulator");

/// <param name="Path">Dónde está instalada; null si su disco no está conectado o la carpeta ya no está.</param>
/// <param name="BackupLibraryId">El disco con la segunda copia en la librería; null si todavía no hay.</param>
public record EmulatorInstall(string EmulatorId, string Name, string Kind, string Version, string Origin, string? Url, string? File,
    string? Sha256, string? BackupLibraryId, string InstalledAt, string? Path);

/// <summary>
/// Instalar emuladores y ports (§8.0, decisiones 287 y 293). Cada versión va a su carpeta y nunca reemplaza otra. El archivo original
/// queda en la librería privada, `Libreria\` del disco ★ (se muda con la carpeta de datos), con una segunda copia en otro disco.
/// </summary>
public static class Installs
{
    public const string Kind = "tools.install";
    /// <summary>Nombre de carpeta de la persona: en español.</summary>
    public const string LibraryFolder = "Libreria";

    /// <summary>Para las pruebas: responde en lugar de internet.</summary>
    public static HttpMessageHandler? Handler { get; set; }

    static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    static string Os => OperatingSystem.IsWindows() ? "windows" : "linux";

    record Target(Recipe? Recipe, string Kind, string Name, string Folder);

    static Target? Resolve(Database db, InstallJob job)
    {
        if (Recipes.Find(job.Emulator) is { Kind: not "complement" } r) return new(r, r.Kind, r.Name, r.Folder);
        using var c = db.Open();
        if (c.QuerySingleOrDefault<(string Kind, string Name, string Folder)?>("SELECT kind, name, folder FROM emulator WHERE id = @Emulator", job) is { } e)
            return new(null, e.Kind, e.Name, e.Folder);
        return job is { Name: { Length: > 0 } name, Folder: { Length: > 0 } folder, Kind: "emulator" or "port" } ? new(null, job.Kind, name, folder) : null;
    }

    static bool BadName(string? s) => string.IsNullOrWhiteSpace(s) || s.Trim('.').Length == 0 || s.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0;

    static string InstallPath(DiskInfo home, Target t, string version) =>
        System.IO.Path.Combine(home.Path, Disk.DiskFolders[t.Kind], t.Folder, t.Name, version);

    static string ShelfPath(string disk, Target t, string emulator, string version) =>
        System.IO.Path.Combine(disk, LibraryFolder, Disk.DiskFolders[t.Kind], emulator, version);

    /// <summary>Valida y encola la instalación. Devuelve la tarea o el error.</summary>
    public static (long? Task, ApiError? Error) Enqueue(Database db, InstallJob job)
    {
        if (BadName(job.Emulator) || Resolve(db, job) is not { } target || BadName(target.Name) || BadName(target.Folder)) return (null, ApiError.Of("install.unknownEmulator"));
        if (job.Path is { } path)
        {
            if (!System.IO.Path.IsPathFullyQualified(path) || !System.IO.Path.Exists(path)) return (null, ApiError.Of("path.notFound"));
            job = job with { Path = Disk.Normalize(path), Url = null, Version = job.Version ?? (Directory.Exists(path) ? Recipes.VersionOf(target.Recipe, path) : null) };
        }
        else if (job.Url is { } url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return (null, ApiError.Of("install.badUrl"));
        }
        else if (target.Recipe?.Downloads.Any(d => d.Version == job.Version && d.System == Os) != true)
            return (null, ApiError.Of("install.noDownload"));
        if (BadName(job.Version)) return (null, ApiError.Of("install.noVersion"));
        if (Disk.DefaultDisk(db) is not { Connected: true } home) return (null, ApiError.Of("library.notConnected"));
        if (Installed(db, job.Emulator, job.Version!) || System.IO.Path.Exists(InstallPath(home, target, job.Version!)))
            return (null, ApiError.Of("install.exists", ("version", job.Version!)));
        return (TaskQueue.Enqueue(db, Kind, JsonSerializer.Serialize(job, Json)), null);
    }

    static bool Installed(Database db, string emulator, string version)
    {
        using var c = db.Open();
        return c.ExecuteScalar<long>("SELECT count(*) FROM emulator_install WHERE emulator_id = @emulator AND version = @version", new { emulator, version }) > 0;
    }

    /// <summary>Lo instalado con su origen, versión y hash, y dónde está.</summary>
    public static List<EmulatorInstall> List(Database db)
    {
        var onDisk = Recipes.Installed(Disk.ToolRoots(db));
        using var c = db.Open();
        return c.Query<EmulatorInstall>("""
            SELECT i.emulator_id AS EmulatorId, e.name, e.kind, i.version, i.origin, i.url, i.file, i.sha256, i.backup_library_id AS BackupLibraryId,
                   i.installed_at AS InstalledAt, e.folder AS Path
            FROM emulator_install i JOIN emulator e ON e.id = i.emulator_id ORDER BY e.name, i.installed_at
            """).Select(i => i with
        {
            Path = onDisk.FirstOrDefault(t => t.Kind == i.Kind && t.Folder == i.Path && t.Name == i.Name && t.Version == i.Version)?.Path,
        }).ToList();
    }

    public static async Task Run(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<InstallJob>(run.Data!, Json)!;
        var version = job.Version!;
        var target = Resolve(run.Db, job) ?? throw new TaskException(ApiError.Of("install.unknownEmulator"));
        if (Disk.DefaultDisk(run.Db) is not { Connected: true } home) throw new TaskException(ApiError.Of("library.notConnected"));
        var install = InstallPath(home, target, version);
        // Nunca se pisa una versión: ni la registrada ni una carpeta que ya esté ahí.
        if (Installed(run.Db, job.Emulator, version) || System.IO.Path.Exists(install)) throw new TaskException(ApiError.Of("install.exists", ("version", version)));
        var download = job.Url is null && job.Path is null ? target.Recipe!.Downloads.First(d => d.Version == version && d.System == Os) : null;
        var origin = job.Path is not null ? (job.Adopt ? "adopted" : "upload") : download is not null ? "recipe" : "url";

        var shelf = ShelfPath(home.Path, target, job.Emulator, version);
        var work = System.IO.Path.Combine(home.Path, LibraryFolder, ".trabajo", run.Id.ToString());
        Directory.CreateDirectory(work);
        try
        {
            // 1. El archivo original: primero la librería (decisión 292); si no está, la descarga, el zip o la carpeta hecha zip.
            run.Report(5, ApiError.Of("install.fetching", ("version", version)));
            string original;
            if (Directory.Exists(shelf) && Directory.GetFiles(shelf) is [var kept]) original = kept;
            else if (job.Path is { } path)
            {
                original = System.IO.Path.Combine(work, File.Exists(path) ? System.IO.Path.GetFileName(path) : $"{target.Name}-{version}.zip");
                if (File.Exists(path)) File.Copy(path, original, overwrite: true);
                else ZipFile.CreateFromDirectory(path, original);
            }
            else original = await Download(job.Url ?? download!.Url, work, ct);

            // 2. Hash: el de la receta tiene que coincidir.
            var sha = await Sha256(original, ct);
            if (download is not null && !MatchesSri(sha, download.Hash))
                throw new TaskException(ApiError.Of("install.badHash", ("version", version)));

            // 3. A la librería del ★: copiar, verificar y recién usarla.
            run.Report(40, ApiError.Of("install.library"));
            var stored = Store(original, shelf);

            // 4. A su carpeta: se extrae al lado y se renombra, así un corte no deja una versión a medias.
            run.Report(60, ApiError.Of("install.extracting", ("path", install)));
            var partial = install + ".parcial";
            if (Directory.Exists(partial)) Directory.Delete(partial, recursive: true);
            if (Archives.IsArchive(stored))
            {
                if (Archives.Extract(stored, partial, [], ct) is { } error) throw new TaskException(error);
                // Una release que trae todo dentro de una carpeta: se usa esa carpeta.
                if (Directory.GetFiles(partial).Length == 0 && Directory.GetDirectories(partial) is [var only])
                {
                    Directory.Move(only, partial + ".x");
                    Directory.Delete(partial);
                    Directory.Move(partial + ".x", partial);
                }
            }
            else
            {
                Directory.CreateDirectory(partial);
                File.Copy(stored, System.IO.Path.Combine(partial, System.IO.Path.GetFileName(stored)));
            }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(install)!);
            Directory.Move(partial, install);
            if (target.Recipe is { } recipe) Recipes.MakePortable(recipe, install);

            Profiles.AddEmulator(run.Db, job.Emulator, target.Kind, target.Name, target.Folder);
            using (var c = run.Db.Open())
                c.Execute("""
                    INSERT INTO emulator_install (emulator_id, version, origin, url, file, sha256, installed_at)
                    VALUES (@Emulator, @version, @origin, @url, @file, @sha, @now)
                    """, new { job.Emulator, version, origin, url = job.Url ?? download?.Url, file = System.IO.Path.GetFileName(stored), sha, now = DateTime.UtcNow.ToString("O") });

            // 5. La segunda copia, en otro disco conectado.
            run.Report(85, ApiError.Of("install.backup"));
            if (Disk.List(run.Db).FirstOrDefault(d => d.Type == "disk" && d.Connected && d.Id != home.Id) is { } other)
            {
                Store(stored, ShelfPath(other.Path, target, job.Emulator, version));
                using var c = run.Db.Open();
                c.Execute("UPDATE emulator_install SET backup_library_id = @Id WHERE emulator_id = @Emulator AND version = @version", new { other.Id, job.Emulator, version });
            }
            else run.Log(ApiError.Of("install.log.noBackup"), "warn");
            run.Log(ApiError.Of("install.log.done", ("name", target.Name), ("version", version), ("path", install)));
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    /// <summary>
    /// Copia <paramref name="file"/> a la carpeta <paramref name="shelf"/> de la librería, fuerza la escritura y la verifica. Si ya hay
    /// un archivo ahí, tiene que ser el mismo: la librería nunca se pisa. Devuelve la ruta en la librería.
    /// </summary>
    public static string Store(string file, string shelf)
    {
        var to = System.IO.Path.Combine(shelf, System.IO.Path.GetFileName(file));
        if (Disk.Same(file, to)) return to;
        if (Directory.Exists(shelf) && Directory.GetFiles(shelf) is { Length: > 0 } there)
        {
            if (there is [var same] && Disk.Same(same, to) && FolderMove.SameContent(file, to)) return to;
            throw new TaskException(ApiError.Of("install.libraryTaken", ("path", shelf)));
        }
        Directory.CreateDirectory(shelf);
        File.Copy(file, to + ".tmp", overwrite: true);
        FolderMove.FlushToDisk(to + ".tmp");
        if (!FolderMove.SameContent(file, to + ".tmp"))
        {
            File.Delete(to + ".tmp");
            throw new TaskException(ApiError.Of("move.copyMismatch", ("path", file), ("detail", System.IO.Path.GetFileName(file))));
        }
        File.Move(to + ".tmp", to);
        return to;
    }

    /// <summary>El SHA-256 en hexadecimal contra el hash SRI de la receta (`sha256-` y base64, como Nix).</summary>
    public static bool MatchesSri(string sha256Hex, string sri) => "sha256-" + Convert.ToBase64String(Convert.FromHexString(sha256Hex)) == sri;

    static async Task<string> Sha256(string file, CancellationToken ct)
    {
        await using var stream = File.OpenRead(file);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
    }

    static async Task<string> Download(string url, string folder, CancellationToken ct)
    {
        using var http = Handler is null ? new HttpClient() : new HttpClient(Handler, disposeHandler: false);
        http.Timeout = TimeSpan.FromMinutes(30);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Otteretro");
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var name = System.IO.Path.GetFileName(Uri.UnescapeDataString(new Uri(url).AbsolutePath));
        var file = System.IO.Path.Combine(folder, BadName(name) ? "descarga" : name);
        await using (var output = File.Create(file)) await response.Content.CopyToAsync(output, ct);
        FolderMove.FlushToDisk(file);
        return file;
    }
}
