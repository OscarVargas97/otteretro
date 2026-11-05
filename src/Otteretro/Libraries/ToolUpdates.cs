using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>Un aviso de versión nueva (§8.4): de algo instalado, o la última de lo que no tienes.</summary>
/// <param name="Channel">stable o dev.</param>
/// <param name="Installed">Las versiones que ya tienes: la nueva se instala al lado. Vacío si no está instalado.</param>
/// <param name="Games">Los juegos de tu colección que lo usan: su perfil activo o, sin perfil, el emulador base de su consola (decisión 286).</param>
/// <param name="Download">El archivo de Windows de esa versión (el `asset` de la receta): con él, «Actualizar» e «Instalar» lo instalan. Null: solo la página.</param>
public record ToolUpdate(string RecipeId, string Name, string Kind, string Channel, string Version, string? Notes, string? Url, string? PublishedAt,
    List<string> Installed, List<string> Games, string? Download);

/// <summary>Una versión publicada de una receta, para elegir cuál instalar (#296).</summary>
public record RecipeRelease(string Version, string Channel, string? PublishedAt, string? Url, string Download);

/// <summary>
/// Avisos de versiones (decisión 294): se revisa al abrir, una vez al día y con «Revisar ahora». De las de desarrollo, como mucho un aviso por
/// semana por receta, salvo al pedirlo a mano. Lo visto y lo omitido se guarda en `setting`, una fila por receta y canal.
/// </summary>
public static class ToolUpdates
{
    public const string Kind = "tools.updates";
    const string Checked = "tools.updates.checked";
    public static readonly string[] Channels = ["stable", "dev"];
    public static TimeSpan Daily { get; set; } = TimeSpan.FromDays(1);
    public static TimeSpan DevInterval { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Lo último publicado en un canal.</summary>
    /// <param name="Download">El archivo de la release que calza con el `asset` de la receta.</param>
    public record Release(string Version, string? Notes, string? Url, string? PublishedAt, string? Download = null);
    /// <param name="NotifiedAt">Cuándo cambió el aviso: el tope semanal de desarrollo se cuenta desde acá.</param>
    record Saved(string Version, string? Notes, string? Url, string? PublishedAt, string NotifiedAt, string? Skipped = null, string? Download = null);

    /// <summary>Para las pruebas: responde en lugar de GitHub, Forgejo o Dolphin.</summary>
    public static HttpMessageHandler? Handler { get; set; }
    static readonly HttpClient Shared = NewClient(null);
    static HttpClient Http => Handler is null ? Shared : NewClient(Handler);

    static HttpClient NewClient(HttpMessageHandler? handler)
    {
        var http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(30);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Otteretro");
        return http;
    }

    static readonly JsonSerializerOptions Snake = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    internal record GitAsset(string Name, string? BrowserDownloadUrl);
    internal record GitRelease(string TagName, string? Name, string? Body, bool Draft, bool Prerelease, string? PublishedAt, string? HtmlUrl, List<GitAsset>? Assets);
    record DolphinArtifact(string Url);
    record DolphinBuild(string Shortrev, string? Date, string? ChangelogHtml, List<DolphinArtifact>? Artifacts);

    /// <param name="manual">«Revisar ahora»: sin el tope semanal de desarrollo.</param>
    public static long Enqueue(Database db, bool manual = false)
    {
        using var c = db.Open();
        return c.ExecuteScalar<long?>("SELECT id FROM task WHERE kind = @Kind AND status IN ('queued', 'running') LIMIT 1", new { Kind })
            ?? TaskQueue.Enqueue(db, Kind, manual ? "manual" : null);
    }

    /// <summary>Una vez al día con la app abierta.</summary>
    public static void EnqueueIfDue(Database db)
    {
        if (DateTime.TryParse(Setting(db, Checked), null, System.Globalization.DateTimeStyles.RoundtripKind, out var at) && DateTime.UtcNow - at < Daily) return;
        Enqueue(db);
    }

    public static async Task Run(TaskRun run, CancellationToken ct)
    {
        var http = Http;
        var manual = run.Data == "manual";
        // También lo que no tienes instalado: el aviso ofrece instalarlo (respuesta a la pregunta abierta de #278).
        var recipes = Recipes.All.Where(r => r.Updates is not null).ToList();
        for (var i = 0; i < recipes.Count; i++)
        {
            var r = recipes[i];
            var u = r.Updates!;
            run.Report(i * 100 / recipes.Count, ApiError.Of("tasks.tools.checking", ("name", r.Name)));
            foreach (var channel in ChannelsOf(u, manual))
                try
                {
                    if (await Latest(http, u, channel, ct, r.Asset) is { } latest) Remember(run.Db, r.Id, channel, latest, manual, DateTime.UtcNow);
                }
                // Una fuente caída no frena a las demás: se vuelve a mirar mañana o con «Revisar ahora».
                catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    run.Log(ApiError.Of("tasks.tools.sourceFailed", ("name", r.Name), ("detail", e.Message)), "warn");
                }
        }
        Save(run.Db, Checked, DateTime.UtcNow.ToString("O"));
    }

    /// <summary>
    /// Qué canales se revisan: el estable siempre; el de desarrollo, solo (con <c>dev</c>) o a pedido si la receta tiene de dónde sacarlo
    /// (<c>devRepo</c>, como el Canary de Ryubing, o <c>devPattern</c>, como los -rc de Eden).
    /// </summary>
    public static string[] ChannelsOf(RecipeUpdates u, bool manual) =>
        u.Dev || manual && (u.DevRepo is not null || u.DevPattern is not null) ? Channels : ["stable"];

    /// <summary>Lo último de un canal; null si la fuente no publica en él.</summary>
    public static async Task<Release?> Latest(HttpClient http, RecipeUpdates u, string channel, CancellationToken ct, string? asset = null) =>
        (await Releases(http, u, channel, ct, asset)).FirstOrDefault();

    /// <summary>
    /// Las versiones de una receta que se pueden instalar (las que traen su `asset`), de la más nueva a la más vieja, en los canales
    /// que revisa «Revisar ahora». Para elegir la versión en Herramientas › Instalar (#296).
    /// </summary>
    public static async Task<List<RecipeRelease>> Installable(Recipe r, CancellationToken ct)
    {
        var list = new List<RecipeRelease>();
        if (r.Updates is not { } u) return list;
        foreach (var channel in ChannelsOf(u, manual: true))
            foreach (var x in await Releases(Http, u, channel, ct, r.Asset))
                if (x.Download is { } download) list.Add(new(x.Version, channel, x.PublishedAt, x.Url, download));
        return list.OrderByDescending(x => x.PublishedAt ?? "").ToList();
    }

    /// <summary>Lo publicado en un canal, de lo más nuevo a lo más viejo; vacío si la fuente no publica en él.</summary>
    static async Task<List<Release>> Releases(HttpClient http, RecipeUpdates u, string channel, CancellationToken ct, string? asset)
    {
        if (u.Source == "dolphin")
        {
            using var response = await http.GetAsync($"https://dolphin-emu.org/update/latest/{(channel == "dev" ? "dev" : "beta")}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return [];
            response.EnsureSuccessStatusCode();
            var b = JsonSerializer.Deserialize<DolphinBuild>(await response.Content.ReadAsStringAsync(ct), Snake)!;
            // Su servidor da el archivo de cada sistema (`artifacts`): el de Windows es el que calza con el `asset` de la receta.
            return [new(b.Shortrev, b.ChangelogHtml is null ? null : PlainText(b.ChangelogHtml), "https://dolphin-emu.org/download/", b.Date,
                asset is null ? null : b.Artifacts?.FirstOrDefault(a => Regex.IsMatch(Path.GetFileName(new Uri(a.Url).AbsolutePath), asset))?.Url)];
        }
        // Con devRepo, todo lo de ese repositorio es de desarrollo; si no, las prereleases y lo que calce con devPattern.
        var fromDevRepo = channel == "dev" && u.DevRepo is not null;
        var repo = fromDevRepo ? u.DevRepo : u.Repo;
        var url = u.Source == "forgejo" ? $"https://{u.Host}/api/v1/repos/{repo}/releases?limit=30" : $"https://api.github.com/repos/{repo}/releases?per_page=30";
        var list = JsonSerializer.Deserialize<List<GitRelease>>(await http.GetStringAsync(url, ct), Snake)!;
        bool IsDev(GitRelease x) => fromDevRepo || x.Prerelease || u.DevPattern is not null && Regex.IsMatch(x.TagName, u.DevPattern);
        // Por fecha: RPCS3 no las lista en orden, y DuckStation o Vita3K reusan tags fijos (latest, preview, continuous).
        return list.Where(x => !x.Draft && IsDev(x) == (channel == "dev")).OrderByDescending(x => x.PublishedAt ?? "")
            .Select(g => new Release(VersionOf(g), g.Body, g.HtmlUrl, g.PublishedAt,
                asset is null ? null : g.Assets?.FirstOrDefault(a => Regex.IsMatch(a.Name, asset))?.BrowserDownloadUrl))
            .ToList();
    }

    static string Normalize(string tag) => tag.TrimStart('v', 'V');

    static readonly Regex VersionText = new(@"\d+(\.\d+)+(-\d+)?");

    /// <summary>
    /// La versión de una release: la del tag si empieza por un número (v2.0.3, v0.1-12083, v0.2.0-rc1), si no la del título (RPCS3: tag build-&lt;hash&gt;, título 0.0.37-18146) y, si
    /// ninguno la dice (latest, preview, continuous), la fecha de publicación (aaaa.MM.dd), como las recetas que versionan por fecha.
    /// </summary>
    internal static string VersionOf(GitRelease g) =>
        Regex.IsMatch(g.TagName, @"^[vV]?\d") ? Normalize(g.TagName)
        : VersionText.Match(g.TagName) is { Success: true } t ? t.Value
        : VersionText.Match(g.Name ?? "") is { Success: true } n ? n.Value
        : g.PublishedAt is { Length: >= 10 } p ? p[..10].Replace('-', '.') : Normalize(g.TagName);

    static string PlainText(string html) =>
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, @"<br\s*/?>|</p>", "\n", RegexOptions.IgnoreCase), "<[^>]+>", "")).Trim();

    /// <summary>Guarda lo último de un canal. Una de desarrollo cambia el aviso como mucho una vez por semana, salvo a pedido.</summary>
    public static void Remember(Database db, string recipeId, string channel, Release latest, bool manual, DateTime now)
    {
        var saved = Read(db, recipeId, channel);
        if (saved?.Version == latest.Version) return;
        if (channel == "dev" && !manual && saved is not null && now - DateTime.Parse(saved.NotifiedAt, null, System.Globalization.DateTimeStyles.RoundtripKind) < DevInterval) return;
        Write(db, recipeId, channel, new(latest.Version, latest.Notes, latest.Url, latest.PublishedAt, now.ToString("O"), saved?.Skipped, latest.Download));
    }

    /// <summary>Los avisos vigentes, ni omitidos ni ya instalados. De lo que no tienes, la última versión (con `Installed` vacío).</summary>
    public static List<ToolUpdate> List(Database db)
    {
        var installed = Recipes.Installed(Disk.ToolRoots(db)).Where(t => t.RecipeId is not null).ToLookup(t => t.RecipeId!, t => t.Version);
        var games = Games(db);
        var list = new List<ToolUpdate>();
        foreach (var r in Recipes.All.Where(r => r.Updates is not null))
            foreach (var channel in Channels)
            {
                if (Read(db, r.Id, channel) is not { } s || s.Skipped == s.Version) continue;
                var versions = installed[r.Id].ToList();
                if (versions.Any(v => Has(r, v, s))) continue;
                list.Add(new(r.Id, r.Name, r.Kind, channel, s.Version, s.Notes, s.Url, s.PublishedAt, versions, games[r.Id].ToList(), s.Download));
            }
        return list;
    }

    /// <summary>
    /// Esa instalación ya es esta versión (o una más nueva, si la receta versiona por fecha).
    /// ponytail: por fecha se compara la del ejecutable con la de publicación; si se instala por la app (#268) con el nombre de la versión, coincide directo.
    /// </summary>
    static bool Has(Recipe r, string installed, Saved s) =>
        Normalize(installed) == s.Version
        || r.Version?.From == "date" && s.PublishedAt is { Length: >= 10 } p && string.CompareOrdinal(installed, p[..10].Replace('-', '.')) >= 0;

    /// <summary>
    /// Qué juegos de tu colección usa cada emulador (decisión 286): el de su perfil activo o, si no tiene, el emulador base de su
    /// consola. El base sale de `console_emulator` y, en las consolas donde no se eligió, del `baseFor` de las recetas.
    /// </summary>
    public static ILookup<string, string> Games(Database db)
    {
        var baseOf = new Dictionary<string, string>();
        foreach (var r in Recipes.All)
            foreach (var console in r.BaseFor) baseOf.TryAdd(console, r.Id);
        using var c = db.Open();
        foreach (var (console, emulator) in c.Query<(string, string)>("SELECT console_id, emulator_id FROM console_emulator WHERE is_base = 1"))
            baseOf[console] = emulator;
        return c.Query<(string Platform, string Name, string? Active)>("""
            SELECT DISTINCT v.platform, g.name_es, p.emulator_id FROM source s JOIN version v ON v.id = s.version_id JOIN game g ON g.id = v.game_id
            LEFT JOIN profile p ON p.game_version_id = v.id AND p.active = 1
            ORDER BY g.name_es
            """)
            .Select(x => (Emulator: x.Active ?? baseOf.GetValueOrDefault(x.Platform), x.Name))
            .Where(x => x.Emulator is not null).Distinct()
            .ToLookup(x => x.Emulator!, x => x.Name);
    }

    /// <summary>«Omitir esta versión»: no vuelve a avisar hasta que salga otra.</summary>
    public static bool Skip(Database db, string recipeId, string channel)
    {
        if (Read(db, recipeId, channel) is not { } s) return false;
        Write(db, recipeId, channel, s with { Skipped = s.Version });
        return true;
    }

    static string Name(string recipeId, string channel) => $"tools.update.{recipeId}.{channel}";

    static Saved? Read(Database db, string recipeId, string channel) =>
        Setting(db, Name(recipeId, channel)) is { } json ? JsonSerializer.Deserialize<Saved>(json, JsonSerializerOptions.Web) : null;

    static void Write(Database db, string recipeId, string channel, Saved s) => Save(db, Name(recipeId, channel), JsonSerializer.Serialize(s, JsonSerializerOptions.Web));

    static string? Setting(Database db, string name)
    {
        using var c = db.Open();
        return c.ExecuteScalar<string?>("SELECT value FROM setting WHERE machine_id = '' AND name = @name", new { name });
    }

    static void Save(Database db, string name, string value)
    {
        using var c = db.Open();
        c.Execute("INSERT INTO setting (machine_id, name, value) VALUES ('', @name, @value) ON CONFLICT (name, machine_id) DO UPDATE SET value = excluded.value", new { name, value });
    }
}
