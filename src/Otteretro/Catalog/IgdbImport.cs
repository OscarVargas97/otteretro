using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Catalog;

/// <summary>
/// Carga del catálogo desde IGDB (F1.5), por partes: una tarea por saga o por consola, de a 500 juegos, y cada
/// página guarda su punto de control. Nunca todo PC de golpe: PC entra solo por sagas (#131).
/// </summary>
public static class IgdbImport
{
    /// <summary>Plataformas de IGDB → `console.id`. Las variantes van con su consola (GBC con Game Boy, DSi con DS…).</summary>
    public static readonly Dictionary<long, string> Platforms = new()
    {
        [18] = "nes",
        [33] = "gb",
        [22] = "gb",
        [19] = "snes",
        [87] = "vb",
        [4] = "n64",
        [416] = "n64",
        [24] = "gba",
        [21] = "gc",
        [20] = "ds",
        [159] = "ds",
        [5] = "wii",
        [37] = "3ds",
        [137] = "3ds",
        [41] = "wiiu",
        [130] = "switch",
        [7] = "ps1",
        [8] = "ps2",
        [38] = "psp",
        [9] = "ps3",
        [46] = "vita",
        [48] = "ps4",
        [167] = "ps5",
        [11] = "xbox",
        [12] = "x360",
        [49] = "xone",
        [169] = "xseries",
        [6] = "pc",
    };

    public const int Page = 500;
    const string Source = "igdb";

    /// <summary>Punto de control de «cargar una saga»: lo que se pidió, la saga encontrada y por dónde va.</summary>
    public record SagaJob(string Query, string? Kind = null, long? IgdbId = null, string? Name = null, long Offset = 0);
    public record PlatformJob(string Platform, long Offset = 0);
    public record SagaRef(string Kind, long IgdbId, string? Name);

    public static long EnqueueSaga(Database db, string query) =>
        TaskQueue.Enqueue(db, "igdb.saga", JsonSerializer.Serialize(new SagaJob(query), JsonSerializerOptions.Web));

    public static long EnqueuePlatform(Database db, string platform) =>
        TaskQueue.Enqueue(db, "igdb.platform", JsonSerializer.Serialize(new PlatformJob(platform), JsonSerializerOptions.Web));

    /// <summary>Para las pruebas: el manejador HTTP falso de IGDB y Twitch.</summary>
    public static HttpMessageHandler? Handler { get; set; }

    static Igdb Client(TaskRun run) => new(new ApiClient(run.Db, Handler), run.Credentials, Handler);

    public static async Task Saga(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<SagaJob>(run.Data!, JsonSerializerOptions.Web)!;
        var igdb = Client(run);
        if (job.IgdbId is null)
        {
            var found = await FindSaga(igdb, job.Query, ct) ?? throw new TaskException(ApiError.Of("tasks.igdb.sagaNotFound", ("query", job.Query)));
            job = job with { Kind = found.Kind, IgdbId = found.IgdbId, Name = found.Name ?? job.Query };
        }
        var saga = new SagaRef(job.Kind!, job.IgdbId!.Value, job.Name);
        var where = $"where {(saga.Kind == "franchise" ? "franchises" : "collections")} = ({saga.IgdbId}) & version_parent = null;";
        var total = await igdb.Count("games", where, ct);
        Report(run, job.Offset, total, ApiError.Of("tasks.igdb.saga", ("name", job.Name!), ("done", job.Offset.ToString()), ("total", total.ToString())), job);

        while (true)
        {
            var page = await igdb.Query<IgdbGame>("games", $"{IgdbGame.Fields} {where} sort id asc; limit {Page}; offset {job.Offset};", ct);
            await Import(run.Db, igdb, page, saga, ct);
            job = job with { Offset = job.Offset + page.Length };
            Report(run, job.Offset, total, ApiError.Of("tasks.igdb.saga", ("name", job.Name!), ("done", job.Offset.ToString()), ("total", total.ToString())), job);
            if (page.Length < Page) break;
        }
        QueueImages(run.Db);
    }

    public static async Task Platform(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<PlatformJob>(run.Data!, JsonSerializerOptions.Web)!;
        if (job.Platform == "pc") throw new TaskException(ApiError.Of("tasks.igdb.pcBySaga"));
        var ids = Platforms.Where(p => p.Value == job.Platform).Select(p => p.Key).ToList();
        if (ids.Count == 0) throw new TaskException(ApiError.Of("tasks.igdb.unknownPlatform", ("platform", job.Platform)));

        var igdb = Client(run);
        var where = $"where platforms = ({string.Join(',', ids)}) & version_parent = null & game_type = ({string.Join(',', IgdbGame.PlayableTypes)});";
        var total = await igdb.Count("games", where, ct);
        while (true)
        {
            var page = await igdb.Query<IgdbGame>("games", $"{IgdbGame.Fields} {where} sort id asc; limit {Page}; offset {job.Offset};", ct);
            await Import(run.Db, igdb, page, null, ct);
            job = job with { Offset = job.Offset + page.Length };
            Report(run, job.Offset, total, ApiError.Of("tasks.igdb.platform", ("platform", job.Platform), ("done", job.Offset.ToString()), ("total", total.ToString())), job);
            if (page.Length < Page) break;
        }
        QueueImages(run.Db);
    }

    record SteamLink(long Game, string Uid);

    /// <summary>
    /// Lo de Steam que no está en el catálogo: busca en IGDB cada id de Steam, carga esos juegos y enlaza el origen
    /// con su versión de PC (también cuando un juego tiene varios ids de Steam). De a 200, con punto de control.
    /// </summary>
    public static async Task FromSteam(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<Steam.LinkJob>(run.Data!, JsonSerializerOptions.Web)!;
        List<long> appIds;
        using (var c = run.Db.Open())
            appIds = c.Query<long>("""
                SELECT CAST(store_id AS INTEGER) AS app FROM source
                WHERE kind = 'store' AND store = 'steam' AND version_id IS NULL AND CAST(store_id AS INTEGER) > @After ORDER BY app
                """, job).ToList();
        var igdb = Client(run);
        for (var i = 0; i < appIds.Count; i += 200)
        {
            var chunk = appIds.Skip(i).Take(200).ToList();
            var uids = string.Join(',', chunk.Select(id => $"\"{id}\""));
            var links = await igdb.Query<SteamLink>("external_games", $"fields game, uid; where external_game_source = 1 & uid = ({uids}); limit 500;", ct);
            var gameIds = links.Select(l => l.Game).Distinct().ToList();
            if (gameIds.Count > 0)
                await Import(run.Db, igdb, await igdb.Query<IgdbGame>("games", $"{IgdbGame.Fields} where id = ({string.Join(',', gameIds)}); limit {Page};", ct), null, ct);

            using (var c = run.Db.Open())
            {
                foreach (var l in links)
                    c.Execute("""
                        UPDATE source SET version_id = (SELECT id FROM version WHERE igdb_id = @Game AND platform = 'pc')
                        WHERE kind = 'store' AND store = 'steam' AND store_id = @Uid AND version_id IS NULL
                        """, l);
                Steam.Link(c);
            }
            var done = Math.Min(i + 200, appIds.Count);
            run.Report(done * 100 / appIds.Count, ApiError.Of("tasks.igdb.steam", ("done", done.ToString()), ("total", appIds.Count.ToString())),
                JsonSerializer.Serialize(new Steam.LinkJob(chunk[^1]), JsonSerializerOptions.Web));
        }
        QueueImages(run.Db);
    }

    /// <summary>Al terminar una carga, las portadas de lo nuevo (si no hay ya una tarea de imágenes esperando).</summary>
    static void QueueImages(Database db)
    {
        using (var c = db.Open())
            if (c.ExecuteScalar<long>("SELECT count(*) FROM task WHERE kind = 'sgdb.images' AND status = 'queued'") > 0) return;
        SteamGridDb.Enqueue(db);
    }

    static void Report<T>(TaskRun run, long done, long total, ApiError detail, T job) =>
        run.Report(total == 0 ? 100 : (int)Math.Min(100, done * 100 / total), detail, JsonSerializer.Serialize(job, JsonSerializerOptions.Web));

    /// <summary>Busca la franquicia o colección que mejor coincide: primero el nombre exacto, después el más corto.</summary>
    static async Task<SagaRef?> FindSaga(Igdb igdb, string query, CancellationToken ct)
    {
        var q = query.Replace("\"", "").Replace("\\", "").Trim();
        var body = $"fields name, slug; where name ~ *\"{q}\"*; limit 50;";
        var franchises = await igdb.Query<IgdbNamed>("franchises", body, ct);
        var collections = await igdb.Query<IgdbNamed>("collections", body, ct);
        bool Exact(IgdbNamed n) => string.Equals(n.Name, q, StringComparison.OrdinalIgnoreCase) || string.Equals(n.Slug, q, StringComparison.OrdinalIgnoreCase);
        IgdbNamed? Shortest(IgdbNamed[] list) => list.Where(n => n.Name is not null).MinBy(n => n.Name!.Length);

        if (franchises.FirstOrDefault(Exact) is { } f) return new("franchise", f.Id, f.Name);
        if (collections.FirstOrDefault(Exact) is { } c) return new("collection", c.Id, c.Name);
        if (Shortest(franchises) is { } fs) return new("franchise", fs.Id, fs.Name);
        return Shortest(collections) is { } cs ? new("collection", cs.Id, cs.Name) : null;
    }

    /// <summary>
    /// Guarda una página de juegos. Los remakes, remasters y ports pasan a ser versiones de su juego original
    /// (si no vinieron en la página, se piden). Lo corregido a mano no se toca: ver <see cref="FieldValues"/>.
    /// </summary>
    public static async Task Import(Database db, Igdb igdb, IEnumerable<IgdbGame> page, SagaRef? preferred, CancellationToken ct)
    {
        var games = page.Where(g => g.VersionParent is null && IgdbGame.PlayableTypes.Contains(g.Type)).ToList();
        var missing = games.SelectMany(Children).Select(r => r.Id).Except(games.Select(g => g.Id)).Distinct().ToList();
        foreach (var chunk in missing.Chunk(Page))
            games.AddRange(await igdb.Query<IgdbGame>("games", $"{IgdbGame.Fields} where id = ({string.Join(',', chunk)}); limit {Page};", ct));

        var parentOf = new Dictionary<long, (long Parent, string Kind)>();
        foreach (var g in games)
            foreach (var (child, kind) in Children(g))
                parentOf.TryAdd(child, (g.Id, kind));

        using var c = db.Open();
        using var t = c.BeginTransaction();
        var byIgdb = new Dictionary<long, long>();
        // Primero los originales, para que sus remakes los encuentren.
        foreach (var g in games.OrderBy(g => parentOf.ContainsKey(g.Id)))
        {
            var kind = parentOf.TryGetValue(g.Id, out var p) ? p.Kind : "original";
            var root = g.Id;
            for (var i = 0; i < 10 && parentOf.TryGetValue(root, out var up); i++) root = up.Parent;

            long gameId;
            if (root != g.Id && byIgdb.TryGetValue(root, out var parentGame))
            {
                gameId = parentGame;
                MergeStandalone(c, t, g.Id, gameId);
            }
            else if (c.ExecuteScalar<long?>("SELECT game_id FROM version WHERE igdb_id = @Id", g, t) is { } existing
                     && c.ExecuteScalar<long?>("SELECT igdb_id FROM game WHERE id = @existing", new { existing }, t) != g.Id)
                gameId = existing; // ya es versión de otro juego (llegó antes con su original)
            else
            {
                gameId = UpsertGame(c, t, g, preferred);
                kind = g.Type switch { 8 => "remake", 9 => "remaster", _ => "original" };
            }
            byIgdb[g.Id] = gameId;
            UpsertVersions(c, t, g, gameId, kind);
        }
        Steam.Link(c, t);
        t.Commit();
    }

    static IEnumerable<(long Id, string Kind)> Children(IgdbGame g) =>
        (g.Remakes ?? []).Select(id => (id, "remake"))
        .Concat((g.Remasters ?? []).Select(id => (id, "remaster")))
        .Concat((g.Ports ?? []).Select(id => (id, "original")));

    static long UpsertGame(SqliteConnection c, SqliteTransaction t, IgdbGame g, SagaRef? preferred)
    {
        var id = c.ExecuteScalar<long?>("SELECT id FROM game WHERE igdb_id = @Id", g, t)
                 ?? c.ExecuteScalar<long>("INSERT INTO game (igdb_id, name_es) VALUES (@Id, @Name) RETURNING id", g, t);
        FieldValues.Set(c, "game", id, "original_name", g.Name, Source, t);
        FieldValues.Set(c, "game", id, "name_es", SpanishName(g), Source, t);
        FieldValues.Set(c, "game", id, "year", Year(g.FirstReleaseDate), Source, t);

        // La saga se elige una vez; recargar otra saga no mueve el juego (Hyrule Warriors sigue en Zelda).
        if (c.ExecuteScalar<long?>("SELECT saga_id FROM game WHERE id = @id", new { id }, t) is null && SagaOf(g, preferred) is { } saga)
            c.Execute("UPDATE game SET saga_id = @saga WHERE id = @id", new { id, saga = UpsertSaga(c, t, saga) }, t);
        return id;
    }

    static SagaRef? SagaOf(IgdbGame g, SagaRef? preferred)
    {
        var franchises = g.Franchises ?? [];
        var collections = g.Collections ?? [];
        if (preferred is not null && (preferred.Kind == "franchise" ? franchises : collections).Any(n => n.Id == preferred.IgdbId)) return preferred;
        if (franchises.FirstOrDefault() is { } f) return new("franchise", f.Id, f.Name);
        return collections.FirstOrDefault() is { } col ? new("collection", col.Id, col.Name) : null;
    }

    static long UpsertSaga(SqliteConnection c, SqliteTransaction t, SagaRef s)
    {
        var id = c.ExecuteScalar<long?>("SELECT id FROM saga WHERE igdb_kind = @Kind AND igdb_id = @IgdbId", s, t)
                 ?? c.ExecuteScalar<long>("INSERT INTO saga (name, igdb_kind, igdb_id) VALUES (@name, @Kind, @IgdbId) RETURNING id",
                     new { name = s.Name ?? $"IGDB {s.IgdbId}", s.Kind, s.IgdbId }, t);
        if (s.Name is not null) FieldValues.Set(c, "saga", id, "name", s.Name, Source, t);
        return id;
    }

    /// <summary>Un remake que antes se cargó como juego propio pasa a su original (si no tiene seguimiento).</summary>
    static void MergeStandalone(SqliteConnection c, SqliteTransaction t, long igdbId, long target)
    {
        if (c.ExecuteScalar<long?>("SELECT id FROM game WHERE igdb_id = @igdbId AND id <> @target", new { igdbId, target }, t) is not { } old) return;
        c.Execute("UPDATE version SET game_id = @target WHERE game_id = @old", new { target, old }, t);
        if (c.ExecuteScalar<long>("SELECT count(*) FROM progress WHERE game_id = @old", new { old }, t) > 0) return;
        c.Execute("DELETE FROM field_value WHERE entity = 'game' AND entity_id = @old; DELETE FROM game WHERE id = @old", new { old }, t);
    }

    static void UpsertVersions(SqliteConnection c, SqliteTransaction t, IgdbGame g, long gameId, string kind)
    {
        foreach (var platform in (g.Platforms ?? []).Where(Platforms.ContainsKey).GroupBy(p => Platforms[p]))
        {
            var dates = (g.ReleaseDates ?? []).Where(d => d.Platform is { } p && platform.Contains(p) && d.Date is not null).Select(d => d.Date!.Value).ToList();
            var year = Year(dates.Count > 0 ? dates.Min() : g.FirstReleaseDate);
            var steam = platform.Key == "pc" ? SteamId(g) : null;
            // Al recargar solo cambian el año y el id de Steam: el tipo y el juego pueden estar corregidos a mano.
            c.Execute("""
                INSERT INTO version (game_id, platform, kind, year, igdb_id, steam_app_id) VALUES (@gameId, @platform, @kind, @year, @igdbId, @steam)
                ON CONFLICT (igdb_id, platform) DO UPDATE SET year = excluded.year, steam_app_id = COALESCE(excluded.steam_app_id, version.steam_app_id)
                """, new { gameId, platform = platform.Key, kind, year, igdbId = g.Id, steam }, t);
        }
    }

    /// <summary>El nombre en español si IGDB lo tiene como nombre alternativo; si no, el original.</summary>
    static string SpanishName(IgdbGame g) =>
        (g.AlternativeNames ?? []).FirstOrDefault(n => n.Name is not null && n.Comment is { } note
            && (note.Contains("spanish", StringComparison.OrdinalIgnoreCase) || note.Contains("español", StringComparison.OrdinalIgnoreCase)))?.Name
        ?? g.Name;

    static int? Year(long? unix) => unix is { } s ? DateTimeOffset.FromUnixTimeSeconds(s).Year : null;

    /// <summary>Fuente 1 de IGDB = Steam (antes `category`, ahora `external_game_source`).</summary>
    static long? SteamId(IgdbGame g) =>
        (g.ExternalGames ?? []).FirstOrDefault(e => (e.ExternalGameSource ?? e.Category) == 1 && long.TryParse(e.Uid, out _)) is { } e ? long.Parse(e.Uid!) : null;
}
