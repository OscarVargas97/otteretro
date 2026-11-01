using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Otteretro.Libraries;

public record RecipeVersion(string From, string? Value = null);
/// <param name="Type">file, folder o junction.</param>
public record PortableStep(string Type, string Path, string? Link = null, string? System = null);
public record GamePath(string File, string Format, string Key);
public record RecipeSetting(string File, string Format, string Key, string Value, string? Section = null);
public record RecipeDownload(string Version, string System, string Url, string Hash);

/// <summary>Una receta de `recetas/` (§8.1, F3.3). El esquema está en `recetas/receta.schema.json`.</summary>
public record Recipe(string Id, string Name, string Kind, string Folder, string Detect)
{
    public string? Parent { get; init; }
    public RecipeVersion? Version { get; init; }
    public List<PortableStep> Portable { get; init; } = [];
    public string? Link { get; init; }
    public List<string> Saves { get; init; } = [];
    public List<string> Config { get; init; } = [];
    public List<GamePath> GamePaths { get; init; } = [];
    public List<RecipeSetting> Settings { get; init; } = [];
    public List<string> Formats { get; init; } = [];
    public List<RecipeDownload> Downloads { get; init; } = [];

    /// <summary>Emuladores, Ports o Complementos: el primer nivel de Herramientas (§5.1). Nombres de carpeta de la persona: en español.</summary>
    public string Group => Kind switch { "emulator" => "Emuladores", "port" => "Ports", _ => "Complementos" };
}

/// <param name="Kind">emulator, port o complement.</param>
/// <param name="Folder">Consola, o el emulador o port de un complemento.</param>
public record ToolInfo(string Kind, string Folder, string Name, string Version, string Path, string? RecipeId);

/// <summary>Las recetas que vienen con la app y lo que se hace con ellas: versión, modo portable, rutas internas y enlaces.</summary>
public static class Recipes
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };

    static readonly string[] Kinds = ["emulator", "port", "complement"];

    static List<Recipe>? all;

    public static IReadOnlyList<Recipe> All => all ??= Load();

    public static Recipe? Find(string id) => All.FirstOrDefault(r => r.Id == id);

    /// <summary>Lee las recetas incrustadas. Una receta que no cumple el esquema hace fallar el arranque (y la prueba que las carga).</summary>
    static List<Recipe> Load()
    {
        var assembly = typeof(Recipes).Assembly;
        var list = new List<Recipe>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("recetas/", StringComparison.Ordinal) && !n.EndsWith(".schema.json", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var r = JsonSerializer.Deserialize<Recipe>(stream, Json) ?? throw new InvalidDataException(name);
            if (!Regex.IsMatch(r.Id, "^[a-z0-9-]+$") || !Kinds.Contains(r.Kind) || (r.Kind == "complement") != (r.Parent is not null && r.Link is not null))
                throw new InvalidDataException($"Receta inválida: {name}");
            list.Add(r);
        }
        return list;
    }

    /// <summary>La receta de una carpeta instalada (por su ejecutable) entre los emuladores y ports.</summary>
    public static Recipe? Identify(string install) =>
        All.FirstOrDefault(r => r.Kind != "complement" && File.Exists(Path.Combine(install, r.Detect)));

    /// <summary>La versión con la que se guarda en Herramientas: la del ejecutable, su fecha o la fija de la receta.</summary>
    public static string VersionOf(Recipe? recipe, string install)
    {
        var exe = recipe is null ? null : Path.Combine(install, recipe.Detect);
        switch (recipe?.Version?.From)
        {
            case "fixed": return recipe.Version.Value ?? "original";
            case "exe" when File.Exists(exe):
                var v = FileVersionInfo.GetVersionInfo(exe).ProductVersion?.Split('+')[0].Trim();
                if (!string.IsNullOrEmpty(v)) return v;
                break;
        }
        // Sin versión en el ejecutable: la fecha del archivo (o de la carpeta, si no hay receta).
        var date = exe is not null && File.Exists(exe) ? File.GetLastWriteTime(exe) : Directory.GetLastWriteTime(install);
        return date.ToString("yyyy.MM.dd");
    }

    /// <summary>Dónde va en Herramientas: `Emuladores\<Consola>\<Instalación>\<versión>` (§5.1).</summary>
    public static string Target(string tools, Recipe recipe, string version) =>
        Path.Combine(tools, recipe.Group, recipe.Folder, recipe.Name, version);

    /// <summary>Lo instalado en Herramientas: tres niveles bajo Emuladores, Ports y Complementos.</summary>
    public static List<ToolInfo> Installed(string tools)
    {
        var list = new List<ToolInfo>();
        foreach (var (group, kind) in new[] { ("Emuladores", "emulator"), ("Ports", "port"), ("Complementos", "complement") })
        {
            var root = Path.Combine(tools, group);
            if (!Directory.Exists(root)) continue;
            foreach (var folder in Directory.EnumerateDirectories(root))
                foreach (var name in Directory.EnumerateDirectories(folder))
                    foreach (var version in Directory.EnumerateDirectories(name))
                    {
                        var recipe = All.FirstOrDefault(r => r.Kind == kind && r.Name == Path.GetFileName(name) && r.Folder == Path.GetFileName(folder));
                        list.Add(new(kind, Path.GetFileName(folder), Path.GetFileName(name), Path.GetFileName(version), version, recipe?.Id));
                    }
        }
        return list.OrderBy(t => t.Kind).ThenBy(t => t.Folder).ThenBy(t => t.Name).ThenBy(t => t.Version).ToList();
    }

    /// <summary>Deja la instalación portable y con los valores que necesita (pasos de la receta para este sistema).</summary>
    public static void MakePortable(Recipe recipe, string install)
    {
        var system = OperatingSystem.IsWindows() ? "windows" : "linux";
        foreach (var step in recipe.Portable.Where(s => s.System is null || s.System == system))
        {
            var path = Path.Combine(install, step.Path);
            switch (step.Type)
            {
                case "file" when !File.Exists(path):
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, "");
                    break;
                case "folder":
                    Directory.CreateDirectory(path);
                    break;
                case "junction":
                    Directory.CreateDirectory(path);
                    Links.Point(Environment.ExpandEnvironmentVariables(step.Link!.Replace('/', Path.DirectorySeparatorChar)), path);
                    break;
            }
        }
        foreach (var s in recipe.Settings) Set(Path.Combine(install, s.File), s);
    }

    /// <summary>
    /// Reescribe las rutas internas a juegos (§5.4): toda ruta que empiece por <paramref name="from"/> pasa a empezar por
    /// <paramref name="to"/>. Antes deja una copia del archivo (`.antes-de-otteretro`). Devuelve cuántas cambió.
    /// </summary>
    public static int RewriteGamePaths(Recipe recipe, string install, string from, string to)
    {
        var changed = 0;
        foreach (var g in recipe.GamePaths)
        {
            var file = Path.Combine(install, g.File);
            if (!File.Exists(file)) continue;
            var text = File.ReadAllText(file);
            var (result, count) = g.Format == "ini" ? RewriteIni(text, g.Key, from, to) : RewriteJson(text, g.Key, from, to);
            if (count == 0) continue;
            File.Copy(file, file + ".antes-de-otteretro", overwrite: true);
            WriteAtomic(file, result);
            changed += count;
        }
        return changed;
    }

    /// <summary>Si <paramref name="value"/> es <paramref name="from"/> o está dentro, la misma ruta bajo <paramref name="to"/>.</summary>
    public static string? Moved(string value, string from, string to)
    {
        var f = Path.TrimEndingDirectorySeparator(from);
        var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (value.Equals(f, cmp)) return Path.TrimEndingDirectorySeparator(to);
        foreach (var sep in new[] { '\\', '/' })
            if (value.StartsWith(f + sep, cmp)) return Path.TrimEndingDirectorySeparator(to) + value[f.Length..];
        return null;
    }

    static (string, int) RewriteIni(string text, string key, string from, string to)
    {
        var pattern = "^" + Regex.Escape(key.TrimEnd('*')) + (key.EndsWith('*') ? @"[^=\s]*" : "") + @"\s*=\s*";
        var count = 0;
        var lines = text.Split('\n').Select(line =>
        {
            var m = Regex.Match(line.TrimEnd('\r'), pattern);
            if (!m.Success) return line;
            var value = line.TrimEnd('\r')[m.Length..];
            if (Moved(value, from, to) is not { } moved) return line;
            count++;
            return line[..m.Length] + moved + (line.EndsWith('\r') ? "\r" : "");
        });
        return (string.Join('\n', lines), count);
    }

    static (string, int) RewriteJson(string text, string key, string from, string to)
    {
        if (JsonNode.Parse(text) is not JsonObject root || root[key] is not { } node) return (text, 0);
        var count = 0;
        string? Map(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && Moved(s, from, to) is { } m ? m : null;
        if (node is JsonArray array)
            for (var i = 0; i < array.Count; i++)
            {
                if (Map(array[i]) is not { } m) continue;
                array[i] = m;
                count++;
            }
        else if (Map(node) is { } moved)
        {
            root[key] = moved;
            count++;
        }
        return count == 0 ? (text, 0) : (root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), count);
    }

    static void Set(string file, RecipeSetting s)
    {
        if (s.Format == "json")
        {
            var root = File.Exists(file) && JsonNode.Parse(File.ReadAllText(file)) is JsonObject o ? o : new JsonObject();
            root[s.Key] = s.Value;
            WriteAtomic(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        var lines = File.Exists(file) ? File.ReadAllLines(file).ToList() : [];
        var header = $"[{s.Section}]";
        var start = lines.FindIndex(l => l.Trim().Equals(header, StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            lines.AddRange([header, $"{s.Key} = {s.Value}"]);
        }
        else
        {
            var end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith('['));
            if (end < 0) end = lines.Count;
            var at = lines.FindIndex(start + 1, end - start - 1, l => Regex.IsMatch(l, "^" + Regex.Escape(s.Key) + @"\s*="));
            if (at >= 0) lines[at] = $"{s.Key} = {s.Value}";
            else lines.Insert(end, $"{s.Key} = {s.Value}");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        WriteAtomic(file, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    static void WriteAtomic(string file, string text)
    {
        File.WriteAllText(file + ".tmp", text);
        File.Move(file + ".tmp", file, overwrite: true);
    }
}

/// <summary>
/// Enlaces de carpeta: en Windows, junctions (no piden permisos de administrador, a diferencia de los symlinks); en Linux,
/// enlaces simbólicos.
/// </summary>
public static class Links
{
    /// <summary>
    /// Borra una carpeta entera sin entrar en sus enlaces: primero quita cada enlace (no lo enlazado) y después el resto.
    /// Directory.Delete recursivo falla con «acceso denegado» ante una junction creada con mklink.
    /// </summary>
    public static void DeleteTree(string path)
    {
        var all = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 };
        foreach (var link in Directory.EnumerateDirectories(path, "*", all).Where(IsLink).OrderByDescending(l => l.Length).ToList())
            Directory.Delete(link);
        Directory.Delete(path, recursive: true);
    }

    public static bool IsLink(string path) =>
        Directory.Exists(path) && new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint);

    /// <summary>Hace que <paramref name="link"/> apunte a <paramref name="target"/>. Un enlace viejo se reemplaza; una carpeta real, nunca.</summary>
    public static void Point(string link, string target)
    {
        // Rutas con «/» de las recetas: mklink tomaría «/texture_replacements» por una opción.
        (link, target) = (Path.GetFullPath(link), Path.GetFullPath(target));
        if (IsLink(link))
        {
            if (Path.TrimEndingDirectorySeparator(new DirectoryInfo(link).LinkTarget ?? "") is var current && Library.Same(current.Replace(@"\??\", ""), target)) return;
            Directory.Delete(link); // borra el enlace, no lo que hay dentro
        }
        else if (Directory.Exists(link))
        {
            throw new IOException($"«{link}» es una carpeta real; no se reemplaza por un enlace.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }
        using var p = Process.Start(new ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, target]) { CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        p.WaitForExit();
        if (p.ExitCode != 0) throw new IOException($"mklink /J falló: {p.StandardError.ReadToEnd()}");
    }
}
