using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <param name="EmulatorVersion">La versión instalada que usa; null: la más nueva.</param>
/// <param name="GameVersionId">La versión del catálogo (el juego en su consola); null: el perfil por defecto del emulador.</param>
/// <param name="SourceId">La ROM que usa; null: la base.</param>
public record Profile(long Id, string EmulatorId, string? EmulatorVersion, long? GameVersionId, string Name, string Settings, long? SourceId);

public record ProfileRow(long Id, string EmulatorId, string? EmulatorVersion, string Name, long? SourceId, bool Active);
/// <param name="Path">El archivo, relativo a su disco.</param>
public record RomEntry(long Id, string Path, bool Modified, string? Label);
/// <param name="Base">El emulador base de la consola: su perfil por defecto es el de los juegos sin perfil activo.</param>
/// <param name="Versions">Las versiones instaladas, de la más vieja a la más nueva.</param>
public record ProfileEmulator(string Id, string Name, string Kind, bool Base, long DefaultProfileId, string DefaultName, List<string> Versions);
/// <summary>Los perfiles de una versión del juego (decisión 301), con qué emuladores se puede jugar y sus ROMs.</summary>
public record VersionProfiles(long VersionId, string Platform, List<ProfileEmulator> Emulators, List<ProfileRow> Profiles, List<RomEntry> Roms);

/// <summary>
/// Emuladores compartidos y perfiles por juego (§8.0, #267): cada consola tiene un emulador base, cada emulador un perfil por
/// defecto y cada juego, a lo más, un perfil activo. Activar otro (#269, <see cref="Switch"/>) guarda lo del que estaba, lo quita
/// del emulador y carga el nuevo. Lo de cada perfil vive en `data\perfiles\<id>\` de la carpeta de datos (se muda con ella), con
/// las mismas rutas que dentro del emulador.
/// </summary>
public static class Profiles
{
    const string Columns = "id, emulator_id AS EmulatorId, emulator_version AS EmulatorVersion, game_version_id AS GameVersionId, name, settings, source_id AS SourceId";

    static string Now() => DateTime.UtcNow.ToString("O");

    /// <summary>Registra un emulador (si no estaba) con su perfil por defecto.</summary>
    public static void AddEmulator(Database db, string id, string kind, string name)
    {
        using var c = db.Open();
        using var t = c.BeginTransaction();
        if (c.Execute("INSERT OR IGNORE INTO emulator (id, kind, name) VALUES (@id, @kind, @name)", new { id, kind, name }, t) > 0)
            c.Execute("INSERT INTO profile (emulator_id, name, created_at) VALUES (@id, @name, @now)", new { id, name, now = Now() }, t);
        // Las consolas que corre según su receta (decisión 291): de la que es base, lo es si la consola todavía no tiene uno.
        if (Recipes.Find(id) is { } recipe)
            foreach (var console in recipe.Consoles)
                c.Execute("""
                    INSERT OR IGNORE INTO console_emulator (console_id, emulator_id, is_base)
                    VALUES (@console, @id, @isBase AND NOT EXISTS (SELECT 1 FROM console_emulator WHERE console_id = @console AND is_base = 1))
                    """, new { console, id, isBase = recipe.BaseFor.Contains(console) }, t);
        t.Commit();
    }

    /// <summary>El emulador base de la consola; el que lo era sigue sirviéndole como alternativo.</summary>
    public static void SetBase(Database db, string console, string emulator)
    {
        using var c = db.Open();
        using var t = c.BeginTransaction();
        c.Execute("UPDATE console_emulator SET is_base = 0 WHERE console_id = @console", new { console }, t);
        c.Execute("""
            INSERT INTO console_emulator (console_id, emulator_id, is_base) VALUES (@console, @emulator, 1)
            ON CONFLICT (console_id, emulator_id) DO UPDATE SET is_base = 1
            """, new { console, emulator }, t);
        t.Commit();
    }

    /// <summary>Un perfil nuevo de un juego, inactivo.</summary>
    public static long Add(Database db, long gameVersion, string emulator, string name, string? emulatorVersion = null, long? source = null)
    {
        using var c = db.Open();
        return c.ExecuteScalar<long>("""
            INSERT INTO profile (emulator_id, emulator_version, game_version_id, name, source_id, created_at)
            VALUES (@emulator, @emulatorVersion, @gameVersion, @name, @source, @now) RETURNING id
            """, new { emulator, emulatorVersion, gameVersion, name, source, now = Now() });
    }

    /// <summary>Deja ese perfil como el activo de su juego y apaga el que lo era. False si no existe o es un perfil por defecto.</summary>
    public static bool Activate(Database db, long id)
    {
        using var c = db.Open();
        using var t = c.BeginTransaction();
        if (c.ExecuteScalar<long?>("SELECT game_version_id FROM profile WHERE id = @id", new { id }, t) is not { } game) return false;
        c.Execute("UPDATE profile SET active = 0 WHERE game_version_id = @game AND active = 1", new { game }, t);
        c.Execute("UPDATE profile SET active = 1 WHERE id = @id", new { id }, t);
        t.Commit();
        return true;
    }

    /// <summary>
    /// Con qué se juega: el perfil activo del juego o, si no tiene, el perfil por defecto del emulador base de su consola. Null si
    /// la consola no tiene emulador base.
    /// </summary>
    public static Profile? Effective(Database db, long gameVersion)
    {
        using var c = db.Open();
        return c.QuerySingleOrDefault<Profile>($"SELECT {Columns} FROM profile WHERE game_version_id = @gameVersion AND active = 1", new { gameVersion })
            ?? c.QuerySingleOrDefault<Profile>($"""
                SELECT {Columns} FROM profile WHERE game_version_id IS NULL AND emulator_id = (
                    SELECT e.emulator_id FROM console_emulator e JOIN version v ON v.platform = e.console_id
                    WHERE v.id = @gameVersion AND e.is_base = 1)
                """, new { gameVersion });
    }

    // ── Activar un perfil (#269) ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Nombre de carpeta de la persona: en español. Va junto a la base, así se muda con la carpeta de datos (§5.1).</summary>
    public const string Folder = "perfiles";
    /// <summary>Lo que había en el emulador de un juego sin perfil activo: vuelve tal cual al dejarlo sin perfil.</summary>
    const string LooseFolder = "_sin-perfil";
    /// <summary>Lo que un cambio de perfil desplaza y no tenía otro lugar: nunca se borra.</summary>
    public const string BackupsFolder = "_respaldos";
    /// <summary>La global de un emulador `swap` mientras corre un perfil: su marca para restaurarla al cerrar (o si la app se cayó).</summary>
    const string SwapFolder = "_swap";

    public static string Root(Database db) => Path.Combine(Path.GetDirectoryName(db.FilePath)!, Folder);

    /// <summary>La carpeta de los archivos de un perfil.</summary>
    public static string StoreOf(Database db, long id) => Path.Combine(Root(db), id.ToString());

    /// <param name="Folder">Una carpeta (se enlaza); si no, un archivo (se copia).</param>
    /// <param name="Mods">Es de los mods del juego: con algo adentro, se aplica <see cref="RecipePerGame.ModsEnable"/>.</param>
    record Item(string Path, bool Folder, bool Mods = false);

    /// <summary>Lo que <see cref="RecipePerGame.ModsEnable"/> cambió en la configuración del emulador y cómo estaba antes.</summary>
    record Changed(string File, RecipeSetting Setting, string? Previous);

    /// <summary>En la carpeta de un perfil activo: los valores que cambió al cargarse, para dejarlos como estaban al quitarlo.</summary>
    internal const string ChangedFile = "_valores-cambiados.json";

    /// <param name="Linked">Un perfil: sus carpetas se enlazan. Sin perfil, lo de la persona vuelve a ser una carpeta real.</param>
    record State(string Emulator, string? Version, string Store, bool Linked, long? ProfileId);

    static readonly string[] Keys = ["titleId", "gameId", "serial"];

    /// <summary>La clave del juego en el emulador: el TitleID o serial de la ROM del perfil, el de su ROM base o el del catálogo.</summary>
    internal static string? GameKey(SqliteConnection c, long version, long? source) =>
        c.QueryFirstOrDefault<string?>("SELECT title_id FROM source WHERE id = @source AND title_id IS NOT NULL", new { source })
        ?? c.QueryFirstOrDefault<string?>("""
            SELECT title_id FROM source WHERE version_id = @version AND title_id IS NOT NULL AND coalesce(component, 'BASE') = 'BASE'
            ORDER BY modified, id
            """, new { version })
        ?? c.QueryFirstOrDefault<string?>("SELECT code FROM catalog.version_code WHERE version_id = @version ORDER BY code", new { version });

    /// <summary>La plantilla de la receta con la clave del juego; null si le falta algo ({crc}, {rom}, {game} todavía no).</summary>
    internal static string? Expand(string template, Recipe recipe, string? key)
    {
        key = recipe.PerGame?.IdCase switch { "upper" => key?.ToUpperInvariant(), "lower" => key?.ToLowerInvariant(), _ => key };
        var missing = false;
        var result = Regex.Replace(template, @"\{(\w+)\}", m =>
        {
            if (Keys.Contains(m.Groups[1].Value) && key is not null) return key;
            missing = true;
            return "";
        });
        return missing ? null : result;
    }

    /// <summary>
    /// Lo de un juego en el emulador que va con el perfil: su configuración (con `apply: file`), sus otros archivos y sus mods
    /// (salvo que se pasen con un argumento al lanzar). Null si la clave del juego no alcanza para armar las rutas.
    /// </summary>
    /// <remarks>ponytail: `apply: section` todavía no mueve su sección (el nombre de la sección está sin verificar en RMG, Flycast y
    /// mGBA); sus mods sí. `swap` y `argument` no tocan nada al activar: se aplican al lanzar (F6, <see cref="SwapIn"/>).</remarks>
    static List<Item>? Items(Recipe? recipe, string? key)
    {
        if (recipe?.PerGame is not { } pg) return [];
        var raw = new List<Item>();
        if (pg is { Apply: "file", Config: { } config }) raw.Add(new(config, false));
        raw.AddRange(pg.Files.Select(f => new Item(f, false)));
        if (pg.ModsArg is null) raw.AddRange(pg.Mods.Select(m => new Item(m, !Path.HasExtension(m), true)));
        var items = raw.Select(i => Expand(i.Path, recipe, key) is { } p ? i with { Path = p } : null).ToList();
        return items.Any(i => i is null) ? null : items.Select(i => i!).ToList();
    }

    /// <summary>
    /// La carpeta de la versión que usa (null: la más nueva instalada), o null si no está instalada o su disco no está. Un port usa
    /// siempre la que tiene instalada, sea cual sea la del perfil (decisión 326).
    /// </summary>
    public static string? InstallOf(Database db, string emulator, string? version) =>
        Installs.List(db).LastOrDefault(i => i.EmulatorId == emulator && i.Path is not null && (version is null || i.Version == version || i.Kind == "port"))?.Path;

    /// <summary>
    /// «Usar esta versión» (decisiones 332 y 334): la marca en uso y le pasa todos los perfiles del emulador. Lo de un perfil activo
    /// sale de la versión que usaba y entra en la nueva; antes de tocar nada comprueba que se pueda con todos.
    /// </summary>
    /// <remarks>ponytail: lo suelto de un juego sin perfil (<see cref="Loose"/>) se queda en la versión donde estaba.</remarks>
    public static ApiError? UseVersion(Database db, string emulator, string version)
    {
        if (Installs.List(db).FirstOrDefault(i => i.EmulatorId == emulator && i.Version == version) is not { } chosen) return ApiError.Of("install.notInstalled");
        if (chosen.Path is not { } to) return ApiError.Of("profile.notInstalled", ("emulator", chosen.Name));
        var recipe = Recipes.Find(emulator);
        using var c = db.Open();
        var plan = new List<(Profile Profile, string From, List<Item> Items, string? Key)>();
        foreach (var p in c.Query<Profile>($"SELECT {Columns} FROM profile WHERE emulator_id = @emulator AND active = 1 AND game_version_id IS NOT NULL", new { emulator }))
        {
            var key = GameKey(c, p.GameVersionId!.Value, p.SourceId);
            if (Items(recipe, key) is not { } items) return ApiError.Of("profile.noGameKey");
            if (items.Count == 0) continue;
            if (InstallOf(db, emulator, p.EmulatorVersion) is not { } from) return ApiError.Of("profile.notInstalled", ("emulator", chosen.Name));
            if (!Disk.Same(from, to)) plan.Add((p, from, items, key));
        }
        try
        {
            foreach (var (p, from, items, key) in plan)
            {
                Unload(db, StateOf(db, p), from, items);
                Load(StateOf(db, p with { EmulatorVersion = version }), to, items, recipe, key);
                c.Execute("UPDATE profile SET emulator_version = @version WHERE id = @Id", new { version, p.Id });
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Los que ya pasaron quedan en la nueva; el que falló, con lo suyo copiado y verificado en su carpeta.
            return ApiError.Of("profile.switchFailed", ("detail", e.Message));
        }
        catch (TaskException e)
        {
            return e.Error;
        }
        c.Execute("UPDATE profile SET emulator_version = @version WHERE emulator_id = @emulator", new { version, emulator });
        return null;
    }

    static State Loose(Database db, long version, string emulator) =>
        new(emulator, null, Path.Combine(Root(db), LooseFolder, version.ToString(), emulator), false, null);

    static State StateOf(Database db, Profile p) => new(p.EmulatorId, p.EmulatorVersion, StoreOf(db, p.Id), true, p.Id);

    static string? BaseOf(SqliteConnection c, long version) => c.ExecuteScalar<string?>(
        "SELECT e.emulator_id FROM console_emulator e JOIN version v ON v.platform = e.console_id WHERE v.id = @version AND e.is_base = 1", new { version });

    public static Profile? Get(SqliteConnection c, long id) => c.QuerySingleOrDefault<Profile>($"SELECT {Columns} FROM profile WHERE id = @id", new { id });

    /// <summary>
    /// Cambia el perfil activo de una versión del juego, con sus archivos: guarda lo del que estaba en su carpeta y lo quita del
    /// emulador, y carga el nuevo. Sin <paramref name="profile"/> (o con un perfil por defecto), el juego queda sin perfil: vuelve lo
    /// que tenía en el emulador base antes de su primer perfil. Antes de tocar nada comprueba que se pueda hacer entero; un archivo se
    /// borra del emulador solo después de copiarlo y verificarlo. Si el juego pasa a otro emulador, lleva sus partidas
    /// (<see cref="SaveTransfer"/>, #270) y devuelve qué pasó con ellas. Si hay que elegir el usuario de consola de destino
    /// (<paramref name="saveUser"/>), no cambia nada y lo pide (saves.chooseUser).
    /// </summary>
    public static (ApiError? Error, SaveNotice? Saves) Switch(Database db, long version, long? profile, string? saveUser = null)
    {
        using var c = db.Open();
        if (c.ExecuteScalar<string?>("SELECT platform FROM version WHERE id = @version", new { version }) is null) return (ApiError.Of("profile.notFound"), null);
        var target = profile is { } id ? Get(c, id) : null;
        if (profile is not null && (target is null || (target.GameVersionId is { } g && g != version))) return (ApiError.Of("profile.notFound"), null);
        if (target is { GameVersionId: null }) target = null;
        var current = c.QuerySingleOrDefault<Profile>($"SELECT {Columns} FROM profile WHERE game_version_id = @version AND active = 1", new { version });
        if (current?.Id == target?.Id) return (null, null);
        var baseEmulator = BaseOf(c, version);
        var from = current is not null ? StateOf(db, current) : baseEmulator is null ? null : Loose(db, version, baseEmulator);
        var to = target is not null ? StateOf(db, target) : baseEmulator is null ? null : Loose(db, version, baseEmulator);

        // Lo que se va a mover, entero, antes de mover nada.
        var plan = new List<(State State, string Install, List<Item> Items, Recipe? Recipe)>();
        foreach (var (state, p) in new[] { (from, current), (to, target) })
        {
            if (state is null) continue;
            var recipe = Recipes.Find(state.Emulator);
            if (Items(recipe, GameKey(c, version, p?.SourceId)) is not { } items) return (ApiError.Of("profile.noGameKey"), null);
            if (items.Count == 0) { plan.Add((state, "", items, recipe)); continue; }
            if (InstallOf(db, state.Emulator, state.Version) is not { } install)
            {
                // Sin perfil y sin el emulador instalado no hay nada que guardar; los archivos de un perfil sí pueden estar en un disco desconectado.
                if (state.ProfileId is not null) return (ApiError.Of("profile.notInstalled", ("emulator", state.Emulator)), null);
                plan.Add((state, "", [], recipe));
                continue;
            }
            plan.Add((state, install, items, recipe));
        }

        SaveNotice? saves = null;
        try
        {
            // Las partidas primero: si no se pueden llevar, se avisa, pero el perfil se cambia igual (la de origen no se toca).
            if (from is not null && to is not null && from.Emulator != to.Emulator)
                saves = SaveTransfer.Transfer(db, from.Emulator, InstallOf(db, from.Emulator, from.Version), to.Emulator,
                    InstallOf(db, to.Emulator, to.Version), GameKey(c, version, current?.SourceId), saveUser);
            if (saves?.Code == "saves.chooseUser") return (null, saves); // todavía no se movió nada
            if (from is not null)
            {
                var unload = plan[0];
                Unload(db, unload.State, unload.Install, unload.Items);
                // Deja ese emulador como estaba antes del perfil: con lo suelto que la persona tenía ahí para este juego.
                if (from.ProfileId is not null && from.Emulator != to?.Emulator && unload.Items.Count > 0)
                    Load(Loose(db, version, from.Emulator), unload.Install, unload.Items, unload.Recipe, null);
            }
            if (to is not null)
            {
                var load = plan[^1];
                // Otro emulador: lo que la persona tenía ahí para este juego, sin perfil, queda guardado aparte para volver tal cual.
                if (from?.Emulator != to.Emulator && load.Items.Count > 0) Unload(db, Loose(db, version, to.Emulator), load.Install, load.Items);
                Load(load.State, load.Install, load.Items, load.Recipe, GameKey(c, version, target?.SourceId));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Lo movido hasta acá ya está copiado y verificado donde va: no se pierde nada, y el perfil activo no cambia.
            return (ApiError.Of("profile.switchFailed", ("detail", e.Message)), saves);
        }
        catch (TaskException e)
        {
            return (e.Error, saves);
        }

        using var t = c.BeginTransaction();
        c.Execute("UPDATE profile SET active = 0 WHERE game_version_id = @version AND active = 1", new { version }, t);
        if (target is not null) c.Execute("UPDATE profile SET active = 1 WHERE id = @Id", new { target.Id }, t);
        t.Commit();
        return (null, saves);
    }

    /// <summary>Saca del emulador lo del juego y lo guarda en <see cref="State.Store"/>. Lo que pisaría queda en los respaldos.</summary>
    static void Unload(Database db, State state, string install, List<Item> items)
    {
        foreach (var item in items)
        {
            var inside = Path.Combine(install, item.Path);
            var kept = Path.Combine(state.Store, item.Path);
            if (item.Folder)
            {
                if (Links.IsLink(inside)) Directory.Delete(inside); // el enlace, no lo enlazado: ya está en la carpeta del perfil
                else if (Directory.Exists(inside))
                {
                    if (Directory.EnumerateFileSystemEntries(inside).Any())
                    {
                        Aside(db, kept);
                        MoveTree(inside, kept);
                    }
                    else Directory.Delete(inside);
                }
            }
            else if (File.Exists(inside))
            {
                CopyFile(inside, kept); // lo del emulador es lo último: reemplaza lo guardado
                File.Delete(inside);
            }
        }
        // Los valores que cambió al cargarse vuelven a como estaban (decisión 286: volver restaura todo).
        var changed = Path.Combine(state.Store, ChangedFile);
        if (!File.Exists(changed)) return;
        foreach (var c in System.Text.Json.JsonSerializer.Deserialize<List<Changed>>(File.ReadAllText(changed))!)
            Recipes.Restore(Path.Combine(install, c.File), c.Setting, c.Previous);
        File.Delete(changed);
    }

    /// <summary>Pone en el emulador lo del juego: copia sus archivos y enlaza sus carpetas (sin perfil, las devuelve como carpetas).</summary>
    static void Load(State state, string install, List<Item> items, Recipe? recipe, string? key)
    {
        var mods = false;
        foreach (var item in items)
        {
            var inside = Path.Combine(install, item.Path);
            var kept = Path.Combine(state.Store, item.Path);
            if (item.Folder)
            {
                if (state.Linked)
                {
                    Directory.CreateDirectory(kept); // así lo que se agregue desde el emulador queda en el perfil
                    Links.Point(inside, kept);
                    mods |= Directory.EnumerateFileSystemEntries(kept).Any();
                }
                else if (Directory.Exists(kept)) MoveTree(kept, inside);
            }
            else if (File.Exists(kept))
            {
                CopyFile(kept, inside);
                if (!state.Linked) File.Delete(kept);
                mods |= state.Linked && item.Mods;
            }
        }
        // Con mods, los valores que los activan (Dolphin: HiresTextures), guardando antes cómo estaban: al quitar el perfil vuelven.
        if (!mods || recipe?.PerGame is not { ModsEnable.Count: > 0 } pg) return;
        var changed = new List<Changed>();
        foreach (var s in pg.ModsEnable)
            if (Expand(s.File, recipe, key) is { } file)
                changed.Add(new(file, s, Recipes.Get(Path.Combine(install, file), s)));
        File.WriteAllText(Path.Combine(state.Store, ChangedFile), System.Text.Json.JsonSerializer.Serialize(changed));
        foreach (var c in changed) Recipes.Set(Path.Combine(install, c.File), c.Setting);
    }

    /// <summary>Lo que había en <paramref name="path"/> (si hay algo) pasa a los respaldos, con la fecha.</summary>
    static void Aside(Database db, string path)
    {
        if (!Directory.Exists(path) || !Directory.EnumerateFileSystemEntries(path).Any()) return;
        var to = Path.Combine(Root(db), BackupsFolder, $"{DateTime.Now:yyyy-MM-dd-HHmmss-fff}", Path.GetRelativePath(Root(db), path));
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        Directory.Move(path, to); // dentro de la carpeta de datos: el mismo disco
    }

    internal static void CopyFile(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Copy(from, to + ".tmp", overwrite: true);
        FolderMove.FlushToDisk(to + ".tmp");
        if (!FolderMove.SameContent(from, to + ".tmp"))
        {
            File.Delete(to + ".tmp");
            throw new TaskException(ApiError.Of("move.copyMismatch", ("path", from), ("detail", Path.GetFileName(from))));
        }
        File.Move(to + ".tmp", to, overwrite: true);
    }

    /// <summary>Copia la carpeta entera y verificada, y recién entonces borra la de origen (pueden estar en discos distintos).</summary>
    static void MoveTree(string from, string to)
    {
        CopyTree(from, to);
        Links.DeleteTree(from);
    }

    internal static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            CopyFile(file, Path.Combine(to, Path.GetRelativePath(from, file)));
    }

    /// <summary>Copia del emulador a la carpeta del perfil activo lo que cambió ahí (sus archivos), sin quitarlo.</summary>
    internal static void Capture(Database db, SqliteConnection c, Profile p)
    {
        if (p.GameVersionId is not { } version || Items(Recipes.Find(p.EmulatorId), GameKey(c, version, p.SourceId)) is not { Count: > 0 } items
            || InstallOf(db, p.EmulatorId, p.EmulatorVersion) is not { } install) return;
        foreach (var item in items.Where(i => !i.Folder && File.Exists(Path.Combine(install, i.Path))))
            CopyFile(Path.Combine(install, item.Path), Path.Combine(StoreOf(db, p.Id), item.Path));
    }

    /// <summary>
    /// Un perfil nuevo de una versión del juego: vacío, con los valores del perfil por defecto del emulador (<paramref name="from"/>
    /// = "default") o duplicando otro (<paramref name="copy"/>, con sus archivos y su ROM). Con `full` (Ryubing) su configuración del
    /// juego nace de la global del emulador, como hace el emulador.
    /// </summary>
    public static (long? Id, ApiError? Error) Create(Database db, long version, string emulator, string name, string from = "empty", long? copy = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return (null, ApiError.Of("profile.badName"));
        using var c = db.Open();
        if (c.ExecuteScalar<long>("SELECT count(*) FROM version WHERE id = @version", new { version }) == 0) return (null, ApiError.Of("profile.notFound"));
        Profile? source = null;
        if (from == "copy")
        {
            if (copy is not { } cid || Get(c, cid) is not { } s || (s.GameVersionId is { } g && g != version)) return (null, ApiError.Of("profile.notFound"));
            source = s;
            emulator = s.EmulatorId;
        }
        else if (from == "default")
            source = c.QuerySingleOrDefault<Profile>($"SELECT {Columns} FROM profile WHERE emulator_id = @emulator AND game_version_id IS NULL", new { emulator });
        else if (from != "empty") return (null, ApiError.Of("profile.badFrom"));
        if (c.ExecuteScalar<long>("SELECT count(*) FROM emulator WHERE id = @emulator", new { emulator }) == 0) return (null, ApiError.Of("profile.notFound"));

        var id = c.ExecuteScalar<long>("""
            INSERT INTO profile (emulator_id, emulator_version, game_version_id, name, settings, source_id, created_at)
            VALUES (@emulator, @emulatorVersion, @version, @name, @settings, @sourceId, @now) RETURNING id
            """, new
        {
            emulator,
            emulatorVersion = source?.GameVersionId is null ? null : source.EmulatorVersion,
            version,
            name = name.Trim(),
            settings = source?.Settings ?? "{}",
            sourceId = source?.GameVersionId is null ? null : source.SourceId,
            now = Now(),
        });
        var store = StoreOf(db, id);
        if (source is { GameVersionId: not null })
        {
            if (c.ExecuteScalar<bool>("SELECT active FROM profile WHERE id = @Id", source)) Capture(db, c, source);
            if (Directory.Exists(StoreOf(db, source.Id))) CopyTree(StoreOf(db, source.Id), store);
            File.Delete(Path.Combine(store, ChangedFile)); // es del original mientras está cargado, no de la copia
        }
        else if (Recipes.Find(emulator) is { PerGame: { Full: true, Config: { } config } } recipe
            && Expand(config, recipe, GameKey(c, version, null)) is { } relative && InstallOf(db, emulator, null) is { } install
            && recipe.Config.Select(g => Path.Combine(install, g)).FirstOrDefault(File.Exists) is { } global)
            CopyFile(global, Path.Combine(store, relative));
        // Con los valores del por defecto (#276): se escriben en su configuración del juego, con las claves de su versión.
        if (source is { GameVersionId: null } && Get(c, id) is { } created) EmulatorSettings.Apply(db, created);
        return (id, null);
    }

    /// <summary>Renombra un perfil (también el por defecto) y elige su ROM, que tiene que ser de su versión del juego.</summary>
    public static ApiError? Update(Database db, long id, string name, long? source)
    {
        if (string.IsNullOrWhiteSpace(name)) return ApiError.Of("profile.badName");
        using var c = db.Open();
        if (Get(c, id) is not { } p) return ApiError.Of("profile.notFound");
        if (source is not null && (p.GameVersionId is null
            || c.ExecuteScalar<long>("SELECT count(*) FROM source WHERE id = @source AND version_id = @GameVersionId AND kind = 'file'", new { source, p.GameVersionId }) == 0))
            return ApiError.Of("profile.badRom");
        c.Execute("UPDATE profile SET name = @name, source_id = @source WHERE id = @id", new { id, name = name.Trim(), source = p.GameVersionId is null ? null : source });
        return null;
    }

    /// <summary>Los perfiles de cada versión del juego, los emuladores con que se puede jugar (los de su consola y los ports de ese juego) y sus ROMs.</summary>
    public static List<VersionProfiles> OfGame(Database db, long game)
    {
        var installs = Installs.List(db);
        using var c = db.Open();
        var names = c.QuerySingleOrDefault<(string Es, string? Original)>("SELECT name_es, original_name FROM game WHERE id = @game", new { game });
        bool Covers(string id) => Recipes.Find(id)?.Game is { } g && (g.Equals(names.Es, StringComparison.OrdinalIgnoreCase) || g.Equals(names.Original, StringComparison.OrdinalIgnoreCase));
        var result = new List<VersionProfiles>();
        foreach (var (version, platform) in c.Query<(long, string)>("SELECT id, platform FROM version WHERE game_id = @game ORDER BY id", new { game }).ToList())
        {
            var emulators = c.Query<(string Id, string Name, string Kind, long? Base, long DefaultId, string DefaultName)>("""
                SELECT e.id, e.name, e.kind, ce.is_base, p.id, p.name FROM emulator e
                JOIN profile p ON p.emulator_id = e.id AND p.game_version_id IS NULL
                LEFT JOIN console_emulator ce ON ce.emulator_id = e.id AND ce.console_id = @platform
                ORDER BY ce.is_base DESC, e.name
                """, new { platform })
                .Where(e => e.Base is not null || (e.Kind == "port" && Covers(e.Id)))
                .Select(e => new ProfileEmulator(e.Id, e.Name, e.Kind, e.Base == 1, e.DefaultId, e.DefaultName,
                    installs.Where(i => i.EmulatorId == e.Id).Select(i => i.Version).ToList()))
                .ToList();
            var profiles = c.Query<(long Id, string Emulator, string? EmulatorVersion, string Name, long? Source, long Active)>("""
                SELECT id, emulator_id, emulator_version, name, source_id, active FROM profile WHERE game_version_id = @version ORDER BY id
                """, new { version }).Select(p => new ProfileRow(p.Id, p.Emulator, p.EmulatorVersion, p.Name, p.Source, p.Active == 1)).ToList();
            var roms = c.Query<(long Id, string Path, long Modified, string? Label)>("""
                SELECT id, relative_path, modified, label FROM source
                WHERE version_id = @version AND kind = 'file' AND coalesce(component, 'BASE') = 'BASE' ORDER BY modified, id
                """, new { version }).Select(r => new RomEntry(r.Id, r.Path, r.Modified == 1, r.Label)).ToList();
            result.Add(new(version, platform, emulators, profiles, roms));
        }
        return result;
    }

    // ── Al lanzar (F6) ───────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Los argumentos de mods al lanzar con este perfil (Dusklight: `--mods <carpeta del perfil>`); vacío si no los usa.</summary>
    public static List<string> LaunchArgs(Database db, long id)
    {
        using var c = db.Open();
        if (Get(c, id) is not { } p || Recipes.Find(p.EmulatorId) is not { PerGame: { ModsArg: { } arg, Mods: [var mods, ..] } } recipe
            || Expand(mods, recipe, p.GameVersionId is { } v ? GameKey(c, v, p.SourceId) : null) is not { } relative) return [];
        var folder = Path.Combine(StoreOf(db, id), relative);
        Directory.CreateDirectory(folder);
        return [arg, folder];
    }

    /// <summary>
    /// `apply: swap` (sin configuración por juego): antes de lanzar, respalda la global y pone la del perfil (si tiene). Deja una marca
    /// con el perfil: <see cref="SwapOut"/> la restaura al cerrar o, si la app se cayó con el juego abierto, la próxima vez.
    /// </summary>
    /// <remarks>ponytail: lo llama Jugar (F6); hasta entonces nadie lanza juegos desde la app.</remarks>
    public static void SwapIn(Database db, long id)
    {
        using var c = db.Open();
        if (Get(c, id) is not { } p || Recipes.Find(p.EmulatorId) is not { PerGame: { Apply: "swap", Config: { } config } }
            || InstallOf(db, p.EmulatorId, p.EmulatorVersion) is not { } install) return;
        SwapOut(db, p.EmulatorId);
        var mark = Path.Combine(Root(db), SwapFolder, p.EmulatorId);
        var global = Path.Combine(install, config);
        if (File.Exists(global)) CopyFile(global, Path.Combine(mark, "global", config));
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(mark).FullName, "perfil"), $"{id}\n{install}");
        if (File.Exists(Path.Combine(StoreOf(db, id), config))) CopyFile(Path.Combine(StoreOf(db, id), config), global);
    }

    /// <summary>Al cerrar: lo que quedó en la global es del perfil (se guarda en él) y vuelve la global de antes. Sin marca, nada.</summary>
    public static void SwapOut(Database db, string emulator)
    {
        var mark = Path.Combine(Root(db), SwapFolder, emulator);
        if (!File.Exists(Path.Combine(mark, "perfil")) || Recipes.Find(emulator)?.PerGame?.Config is not { } config) return;
        var lines = File.ReadAllLines(Path.Combine(mark, "perfil"));
        var global = Path.Combine(lines[1], config);
        if (File.Exists(global)) CopyFile(global, Path.Combine(StoreOf(db, long.Parse(lines[0])), config));
        var before = Path.Combine(mark, "global", config);
        if (File.Exists(before)) CopyFile(before, global);
        else if (File.Exists(global)) File.Delete(global); // no había global: el emulador la vuelve a crear
        Directory.Delete(mark, recursive: true);
    }
}
