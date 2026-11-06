using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <param name="Value">El valor guardado en el perfil; null: el del emulador.</param>
/// <param name="Current">El que tiene ahora la configuración global de esa versión (en json, tal cual: `2`, `"vulkan"`).</param>
public record SettingField(string Name, string? Value, string? Current);

/// <param name="Version">La versión del emulador cuyas claves se muestran: la del perfil o, si no eligió, la más nueva instalada.</param>
/// <param name="Versions">Las versiones instaladas del emulador.</param>
/// <param name="Undescribed">La versión es más nueva que la última que describe la receta: se usan las claves de la anterior más cercana.</param>
/// <param name="Kept">Ajustes guardados en el perfil que esta versión no tiene: quedan por si vuelve a otra que sí.</param>
/// <param name="Input">Las claves de mando: las de la configuración del juego o el argumento al lanzar.</param>
/// <param name="InputProfiles">Los perfiles de mando con nombre que hay en esa versión.</param>
public record ProfileSettings(string? EmulatorVersion, string? Version, List<string> Versions, bool Undescribed, List<SettingField> Options,
    List<string> Kept, List<SettingField> Input, List<string> InputProfiles);

/// <param name="Options">Por nombre del ajuste (resolution, fps…); vacío: vuelve al del emulador.</param>
/// <param name="Input">Por clave de mando; vacío: la quita.</param>
/// <param name="EmulatorVersion">La versión que usa el perfil; null: la más nueva.</param>
public record ProfileSettingsRequest(Dictionary<string, string> Options, Dictionary<string, string> Input, string? EmulatorVersion);

/// <summary>
/// Ajustes y mandos de cada perfil (decisión 298, #276). Se guardan por nombre (resolution, fps…) en `profile.settings`, sin
/// importar la versión, y se escriben con las claves de la versión que usa el perfil: en el perfil por defecto, en la configuración
/// global; en el de un juego, en su configuración del juego guardada en el perfil, que entra al emulador al activarlo. Lo que una
/// versión no tiene queda guardado para cuando vuelva a otra que sí.
/// </summary>
public static class EmulatorSettings
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public class Values
    {
        public Dictionary<string, string> Options { get; init; } = [];
        public Dictionary<string, string> Input { get; init; } = [];
    }

    public static Values Read(string settings) => JsonSerializer.Deserialize<Values>(settings, Json) ?? new();

    /// <summary>Compara versiones por sus números: 0.10 &gt; 0.9 y 2026.06.01 &gt; 2026.5.30.</summary>
    public static int Compare(string a, string b)
    {
        string[] Parts(string v) => Regex.Matches(v, @"\d+").Select(m => m.Value.TrimStart('0')).ToArray();
        var (x, y) = (Parts(a), Parts(b));
        for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            var (p, q) = (i < x.Length ? x[i] : "", i < y.Length ? y[i] : "");
            var r = p.Length != q.Length ? p.Length.CompareTo(q.Length) : string.CompareOrdinal(p, q);
            if (r != 0) return r;
        }
        return 0;
    }

    /// <summary>
    /// Las claves de cada ajuste en esa versión: de las que valen para ella (sin `since` o con uno que no la pasa), la de `since` más
    /// alta. Sin versión (no hay ninguna instalada), las más nuevas.
    /// </summary>
    public static List<RecipeOption> For(Recipe recipe, string? version) =>
        recipe.Options.Where(o => o.Since is null || version is null || Compare(o.Since, version) <= 0)
            .GroupBy(o => o.Name)
            .Select(g => g.OrderBy(o => o.Since ?? "", Comparer<string>.Create(Compare)).Last())
            .ToList();

    /// <summary>La versión no está descrita: es más nueva que la última que revisó la receta (<see cref="Recipe.OptionsUpTo"/>).</summary>
    public static bool Undescribed(Recipe recipe, string? version) =>
        recipe.OptionsUpTo is { } upTo && version is not null && Compare(version, upTo) > 0;

    /// <summary>Las claves de mando de un perfil: en el de un juego, las de su configuración; en los dos, el argumento al lanzar.</summary>
    static List<string> InputKeys(Recipe recipe, Profile p) => recipe.Input is not { } input ? []
        : [.. p.GameVersionId is null ? [] : input.Keys ?? [], .. input.Arg is { } arg ? [arg] : Array.Empty<string>()];

    static string? VersionOf(Database db, Profile p) =>
        p.EmulatorVersion ?? Installs.List(db).LastOrDefault(i => i.EmulatorId == p.EmulatorId && i.Path is not null)?.Version;

    public static ProfileSettings? View(Database db, long id)
    {
        Profile? p;
        using (var c = db.Open()) p = Profiles.Get(c, id);
        if (p is null) return null;
        var versions = Installs.List(db).Where(i => i.EmulatorId == p.EmulatorId).Select(i => i.Version).ToList();
        if (Recipes.Find(p.EmulatorId) is not { } recipe) return new(p.EmulatorVersion, null, versions, false, [], [], [], []);
        var version = VersionOf(db, p);
        var install = Profiles.InstallOf(db, p.EmulatorId, p.EmulatorVersion);
        var values = Read(p.Settings);
        var chosen = For(recipe, version);
        var options = chosen.Select(o => new SettingField(o.Name, values.Options.GetValueOrDefault(o.Name),
            install is null || o.Format is not ("ini" or "json") ? null : Recipes.Get(Path.Combine(install, o.File), Setting(o, o.Section, "")))).ToList();
        var kept = values.Options.Keys.Where(n => chosen.All(o => o.Name != n)).Order().ToList();
        var input = InputKeys(recipe, p).Select(k => new SettingField(k, values.Input.GetValueOrDefault(k), null)).ToList();
        var profiles = install is null ? [] : (recipe.Input?.Profiles ?? []).Select(d => Path.Combine(install, d)).Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.ini")).Select(Path.GetFileNameWithoutExtension).Distinct().Order().ToList();
        return new(p.EmulatorVersion, version, versions, Undescribed(recipe, version), options, kept, input, profiles!);
    }

    /// <summary>Guarda los valores (junto a los que ya tenía) y la versión, y los escribe donde los lee el emulador.</summary>
    public static ApiError? Save(Database db, long id, ProfileSettingsRequest r)
    {
        Profile p;
        using (var c = db.Open())
        {
            if (Profiles.Get(c, id) is not { } found) return ApiError.Of("profile.notFound");
            p = found;
            if (Recipes.Find(p.EmulatorId) is not { } recipe) return ApiError.Of("settings.noRecipe");
            if (r.EmulatorVersion is not null && !Installs.List(db).Any(i => i.EmulatorId == p.EmulatorId && i.Version == r.EmulatorVersion))
                return ApiError.Of("settings.badVersion");
            // Cambiar la versión de un perfil cargado dejaría sus archivos en la otra: primero se activa otro.
            if (r.EmulatorVersion != p.EmulatorVersion && c.ExecuteScalar<bool>("SELECT active FROM profile WHERE id = @id", new { id }))
                return ApiError.Of("settings.versionWhileActive");
            var keys = InputKeys(recipe, p);
            // Un salto de línea agregaría líneas al ini.
            if (r.Options.Keys.Any(k => recipe.Options.All(o => o.Name != k)) || r.Input.Keys.Any(k => !keys.Contains(k))
                || r.Options.Values.Concat(r.Input.Values).Any(v => v.Length > 200 || v.IndexOfAny(['\r', '\n']) >= 0))
                return ApiError.Of("settings.badValue");
        }
        var values = Read(p.Settings);
        var cleared = new HashSet<string>();
        foreach (var (into, from) in new[] { (values.Options, r.Options), (values.Input, r.Input) })
            foreach (var (k, v) in from)
                if (string.IsNullOrWhiteSpace(v)) { into.Remove(k); cleared.Add(k); }
                else into[k] = v.Trim();
        p = p with { Settings = JsonSerializer.Serialize(values, Json), EmulatorVersion = r.EmulatorVersion };
        using (var c = db.Open()) c.Execute("UPDATE profile SET settings = @Settings, emulator_version = @EmulatorVersion WHERE id = @Id", p);
        return Apply(db, p, cleared);
    }

    static RecipeSetting Setting(RecipeOption o, string? section, string value) => new(o.File, o.Format, o.Key, value, section);

    /// <summary>
    /// Escribe los valores del perfil con las claves de la versión que usa, y quita las de otras versiones del mismo ajuste. Lo de
    /// <paramref name="cleared"/> se quita. Un ajuste que esa versión no tiene no se toca: queda guardado en el perfil.
    /// </summary>
    /// <param name="recipe">La del emulador del perfil, salvo que se pase otra.</param>
    public static ApiError? Apply(Database db, Profile p, ICollection<string>? cleared = null, Recipe? recipe = null)
    {
        if ((recipe ?? Recipes.Find(p.EmulatorId)) is not { } r) return null;
        recipe = r;
        cleared ??= [];
        var values = Read(p.Settings);
        var install = Profiles.InstallOf(db, p.EmulatorId, p.EmulatorVersion);
        string? gameFile = null, relative = null;
        var active = false;
        if (p.GameVersionId is { } game)
        {
            using var c = db.Open();
            if (recipe.PerGame is not { Apply: "file" or "swap", Config: { } config }) return ApiError.Of("settings.noGameConfig");
            if (Profiles.Expand(config, recipe, Profiles.GameKey(c, game, p.SourceId)) is not { } rel) return ApiError.Of("profile.noGameKey");
            (relative, gameFile) = (rel, Path.Combine(Profiles.StoreOf(db, p.Id), rel));
            active = recipe.PerGame.Apply == "file" && install is not null && c.ExecuteScalar<bool>("SELECT active FROM profile WHERE id = @Id", p);
            if (active) Profiles.Capture(db, c, p); // lo último que cambió el emulador, antes de escribir encima
            // `swap` cambia la global entera al lanzar: nace de ella para no perder lo demás (la ruta del juego, por ejemplo).
            if (recipe.PerGame.Apply == "swap" && !File.Exists(gameFile) && values.Options.Count > 0 && install is not null
                && File.Exists(Path.Combine(install, config)))
                Profiles.CopyFile(Path.Combine(install, config), gameFile);
        }
        else if (install is null) return null; // queda guardado; se escribe al volver a guardar con una versión instalada

        var yuzu = recipe.OptionFlags == "yuzu";
        var chosen = For(recipe, VersionOf(db, p));
        foreach (var name in values.Options.Keys.Concat(cleared).Distinct())
        {
            if (chosen.FirstOrDefault(o => o.Name == name) is not { } option) continue;
            var value = values.Options.GetValueOrDefault(name);
            foreach (var o in recipe.Options.Where(o => o.Name == name && o.Format is "ini" or "json"))
            {
                var file = gameFile ?? Path.Combine(install!, o.File);
                var section = gameFile is null ? o.Section : o.GameSection ?? o.Section;
                if (o == option) Put(file, Setting(o, section, value ?? ""), value, yuzu, gameFile is not null);
                else if (o.Key != option.Key || section != (gameFile is null ? option.Section : option.GameSection ?? option.Section))
                    Put(file, Setting(o, section, ""), null, yuzu, gameFile is not null); // la clave de otra versión
            }
        }
        // Mandos en la configuración del juego (Dolphin: PadProfile1…); el argumento se pasa al lanzar.
        if (gameFile is not null && recipe.Input is { Keys: { } keys } input)
            foreach (var key in keys.Where(k => values.Input.ContainsKey(k) || cleared.Contains(k)))
                Put(gameFile, new RecipeSetting("", "ini", key, values.Input.GetValueOrDefault(key) ?? "", input.Section), values.Input.GetValueOrDefault(key), false, true);

        if (active && File.Exists(gameFile)) Profiles.CopyFile(gameFile, Path.Combine(install!, relative!));
        return null;
    }

    /// <summary>Pone o quita (<paramref name="value"/> null) una clave. En json, el valor va con su tipo si lo tiene (2, true).</summary>
    static void Put(string file, RecipeSetting s, string? value, bool yuzu, bool perGame)
    {
        if (s.Format == "json")
        {
            if (value is null) { Recipes.Restore(file, s, null); return; }
            if (!File.Exists(file))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, "{}");
            }
            Recipes.Restore(file, s, IsJson(value) ? value : JsonSerializer.Serialize(value));
            return;
        }
        if (value is null) Recipes.Restore(file, s, null);
        else Recipes.Set(file, s);
        // Los de yuzu (Eden, Citron) leen el valor solo con `\default=false` y, en el del juego, `\use_global=false`.
        if (!yuzu) return;
        foreach (var flag in perGame ? ["\\default", "\\use_global"] : new[] { "\\default" })
            if (value is null) Recipes.Restore(file, s with { Key = s.Key + flag }, null);
            else Recipes.Set(file, s with { Key = s.Key + flag, Value = "false" });
    }

    static bool IsJson(string value)
    {
        try { return JsonNode.Parse(value) is JsonValue; }
        catch (JsonException) { return false; }
    }

    // ── Abrir la configuración del emulador ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Con qué se abre el emulador para un perfil: en el juego (con `perGame.args`, su mando y sus mods) o, si no se puede armar
    /// (falta la ROM o su disco), normal. Un port es el juego. El perfil de un juego tiene que estar activo: su configuración es la
    /// que está en el emulador.
    /// </summary>
    public static (string? Exe, List<string> Args, bool InGame, ApiError? Error) OpenArgs(Database db, long id)
    {
        using var c = db.Open();
        if (Profiles.Get(c, id) is not { } p) return (null, [], false, ApiError.Of("profile.notFound"));
        if (Recipes.Find(p.EmulatorId) is not { } recipe) return (null, [], false, ApiError.Of("settings.noRecipe"));
        if (Profiles.InstallOf(db, p.EmulatorId, p.EmulatorVersion) is not { } install)
            return (null, [], false, ApiError.Of("profile.notInstalled", ("emulator", p.EmulatorId)));
        var exe = Path.Combine(install, recipe.Detect);
        var values = Read(p.Settings);
        List<string> extra = recipe.Input?.Arg is { } arg && values.Input.GetValueOrDefault(arg) is { } name ? [arg, name] : [];
        if (p.GameVersionId is not { } game) return (exe, extra, false, null);
        if (recipe.PerGame?.Apply != "swap" && !c.ExecuteScalar<bool>("SELECT active FROM profile WHERE id = @id", new { id }))
            return (null, [], false, ApiError.Of("settings.notActive"));
        extra.AddRange(Profiles.LaunchArgs(db, id));
        var args = recipe.PerGame?.Args ?? [];
        if (args.Count == 0) return (exe, extra, recipe.Kind == "port", null);
        var rom = c.QueryFirstOrDefault<(string LibraryId, string Relative)?>("""
            SELECT library_id, relative_path FROM source WHERE kind = 'file' AND (id = @SourceId OR (@SourceId IS NULL AND version_id = @game
                AND coalesce(component, 'BASE') = 'BASE')) ORDER BY modified, id
            """, new { p.SourceId, game });
        var path = rom is { } r && Disk.List(db).FirstOrDefault(d => d.Id == r.LibraryId && d.Connected) is { } disk ? Path.Combine(disk.Path, r.Relative) : null;
        var key = Profiles.GameKey(c, game, p.SourceId);
        var expanded = path is null || !File.Exists(path) ? null
            : args.Select(a => Profiles.Expand(a.Replace("{rom}", path), recipe, key)).ToList();
        return expanded is null || expanded.Any(a => a is null) ? (exe, extra, false, null) : (exe, [.. expanded!.Select(a => a!), .. extra], true, null);
    }

    /// <summary>Abre el emulador (ver <see cref="OpenArgs"/>). Devuelve si quedó en el juego (`settings.opened`) o normal (`settings.openedPlain`).</summary>
    public static (string? Code, ApiError? Error) Open(Database db, long id)
    {
        var (exe, args, inGame, error) = OpenArgs(db, id);
        if (error is not null) return (null, error);
        Profile p;
        using (var c = db.Open()) p = Profiles.Get(c, id)!;
        var swap = p.GameVersionId is not null && Recipes.Find(p.EmulatorId)?.PerGame?.Apply == "swap";
        if (swap) Profiles.SwapIn(db, id);
        var process = new Process { StartInfo = new(exe!, args) { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = false }, EnableRaisingEvents = swap };
        if (swap) process.Exited += (_, _) => Profiles.SwapOut(db, p.EmulatorId);
        try { process.Start(); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException)
        {
            if (swap) Profiles.SwapOut(db, p.EmulatorId);
            return (null, ApiError.Of("settings.openFailed", ("detail", e.Message)));
        }
        return (p.GameVersionId is null || inGame ? "settings.opened" : "settings.openedPlain", null);
    }
}
