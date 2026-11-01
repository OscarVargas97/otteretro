using System.Text.Json;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>
/// Mover (§5.3) y pasar lo de antes al estándar (§5.5, #219), como tareas de la cola. Cada paso se puede repetir sin daño, así que
/// una tarea cortada sigue desde el principio y salta lo que ya hizo: es reanudable sin punto de control.
/// </summary>
public static class DiskTasks
{
    public const string Move = "library.move";
    public const string Migrate = "disk.migrate";

    /// <param name="From">Carpeta de origen (absoluta).</param>
    /// <param name="To">Carpeta de destino (absoluta, vacía o inexistente).</param>
    /// <param name="LibraryId">Se mueve la biblioteca entera: cambia su ruta.</param>
    /// <param name="FromLibrary">Se mueve una carpeta de una biblioteca (consola o juego): sus archivos cambian de ruta relativa.</param>
    public record MoveJob(string From, string To, string? LibraryId = null, string? FromLibrary = null, string? FromRelative = null,
        string? ToLibrary = null, string? ToRelative = null);

    static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    /// <summary>Valida y encola una mudanza. Devuelve el id de la tarea o el error.</summary>
    public static (long? Task, ApiError? Error) EnqueueMove(Database db, MoveJob job)
    {
        if (!Path.Exists(job.From)) return (null, ApiError.Of("path.notFound"));
        if (FolderMove.Validate(job.From, job.To) is { } error) return (null, error);
        return (TaskQueue.Enqueue(db, Move, JsonSerializer.Serialize(job with { From = Disk.Normalize(job.From), To = Disk.Normalize(job.To) }, Json)), null);
    }

    public static long EnqueueMigrate(Database db) => TaskQueue.Enqueue(db, Migrate);

    /// <summary>
    /// Al abrir la app (#219): si quedan carpetas de antes o cosas en `Herramientas` de la carpeta de datos, se encola «Pasar al
    /// estándar», salvo que ya esté en la cola. Devuelve la tarea, o null si no había nada que pasar.
    /// </summary>
    public static long? EnqueueMigrateIfPending(Database db)
    {
        var plan = Pending(db);
        if (plan.Locations.Count == 0 && !plan.Tools) return null;
        using (var c = db.Open())
            if (c.ExecuteScalar<long?>("SELECT id FROM task WHERE kind = @Migrate AND status IN ('queued', 'running') LIMIT 1", new { Migrate }) is { } waiting)
                return waiting;
        return EnqueueMigrate(db);
    }

    /// <summary>`Herramientas` de la carpeta de datos de esta base.</summary>
    public static string ToolsOf(Database db) => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(db.FilePath)!)!, "Herramientas");

    /// <summary>Dónde se instala lo nuevo (#204): el disco predeterminado; sin discos todavía, `Herramientas`.</summary>
    public static string ToolsHome(Database db) => Disk.DefaultDisk(db) is { Connected: true } d ? d.Path : ToolsOf(db);

    /// <param name="Locations">Las ubicaciones de un solo tipo de antes (#198), que pasan a la carpeta fija de su disco.</param>
    /// <param name="Tools">Hay cosas en `Herramientas` de la carpeta de datos que pasan al disco predeterminado.</param>
    public record MigrationPlan(List<DiskInfo> Locations, bool Tools);

    public static MigrationPlan Pending(Database db) =>
        new(Disk.List(db).Where(l => l.Type != "disk").ToList(), Recipes.Installed(ToolsOf(db)).Count > 0);

    /// <summary>
    /// Pasa todo al estándar (#159, #202, #204). Cada ubicación de un solo tipo va a la carpeta fija del disco donde está (en el
    /// mismo disco: se renombra), con su registro, sus archivos y las rutas de los emuladores; lo de `Herramientas` va al disco
    /// predeterminado. Se puede repetir: salta lo que ya hizo.
    /// </summary>
    public static Task RunMigrate(TaskRun run, CancellationToken ct)
    {
        var locations = Disk.List(run.Db).Where(l => l.Type != "disk").ToList();
        for (var i = 0; i < locations.Count; i++)
        {
            var location = locations[i];
            if (!location.Connected)
            {
                run.Log(ApiError.Of("library.log.skipped", ("name", location.Name)), "warn");
                continue;
            }
            run.Report(i * 70 / locations.Count, ApiError.Of("library.toDisk", ("name", location.Name)));
            var disk = DiskOf(run.Db, location.Path);
            if (location.Type == "game" && Disk.IsLegacy(location.Path)) AdoptLegacy(run, location, ct);
            var target = Disk.FolderOf(disk, location.Type)!;
            var entries = Directory.EnumerateFileSystemEntries(location.Path)
                .Where(e => Path.GetFileName(e) is not (Disk.MarkerFile or Disk.OldMarkerFile)).ToList();
            foreach (var entry in entries)
            {
                var to = Path.Combine(target, Path.GetFileName(entry));
                if (Path.Exists(to)) throw new TaskException(ApiError.Of("library.conflict", ("path", to)));
                MoveFolder(entry, to, (_, _) => { }, ct);
            }
            using (var c = run.Db.Open())
            {
                var prefix = Disk.DiskFolders[location.Type] + Path.DirectorySeparatorChar;
                c.Execute("UPDATE source SET library_id = @disk, relative_path = @prefix || relative_path WHERE kind = 'file' AND library_id = @old",
                    new { disk = disk.Id, prefix, old = location.Id });
                c.Execute("UPDATE console_destination SET library_id = @disk WHERE library_id = @old", new { disk = disk.Id, old = location.Id });
                c.Execute("DELETE FROM library WHERE id = @old", new { old = location.Id });
            }
            var installed = Recipes.Installed(Disk.ToolRoots(run.Db));
            var changed = RewriteAll(installed, location.Path, target);
            Relink(installed, location.Path, target);
            // La carpeta de antes, solo con su marcador, se borra (nunca con archivos).
            foreach (var marker in new[] { Disk.MarkerFile, Disk.OldMarkerFile }) File.Delete(Path.Combine(location.Path, marker));
            if (!Directory.EnumerateFileSystemEntries(location.Path).Any()) Directory.Delete(location.Path);
            run.Log(ApiError.Of("library.log.moved", ("from", location.Path), ("to", target), ("paths", changed.ToString())));
        }

        run.Report(75, ApiError.Of("library.toolsToDisk"));
        if (Disk.DefaultDisk(run.Db) is { Connected: true } home)
            foreach (var tool in Recipes.Installed(ToolsOf(run.Db)))
            {
                var to = Path.Combine(home.Path, Path.GetRelativePath(ToolsOf(run.Db), tool.Path));
                if (Path.Exists(to))
                {
                    run.Log(ApiError.Of("library.log.notEmpty", ("path", to)), "warn");
                    continue;
                }
                MoveFolder(tool.Path, to, (_, _) => { }, ct);
                Relink(Recipes.Installed(Disk.ToolRoots(run.Db)), tool.Path, to);
                run.Log(ApiError.Of("library.log.tool", ("name", tool.Name), ("path", to)));
            }
        Scan.Enqueue(run.Db); // los juegos cambiaron de lugar (#212)
        return Task.CompletedTask;
    }

    /// <summary>El disco de la unidad donde está <paramref name="path"/>; si no hay, se agrega en su raíz (#202).</summary>
    static DiskInfo DiskOf(Database db, string path)
    {
        var network = Disk.IsNetwork(path);
        var drive = network ? Path.GetPathRoot(path)! : Mounts.Of(path);
        if (Disk.List(db).FirstOrDefault(l => l.Type == "disk" && l.Connected
                && (network ? Disk.Same(Path.GetPathRoot(l.Path)!, drive) : Mounts.Same(l.Path, path))) is { } disk) return disk;
        var root = Path.Combine(drive, "Otteretro");
        Directory.CreateDirectory(root);
        var (id, error) = Disk.Adopt(db, root, Path.TrimEndingDirectorySeparator(drive), "disk");
        if (error is not null) throw new TaskException(error);
        return Disk.Get(db, id!)!;
    }

    public static Task RunMove(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<MoveJob>(run.Data!, Json)!;
        MoveFolder(job.From, job.To, (done, total) => run.Report(Percent(done, total, 0, 90), ApiError.Of("library.copying", ("path", job.To))), ct);
        run.Report(92, ApiError.Of("library.updating"));
        using (var c = run.Db.Open())
        {
            if (job.LibraryId is not null) c.Execute("UPDATE library SET path = @To WHERE id = @LibraryId", job);
            else if (job.FromLibrary is not null)
                MoveSources(c, job.FromLibrary, job.FromRelative!, job.ToLibrary ?? job.FromLibrary, job.ToRelative!);
        }
        var installed = Recipes.Installed(Disk.ToolRoots(run.Db));
        var changed = RewriteAll(installed, job.From, job.To);
        Relink(installed, job.From, job.To);
        run.Log(ApiError.Of("library.log.moved", ("from", job.From), ("to", job.To), ("paths", changed.ToString())));
        Scan.Enqueue(run.Db); // al terminar una mudanza (#212)
        return Task.CompletedTask;
    }

    /// <summary>
    /// Lleva una carpeta con la estructura antigua a la de §5.1: emuladores y ports a Herramientas con su receta, sus
    /// complementos aparte (enlazados donde el emulador los busca), y los juegos de `<Consola>\Juegos\<Juego>` a `<Consola>\<Juego>`.
    /// </summary>
    static void AdoptLegacy(TaskRun run, DiskInfo library, CancellationToken ct)
    {
        var tools = ToolsHome(run.Db);
        var consoles = Directory.EnumerateDirectories(library.Path).Where(c => !Path.GetFileName(c).StartsWith('_')).ToList();

        // 1. Emuladores y ports a Herramientas.
        var installs = consoles.SelectMany(c => new[] { "Emuladores", "Ports" }.Select(g => Path.Combine(c, g)))
            .Where(Directory.Exists).SelectMany(Directory.EnumerateDirectories).ToList();
        for (var i = 0; i < installs.Count; i++)
        {
            var install = installs[i];
            var recipe = Recipes.Identify(install);
            var version = Recipes.VersionOf(recipe, install);
            var group = Path.GetFileName(Path.GetDirectoryName(install)!);
            var console = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(install)!)!);
            var target = recipe is not null ? Recipes.Target(tools, recipe, version) : Path.Combine(tools, group, console, Path.GetFileName(install), version);
            var (from, to) = (i * 60 / installs.Count, (i + 1) * 60 / installs.Count);
            MoveFolder(install, target, (done, total) => run.Report(Percent(done, total, from, to), ApiError.Of("library.copying", ("path", target))), ct);
            run.Log(ApiError.Of("library.log.tool", ("name", Path.GetFileName(install)), ("path", target)));
        }

        // 2. Complementos aparte y cada instalación portable.
        run.Report(62, ApiError.Of("library.portable"));
        foreach (var tool in Recipes.Installed(tools).Where(t => t.RecipeId is not null && t.Kind != "complement"))
        {
            var recipe = Recipes.Find(tool.RecipeId!)!;
            foreach (var complement in Recipes.All.Where(r => r.Parent == recipe.Id))
            {
                var inside = Path.Combine(tool.Path, complement.Link!);
                if (!Directory.Exists(inside) || Links.IsLink(inside)) continue;
                var target = Recipes.Target(tools, complement, Recipes.VersionOf(complement, inside));
                MoveFolder(inside, target, (_, _) => { }, ct);
                Links.Point(inside, target);
                run.Log(ApiError.Of("library.log.tool", ("name", complement.Name), ("path", target)));
            }
            Recipes.MakePortable(recipe, tool.Path);
        }

        // 3. Juegos: <Consola>\Juegos\<Juego> → <Consola>\<Juego>, y las rutas que apuntaban ahí.
        for (var i = 0; i < consoles.Count; i++)
        {
            var console = consoles[i];
            var games = Path.Combine(console, "Juegos");
            run.Report(65 + i * 30 / consoles.Count, ApiError.Of("library.games", ("console", Path.GetFileName(console))));
            if (Directory.Exists(games))
                foreach (var entry in Directory.EnumerateFileSystemEntries(games).ToList())
                {
                    var target = Path.Combine(console, Path.GetFileName(entry));
                    if (Path.Exists(target)) throw new TaskException(ApiError.Of("library.conflict", ("path", target)));
                    if (Directory.Exists(entry)) Directory.Move(entry, target); // misma biblioteca, mismo disco: se renombra
                    else File.Move(entry, target);
                }
            RewriteAll(tools, games, console);
            using (var c = run.Db.Open())
                MoveSources(c, library.Id, Path.Combine(Path.GetFileName(console), "Juegos"), library.Id, Path.GetFileName(console));
        }

        // 4. Las carpetas de la estructura antigua, si quedaron vacías (nunca se borra nada con archivos).
        foreach (var old in consoles.SelectMany(c => Disk.LegacyFolders.Select(l => Path.Combine(c, l))).Where(Directory.Exists))
            if (!Directory.EnumerateFileSystemEntries(old).Any()) Directory.Delete(old);
            else run.Log(ApiError.Of("library.log.notEmpty", ("path", old)), "warn");
    }

    /// <summary>Reescribe las rutas a juegos de todo lo instalado con receta. Devuelve cuántas cambió.</summary>
    public static int RewriteAll(string tools, string from, string to) => RewriteAll(Recipes.Installed(tools), from, to);

    static int RewriteAll(List<ToolInfo> installed, string from, string to) =>
        installed.Where(t => t.RecipeId is not null).Sum(t => Recipes.RewriteGamePaths(Recipes.Find(t.RecipeId!)!, t.Path, from, to));

    /// <summary>
    /// Tras mover una herramienta o una ubicación de herramientas (#198): los complementos enlazados dentro de un emulador o
    /// port apuntan a su ruta nueva, y lo que quedó dentro del destino vuelve a hacerse portable, para que sus enlaces de
    /// fuera (como el de AppData de Dusklight) apunten a la instalación nueva.
    /// </summary>
    static void Relink(List<ToolInfo> installed, string from, string to)
    {
        foreach (var tool in installed.Where(t => t.RecipeId is not null && t.Kind != "complement"))
        {
            var recipe = Recipes.Find(tool.RecipeId!)!;
            foreach (var complement in Recipes.All.Where(r => r.Parent == recipe.Id))
            {
                var inside = Path.Combine(tool.Path, complement.Link!);
                if (Links.IsLink(inside) && Recipes.Moved(new DirectoryInfo(inside).LinkTarget!.Replace(@"\??\", ""), from, to) is { } moved)
                    Links.Point(inside, moved);
            }
            if ((Disk.Same(tool.Path, to) || Disk.Inside(tool.Path, to)) && recipe.Portable.Any(s => s.Type == "junction"))
                Recipes.MakePortable(recipe, tool.Path);
        }
    }

    /// <summary>Los archivos registrados bajo <paramref name="fromRelative"/> pasan a <paramref name="toRelative"/> (§5.4: id de biblioteca + ruta relativa).</summary>
    static void MoveSources(Microsoft.Data.Sqlite.SqliteConnection c, string fromLibrary, string fromRelative, string toLibrary, string toRelative)
    {
        foreach (var sep in new[] { "\\", "/" })
            c.Execute("""
                UPDATE source SET library_id = @toLibrary, relative_path = @toRelative || substr(relative_path, length(@fromRelative) + 1)
                WHERE kind = 'file' AND library_id = @fromLibrary AND (relative_path = @fromRelative OR substr(relative_path, 1, length(@prefix)) = @prefix)
                """, new { fromLibrary, fromRelative, toLibrary, toRelative, prefix = fromRelative + sep });
    }

    /// <summary>
    /// Mueve una carpeta sin perder nada (§5.3). En el mismo disco la renombra. Entre discos copia lo que falta (si se cortó,
    /// lo ya copiado con el mismo tamaño se salta), fuerza la escritura, verifica todo por hash y recién entonces borra el origen.
    /// Los enlaces que haya adentro se rehacen en el destino apuntando a lo mismo, sin copiar lo enlazado.
    /// </summary>
    public static void MoveFolder(string from, string to, Action<long, long> report, CancellationToken ct, bool forceCopy = false)
    {
        if (File.Exists(from))
        {
            MoveFile(from, to);
            return;
        }
        if (!Directory.Exists(from))
        {
            if (Directory.Exists(to)) return; // ya se movió en una corrida anterior
            throw new TaskException(ApiError.Of("path.notFound"));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        if (!forceCopy && Mounts.Same(from, to) && !Directory.Exists(to))
        {
            Directory.Move(from, to);
            return;
        }

        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        var links = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 };
        var files = Directory.EnumerateFiles(from, "*", options).Select(f => (relative: Path.GetRelativePath(from, f), size: new FileInfo(f).Length)).ToList();
        var total = files.Sum(f => f.size) * 2;
        long done = 0;
        foreach (var (relative, size) in files)
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(to, relative);
            if (!File.Exists(target) || new FileInfo(target).Length != size)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(from, relative), target, overwrite: true);
                FolderMove.FlushToDisk(target);
            }
            report(done += size, total);
        }
        foreach (var folder in Directory.EnumerateDirectories(from, "*", options))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, folder)));
        foreach (var link in Directory.EnumerateDirectories(from, "*", links).Where(Links.IsLink))
            Links.Point(Path.Combine(to, Path.GetRelativePath(from, link)), new DirectoryInfo(link).LinkTarget!.Replace(@"\??\", ""));
        foreach (var (relative, size) in files)
        {
            ct.ThrowIfCancellationRequested();
            if (!FolderMove.SameContent(Path.Combine(from, relative), Path.Combine(to, relative)))
            {
                File.Delete(Path.Combine(to, relative)); // la próxima corrida lo vuelve a copiar
                throw new TaskException(ApiError.Of("move.copyMismatch", ("path", from), ("detail", relative)));
            }
            report(done += size, total);
        }
        Links.DeleteTree(from);
    }

    /// <summary>Un archivo suelto (una ROM, #160): igual que una carpeta, copiar, verificar y recién entonces borrar.</summary>
    static void MoveFile(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        if (Mounts.Same(from, to) && !File.Exists(to))
        {
            File.Move(from, to);
            return;
        }
        File.Copy(from, to, overwrite: true);
        FolderMove.FlushToDisk(to);
        if (!FolderMove.SameContent(from, to))
        {
            File.Delete(to);
            throw new TaskException(ApiError.Of("move.copyMismatch", ("path", from), ("detail", Path.GetFileName(from))));
        }
        File.Delete(from);
    }

    /// <summary>
    /// Mover lo marcado de un disco a otro (#160, #205): cada cosa a la misma ruta dentro del otro, en su propia tarea. Se valida
    /// todo antes de encolar nada.
    /// </summary>
    public static (List<long>? Tasks, ApiError? Error) EnqueueMoveItems(Database db, DiskInfo from, DiskInfo to, IEnumerable<string> items)
    {
        var jobs = new List<MoveJob>();
        foreach (var item in items)
        {
            var relative = item.Trim('\\', '/');
            var source = Path.GetFullPath(Path.Combine(from.Path, relative));
            var target = Path.GetFullPath(Path.Combine(to.Path, relative));
            if (relative.Length == 0 || !Disk.Inside(source, from.Path) || !Disk.Inside(target, to.Path))
                return (null, ApiError.Of("library.outside"));
            if (!Path.Exists(source)) return (null, ApiError.Of("path.notFound"));
            // Una carpeta (toda una consola, #171) se junta con la que ya tiene el otro disco si nada de adentro choca.
            if (Path.Exists(target) && Clash(source, target) is { } clash) return (null, ApiError.Of("library.conflict", ("path", clash)));
            jobs.Add(new(source, target, FromLibrary: from.Id, FromRelative: relative, ToLibrary: to.Id, ToRelative: relative));
        }
        var tasks = new List<long>();
        foreach (var job in jobs)
        {
            var (task, error) = EnqueueMove(db, job);
            if (error is not null) return (tasks, error);
            tasks.Add(task!.Value);
        }
        return (tasks, null);
    }

    /// <summary>Lo primero de <paramref name="source"/> que ya existe en <paramref name="target"/>; null si se pueden juntar.</summary>
    static string? Clash(string source, string target) =>
        !Directory.Exists(source) || !Directory.Exists(target) ? target
            : Directory.EnumerateFileSystemEntries(source).Select(e => Path.Combine(target, Path.GetFileName(e))).FirstOrDefault(Path.Exists);

    static int Percent(long done, long total, int from, int to) => total == 0 ? to : from + (int)((to - from) * done / total);
}
