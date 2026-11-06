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
/// instalado a mano). Sin receta, <paramref name="Name"/> dice cómo se llama su carpeta. <paramref name="Reinstall"/>: una versión
/// registrada que perdió su carpeta vuelve desde la librería (decisión 304). <paramref name="Port"/>: el port del catálogo que se
/// instala (#287); sus juegos reciben un perfil con él.
/// </summary>
public record InstallJob(string Emulator, string? Version = null, string? Url = null, string? Path = null, bool Adopt = false,
    string? Name = null, string Kind = "emulator", bool Reinstall = false, long? Port = null);

/// <param name="Path">Dónde está instalada; null si su disco no está conectado o la carpeta ya no está (se puede reinstalar).</param>
/// <param name="BackupLibraryId">El disco con la segunda copia en la librería; null si todavía no hay.</param>
public record EmulatorInstall(string EmulatorId, string Name, string Kind, string Version, string Origin, string? Url, string? File,
    string? Sha256, string? BackupLibraryId, string InstalledAt, string? Path);

/// <param name="DiskId">El disco elegido para la segunda copia de la librería; null si no se eligió.</param>
/// <param name="Pending">Versiones de la librería que todavía no tienen su copia en ese disco.</param>
public record LibraryBackup(string? DiskId, int Pending);

/// <summary>
/// Instalar emuladores y ports (§8.0, decisiones 287, 293, 302 a 304 y 324 a 326). Cada versión de un emulador va a
/// `Emuladores\<Emulador>\<versión>` y nunca reemplaza otra; un port tiene una sola, en `Ports\<Port>`, que se reemplaza en el lugar. El archivo original queda en la librería privada, `Libreria\` del disco ★ (se muda con la carpeta de datos, que es
/// lo que define el ★), con una segunda copia en el disco que elige la persona.
/// </summary>
public static class Installs
{
    public const string Kind = "tools.install";
    /// <summary>Copiar a la segunda copia lo que le falta. Es de `library.`: no corre a la vez que una mudanza.</summary>
    public const string BackupKind = "library.backup";
    /// <summary>Nombre de carpeta de la persona: en español.</summary>
    public const string LibraryFolder = "Libreria";
    const string BackupSetting = "library.backupDisk";

    /// <summary>Para las pruebas: responde en lugar de internet.</summary>
    public static HttpMessageHandler? Handler { get; set; }

    static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    static string Os => OperatingSystem.IsWindows() ? "windows" : "linux";

    record Target(Recipe? Recipe, string Kind, string Name);

    record Row(string Kind, string Name, string? File, string? Sha256);

    static Row? Registered(Database db, string emulator, string version)
    {
        using var c = db.Open();
        return c.QuerySingleOrDefault<Row>("""
            SELECT e.kind, e.name, i.file, i.sha256 FROM emulator_install i JOIN emulator e ON e.id = i.emulator_id
            WHERE i.emulator_id = @emulator AND i.version = @version
            """, new { emulator, version });
    }

    static Target? Resolve(Database db, InstallJob job)
    {
        if (Recipes.Find(job.Emulator) is { Kind: not "complement" } r) return new(r, r.Kind, r.Name);
        using var c = db.Open();
        if (c.QuerySingleOrDefault<Row>("SELECT kind, name, name AS file, name AS sha256 FROM emulator WHERE id = @Emulator", job) is { } e) return new(null, e.Kind, e.Name);
        return job is { Name: { Length: > 0 } name, Kind: "emulator" or "port" } ? new(null, job.Kind, name) : null;
    }

    internal static bool BadName(string? s) => string.IsNullOrWhiteSpace(s) || s.Trim('.').Length == 0 || s.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0;

    /// <summary>
    /// `Emuladores\<Emulador>\<versión>`, una carpeta aunque sirva a varias consolas (decisión 302). Un port, `Ports\<Port>`, sin la
    /// versión (decisión 324): su versión va en <see cref="Recipes.VersionFile"/>.
    /// </summary>
    static string InstallPath(DiskInfo home, Target t, string version) => t.Kind == "port"
        ? System.IO.Path.Combine(home.Path, Disk.DiskFolders[t.Kind], t.Name)
        : System.IO.Path.Combine(home.Path, Disk.DiskFolders[t.Kind], t.Name, version);

    internal static string ShelfPath(string disk, string kind, string emulator, string version) =>
        System.IO.Path.Combine(disk, LibraryFolder, Disk.DiskFolders[kind], emulator, version);

    /// <summary>Valida y encola la instalación. Devuelve la tarea o el error.</summary>
    public static (long? Task, ApiError? Error) Enqueue(Database db, InstallJob job)
    {
        if (BadName(job.Emulator) || Resolve(db, job) is not { } target || BadName(target.Name)) return (null, ApiError.Of("install.unknownEmulator"));
        var home = Disk.DefaultDisk(db) is { Connected: true } d ? d : null;
        if (job.Reinstall)
        {
            if (home is null) return (null, ApiError.Of("library.notConnected"));
            if (BadName(job.Version) || Registered(db, job.Emulator, job.Version!) is not { File: { } file }) return (null, ApiError.Of("install.notInstalled"));
            // Un port se reinstala sobre la que tenga (decisión 325); un emulador, solo si perdió su carpeta.
            if (target.Kind != "port" && System.IO.Path.Exists(InstallPath(home, target, job.Version!))) return (null, ApiError.Of("install.exists", ("version", job.Version!)));
            if (!File.Exists(System.IO.Path.Combine(ShelfPath(home.Path, target.Kind, job.Emulator, job.Version!), file)))
                return (null, ApiError.Of("install.notInLibrary", ("version", job.Version!)));
            return (TaskQueue.Enqueue(db, Kind, JsonSerializer.Serialize(new InstallJob(job.Emulator, job.Version, Reinstall: true), Json)), null);
        }
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
        if (home is null) return (null, ApiError.Of("library.notConnected"));
        if (Registered(db, job.Emulator, job.Version!) is not null || (target.Kind != "port" && System.IO.Path.Exists(InstallPath(home, target, job.Version!))))
            return (null, ApiError.Of("install.exists", ("version", job.Version!)));
        return (TaskQueue.Enqueue(db, Kind, JsonSerializer.Serialize(job, Json)), null);
    }

    /// <summary>Lo instalado con su origen, versión y hash, y dónde está.</summary>
    public static List<EmulatorInstall> List(Database db)
    {
        var onDisk = Recipes.Installed(Disk.ToolRoots(db));
        using var c = db.Open();
        return c.Query<EmulatorInstall>("""
            SELECT i.emulator_id AS EmulatorId, e.name, e.kind, i.version, i.origin, i.url, i.file, i.sha256, i.backup_library_id AS BackupLibraryId,
                   i.installed_at AS InstalledAt, e.name AS Path
            FROM emulator_install i JOIN emulator e ON e.id = i.emulator_id ORDER BY e.name, i.installed_at
            """).Select(i => i with // Path: se reemplaza por la carpeta en disco (la columna solo le da el tipo a Dapper)
        {
            Path = onDisk.FirstOrDefault(t => t.Kind == i.Kind && t.Name == i.Name && t.Version == i.Version)?.Path,
        }).ToList();
    }

    public static Task Run(TaskRun run, CancellationToken ct) => Install(run, JsonSerializer.Deserialize<InstallJob>(run.Data!, Json)!, ct);

    /// <summary>Ya ocupada: registrada o con su carpeta en el disco ★ (un port se reemplaza: solo si está registrada).</summary>
    internal static bool Taken(Database db, InstallJob job) =>
        Registered(db, job.Emulator, job.Version!) is not null
        || (Disk.DefaultDisk(db) is { Connected: true } home && Resolve(db, job) is { Kind: not "port" } t && System.IO.Path.Exists(InstallPath(home, t, job.Version!)));

    /// <summary>Instala dentro de otra tarea (cargar un perfil compartido, #282).</summary>
    internal static async Task Install(TaskRun run, InstallJob job, CancellationToken ct)
    {
        var version = job.Version!;
        var target = Resolve(run.Db, job) ?? throw new TaskException(ApiError.Of("install.unknownEmulator"));
        if (Disk.DefaultDisk(run.Db) is not { Connected: true } home) throw new TaskException(ApiError.Of("library.notConnected"));
        var install = InstallPath(home, target, version);
        // Nunca se pisa una versión: ni la registrada (salvo al reinstalarla) ni una carpeta que ya esté ahí. Un port sí se reemplaza
        // (decisión 325), salvo que todavía tenga varias versiones adentro: primero se juntan en una (#316).
        var registered = Registered(run.Db, job.Emulator, version);
        var isPort = target.Kind == "port";
        if ((registered is not null && !job.Reinstall) || (!isPort && System.IO.Path.Exists(install))) throw new TaskException(ApiError.Of("install.exists", ("version", version)));
        var replace = isPort && Directory.Exists(install);
        if (replace && !File.Exists(System.IO.Path.Combine(install, Recipes.VersionFile))
            && Directory.EnumerateDirectories(install).Any(d => Registered(run.Db, job.Emulator, System.IO.Path.GetFileName(d)) is not null))
            throw new TaskException(ApiError.Of("install.manyVersions", ("name", target.Name)));
        var download = job.Url is null && job.Path is null && !job.Reinstall ? target.Recipe!.Downloads.First(d => d.Version == version && d.System == Os) : null;
        var origin = job.Path is not null ? (job.Adopt ? "adopted" : "upload") : download is not null ? "recipe" : "url";

        var shelf = ShelfPath(home.Path, target.Kind, job.Emulator, version);
        var work = System.IO.Path.Combine(home.Path, LibraryFolder, ".trabajo", run.Id.ToString());
        Directory.CreateDirectory(work);
        try
        {
            string stored, sha;
            if (job.Reinstall)
            {
                // Desde la librería, sin bajar nada, y solo si sigue siendo el mismo archivo.
                run.Report(10, ApiError.Of("install.library"));
                stored = System.IO.Path.Combine(shelf, registered?.File ?? throw new TaskException(ApiError.Of("install.notInstalled")));
                if (!File.Exists(stored)) throw new TaskException(ApiError.Of("install.notInLibrary", ("version", version)));
                sha = await Sha256(stored, ct);
                if (sha != registered.Sha256) throw new TaskException(ApiError.Of("install.badHash", ("version", version)));
            }
            else
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
                sha = await Sha256(original, ct);
                if (download is not null && !MatchesSri(sha, download.Hash))
                    throw new TaskException(ApiError.Of("install.badHash", ("version", version)));

                // 3. A la librería del ★: copiar, verificar y recién usarla.
                run.Report(40, ApiError.Of("install.library"));
                stored = Store(original, shelf);
            }

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
                // Un .exe con la versión en el nombre (Steam ROM Manager) va con el nombre que busca la receta (`saveAs`, #274).
                Directory.CreateDirectory(partial);
                File.Copy(stored, System.IO.Path.Combine(partial, target.Recipe?.SaveAs ?? System.IO.Path.GetFileName(stored)));
            }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(install)!);
            if (replace) Replace(install, partial, target.Recipe, version);
            else Directory.Move(partial, install);
            if (isPort) File.WriteAllText(System.IO.Path.Combine(install, Recipes.VersionFile), version);
            if (target.Recipe is { } recipe)
            {
                Recipes.MakePortable(recipe, install);
                ShareSaves(recipe, install, version);
            }

            if (!job.Reinstall)
            {
                Profiles.AddEmulator(run.Db, job.Emulator, target.Kind, target.Name);
                if (job.Port is { } port) Ports.AddProfiles(run.Db, port, job.Emulator, target.Name);
                using (var c = run.Db.Open())
                    c.Execute("""
                        INSERT INTO emulator_install (emulator_id, version, origin, url, file, sha256, installed_at)
                        VALUES (@Emulator, @version, @origin, @url, @file, @sha, @now)
                        """, new { job.Emulator, version, origin, url = job.Url ?? download?.Url, file = System.IO.Path.GetFileName(stored), sha, now = DateTime.UtcNow.ToString("O") });
                // 5. La segunda copia va aparte, al disco elegido; si no está, queda pendiente y se copia al conectarlo.
                if (EnqueueBackupIfPending(run.Db) is null)
                    run.Log(ApiError.Of(BackupDisk(run.Db) is null ? "install.log.noBackupDisk" : "install.log.backupPending"), "warn");
            }
            run.Log(ApiError.Of("install.log.done", ("name", target.Name), ("version", version), ("path", install)));
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    /// <summary>
    /// Reemplaza un port en el lugar (decisión 325) con la versión ya extraída en <paramref name="partial"/>. Lo de la persona se queda:
    /// `_compartido` y `_respaldos` (partidas y contenido, decisión 308), la configuración, los mods y los archivos por juego de la receta,
    /// y todo archivo que no venga en el paquete nuevo. Antes se copia, verificado, a `_respaldos`; recién entonces se cambia la
    /// carpeta, y la vieja se borra al final, cuando lo nuevo ya está en su lugar.
    /// </summary>
    /// <remarks>ponytail: un corte entre los dos renombres deja `<Port>.anterior` al lado, con todo: se arregla a mano.</remarks>
    static void Replace(string install, string partial, Recipe? recipe, string version)
    {
        var mine = Mine(recipe);
        var kept = Files(install).Where(rel => rel != Recipes.VersionFile
            && (!File.Exists(System.IO.Path.Combine(partial, rel)) || mine.Any(m => Under(rel, m)))).ToList();
        var backup = System.IO.Path.Combine(install, SnapshotsFolder, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-archivos-antes-de-{version}");
        foreach (var rel in kept) CopyVerified(System.IO.Path.Combine(install, rel), System.IO.Path.Combine(backup, rel));
        foreach (var rel in kept) CopyVerified(System.IO.Path.Combine(install, rel), System.IO.Path.Combine(partial, rel), overwrite: true);
        MoveCommon(install, partial);
        var old = install + ".anterior";
        Directory.Move(install, old);
        Directory.Move(partial, install);
        Links.DeleteTree(old); // ya sin lo de la persona: los enlaces se borran, no lo enlazado
    }

    /// <summary>Lo de la persona según la receta, hasta el primer marcador (`data/mods`, `User/GameSettings/`): gana sobre lo que traiga el paquete.</summary>
    static List<string> Mine(Recipe? recipe) =>
        (recipe is null ? [] : recipe.Config.Concat(recipe.PerGame is { } pg ? [.. pg.Config is { } c ? [c] : Array.Empty<string>(), .. pg.Files, .. pg.Mods] : []))
            .Select(p => p.Split('{')[0].Replace('\\', '/').TrimEnd('/')).Where(p => p.Length > 0).ToList();

    static bool Under(string rel, string folder) => rel == folder || rel.StartsWith(folder + "/", StringComparison.Ordinal);

    /// <summary>`_compartido` y `_respaldos` pasan de una carpeta a la otra, en el mismo disco: se renombran.</summary>
    static void MoveCommon(string from, string to)
    {
        foreach (var folder in new[] { SharedFolder, SnapshotsFolder })
            if (Directory.Exists(System.IO.Path.Combine(from, folder)))
            {
                var there = System.IO.Path.Combine(to, folder);
                if (System.IO.Path.Exists(there)) Directory.Move(there, there + ".del-paquete");
                Directory.Move(System.IO.Path.Combine(from, folder), there);
            }
    }

    /// <summary>
    /// Un port de antes, `Ports\<Port>\<versión>` con una o varias, pasa a una sola en `Ports\<Port>`: la más nueva (decisión 327, #316).
    /// Primero copia, verificado, a `_respaldos\<fecha>-antes-de-juntar-versiones\<versión>` lo de la persona de cada versión (lo que
    /// <see cref="Replace"/> conserva: partidas, configuración, mods y lo que no trae la nueva) y lo que se va a pisar; después junta en
    /// la nueva las partidas y lo de la persona (si el mismo archivo está en varias, gana el más reciente) y recién entonces borra las
    /// demás. Los archivos de cada versión siguen en la librería. Devuelve la versión que quedó.
    /// </summary>
    /// <remarks>ponytail: un corte entre los renombres deja `<Port>.nueva` o `<Port>.anterior` al lado, con todo: se arregla a mano.</remarks>
    internal static string JoinVersions(string folder, Recipe? recipe, IReadOnlyList<string> versions)
    {
        string P(params string[] parts) => System.IO.Path.Combine(parts);
        var newest = versions.Order(Comparer<string>.Create((a, b) => EmulatorSettings.Compare(a, b) is var r and not 0 ? r : string.CompareOrdinal(a, b))).Last();
        var keep = P(folder, newest);
        var shared = P(folder, SharedFolder);
        var together = recipe is null ? [] : recipe.Saves.Concat(recipe.Content).Select(s => s.Replace('\\', '/').TrimEnd('/')).ToList();
        var mine = Mine(recipe).Concat(together).ToList();
        // Las partidas que ya están en común (decisión 308) se juntan ahí; lo demás, en la versión nueva.
        string Dest(string rel) => together.Any(s => Under(rel, s) && Directory.Exists(P(shared, s))) ? P(shared, rel) : P(keep, rel);

        var candidates = versions
            .SelectMany(v => Files(P(folder, v)).Where(rel => rel != Recipes.VersionFile
                && (mine.Any(m => Under(rel, m)) || (v != newest && !File.Exists(P(keep, rel))))).Select(rel => (Label: v, Rel: rel, File: P(folder, v, rel))))
            .Concat(together.Where(s => Directory.Exists(P(shared, s)))
                .SelectMany(s => Files(P(shared, s)).Select(rel => (Label: SharedFolder, Rel: s + "/" + rel, File: P(shared, s, rel)))))
            .ToList();
        var winners = candidates.GroupBy(c => c.Rel).Select(g => g.MaxBy(c => File.GetLastWriteTimeUtc(c.File)))
            .Where(w => !Disk.Same(w.File, Dest(w.Rel))).ToList();

        // 1. Respaldo verificado: lo de la persona de cada versión que se borra (salvo lo que la nueva ya tiene igual) y lo que se pisa.
        var backup = P(folder, SnapshotsFolder, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-antes-de-juntar-versiones");
        foreach (var c in candidates.Where(c => c.Label != newest && c.Label != SharedFolder))
            if (!File.Exists(Dest(c.Rel)) || !FolderMove.SameContent(c.File, Dest(c.Rel))) CopyVerified(c.File, P(backup, c.Label, c.Rel));
        foreach (var w in winners.Where(w => File.Exists(Dest(w.Rel))))
            CopyVerified(Dest(w.Rel), P(backup, Dest(w.Rel).StartsWith(shared + System.IO.Path.DirectorySeparatorChar) ? SharedFolder : newest, w.Rel), overwrite: true);
        // 2. Se juntan en la nueva.
        foreach (var w in winners) CopyVerified(w.File, Dest(w.Rel), overwrite: true);
        // 3. Lo que esté suelto junto a las versiones va al respaldo (el mismo disco: se renombra), y la nueva queda en `Ports\<Port>`.
        foreach (var entry in Directory.EnumerateFileSystemEntries(folder)
            .Where(e => System.IO.Path.GetFileName(e) is var name && !versions.Contains(name) && name is not (SharedFolder or SnapshotsFolder)).ToList())
        {
            var to = P(backup, "_sueltos", System.IO.Path.GetFileName(entry));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(to)!);
            if (File.Exists(entry)) File.Move(entry, to);
            else Directory.Move(entry, to);
        }
        var next = folder + ".nueva";
        Directory.Move(keep, next);
        MoveCommon(folder, next);
        var old = folder + ".anterior";
        Directory.Move(folder, old);
        Directory.Move(next, folder);
        File.WriteAllText(P(folder, Recipes.VersionFile), newest);
        Links.DeleteTree(old); // solo las versiones viejas, con lo de la persona ya respaldado y junto: los enlaces se borran, no lo enlazado
        if (recipe is not null)
        {
            Recipes.MakePortable(recipe, folder);
            ShareSaves(recipe, folder, newest);
        }
        return newest;
    }

    /// <summary>Los archivos de una carpeta, relativos y con `/`, sin entrar a los enlaces ni a `_compartido` y `_respaldos`.</summary>
    static IEnumerable<string> Files(string root)
    {
        var pending = new Stack<string>([root]);
        while (pending.TryPop(out var dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir)) yield return System.IO.Path.GetRelativePath(root, file).Replace('\\', '/');
            foreach (var sub in Directory.EnumerateDirectories(dir))
                if (!Links.IsLink(sub) && !(dir == root && System.IO.Path.GetFileName(sub) is SharedFolder or SnapshotsFolder)) pending.Push(sub);
        }
    }

    /// <summary>Lo común a todas las versiones de un emulador, junto a ellas (decisión 308). Con `_`: no es una versión.</summary>
    public const string SharedFolder = "_compartido";
    /// <summary>Las copias puntuales de lo común, antes de que lo use una versión nueva.</summary>
    public const string SnapshotsFolder = "_respaldos";

    /// <summary>
    /// Las partidas y el contenido instalado (NAND, dev_hdd0, mlc01) son de todas las versiones (decisión 308): cada carpeta `saves`
    /// y `content` de la receta apunta, con un enlace, a la misma en `Emuladores\<Emulador>\_compartido`. Antes, si ya hay partidas en
    /// común, se hace una copia puntual de ellas (el contenido no: es grande y vuelve a instalarse desde `Juegos\`). Nunca se borra
    /// nada: lo que traiga la versión va a lo común si todavía no existe, y si ya existe queda al lado como `.antes-de-otteretro`.
    /// Unas partidas dentro de una carpeta de contenido (RPCS3: `dev_hdd0\home\…\savedata`) van con ella.
    /// </summary>
    /// <remarks>ponytail: mover una versión a otro disco deja su enlace apuntando a lo común del disco de origen. Una versión de antes
    /// de #280 con contenido propio lo deja en `.antes-de-otteretro` si lo común ya existe: no se mezclan.</remarks>
    public static void ShareSaves(Recipe recipe, string install, string version)
    {
        var folders = recipe.Content.Concat(recipe.Saves.Where(s => !recipe.Content.Any(c => s.StartsWith(c + "/", StringComparison.Ordinal)))).ToList();
        // Ya enlazada (se repite): no es una versión nueva para lo común.
        if (folders.All(folder => Links.IsLink(System.IO.Path.Combine(install, folder)))) return;
        var root = recipe.Kind == "port" ? install : System.IO.Path.GetDirectoryName(install)!; // un port: `Ports\<Port>\_compartido`
        var shared = System.IO.Path.Combine(root, SharedFolder);
        var snapshot = System.IO.Path.Combine(root, SnapshotsFolder, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-antes-de-{version}");
        foreach (var save in recipe.Saves.Select(s => System.IO.Path.Combine(shared, s)))
            if (Directory.Exists(save) && Directory.EnumerateFiles(save, "*", SearchOption.AllDirectories).Any())
                Snapshot(save, System.IO.Path.Combine(snapshot, System.IO.Path.GetRelativePath(shared, save)));
        foreach (var folder in folders)
        {
            var inside = System.IO.Path.Combine(install, folder);
            var common = System.IO.Path.Combine(shared, folder);
            if (!Links.IsLink(inside) && Directory.Exists(inside))
            {
                if (!Directory.EnumerateFileSystemEntries(inside).Any()) Directory.Delete(inside);
                else if (!Directory.Exists(common))
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(common)!);
                    Directory.Move(inside, common); // la misma carpeta del emulador: se renombra
                }
                else Directory.Move(inside, inside + ".antes-de-otteretro");
            }
            Directory.CreateDirectory(common);
            Links.Point(inside, common);
        }
    }

    /// <summary>Copia toda la carpeta, forzando la escritura y verificando cada archivo.</summary>
    static void Snapshot(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            CopyVerified(file, System.IO.Path.Combine(to, System.IO.Path.GetRelativePath(from, file)));
    }

    static void CopyVerified(string file, string target, bool overwrite = false)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
        File.Copy(file, target, overwrite);
        FolderMove.FlushToDisk(target);
        if (!FolderMove.SameContent(file, target)) throw new TaskException(ApiError.Of("move.copyMismatch", ("path", file), ("detail", target)));
    }

    /// <summary>El disco elegido para la segunda copia (decisión 303).</summary>
    public static string? BackupDisk(Database db)
    {
        using var c = db.Open();
        return c.ExecuteScalar<string?>("SELECT value FROM setting WHERE machine_id = '' AND name = @BackupSetting", new { BackupSetting });
    }

    public static LibraryBackup Backup(Database db)
    {
        var disk = BackupDisk(db);
        using var c = db.Open();
        return new(disk, c.ExecuteScalar<int>("SELECT count(*) FROM emulator_install WHERE backup_library_id IS NOT @disk", new { disk }));
    }

    /// <summary>Elige el disco de la segunda copia (null: ninguno) y, si le falta algo, lo copia. No puede ser el ★.</summary>
    public static ApiError? SetBackupDisk(Database db, string? disk)
    {
        if (disk is not null)
        {
            if (Disk.Get(db, disk) is not { Type: "disk" } chosen) return ApiError.Of("library.notFound");
            if (chosen.Default) return ApiError.Of("install.backupIsDefault");
        }
        using (var c = db.Open())
            if (disk is null) c.Execute("DELETE FROM setting WHERE machine_id = '' AND name = @BackupSetting", new { BackupSetting });
            else c.Execute("INSERT INTO setting (machine_id, name, value) VALUES ('', @BackupSetting, @disk) ON CONFLICT (name, machine_id) DO UPDATE SET value = excluded.value",
                new { BackupSetting, disk });
        EnqueueBackupIfPending(db);
        return null;
    }

    /// <summary>
    /// Si a la segunda copia le falta algo y su disco está conectado, encola copiarlo (salvo que ya esté en la cola). Devuelve la
    /// tarea, o null si no hay nada que copiar o el disco no está. La cola lo mira cada tanto: así se copia sola al conectar el disco.
    /// </summary>
    public static long? EnqueueBackupIfPending(Database db)
    {
        if (BackupDisk(db) is not { } disk) return null;
        using (var c = db.Open())
        {
            if (c.ExecuteScalar<long>("SELECT count(*) FROM emulator_install WHERE backup_library_id IS NOT @disk", new { disk }) == 0) return null;
            if (c.ExecuteScalar<long?>("SELECT id FROM task WHERE kind = @BackupKind AND status IN ('queued', 'running') LIMIT 1", new { BackupKind }) is { } waiting)
                return waiting;
        }
        if (Disk.Get(db, disk) is not { Connected: true, Default: false }) return null;
        return TaskQueue.Enqueue(db, BackupKind);
    }

    /// <summary>Copia a la librería del disco elegido, verificado, lo que todavía no tiene. Nunca borra la copia de un disco anterior.</summary>
    public static Task RunBackup(TaskRun run, CancellationToken ct)
    {
        if (Disk.DefaultDisk(run.Db) is not { Connected: true } home || BackupDisk(run.Db) is not { } id
            || Disk.Get(run.Db, id) is not { Connected: true, Default: false } other) throw new TaskException(ApiError.Of("library.notConnected"));
        using var c = run.Db.Open();
        var pending = c.Query<(string Emulator, string Version, string Kind, string? File)>("""
            SELECT i.emulator_id, i.version, e.kind, i.file FROM emulator_install i JOIN emulator e ON e.id = i.emulator_id
            WHERE i.backup_library_id IS NOT @id
            """, new { id }).ToList();
        for (var i = 0; i < pending.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (emulator, version, kind, file) = pending[i];
            run.Report(i * 100 / pending.Count, ApiError.Of("install.backup"));
            var original = System.IO.Path.Combine(ShelfPath(home.Path, kind, emulator, version), file ?? "");
            if (file is null || !File.Exists(original))
            {
                run.Log(ApiError.Of("install.notInLibrary", ("version", $"{emulator} {version}")), "warn");
                continue;
            }
            Store(original, ShelfPath(other.Path, kind, emulator, version));
            c.Execute("UPDATE emulator_install SET backup_library_id = @id WHERE emulator_id = @emulator AND version = @version", new { id, emulator, version });
        }
        return Task.CompletedTask;
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

    internal static async Task<string> Sha256(string file, CancellationToken ct)
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
