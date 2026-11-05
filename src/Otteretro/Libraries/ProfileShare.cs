using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <param name="GameId">El juego del catálogo.</param>
/// <param name="VersionId">La versión del catálogo (el juego en su consola).</param>
/// <param name="Key">Su TitleID o serial: lo encuentra si el otro catálogo numera distinto.</param>
public record SharedGame(long GameId, long VersionId, string Platform, string Name, string? Key);
/// <param name="File">El archivo del emulador dentro del paquete, en `emulador/`.</param>
public record SharedEmulator(string Id, string Name, string Kind, string Version, string File, string Sha256);
/// <param name="Path">Dentro de `perfil/` del paquete; las mismas rutas que dentro del emulador.</param>
public record SharedFile(string Path, string Sha256);
/// <param name="Settings">Los ajustes del perfil (JSON), tal como están en la base.</param>
public record ProfileManifest(int Format, SharedGame Game, SharedEmulator Emulator, string Name, string Settings, List<SharedFile> Files);

/// <summary>
/// Compartir perfiles (#282, decisión 297): un .zip con el emulador (la versión exacta, desde la librería), los archivos del perfil
/// (configuración y mods) y un manifiesto con versiones y hashes. Nunca lleva keys, BIOS, firmware, juegos, partidas ni la licencia
/// zRIF. Cargarlo en otro Otteretro instala siempre la copia del paquete, al lado de las versiones que ya haya, y crea el perfil.
/// </summary>
public static class ProfileShare
{
    public const string ImportKind = "profiles.import";
    const string ManifestEntry = "otteretro-perfil.json";
    const string EmulatorFolder = "emulador/";
    const string ProfileFolder = "perfil/";
    static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    /// <summary>Carpetas que nunca salen del equipo: partidas, keys, firmware, BIOS, NAND y licencias.</summary>
    static readonly HashSet<string> PrivateFolders = new(StringComparer.OrdinalIgnoreCase)
        { "keys", "firmware", "bios", "nand", "sysdata", "dev_flash", "license", "saves", "savedata", "save" };

    /// <summary>
    /// Lo que no se comparte: dentro de una carpeta privada, un archivo de keys o de BIOS, una carpeta guardada antes de Otteretro, o
    /// las partidas y el contenido de la receta (con <paramref name="config"/>, también su configuración global, que puede llevar rutas
    /// de la persona). La ruta, relativa; también se mira sin su primera carpeta (una release que trae todo dentro de una).
    /// </summary>
    /// <remarks>ponytail: por nombre de carpeta y de archivo; una BIOS suelta con otro nombre (scph1001.bin) en una carpeta cualquiera
    /// se colaría. Solo pasa con un emulador instalado a mano: los de receta o URL van tal como se publicaron.</remarks>
    internal static bool IsPrivate(string relative, Recipe? recipe, bool config)
    {
        var parts = relative.Replace('\\', '/').Trim('/').Split('/');
        var name = parts[^1];
        if (parts[..^1].Any(p => PrivateFolders.Contains(p) || p.EndsWith(".antes-de-otteretro", StringComparison.OrdinalIgnoreCase))) return true;
        if (name.EndsWith(".keys", StringComparison.OrdinalIgnoreCase) || name.Equals("keys.txt", StringComparison.OrdinalIgnoreCase)
            || name.Contains("bios", StringComparison.OrdinalIgnoreCase)) return true;
        if (recipe is null) return false;
        var paths = recipe.Saves.Concat(recipe.Content).Concat(config ? recipe.Config : []).ToList();
        bool Under(string r) => paths.Any(p => r.Equals(p, StringComparison.OrdinalIgnoreCase) || r.StartsWith(p.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
        return Under(string.Join('/', parts)) || (parts.Length > 1 && Under(string.Join('/', parts[1..])));
    }

    static string Hash(Stream s) => Convert.ToHexStringLower(SHA256.HashData(s));
    static string Hash(string file) { using var s = File.OpenRead(file); return Hash(s); }

    // ── Compartir ───────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Arma el paquete del perfil en <paramref name="path"/> (un .zip nuevo: nunca pisa uno que ya esté).</summary>
    /// <remarks>ponytail: se arma en la misma petición; con emuladores de cientos de MB tarda unos segundos. Pasarlo a una tarea si molesta.</remarks>
    public static ApiError? Export(Database db, long id, string path)
    {
        if (!Path.IsPathFullyQualified(path) || !path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(Path.GetDirectoryName(path)))
            return ApiError.Of("profile.share.badPath");
        if (Path.Exists(path)) return ApiError.Of("profile.share.exists", ("path", path));
        using var c = db.Open();
        if (Profiles.Get(c, id) is not { GameVersionId: { } version } p) return ApiError.Of("profile.notFound");
        var install = Installs.List(db).LastOrDefault(i => i.EmulatorId == p.EmulatorId && (p.EmulatorVersion is null || i.Version == p.EmulatorVersion));
        if (Disk.DefaultDisk(db) is not { Connected: true } home) return ApiError.Of("library.notConnected");
        var original = install is { File: { } file } ? Path.Combine(Installs.ShelfPath(home.Path, install.Kind, install.EmulatorId, install.Version), file) : null;
        if (install is null || original is null || !File.Exists(original)) return ApiError.Of("profile.share.notInLibrary", ("emulator", install?.Name ?? p.EmulatorId));
        if (Hash(original) != install.Sha256) return ApiError.Of("install.badHash", ("version", install.Version));

        var game = c.QuerySingle<(long GameId, string Platform, string Name)>(
            "SELECT v.game_id, v.platform, g.name_es FROM version v JOIN game g ON g.id = v.game_id WHERE v.id = @version", new { version });
        var key = Profiles.GameKey(c, version, p.SourceId);
        // Activo: lo último que cambió en el emulador.
        if (c.ExecuteScalar<bool>("SELECT active FROM profile WHERE id = @id", new { id })) Profiles.Capture(db, c, p);
        var recipe = Recipes.Find(p.EmulatorId);
        var store = Profiles.StoreOf(db, id);
        var files = (Directory.Exists(store) ? Directory.EnumerateFiles(store, "*", SearchOption.AllDirectories) : Enumerable.Empty<string>())
            .Select(f => Path.GetRelativePath(store, f).Replace('\\', '/'))
            .Where(r => r != Profiles.ChangedFile && !IsPrivate(r, recipe, config: false)).Order().ToList();

        // La licencia del juego nunca sale: si algún archivo o los ajustes la tienen, no se comparte.
        if (c.ExecuteScalar<string?>("SELECT zrif FROM version_license WHERE version_id = @version", new { version }) is { Length: > 0 } zrif)
        {
            var bytes = Encoding.UTF8.GetBytes(zrif);
            var leak = files.FirstOrDefault(r => File.ReadAllBytes(Path.Combine(store, r)).AsSpan().IndexOf(bytes) >= 0);
            if (leak is not null || p.Settings.Contains(zrif, StringComparison.Ordinal)) return ApiError.Of("profile.share.private", ("path", leak ?? "settings"));
        }

        var work = Path.Combine(home.Path, Installs.LibraryFolder, ".trabajo", "compartir-" + Guid.NewGuid().ToString("N"));
        var partial = path + ".parcial";
        try
        {
            // Lo instalado a mano o subido es una copia de la carpeta de la persona: se limpia. Lo de una receta o URL va tal cual.
            var emulator = original;
            if (install.Origin is "upload" or "adopted" && Archives.IsArchive(original))
            {
                if (Archives.Extract(original, Path.Combine(work, "x"), [], CancellationToken.None) is { } error) return error;
                emulator = Path.Combine(work, Path.GetFileNameWithoutExtension(original) + ".zip");
                var root = Path.Combine(work, "x");
                using var clean = ZipFile.Open(emulator, ZipArchiveMode.Create);
                foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    var r = Path.GetRelativePath(root, f).Replace('\\', '/');
                    if (!IsPrivate(r, recipe, config: true)) clean.CreateEntryFromFile(f, r);
                }
            }
            var manifest = new ProfileManifest(1, new(game.GameId, version, game.Platform, game.Name, key),
                new(p.EmulatorId, install.Name, install.Kind, install.Version, Path.GetFileName(emulator), Hash(emulator)), p.Name, p.Settings,
                files.Select(r => new SharedFile(r, Hash(Path.Combine(store, r)))).ToList());
            using (var zip = ZipFile.Open(partial, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(emulator, EmulatorFolder + manifest.Emulator.File, CompressionLevel.NoCompression);
                foreach (var r in files) zip.CreateEntryFromFile(Path.Combine(store, r), ProfileFolder + r);
                using var w = zip.CreateEntry(ManifestEntry).Open();
                JsonSerializer.Serialize(w, manifest, new JsonSerializerOptions(Json) { WriteIndented = true });
            }
            FolderMove.FlushToDisk(partial);
            File.Move(partial, path);
            return null;
        }
        finally
        {
            if (Directory.Exists(work)) Directory.Delete(work, recursive: true);
            File.Delete(partial);
        }
    }

    // ── Cargar ──────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>El manifiesto, si el archivo es un paquete válido: nombres sin rutas raras y archivos dentro de `perfil/`.</summary>
    static ProfileManifest? Read(ZipArchive zip)
    {
        try
        {
            if (zip.GetEntry(ManifestEntry) is not { } entry) return null;
            using var s = entry.Open();
            var m = JsonSerializer.Deserialize<ProfileManifest>(s, Json);
            if (m is not { Format: 1, Emulator: { Kind: "emulator" or "port" } e, Game: not null, Files: not null, Settings: not null }
                || Installs.BadName(e.Id) || Installs.BadName(e.Name) || Installs.BadName(e.Version) || Installs.BadName(e.File) || string.IsNullOrWhiteSpace(m.Name)
                || m.Files.Any(f => f.Path.Split('/').Any(part => part is "" or "." or ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)))
                return null;
            return m;
        }
        catch (JsonException) { return null; }
    }

    static ProfileManifest? Read(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            return Read(zip);
        }
        catch (Exception e) when (e is InvalidDataException or IOException) { return null; }
    }

    /// <summary>La versión del juego en este catálogo: la misma, o la que tiene su TitleID o serial.</summary>
    static long? VersionFor(SqliteConnection c, SharedGame g) =>
        c.ExecuteScalar<long?>("SELECT id FROM version WHERE id = @VersionId AND platform = @Platform", g)
        ?? (g.Key is null ? null : c.ExecuteScalar<long?>("""
            SELECT v.id FROM version v WHERE v.platform = @Platform AND (
                v.id IN (SELECT version_id FROM catalog.version_code WHERE code = @Key COLLATE NOCASE)
                OR v.id IN (SELECT version_id FROM source WHERE title_id = @Key COLLATE NOCASE))
            ORDER BY v.id LIMIT 1
            """, g));

    /// <summary>Valida el paquete y encola cargarlo. Si el juego no está en el catálogo, lo dice y no instala nada.</summary>
    public static (long? Task, ApiError? Error) EnqueueImport(Database db, string path)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path)) return (null, ApiError.Of("profile.import.notFound"));
        if (Read(path) is not { } m) return (null, ApiError.Of("profile.import.badPackage"));
        using (var c = db.Open())
            if (VersionFor(c, m.Game) is null) return (null, ApiError.Of("profile.import.noGame", ("name", m.Game.Name)));
        if (Disk.DefaultDisk(db) is not { Connected: true }) return (null, ApiError.Of("library.notConnected"));
        return (TaskQueue.Enqueue(db, ImportKind, Disk.Normalize(path)), null);
    }

    /// <summary>
    /// Carga el paquete: comprueba cada hash, instala la copia del emulador que trae (con su versión o, si ya está, al lado con
    /// otro nombre: nunca pisa una) y crea el perfil, sin activar, para esa versión del juego.
    /// </summary>
    public static async Task RunImport(TaskRun run, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(run.Data!);
        var m = Read(zip) ?? throw new TaskException(ApiError.Of("profile.import.badPackage"));
        run.Report(2, ApiError.Of("profile.import.checking"));
        string EntryHash(string name)
        {
            using var s = (zip.GetEntry(name) ?? throw new TaskException(ApiError.Of("profile.import.badPackage"))).Open();
            return Hash(s);
        }
        if (EntryHash(EmulatorFolder + m.Emulator.File) != m.Emulator.Sha256 || m.Files.Any(f => EntryHash(ProfileFolder + f.Path) != f.Sha256))
            throw new TaskException(ApiError.Of("profile.import.badHash"));
        long version;
        using (var c = run.Db.Open())
            version = VersionFor(c, m.Game) ?? throw new TaskException(ApiError.Of("profile.import.noGame", ("name", m.Game.Name)));
        if (Disk.DefaultDisk(run.Db) is not { Connected: true } home) throw new TaskException(ApiError.Of("library.notConnected"));

        // El emulador: siempre la copia del paquete, al lado de las versiones que ya haya.
        var job = new InstallJob(m.Emulator.Id, m.Emulator.Version, Name: m.Emulator.Name, Kind: m.Emulator.Kind);
        for (var n = 1; Installs.Taken(run.Db, job); n++)
            job = job with { Version = n == 1 ? $"{m.Emulator.Version}-compartido" : $"{m.Emulator.Version}-compartido-{n}" };
        var work = Path.Combine(home.Path, Installs.LibraryFolder, ".trabajo", $"{run.Id}-perfil");
        try
        {
            Directory.CreateDirectory(work);
            var file = Path.Combine(work, m.Emulator.File);
            zip.GetEntry(EmulatorFolder + m.Emulator.File)!.ExtractToFile(file);
            await Installs.Install(run, job with { Path = file }, ct);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }

        // El perfil, con sus archivos.
        run.Report(90, ApiError.Of("profile.import.creating"));
        using var db = run.Db.Open();
        var platform = db.ExecuteScalar<string>("SELECT platform FROM version WHERE id = @version", new { version });
        if (m.Emulator.Kind == "emulator") // uno sin receta también tiene que aparecer en su consola
            db.Execute("INSERT OR IGNORE INTO console_emulator (console_id, emulator_id, is_base) VALUES (@platform, @Id, 0)", new { platform, m.Emulator.Id });
        var id = db.ExecuteScalar<long>("""
            INSERT INTO profile (emulator_id, emulator_version, game_version_id, name, settings, created_at)
            VALUES (@emulator, @emulatorVersion, @version, @name, @settings, @now) RETURNING id
            """, new { emulator = m.Emulator.Id, emulatorVersion = job.Version, version, name = m.Name.Trim(), settings = m.Settings, now = DateTime.UtcNow.ToString("O") });
        var store = Profiles.StoreOf(run.Db, id);
        foreach (var f in m.Files)
        {
            var to = Path.Combine(store, f.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            zip.GetEntry(ProfileFolder + f.Path)!.ExtractToFile(to);
        }
        if (db.ExecuteScalar<long>("SELECT count(*) FROM source WHERE version_id = @version", new { version }) == 0)
            run.Log(ApiError.Of("profile.import.log.notOwned", ("name", m.Game.Name)), "warn");
        run.Log(ApiError.Of("profile.import.log.done", ("name", m.Name), ("game", m.Game.Name), ("emulator", m.Emulator.Name), ("version", job.Version!)));
    }
}
