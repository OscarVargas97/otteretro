using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Otteretro.Libraries;

/// <summary>Un juego para el que se recomienda un fork, con su fuente (#358) y, si el catálogo lo tiene, su id (#366).</summary>
public record RecipeGame(string Game, string? Source = null, long? GameId = null);

public record RecipeVersion(string From, string? Value = null);
/// <param name="Type">file, folder, junction o rename (<paramref name="From"/> pasa a <paramref name="Path"/>).</param>
public record PortableStep(string Type, string Path, string? Link = null, string? System = null, string? From = null);
/// <param name="Section">En ini: dónde se agregan las carpetas de <see cref="Recipe.ContentPaths"/>.</param>
public record GamePath(string File, string Format, string Key, string? Section = null);
public record RecipeSetting(string File, string Format, string Key, string Value, string? Section = null);
public record RecipeDownload(string Version, string System, string Url, string Hash);
/// <summary>Dónde publica sus versiones (decisión 294). El mismo que trae #278 en ToolUpdates.cs: al fusionar las dos ramas queda uno.</summary>
/// <param name="DevRepo">Las versiones de desarrollo en otro repo del mismo servidor (Ryubing Canary); se piden a mano si no hay <paramref name="Dev"/>.</param>
/// <param name="DevPattern">Tags que cuentan como de desarrollo aunque no sean prerelease (Eden: -rc).</param>
public record RecipeUpdates(string Source, string? Repo = null, string? Host = null, bool Dev = false, string? DevRepo = null, string? DevPattern = null);
/// <summary>Port: cómo se le entrega el archivo del juego, investigado en el catálogo (#449, otteretro-catalogo#182).</summary>
/// <param name="Deliver">arg (con <paramref name="Args"/>), copy (a <paramref name="CopyTo"/>), none o manual.</param>
/// <param name="Formats">Las extensiones del archivo de la persona que sirven; vacía, cualquiera.</param>
public record RecipeGameFile(string Deliver, List<string>? Args = null, string? CopyTo = null, List<string>? Formats = null);
/// <param name="Apply">file, section, swap o argument (ver el esquema).</param>
public record RecipePerGame(string? Config = null, string? Apply = null)
{
    public List<string> Mods { get; init; } = [];
    public List<string> Args { get; init; } = [];
    /// <summary>{titleId} en mayúsculas (Eden) o en minúsculas (Ryubing), como lo escribe el emulador (#273).</summary>
    public string? IdCase { get; init; }
    /// <summary>La configuración del juego es una copia completa de la global (Ryubing): se crea desde ella, no vacía.</summary>
    public bool Full { get; init; }
    /// <summary>Otros archivos del juego que van con el perfil (mods activos, UPD y DLC elegidos).</summary>
    public List<string> Files { get; init; } = [];
    /// <summary>Argumento que apunta los mods a la carpeta del perfil al lanzar, en lugar del enlace (Dusklight, --mods).</summary>
    public string? ModsArg { get; init; }
    /// <summary>Valores que activan los mods (Dolphin, HiresTextures).</summary>
    public List<RecipeSetting> ModsEnable { get; init; } = [];
}
/// <param name="Keys">Claves de la configuración del juego que eligen el perfil de mando o guardan los mandos.</param>
public record RecipeInput(List<string>? Profiles = null, string? Arg = null, string? Section = null, List<string>? Keys = null);
public record ContentInstall(List<string> Formats, List<string> Args);
/// <param name="Name">resolution, renderer, aspectRatio, filter, vsync, speed, anisotropy, fps o antialiasing.</param>
/// <param name="Since">La versión desde la que vale esa clave, cuando cambia de nombre entre versiones (decisión 298).</param>
/// <param name="GameSection">La sección en la configuración del juego, si no es la misma que en la global (Dolphin: Video_Settings).</param>
public record RecipeOption(string Name, string File, string Format, string Key, string? Section = null, string? Since = null, string? GameSection = null);

/// <summary>Una receta de `recetas/` (§8.1, F3.3). El esquema está en `recetas/receta.schema.json`.</summary>
public record Recipe(string Id, string Name, string Kind, string Folder, string Detect)
{
    public string? Parent { get; init; }
    /// <summary>Port: el juego que cubre (#193).</summary>
    public string? Game { get; init; }
    /// <summary>Port: el enlace de su fila de port, que manda en juego y consola (#384, de #382).</summary>
    public string? Port { get; init; }
    public RecipeVersion? Version { get; init; }
    public List<PortableStep> Portable { get; init; } = [];
    public string? Link { get; init; }
    public List<string> Saves { get; init; } = [];
    public List<string> Config { get; init; } = [];
    public List<GamePath> GamePaths { get; init; } = [];
    public List<RecipeSetting> Settings { get; init; } = [];
    public List<string> Formats { get; init; } = [];
    public List<RecipeDownload> Downloads { get; init; } = [];
    /// <summary>Ids del catálogo de las consolas que corre y de las que es el emulador base (decisiones 286 y 291).</summary>
    public List<string> Consoles { get; init; } = [];
    public List<string> BaseFor { get; init; } = [];
    public string? License { get; init; }
    public bool Experimental { get; init; }
    /// <summary>Fork de otro emulador: el id de la receta original, y los juegos para los que se recomienda (#98, #358).</summary>
    public string? ForkOf { get; init; }
    public List<RecipeGame> RecommendedFor { get; init; } = [];
    /// <summary>Solo publica versiones de desarrollo: se instala la última con aviso (decisión 292).</summary>
    public bool DevOnly { get; init; }
    public RecipeUpdates? Updates { get; init; }
    /// <summary>Expresión regular del archivo de Windows en cada release de <see cref="Updates"/>.</summary>
    public string? Asset { get; init; }
    /// <summary>Lo mismo para Linux (otteretro-catalogo#153): lo usa PlayOS; Otteretro instala solo en Windows.</summary>
    public string? AssetLinux { get; init; }
    public RecipePerGame? PerGame { get; init; }
    public RecipeGameFile? GameFile { get; init; }
    /// <summary>Contenido instalado dentro del emulador; como las partidas, lo comparten todas sus versiones.</summary>
    public List<string> Content { get; init; } = [];
    public List<ContentInstall> Install { get; init; } = [];
    public List<RecipeOption> Options { get; init; } = [];
    /// <summary>La versión más nueva en que se revisaron las claves de <see cref="Options"/>: una más nueva usa esas, con aviso.</summary>
    public string? OptionsUpTo { get; init; }
    /// <summary>yuzu (Eden, Citron): cada valor va con `clave\default=false` y, en el del juego, `clave\use_global=false`.</summary>
    public string? OptionFlags { get; init; }
    /// <summary>Dónde se apuntan UPD y DLC sin copiarlos (decisión 295); se reescriben al mover, como <see cref="GamePaths"/>.</summary>
    public List<GamePath> ContentPaths { get; init; } = [];
    public RecipeInput? Input { get; init; }
    /// <summary>Con qué nombre se guarda el archivo bajado cuando no es un zip (un .exe con la versión en el nombre), para que coincida con Detect (#268).</summary>
    public string? SaveAs { get; init; }
    /// <summary>Con qué se juega, si no es <c>Detect</c>: un port cuyo <c>Detect</c> es su instalador (Hookshot, #457).</summary>
    public string? Run { get; init; }
    /// <summary>Si lo bajado es un instalador: sus argumentos para instalar sin preguntar, con <c>{dir}</c> donde va la carpeta (#443).</summary>
    public List<string>? Installer { get; init; }

    /// <summary>Emuladores, Ports o Complementos: el primer nivel de Herramientas (§5.1). Nombres de carpeta de la persona: en español.</summary>
    [JsonIgnore]
    public string Group => Kind switch { "emulator" => "Emuladores", "port" => "Ports", "tool" => "Herramientas", _ => "Complementos" };
}

/// <param name="Kind">emulator, port o complement.</param>
/// <param name="Folder">Consola, o el emulador o port de un complemento.</param>
/// <param name="Game">Port: el juego que cubre, según su receta (#193).</param>
/// <param name="LocationId">La ubicación donde está; sin ella, en `Herramientas` de la carpeta de datos (#198).</param>
public record ToolInfo(string Kind, string Folder, string Name, string Version, string Path, string? RecipeId, string? Game = null, string? LocationId = null);

/// <summary>Una carpeta donde hay instalaciones de un tipo: `<Consola o padre>\<Nombre>\<versión>` adentro (#198).</summary>
public record ToolRoot(string Kind, string Path, string? LocationId = null);

/// <summary>Una receta local que chocó con la publicada: ganó la publicada y la local quedó en <paramref name="Backup"/> (decisión 351).</summary>
public record RecipeBackup(string Id, string Name, string Backup);

/// <summary>Las recetas que vienen con la app y lo que se hace con ellas: versión, modo portable, rutas internas y enlaces.</summary>
public static class Recipes
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };

    static readonly string[] Kinds = ["emulator", "port", "complement", "tool"];

    /// <summary>En `Ports\<Port>` (decisión 324): la versión instalada, que ya no va en la ruta. Nombre de la persona: en español.</summary>
    public const string VersionFile = "otteretro-version.txt";

    static List<Recipe>? all;
    static string? localFolder;
    static Data.Database? catalog;
    static List<RecipeBackup> backups = [];

    static readonly Lock gate = new();

    // Load respalda archivos: con el cerrojo, dos hilos que arrancan a la vez no respaldan dos veces.
    public static IReadOnlyList<Recipe> All { get { lock (gate) return all ??= Load(); } }

    /// <summary>Las recetas locales que chocaron con la publicada en esta sesión: se respaldaron y se avisa en Inicio (decisión 351).</summary>
    public static IReadOnlyList<RecipeBackup> Backups { get { _ = All; return backups; } }

    /// <summary>De dónde salen las recetas publicadas: la tabla `recipe` del catálogo instalado (#357, decisión 349).</summary>
    public static Data.Database? Catalog
    {
        get => catalog;
        set { catalog = value; all = null; }
    }

    /// <summary>Vuelve a leerlas, después de aplicar otra versión del catálogo.</summary>
    public static void Reload() => all = null;

    public static Recipe? Find(string id) => All.FirstOrDefault(r => r.Id == id);

    /// <summary>
    /// Las recetas locales: `data\recetas` de la carpeta de datos, las que se crean al agregar un emulador desde su
    /// repo (#350). Se fija al preparar la carpeta de datos; cambiarla vuelve a leer las recetas.
    /// </summary>
    public static string? LocalFolder
    {
        get => localFolder;
        set { localFolder = value; all = null; }
    }

    /// <summary>
    /// Las publicadas (las del catálogo instalado o, si ese catálogo no las trae, las incrustadas) y las locales. Una incrustada que no cumple
    /// el esquema hace fallar el arranque (y la prueba que las carga); una publicada o una local rota se salta. Una local con el id de una
    /// publicada se unifica con ella por campo (<see cref="Unify"/>).
    /// </summary>
    static List<Recipe> Load()
    {
        var found = new List<RecipeBackup>();
        var embedded = Embedded();
        // Las del catálogo mandan; una incrustada cuyo id el catálogo no trae (o trae rota) se mantiene, para no perderla.
        var published = FromCatalog() is { } fromCatalog
            ? [.. fromCatalog, .. embedded.Where(e => fromCatalog.All(c => (string?)c.Json["id"] != (string?)e.Json["id"]))]
            : embedded;
        var list = new List<Recipe>();
        foreach (var (name, json) in published)
            if (TryParse(json, name, "publicada") is { } r) list.Add(r);
        if (localFolder is not null && Directory.Exists(localFolder))
            foreach (var file in Directory.EnumerateFiles(localFolder, "*.json"))
                try
                {
                    var local = JsonNode.Parse(File.ReadAllText(file)) as JsonObject ?? throw new InvalidDataException(file);
                    var id = local["id"]?.GetValue<string>();
                    var index = list.FindIndex(x => x.Id == id);
                    if (index < 0) { list.Add(Parse(local, file)); continue; }
                    var (merged, conflict) = Unify(published.First(p => p.Json["id"]?.GetValue<string>() == id).Json, local);
                    var recipe = Parse(merged, file);
                    if (conflict && Backup(file) is { } backup) found.Add(new(recipe.Id, recipe.Name, backup));
                    list[index] = recipe;
                }
                catch (Exception e) when (e is JsonException or InvalidDataException or InvalidOperationException or IOException)
                {
                    App.FileLog.Write(Microsoft.Extensions.Logging.LogLevel.Warning, "Recipes", $"Receta local ignorada: {file}: {e.Message}");
                }
        backups = [.. backups.Where(b => found.All(f => f.Id != b.Id)), .. found];
        return list;
    }

    /// <summary>Las de la tabla `recipe` del catálogo; null si no hay catálogo o no las trae (uno de antes de #357): se usan las incrustadas.</summary>
    static List<(string Name, JsonObject Json)>? FromCatalog()
    {
        if (catalog is null) return null;
        try
        {
            using var c = catalog.Open();
            if (Dapper.SqlMapper.ExecuteScalar<long>(c, "SELECT count(*) FROM catalog.sqlite_master WHERE name = 'recipe'") == 0) return null;
            var rows = Dapper.SqlMapper.Query<(string Id, string Json)>(c, "SELECT id, json FROM catalog.recipe ORDER BY id").ToList();
            var list = new List<(string, JsonObject)>();
            foreach (var (id, json) in rows)
                try { list.Add(($"catalogo:{id}", JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException(id))); }
                catch (Exception e) when (e is JsonException or InvalidDataException)
                {
                    App.FileLog.Write(Microsoft.Extensions.Logging.LogLevel.Warning, "Recipes", $"Receta del catálogo ignorada: {id}: {e.Message}");
                }
            return list.Count == 0 ? null : list;
        }
        catch (Microsoft.Data.Sqlite.SqliteException) { return null; }
    }

    static List<(string Name, JsonObject Json)> Embedded()
    {
        var assembly = typeof(Recipes).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("recetas/", StringComparison.Ordinal) && !n.EndsWith(".schema.json", StringComparison.Ordinal))
            .Select(n =>
            {
                using var stream = assembly.GetManifestResourceStream(n)!;
                return (n, (JsonObject)JsonNode.Parse(stream)!);
            }).ToList();
    }

    /// <summary>Las incrustadas tienen que estar bien (falla el arranque); una del catálogo que no se entiende se salta con un aviso en el log.</summary>
    static Recipe? TryParse(JsonObject json, string name, string what)
    {
        if (!name.StartsWith("catalogo:", StringComparison.Ordinal)) return Parse(json, name);
        try { return Parse(json, name); }
        catch (Exception e) when (e is JsonException or InvalidDataException)
        {
            App.FileLog.Write(Microsoft.Extensions.Logging.LogLevel.Warning, "Recipes", $"Receta {what} ignorada: {name}: {e.Message}");
            return null;
        }
    }

    static Recipe Parse(JsonObject json, string name)
    {
        var r = json.Deserialize<Recipe>(Json) ?? throw new InvalidDataException(name);
        if (!Regex.IsMatch(r.Id, "^[a-z0-9-]+$") || !Kinds.Contains(r.Kind) || (r.Kind == "complement") != (r.Parent is not null && r.Link is not null))
            throw new InvalidDataException($"Receta inválida: {name}");
        return r;
    }

    /// <summary>
    /// Une una receta local con la publicada del mismo id (decisión 351): se queda lo que solo trae cada lado y, en los objetos, campo por campo.
    /// Si chocan en un campo, gana la publicada.
    /// </summary>
    /// <returns>La unión y si algún campo chocó.</returns>
    public static (JsonObject Merged, bool Conflict) Unify(JsonObject published, JsonObject local)
    {
        var merged = (JsonObject)published.DeepClone();
        return (merged, Merge(merged, local));
    }

    /// <summary>
    /// Tras un choque: copia la local a `_respaldos\&lt;fecha&gt;-&lt;id&gt;.json`. La local no se reescribe (sigue siendo solo de la persona,
    /// así una versión nueva del catálogo no choca con una copia vieja de la publicada); si ya hay un respaldo idéntico, no se repite ni se avisa.
    /// </summary>
    /// <returns>El respaldo nuevo, o null si esa misma local ya estaba respaldada.</returns>
    public static string? Backup(string localFile)
    {
        var folder = Path.Combine(Path.GetDirectoryName(localFile)!, "_respaldos");
        var content = File.ReadAllBytes(localFile);
        if (Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*-" + Path.GetFileName(localFile))
                .Any(f => File.ReadAllBytes(f).AsSpan().SequenceEqual(content)))
            return null;
        Directory.CreateDirectory(folder);
        var backup = Path.Combine(folder, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{Path.GetFileName(localFile)}");
        File.Copy(localFile, backup, overwrite: false);
        if (!File.ReadAllBytes(backup).AsSpan().SequenceEqual(content)) throw new IOException($"El respaldo no quedó igual: {backup}");
        return backup;
    }

    /// <summary>Suma a <paramref name="into"/> lo de <paramref name="local"/> que no tiene; true si algún campo chocó.</summary>
    static bool Merge(JsonObject into, JsonObject local)
    {
        var conflict = false;
        foreach (var (key, value) in local)
            if (!into.TryGetPropertyValue(key, out var mine)) into[key] = value?.DeepClone();
            else if (mine is JsonObject a && value is JsonObject b) conflict |= Merge(a, b);
            else conflict |= !JsonNode.DeepEquals(mine, value);
        return conflict;
    }

    /// <summary>Guarda una receta local y vuelve a leer las recetas: desde ya se elige en perfiles y avisa de versiones.</summary>
    public static void SaveLocal(Recipe recipe)
    {
        var folder = localFolder ?? throw new InvalidOperationException("No hay carpeta de recetas locales.");
        Directory.CreateDirectory(folder);
        WriteAtomic(Path.Combine(folder, recipe.Id + ".json"), JsonSerializer.Serialize(recipe, Local));
        all = null;
    }

    static readonly JsonSerializerOptions Local = new(JsonSerializerDefaults.Web) { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };

    /// <summary>
    /// La expresión del archivo de Windows de una receta local, desde el nombre del que se eligió: los números pasan a `[0-9.]+`, así
    /// calza con el de la próxima versión (Fritchii-v4.1-Windows-x64.zip → ^Fritchii-v[0-9.]+-Windows-x[0-9.]+\.zip$).
    /// </summary>
    public static string AssetPattern(string file) => "^" + Regex.Replace(Regex.Escape(file), @"\d+(\\\.\d+)*", "[0-9.]+") + "$";

    /// <summary>
    /// La receta de una carpeta instalada (por su ejecutable) entre los emuladores y ports. Si varias tienen el mismo ejecutable
    /// (Ryubing y Ryujinx SS4K), la que se llama como la carpeta.
    /// </summary>
    public static Recipe? Identify(string install)
    {
        var matches = All.Where(r => r.Kind != "complement" && File.Exists(Path.Combine(install, r.Detect))).ToList();
        return matches.FirstOrDefault(r => r.Name == Path.GetFileName(Path.TrimEndingDirectorySeparator(install))) ?? matches.FirstOrDefault();
    }

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

    /// <summary>
    /// Dónde va (§5.1): `Emuladores\<Emulador>\<versión>`, una carpeta aunque sirva a varias consolas (decisión 302); un port,
    /// `Ports\<Port>`, sin la versión (decisión 324); una herramienta, `Herramientas\<Nombre>\<versión>` (#274);
    /// `Complementos\<Emulador o port>\<Complemento>\<versión>`.
    /// </summary>
    public static string Target(string tools, Recipe recipe, string version) => recipe.Kind switch
    {
        "complement" => Path.Combine(tools, recipe.Group, recipe.Folder, recipe.Name, version),
        "port" => Path.Combine(tools, recipe.Group, recipe.Name),
        _ => Path.Combine(tools, recipe.Group, recipe.Name, version),
    };

    /// <summary>Emuladores, Ports, Complementos y Herramientas (§5.1): la primera ubicación de cada tipo (#198).</summary>
    public static List<ToolRoot> DefaultRoots(string tools) =>
        [new("emulator", Path.Combine(tools, "Emuladores")), new("port", Path.Combine(tools, "Ports")), new("complement", Path.Combine(tools, "Complementos")),
         new("tool", Path.Combine(tools, "Herramientas"))];

    /// <summary>Lo instalado en Herramientas: tres niveles bajo Emuladores, Ports y Complementos.</summary>
    public static List<ToolInfo> Installed(string tools) => Installed(DefaultRoots(tools));

    /// <summary>Lo instalado en esas carpetas: dos niveles bajo cada una, o tres en lo de antes y en los complementos (#198).</summary>
    public static List<ToolInfo> Installed(IEnumerable<ToolRoot> roots)
    {
        var list = new List<ToolInfo>();
        foreach (var (kind, root, locationId) in roots)
        {
            if (!Directory.Exists(root)) continue;
            if (kind == "tool")
            {
                // Dos niveles, sin consola. Solo las que tienen receta: en el ★, `Herramientas\` es también la carpeta de antes con
                // Emuladores, Ports y Complementos adentro (DiskTasks.ToolsOf), hasta que pasa al estándar.
                foreach (var name in Directory.EnumerateDirectories(root))
                    if (All.FirstOrDefault(r => r.Kind == kind && r.Name == Path.GetFileName(name)) is { } tool)
                        foreach (var version in Directory.EnumerateDirectories(name))
                            list.Add(new(kind, tool.Folder, tool.Name, Path.GetFileName(version), version, tool.Id, null, locationId));
                continue;
            }
            foreach (var folder in Directory.EnumerateDirectories(root))
            {
                var named = kind == "complement" ? null : All.FirstOrDefault(r => r.Kind == kind && r.Name == Path.GetFileName(folder));
                // Un port con una sola versión, en `Ports\<Port>` (decisión 324); sin el archivo, el formato de antes, que se lee hasta
                // que «Pasar al estándar» lo junta en una (DiskTasks.OldPorts, decisión 327).
                if (kind == "port" && File.Exists(Path.Combine(folder, VersionFile)))
                {
                    list.Add(new(kind, named?.Folder ?? "", Path.GetFileName(folder), File.ReadAllText(Path.Combine(folder, VersionFile)).Trim(), folder, named?.Id, named?.Game, locationId));
                    continue;
                }
                foreach (var name in Directory.EnumerateDirectories(folder))
                {
                    // `Emuladores\<Emulador>\<versión>` (decisión 302): el segundo nivel ya es la versión, con sus archivos. Lo de antes,
                    // `Emuladores\<Consola>\<Instalación>\<versión>`, se sigue leyendo; Folder es la consola de su receta, si la tiene.
                    if (kind != "complement" && Path.GetFileName(name).StartsWith('_')) continue; // `_compartido`, `_respaldos` (decisión 308)
                    if (kind != "complement" && (named is not null || Directory.EnumerateFiles(name).Any()))
                    {
                        list.Add(new(kind, named?.Folder ?? "", Path.GetFileName(folder), Path.GetFileName(name), name, named?.Id, named?.Game, locationId));
                        continue;
                    }
                    foreach (var version in Directory.EnumerateDirectories(name))
                    {
                        var recipe = All.FirstOrDefault(r => r.Kind == kind && r.Name == Path.GetFileName(name) && r.Folder == Path.GetFileName(folder));
                        list.Add(new(kind, Path.GetFileName(folder), Path.GetFileName(name), Path.GetFileName(version), version, recipe?.Id, recipe?.Game, locationId));
                    }
                }
            }
        }
        return list.OrderBy(t => t.Kind).ThenBy(t => t.Folder).ThenBy(t => t.Name).ThenBy(t => t.Version).ToList();
    }

    /// <summary>
    /// Con rename: el enlace de fuera (<see cref="PortableStep.Link"/>) que apuntaba a la carpeta vieja de esta instalación, si sigue ahí.
    /// Uno que apunta a otra instalación no se toca.
    /// </summary>
    static string? OldLink(PortableStep step, string install)
    {
        if (step.Link is null) return null;
        var link = Path.GetFullPath(Environment.ExpandEnvironmentVariables(step.Link.Replace('/', Path.DirectorySeparatorChar)));
        return Links.IsLink(link) && new DirectoryInfo(link).LinkTarget is { } target
            && Disk.Same(Path.TrimEndingDirectorySeparator(target.Replace(@"\??\", "")), Path.GetFullPath(Path.Combine(install, step.From!))) ? link : null;
    }

    /// <summary>Una instalación de una receta anterior que todavía tiene que pasar a la actual (la carpeta vieja o su enlace).</summary>
    public static bool Outdated(Recipe recipe, string install) =>
        recipe.Portable.Any(s => s.Type == "rename" && (Directory.Exists(Path.Combine(install, s.From!)) || OldLink(s, install) is not null));

    /// <summary>Deja la instalación portable y con los valores que necesita (pasos de la receta para este sistema).</summary>
    public static void MakePortable(Recipe recipe, string install)
    {
        var system = OperatingSystem.IsWindows() ? "windows" : "linux";
        foreach (var step in recipe.Portable.Where(s => s.System is null || s.System == system))
        {
            var path = Path.Combine(install, step.Path);
            switch (step.Type)
            {
                case "rename":
                    var from = Path.Combine(install, step.From!);
                    if (Directory.Exists(from) && !Links.IsLink(from) && !Path.Exists(path))
                        Directory.Move(from, path); // dentro de la instalación: el mismo disco, se renombra
                    if (OldLink(step, install) is { } old) Directory.Delete(old); // borra el enlace, no lo enlazado
                    break;
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
        foreach (var g in recipe.GamePaths.Concat(recipe.ContentPaths))
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

    public static void Set(string file, RecipeSetting s)
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

    /// <summary>
    /// El valor que tiene ahora la clave de <paramref name="s"/> en su archivo (en json, el valor tal cual, con su tipo); null si no
    /// está. Con <see cref="Restore"/> se deja como estaba después de un <see cref="Set"/>.
    /// </summary>
    public static string? Get(string file, RecipeSetting s)
    {
        if (!File.Exists(file)) return null;
        if (s.Format == "json") return JsonNode.Parse(File.ReadAllText(file)) is JsonObject o && o[s.Key] is { } v ? v.ToJsonString() : null;
        var lines = File.ReadAllLines(file);
        var start = Array.FindIndex(lines, l => l.Trim().Equals($"[{s.Section}]", StringComparison.OrdinalIgnoreCase));
        if (start < 0) return null;
        for (var i = start + 1; i < lines.Length && !lines[i].TrimStart().StartsWith('['); i++)
            if (Regex.Match(lines[i], "^" + Regex.Escape(s.Key) + @"\s*=\s*(.*)$") is { Success: true } m) return m.Groups[1].Value.TrimEnd();
        return null;
    }

    /// <summary>Vuelve la clave al valor que dio <see cref="Get"/>: lo pone, o la quita si no estaba.</summary>
    public static void Restore(string file, RecipeSetting s, string? previous)
    {
        if (previous is not null)
        {
            if (s.Format != "json") { Set(file, s with { Value = previous }); return; }
        }
        if (!File.Exists(file)) return;
        if (s.Format == "json")
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root) return;
            if (previous is null) root.Remove(s.Key);
            else root[s.Key] = JsonNode.Parse(previous);
            WriteAtomic(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        var lines = File.ReadAllLines(file).ToList();
        var start = lines.FindIndex(l => l.Trim().Equals($"[{s.Section}]", StringComparison.OrdinalIgnoreCase));
        if (start < 0) return;
        var end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith('['));
        var at = lines.FindIndex(start + 1, (end < 0 ? lines.Count : end) - start - 1, l => Regex.IsMatch(l, "^" + Regex.Escape(s.Key) + @"\s*="));
        if (at < 0) return;
        lines.RemoveAt(at);
        WriteAtomic(file, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    internal static void WriteAtomic(string file, string text)
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
            if (Path.TrimEndingDirectorySeparator(new DirectoryInfo(link).LinkTarget ?? "") is var current && Disk.Same(current.Replace(@"\??\", ""), target)) return;
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
