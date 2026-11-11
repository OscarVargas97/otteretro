using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>Un port nativo de PC de un juego, como lo publica el catálogo (otteretro-catalogo#69).</summary>
/// <param name="Status">complete, playable, beta, experimental o no-port (sin port todavía: no se instala).</param>
/// <param name="FullyPlayable">100 % jugable (status complete): el «port al 100 %» de la regla de cómo jugar (§8.3).</param>
/// <param name="Delisted">Su fuente ya no lo lista.</param>
/// <param name="EmulatorLevel">Cómo corre el juego en <paramref name="Emulator"/>: 3 perfecto, 2 jugable, 1 parcial, 0 no funciona.</param>
/// <param name="RecipeId">Su receta, si la hay: se instala con un clic.</param>
/// <param name="Installed">Las versiones que ya tienes instaladas.</param>
public record PortInfo(long Id, long GameId, string GameName, string Console, string Project, string Kind, string? Toolchain, string Status,
    bool FullyPlayable, bool Abandoned, bool Delisted, string Link, string Added, string? Emulator, int? EmulatorLevel, string? RecipeId,
    List<string> Installed);

/// <param name="Url">La release ya compilada; sin ella, la receta del port.</param>
/// <param name="Version">Sin ella, la que diga el nombre del archivo.</param>
public record PortInstallRequest(string? Url = null, string? Version = null);

/// <summary>
/// Los ports en Otteretro (#286, #287; decisiones 296 y 299): los muestra la ficha de cada juego e Inicio › Novedades (los nuevos de
/// juegos que tienes o sigues) y se instalan con su receta o con la URL de su release, como cualquier emulador.
/// </summary>
public static class Ports
{
    public const string Complete = "complete", NoPort = "no-port";
    /// <summary>La fecha de alta (AAAA-MM-DD) del port más nuevo que ya se vio en Novedades.</summary>
    const string SeenSetting = "ports.seen";

    sealed class Row
    {
        public long Id { get; init; }
        public long GameId { get; init; }
        public string GameName { get; init; } = "";
        public string Console { get; init; } = "";
        public string Project { get; init; } = "";
        public string Kind { get; init; } = "";
        public string? Toolchain { get; init; }
        public string Status { get; init; } = "";
        public bool Abandoned { get; init; }
        public bool Delisted { get; init; }
        public string Link { get; init; } = "";
        public string Added { get; init; } = "";
        public string? Emulator { get; init; }
        public long? EmulatorLevel { get; init; }
    }

    const string Select = """
        SELECT p.id, p.game_id AS GameId, g.name_es AS GameName, p.console, p.project, p.kind, p.toolchain, p.status, p.abandoned, p.delisted,
               p.link, p.added, p.emulator, p.emulator_level AS EmulatorLevel
        FROM port p JOIN game g ON g.id = p.game_id
        """;

    /// <summary>La receta de un port: la de tipo port cuyo <c>port</c> es su enlace (#384). Una receta sin <c>port</c> no se cruza.</summary>
    public static Recipe? RecipeOf(string link) =>
        Recipes.All.FirstOrDefault(r => r.Kind == "port" && r.Port is not null && LinkKey(r.Port) == LinkKey(link));

    /// <summary>El enlace como lo compara el catálogo: sin espacios, sin «/» final y sin mayúsculas.</summary>
    static string LinkKey(string link) => link.Trim().TrimEnd('/').ToLowerInvariant();

    /// <summary>Con qué id se instala: el de su receta o, sin ella, uno propio.</summary>
    static string EmulatorId(Recipe? recipe, long id) => recipe?.Id ?? $"port-{id}";

    static List<PortInfo> Query(Database db, string where, object? args = null)
    {
        using var c = db.Open();
        var rows = c.Query<Row>($"{Select} {where}", args).ToList();
        // La instalada, no las que solo quedan en la librería (decisión 325).
        var installed = Installs.List(db).Where(i => i.Path is not null).ToLookup(i => i.EmulatorId, i => i.Version);
        return rows.Select(r =>
        {
            var recipe = RecipeOf(r.Link);
            return new PortInfo(r.Id, r.GameId, r.GameName, r.Console, r.Project, r.Kind, r.Toolchain, r.Status, r.Status == Complete, r.Abandoned, r.Delisted,
                r.Link, r.Added, r.Emulator, (int?)r.EmulatorLevel, recipe?.Id, installed[EmulatorId(recipe, r.Id)].ToList());
        }).ToList();
    }

    /// <summary>Los ports de un juego: primero los 100 % jugables, después por estado y los ya no listados al final.</summary>
    public static List<PortInfo> OfGame(Database db, long gameId) =>
        Query(db, """
            WHERE p.game_id = @gameId
            ORDER BY p.delisted, CASE p.status WHEN 'complete' THEN 0 WHEN 'playable' THEN 1 WHEN 'beta' THEN 2 WHEN 'experimental' THEN 3 ELSE 4 END, p.project
            """, new { gameId });

    /// <summary>
    /// Novedades (decisión 296): los ports que entraron al catálogo desde la última vez que se vieron (sin ningún «Visto», los de los últimos
    /// 30 días), de juegos que tienes o de sagas que sigues.
    /// </summary>
    public static List<PortInfo> News(Database db)
    {
        string? seen;
        using (var c = db.Open())
            seen = c.ExecuteScalar<string?>("SELECT value FROM setting WHERE machine_id = '' AND name = @SeenSetting", new { SeenSetting });
        return Query(db, """
            WHERE (@seen IS NULL AND p.added >= date('now', '-30 days') OR p.added > @seen)
              AND (EXISTS (SELECT 1 FROM source o JOIN version v ON v.id = o.version_id WHERE v.game_id = p.game_id)
                   OR EXISTS (SELECT 1 FROM saga_user u WHERE u.saga_id = g.saga_id AND u.following))
            ORDER BY p.added DESC, g.name_es, p.project
            """, new { seen });
    }

    /// <summary>«Visto»: lo que hoy está en Novedades deja de estarlo; lo que entre después, aparece.</summary>
    public static void MarkSeen(Database db)
    {
        if (News(db).MaxBy(p => p.Added)?.Added is not { } added) return;
        using var c = db.Open();
        c.Execute("INSERT INTO setting (machine_id, name, value) VALUES ('', @SeenSetting, @added) ON CONFLICT (name, machine_id) DO UPDATE SET value = excluded.value",
            new { SeenSetting, added });
    }

    static readonly Regex VersionText = new(@"\d+(\.\d+)+(-\d+)?");

    record GitAsset(string Name, string BrowserDownloadUrl);
    record GitRelease(string TagName, string? Name, bool Draft, bool Prerelease, string? PublishedAt, List<GitAsset> Assets);
    static readonly JsonSerializerOptions Snake = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>
    /// La última versión estable de la receta para este sistema: la de sus descargas o, si no trae, la release de GitHub con el archivo que
    /// dice su <c>asset</c>. Null si no se sabe de dónde.
    /// </summary>
    static async Task<(string Version, string? Url)?> Latest(Recipe r, string? token, CancellationToken ct)
    {
        var os = OperatingSystem.IsWindows() ? "windows" : "linux";
        if (r.Downloads.LastOrDefault(d => d.System == os) is { } d) return (d.Version, null);
        if (r.Updates is not { Source: "github", Repo: { } repo } || r.Asset is null || !OperatingSystem.IsWindows()) return null;
        using var http = Installs.Handler is null ? new HttpClient() : new HttpClient(Installs.Handler, disposeHandler: false);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Otteretro");
        var releases = JsonSerializer.Deserialize<List<GitRelease>>(await GithubApi.GetString(http, $"https://api.github.com/repos/{repo}/releases?per_page=30", token, ct), Snake)!;
        var asset = new Regex(r.Asset);
        return releases.Where(x => !x.Draft && !x.Prerelease && x.Assets.Any(a => asset.IsMatch(a.Name))).MaxBy(x => x.PublishedAt ?? "") is { } g
            ? (ToolUpdates.VersionOf(new(g.TagName, g.Name, null, g.Draft, g.Prerelease, g.PublishedAt, null, null)), g.Assets.First(a => asset.IsMatch(a.Name)).BrowserDownloadUrl)
            : null;
    }

    /// <summary>Instalar un port (#287): con un clic si tiene receta, o con la URL de su release. Devuelve la tarea o el error.</summary>
    public static async Task<(long? Task, ApiError? Error)> Install(Database db, long id, PortInstallRequest request, CancellationToken ct = default, string? token = null)
    {
        if (Query(db, "WHERE p.id = @id", new { id }).FirstOrDefault() is not { } port) return (null, ApiError.Of("ports.notFound"));
        if (port.Status == NoPort) return (null, ApiError.Of("ports.noPort"));
        var recipe = RecipeOf(port.Link);
        var job = new InstallJob(EmulatorId(recipe, id), request.Version?.Trim() is { Length: > 0 } v ? v : null, Name: port.Project, Kind: "port", Port: id);
        if (request.Url?.Trim() is { Length: > 0 } url)
            job = job with { Url = url, Version = job.Version ?? (VersionText.Match(Uri.TryCreate(url, UriKind.Absolute, out var u) ? Path.GetFileName(u.AbsolutePath) : url) is { Success: true } m ? m.Value : null) };
        else if (recipe is null) return (null, ApiError.Of("ports.needsUrl"));
        else
        {
            (string Version, string? Url)? latest;
            try { latest = await Latest(recipe, token, ct); }
            catch (GithubRateLimitException e) { return (null, e.Error); }
            catch (Exception e) when (e is HttpRequestException or JsonException) { return (null, ApiError.Of("ports.sourceUnreachable")); }
            if (latest is not { } l) return (null, ApiError.Of("ports.needsUrl"));
            job = job with { Version = l.Version, Url = l.Url };
        }
        return Installs.Enqueue(db, job);
    }

    /// <summary>
    /// Sus juegos tienen perfil con el port, como con cualquier emulador (§8.0): uno por versión del juego en la consola del port, aunque
    /// ya tenga otros (si ya tiene el del port, no se repite). Un port 100 % jugable queda como el activo, salvo que esa versión ya tenga
    /// uno propio activo: ese no se cambia.
    /// </summary>
    /// <remarks>Activa con <see cref="Profiles.Switch"/> (#269): guarda el perfil que estaba y carga el del port. Si no se puede hacer
    /// entero (o hay que elegir el usuario de consola de las partidas), el del port queda creado pero inactivo, y se activa desde la ficha.</remarks>
    public static void AddProfiles(Database db, long portId, string emulator, string name)
    {
        List<(long Version, bool Activate)> versions;
        using (var c = db.Open())
            versions = c.Query<(long, bool)>("""
                SELECT v.id, p.status = 'complete' AND NOT EXISTS (SELECT 1 FROM profile a WHERE a.game_version_id = v.id AND a.active)
                FROM port p JOIN version v ON v.game_id = p.game_id AND v.platform = p.console
                WHERE p.id = @portId AND NOT EXISTS (SELECT 1 FROM profile f WHERE f.game_version_id = v.id AND f.emulator_id = @emulator)
                """, new { portId, emulator }).ToList();
        foreach (var (version, activate) in versions)
        {
            var profile = Profiles.Add(db, version, emulator, name);
            if (activate) Profiles.Switch(db, version, profile);
        }
    }
}
