using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>Dónde publica sus versiones una receta (decisión 294).</summary>
/// <param name="Source">github, forgejo (con <paramref name="Host"/>) o dolphin (su servidor de actualizaciones).</param>
/// <param name="Dev">También avisa de las versiones de desarrollo (prereleases; en Dolphin, la pista dev).</param>
public record RecipeUpdates(string Source, string? Repo = null, string? Host = null, bool Dev = false);

/// <summary>Un aviso de versión nueva (§8.4): de algo instalado, o la última de lo que no tienes.</summary>
/// <param name="Channel">stable o dev.</param>
/// <param name="Installed">Las versiones que ya tienes: la nueva se instala al lado. Vacío si no está instalado.</param>
/// <param name="Games">Los juegos que usan ese emulador. Vacío hasta que existan los perfiles (#267).</param>
public record ToolUpdate(string RecipeId, string Name, string Kind, string Channel, string Version, string? Notes, string? Url, string? PublishedAt,
    List<string> Installed, List<string> Games);

/// <summary>
/// Avisos de versiones (decisión 294): se revisa al abrir, una vez al día y con «Revisar ahora». De las de desarrollo, como mucho un aviso por
/// semana por receta, salvo al pedirlo a mano. Lo visto y lo omitido se guarda en `setting`, una fila por receta y canal.
/// Los mods con URL de release se suman cuando existan los perfiles (#267).
/// </summary>
public static class ToolUpdates
{
    public const string Kind = "tools.updates";
    const string Checked = "tools.updates.checked";
    public static readonly string[] Channels = ["stable", "dev"];
    public static TimeSpan Daily { get; set; } = TimeSpan.FromDays(1);
    public static TimeSpan DevInterval { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Lo último publicado en un canal.</summary>
    public record Release(string Version, string? Notes, string? Url, string? PublishedAt);
    /// <param name="NotifiedAt">Cuándo cambió el aviso: el tope semanal de desarrollo se cuenta desde acá.</param>
    record Saved(string Version, string? Notes, string? Url, string? PublishedAt, string NotifiedAt, string? Skipped = null);

    /// <summary>Para las pruebas: responde en lugar de GitHub, Forgejo o Dolphin.</summary>
    public static HttpMessageHandler? Handler { get; set; }
    static readonly HttpClient Shared = NewClient(null);

    static HttpClient NewClient(HttpMessageHandler? handler)
    {
        var http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(30);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Otteretro");
        return http;
    }

    static readonly JsonSerializerOptions Snake = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    record GitRelease(string TagName, string? Body, bool Draft, bool Prerelease, string? PublishedAt, string? HtmlUrl);
    record DolphinBuild(string Shortrev, string? Date, string? ChangelogHtml);

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
        var http = Handler is null ? Shared : NewClient(Handler);
        var manual = run.Data == "manual";
        // También lo que no tienes instalado: el aviso ofrece instalarlo (respuesta a la pregunta abierta de #278).
        var recipes = Recipes.All.Where(r => r.Updates is not null).ToList();
        for (var i = 0; i < recipes.Count; i++)
        {
            var r = recipes[i];
            run.Report(i * 100 / recipes.Count, ApiError.Of("tasks.tools.checking", ("name", r.Name)));
            foreach (var channel in r.Updates!.Dev ? Channels : ["stable"])
                try
                {
                    if (await Latest(http, r.Updates, channel, ct) is { } latest) Remember(run.Db, r.Id, channel, latest, manual, DateTime.UtcNow);
                }
                // Una fuente caída no frena a las demás: se vuelve a mirar mañana o con «Revisar ahora».
                catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    run.Log(ApiError.Of("tasks.tools.sourceFailed", ("name", r.Name), ("detail", e.Message)), "warn");
                }
        }
        Save(run.Db, Checked, DateTime.UtcNow.ToString("O"));
    }

    /// <summary>Lo último de un canal; null si la fuente no publica en él.</summary>
    static async Task<Release?> Latest(HttpClient http, RecipeUpdates u, string channel, CancellationToken ct)
    {
        if (u.Source == "dolphin")
        {
            using var response = await http.GetAsync($"https://dolphin-emu.org/update/latest/{(channel == "dev" ? "dev" : "beta")}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            var b = JsonSerializer.Deserialize<DolphinBuild>(await response.Content.ReadAsStringAsync(ct), Snake)!;
            return new(b.Shortrev, b.ChangelogHtml is null ? null : PlainText(b.ChangelogHtml), "https://dolphin-emu.org/download/", b.Date);
        }
        var url = u.Source == "forgejo" ? $"https://{u.Host}/api/v1/repos/{u.Repo}/releases?limit=30" : $"https://api.github.com/repos/{u.Repo}/releases?per_page=30";
        var list = JsonSerializer.Deserialize<List<GitRelease>>(await http.GetStringAsync(url, ct), Snake)!;
        var g = list.FirstOrDefault(x => !x.Draft && x.Prerelease == (channel == "dev"));
        return g is null ? null : new(Normalize(g.TagName), g.Body, g.HtmlUrl, g.PublishedAt);
    }

    static string Normalize(string tag) => tag.TrimStart('v', 'V');

    static string PlainText(string html) =>
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, @"<br\s*/?>|</p>", "\n", RegexOptions.IgnoreCase), "<[^>]+>", "")).Trim();

    /// <summary>Guarda lo último de un canal. Una de desarrollo cambia el aviso como mucho una vez por semana, salvo a pedido.</summary>
    public static void Remember(Database db, string recipeId, string channel, Release latest, bool manual, DateTime now)
    {
        var saved = Read(db, recipeId, channel);
        if (saved?.Version == latest.Version) return;
        if (channel == "dev" && !manual && saved is not null && now - DateTime.Parse(saved.NotifiedAt, null, System.Globalization.DateTimeStyles.RoundtripKind) < DevInterval) return;
        Write(db, recipeId, channel, new(latest.Version, latest.Notes, latest.Url, latest.PublishedAt, now.ToString("O"), saved?.Skipped));
    }

    /// <summary>Los avisos vigentes, ni omitidos ni ya instalados. De lo que no tienes, la última versión (con `Installed` vacío).</summary>
    public static List<ToolUpdate> List(Database db)
    {
        var installed = Recipes.Installed(Disk.ToolRoots(db)).Where(t => t.RecipeId is not null).ToLookup(t => t.RecipeId!, t => t.Version);
        var list = new List<ToolUpdate>();
        foreach (var r in Recipes.All.Where(r => r.Updates is not null))
            foreach (var channel in Channels)
            {
                if (Read(db, r.Id, channel) is not { } s || s.Skipped == s.Version) continue;
                var versions = installed[r.Id].ToList();
                if (versions.Any(v => Has(r, v, s))) continue;
                list.Add(new(r.Id, r.Name, r.Kind, channel, s.Version, s.Notes, s.Url, s.PublishedAt, versions, []));
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
