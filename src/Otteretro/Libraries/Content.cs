using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <param name="Action">pointed (apuntado sin copiar), installed (con el instalador del emulador), already (ya instalado), ps4 (instalarlo
/// a mano en shadPS4), license (falta su licencia), manual (el emulador no lo apunta ni lo instala solo) o failed.</param>
/// <param name="File">El archivo en `Juegos\`.</param>
public record ContentStep(string Action, string File);

/// <summary>
/// UPD, DLC y lo que se instala dentro del emulador (decisión 295, #280): «preparar el juego para este emulador», lo que llamará Jugar
/// la primera vez que se abre con él. Según la receta, el emulador apunta a los archivos donde están, sin copiarlos (`contentPaths`), o
/// se instalan con su línea de comandos (`install`) en su contenido común (decisión 308). Los originales siguen en `Juegos\`.
/// PS4: solo aviso de qué instalar en shadPS4 (Otteretro no descifra nada).
/// </summary>
public static class Content
{
    public const string Kind = "content.prepare";

    public record Job(long Version, string? Emulator);

    record Target(Recipe Recipe, string Install, string EmulatorVersion, string Console);

    /// <summary>Para las pruebas: corre en lugar del emulador (ejecutable y argumentos) y devuelve su código de salida.</summary>
    public static Func<string, IReadOnlyList<string>, int>? Runner { get; set; }

    static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    /// <summary>
    /// El emulador con el que se juega: el pedido o, si no, el del perfil activo o el base de su consola (#267), o el primero instalado
    /// que corre esa consola. La versión: la del perfil, o la más nueva instalada.
    /// </summary>
    static (Target? Target, ApiError? Error) Resolve(Database db, long version, string? emulator)
    {
        string? console;
        using (var c = db.Open()) console = c.ExecuteScalar<string?>("SELECT platform FROM version WHERE id = @version", new { version });
        if (console is null) return (null, ApiError.Of("content.unknownGame"));
        var profile = Profiles.Effective(db, version);
        var installs = Installs.List(db).Where(i => i.Path is not null).ToList();
        emulator ??= profile?.EmulatorId ?? installs.Select(i => Recipes.Find(i.EmulatorId)).OfType<Recipe>().Where(r => r.Consoles.Contains(console))
            .OrderBy(r => r.BaseFor.Contains(console) ? 0 : 1).FirstOrDefault()?.Id;
        if (emulator is null || Recipes.Find(emulator) is not { } recipe) return (null, ApiError.Of("content.noEmulator"));
        var wanted = profile?.EmulatorId == emulator ? profile.EmulatorVersion : null;
        if (installs.LastOrDefault(i => i.EmulatorId == emulator && (wanted is null || i.Version == wanted || i.Kind == "port")) is not { } install)
            return (null, ApiError.Of("content.notInstalled", ("name", recipe.Name)));
        return (new(recipe, install.Path!, install.Version, console), null);
    }

    public static (long? Task, ApiError? Error) Enqueue(Database db, long version, string? emulator)
    {
        if (Resolve(db, version, emulator).Error is { } error) return (null, error);
        return (TaskQueue.Enqueue(db, Kind, JsonSerializer.Serialize(new Job(version, emulator), Json)), null);
    }

    public static Task Run(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<Job>(run.Data!, Json)!;
        var (steps, error) = Prepare(run.Db, job.Version, job.Emulator, ct);
        if (error is not null) throw new TaskException(error);
        foreach (var s in steps!)
            run.Log(ApiError.Of($"content.log.{s.Action}", ("file", s.File)), s.Action is "pointed" or "installed" or "already" ? "info" : "warn");
        return Task.CompletedTask;
    }

    /// <summary>Deja el contenido del juego disponible en el emulador. Se puede repetir: lo apuntado se reescribe y lo instalado no se repite.</summary>
    public static (List<ContentStep>? Steps, ApiError? Error) Prepare(Database db, long version, string? emulator, CancellationToken ct = default)
    {
        var (target, error) = Resolve(db, version, emulator);
        if (target is not { Recipe: var recipe, Install: var install }) return (null, error);
        Installs.ShareSaves(recipe, install, target.EmulatorVersion);
        var disks = Disk.List(db).Where(d => d.Connected).ToDictionary(d => d.Id, d => d.Path);
        using var c = db.Open();
        // Wii U (decisión 295): sus UPD y DLC son carpetas (`meta\meta.xml`) que el escaneo cuenta como juego; los distingue su TitleID.
        var files = c.Query<(long Id, string LibraryId, string Relative, string? Component, string? TitleId)>(
                "SELECT id, library_id, relative_path, component, title_id FROM source WHERE kind = 'file' AND version_id = @version ORDER BY relative_path", new { version })
            .Where(f => disks.ContainsKey(f.LibraryId))
            .Select(f => (f.Id, Path: System.IO.Path.Combine(disks[f.LibraryId], f.Relative), Component: target.Console == "wiiu" ? WiiUComponent(f.TitleId) : f.Component))
            .Where(f => System.IO.Path.Exists(f.Path)).ToList();
        var zrif = c.ExecuteScalar<string?>("SELECT zrif FROM version_license WHERE version_id = @version", new { version });
        var steps = new List<ContentStep>();
        var pointed = new List<string>();
        foreach (var (id, file, component) in files)
        {
            ct.ThrowIfCancellationRequested();
            var extension = System.IO.Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
            var installer = recipe.Install.FirstOrDefault(i => i.Formats.Contains(extension));
            if (target.Console == "ps4")
            {
                if (extension == "pkg") steps.Add(new("ps4", file));
                continue;
            }
            if (component is not ("UPD" or "DLC") && installer is null) continue; // la BASE se abre tal cual
            if (recipe.ContentPaths.Count > 0)
            {
                pointed.Add(System.IO.Path.GetDirectoryName(file)!);
                steps.Add(new("pointed", file));
            }
            else if (installer is null) steps.Add(new("manual", file));
            else if (zrif is null && installer.Args.Any(a => a.Contains("{zrif}"))) steps.Add(new("license", file)); // Vita sin la licencia de la ficha
            else if (c.ExecuteScalar<long>("SELECT count(*) FROM content_install WHERE emulator_id = @Id AND source_id = @id", new { recipe.Id, id }) > 0)
                steps.Add(new("already", file));
            else if (Execute(System.IO.Path.Combine(install, recipe.Detect), installer.Args.Select(a => a.Replace("{file}", file).Replace("{zrif}", zrif)).ToList(), install) == 0)
            {
                c.Execute("INSERT INTO content_install (emulator_id, source_id, installed_at) VALUES (@Id, @id, @now)", new { recipe.Id, id, now = DateTime.UtcNow.ToString("O") });
                steps.Add(new("installed", file));
            }
            else steps.Add(new("failed", file));
        }
        if (pointed.Count > 0)
            foreach (var g in recipe.ContentPaths) AddFolders(System.IO.Path.Combine(install, g.File), g, pointed.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        return (steps, null);
    }

    /// <summary>`0005000E…` es una actualización y `0005000C…` un DLC; el resto, la base.</summary>
    static string WiiUComponent(string? titleId) => titleId?.ToUpperInvariant() switch
    {
        { } t when t.StartsWith("0005000E", StringComparison.Ordinal) => "UPD",
        { } t when t.StartsWith("0005000C", StringComparison.Ordinal) => "DLC",
        _ => "BASE",
    };

    /// <summary>La licencia (zRIF) de un juego de Vita, solo en la base privada. Null o vacía la borra.</summary>
    /// <remarks>ponytail: una por juego; un DLC con su propia licencia se instala a mano.</remarks>
    public static ApiError? SetLicense(Database db, long version, string? zrif)
    {
        zrif = zrif?.Trim();
        if (!string.IsNullOrEmpty(zrif) && (zrif.Length > 1024 || !Regex.IsMatch(zrif, "^[A-Za-z0-9+/=_-]+$"))) return ApiError.Of("content.badLicense");
        using var c = db.Open();
        if (c.ExecuteScalar<string?>("SELECT platform FROM version WHERE id = @version", new { version }) != "vita") return ApiError.Of("content.notVita");
        if (string.IsNullOrEmpty(zrif)) c.Execute("DELETE FROM version_license WHERE version_id = @version", new { version });
        else c.Execute("""
            INSERT INTO version_license (version_id, zrif, updated_at) VALUES (@version, @zrif, @now)
            ON CONFLICT (version_id) DO UPDATE SET zrif = excluded.zrif, updated_at = excluded.updated_at
            """, new { version, zrif, now = DateTime.UtcNow.ToString("O") });
        return null;
    }

    /// <summary>Si el juego tiene su licencia guardada (la licencia misma no vuelve al front).</summary>
    public static bool HasLicense(Database db, long version)
    {
        using var c = db.Open();
        return c.ExecuteScalar<long>("SELECT count(*) FROM version_license WHERE version_id = @version", new { version }) > 0;
    }

    static int Execute(string exe, List<string> args, string folder)
    {
        if (Runner is { } fake) return fake(exe, args);
        using var p = Process.Start(new ProcessStartInfo(exe, args) { WorkingDirectory = folder })!;
        p.WaitForExit();
        return p.ExitCode;
    }

    /// <summary>
    /// Suma las carpetas que faltan a la lista de la receta. En json, un arreglo de rutas (Ryubing `autoload_dirs`); en ini, una lista
    /// de Qt como la escribe Eden (`<clave>\N\path="D:/…"` y `<clave>\size`, con «/» y entre comillas). Lo que ya estaba no se toca.
    /// </summary>
    /// <remarks>ponytail: al mover el juego de disco la ruta vieja queda y la nueva se suma la próxima vez que se prepara; Eden la
    /// guarda con «/», que <see cref="Recipes.RewriteGamePaths"/> no reconoce.</remarks>
    static void AddFolders(string file, GamePath g, List<string> folders)
    {
        var known = (string s) => folders.RemoveAll(f => Disk.Same(f, s.Trim('"').Replace('/', System.IO.Path.DirectorySeparatorChar)));
        if (g.Format == "json")
        {
            var root = File.Exists(file) && JsonNode.Parse(File.ReadAllText(file)) is JsonObject o ? o : new JsonObject();
            if (root[g.Key] is not JsonArray list) root[g.Key] = list = [];
            foreach (var v in list) if (v?.GetValueKind() == JsonValueKind.String) known(v.GetValue<string>());
            foreach (var f in folders) list.Add(f);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            Recipes.WriteAtomic(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        var key = g.Key.TrimEnd('*');
        var item = new Regex("^" + Regex.Escape(key) + @"\\(\d+)\\path\s*=\s*(.*)$");
        var count = 0;
        foreach (var line in File.Exists(file) ? File.ReadLines(file) : [])
            if (item.Match(line.Trim()) is { Success: true } m)
            {
                count = Math.Max(count, int.Parse(m.Groups[1].Value));
                known(m.Groups[2].Value);
            }
        if (folders.Count == 0) return;
        foreach (var f in folders)
            Recipes.Set(file, new(g.File, "ini", $@"{key}\{++count}\path", $"\"{f.Replace('\\', '/')}\"", g.Section));
        Recipes.Set(file, new(g.File, "ini", $@"{key}\size", count.ToString(), g.Section));
    }
}
