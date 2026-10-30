using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Data;

namespace Otteretro.Catalog;

/// <param name="Id">0 = «Emulación», los juegos sin saga (§6.2).</param>
public record SagaCard(long Id, string Name, bool Favorite, bool Following, bool Iconic, string[] Platforms, int Games, int Owned,
    long? CoverGameId, long Playtime, string? LastPlayed);

/// <param name="Stores">Tiendas donde lo tienes (por ahora, steam).</param>
/// <param name="Playtime">Minutos jugados según las tiendas.</param>
public record GameCard(long Id, string Name, int? Year, string[] Platforms, bool Owned, string? Status, long Playtime, bool HasCover,
    long? SagaId, string? SagaName, string[] Stores);

public record SagaDetail(SagaCard Saga, List<GameCard> Games);
public record ConsoleCount(string Id, string Name, string? Company, int Games, int Owned);

/// <param name="Unlinked">Lo que tienes en una tienda y todavía no está en el catálogo (solo en PC): aparece igual.</param>
public record CollectionPage(List<GameCard> Games, List<StoreEntry> Unlinked);
public record StoreEntry(string Store, string? Name, long? Playtime, string? LastPlayed);
public record VersionRow(long Id, string Platform, string Kind, int? Year, List<StoreEntry> Stores);

/// <param name="Winner">Es el valor que se muestra (lo manual siempre gana).</param>
public record FieldRow(string Field, string? Value, string Source, bool Manual, bool Winner);
public record ProgressInfo(string Status, string? StartedAt, string? FinishedAt, int? Rating, string? Notes, bool Favorite);
public record GameDetail(long Id, string NameEs, string? OriginalName, int? Year, long? SagaId, string? SagaName, bool HasCover, bool HasHero,
    List<VersionRow> Versions, List<FieldRow> Fields, ProgressInfo Progress, long Playtime, string? LastPlayed);
public record SearchResults(List<SagaCard> Sagas, List<GameCard> Games);

/// <summary>Lo que leen las pantallas del catálogo (F1.9). Las listas son de una persona: se arman en memoria.</summary>
public static class CatalogQueries
{
    public static readonly string[] Statuses = ["pending", "playing", "finished", "completed", "dropped"];

    /// <summary>Campos de un juego que se pueden corregir a mano desde la ficha.</summary>
    public static readonly string[] EditableFields = ["name_es", "original_name", "year"];

    // Clases con propiedades (no registros posicionales): Dapper las llena por nombre aunque una columna venga vacía.
    sealed class GameRow
    {
        public long Id { get; init; }
        public string Name { get; init; } = "";
        public long? Year { get; init; }
        public long? SagaId { get; init; }
        public string? SagaName { get; init; }
        public string? Platforms { get; init; }
        public long Owned { get; init; }
        public string? Status { get; init; }
        public long Playtime { get; init; }
        public string? Stores { get; init; }
    }

    sealed class SagaAgg
    {
        public long SagaId { get; init; }
        public long Games { get; init; }
        public long Owned { get; init; }
        public string? Platforms { get; init; }
        public long Playtime { get; init; }
        public string? LastPlayed { get; init; }
    }

    sealed class SagaRow
    {
        public long Id { get; init; }
        public string Name { get; init; } = "";
        public long Favorite { get; init; }
        public long Following { get; init; }
        public long Iconic { get; init; }
    }

    const string GameSelect = """
        SELECT g.id, g.name_es AS Name, g.year, g.saga_id AS SagaId, s.name AS SagaName,
               (SELECT group_concat(DISTINCT v.platform) FROM version v WHERE v.game_id = g.id) AS Platforms,
               EXISTS (SELECT 1 FROM source o JOIN version v ON v.id = o.version_id WHERE v.game_id = g.id) AS Owned,
               p.status,
               (SELECT coalesce(sum(o.playtime), 0) FROM source o JOIN version v ON v.id = o.version_id WHERE v.game_id = g.id) AS Playtime,
               (SELECT group_concat(DISTINCT o.store) FROM source o JOIN version v ON v.id = o.version_id WHERE v.game_id = g.id AND o.kind = 'store') AS Stores
        FROM game g LEFT JOIN saga s ON s.id = g.saga_id LEFT JOIN progress p ON p.game_id = g.id
        """;

    /// <summary>Orden de las consolas (el de la semilla) para mostrar las plataformas siempre igual.</summary>
    static string[] Ordered(SqliteConnection c, string? csv)
    {
        if (string.IsNullOrEmpty(csv)) return [];
        var order = c.Query<string>("SELECT id FROM console ORDER BY rowid").Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return csv.Split(',').OrderBy(p => order.GetValueOrDefault(p, int.MaxValue)).ToArray();
    }

    static HashSet<long> Covers(Database db) =>
        Directory.Exists(SteamGridDb.Folder(db))
            ? Directory.EnumerateFiles(SteamGridDb.Folder(db), "*-cover.*")
                .Select(f => long.TryParse(Path.GetFileName(f).Split('-')[0], out var id) ? id : 0).ToHashSet()
            : [];

    static List<GameCard> Games(Database db, SqliteConnection c, string where, object? args)
    {
        var covers = Covers(db);
        return c.Query<GameRow>($"{GameSelect} {where}", args)
            .Select(r => new GameCard(r.Id, r.Name, (int?)r.Year, Ordered(c, r.Platforms), r.Owned != 0, r.Status, r.Playtime,
                covers.Contains(r.Id), r.SagaId, r.SagaName, r.Stores?.Split(',') ?? []))
            .ToList();
    }

    /// <param name="filter">favorite, following, iconic, owned, missing o pc.</param>
    /// <param name="sort">name, iconic, played o activity.</param>
    public static List<SagaCard> Sagas(Database db, string? filter = null, string? platform = null, string? company = null, string? sort = null)
    {
        using var c = db.Open();
        var aggs = c.Query<SagaAgg>("""
            SELECT coalesce(g.saga_id, 0) AS SagaId, count(DISTINCT g.id) AS Games,
                   count(DISTINCT CASE WHEN o.id IS NOT NULL THEN g.id END) AS Owned,
                   group_concat(DISTINCT v.platform) AS Platforms,
                   coalesce(sum(o.playtime), 0) AS Playtime, max(o.last_played) AS LastPlayed
            FROM game g LEFT JOIN version v ON v.game_id = g.id LEFT JOIN source o ON o.version_id = v.id
            GROUP BY coalesce(g.saga_id, 0)
            """).ToDictionary(a => a.SagaId);
        // La portada de una saga: la del primer juego (por año) que la tenga.
        var covers = Covers(db);
        var coverOf = c.Query<(long SagaId, long Id)>("SELECT coalesce(saga_id, 0), id FROM game ORDER BY year IS NULL, year, id")
            .Where(g => covers.Contains(g.Id)).GroupBy(g => g.SagaId).ToDictionary(g => g.Key, g => g.First().Id);

        var rows = c.Query<SagaRow>("SELECT id, name, favorite, following, iconic FROM saga").ToList();
        if (aggs.ContainsKey(0)) rows.Add(new SagaRow());
        var companies = c.Query<(string Id, string? Company)>("SELECT id, company FROM console").ToDictionary(x => x.Id, x => x.Company);

        var cards = rows.Select(s =>
        {
            var a = aggs.GetValueOrDefault(s.Id) ?? new SagaAgg { SagaId = s.Id };
            return new SagaCard(s.Id, s.Name, s.Favorite != 0, s.Following != 0, s.Iconic != 0, Ordered(c, a.Platforms), (int)a.Games, (int)a.Owned,
                coverOf.TryGetValue(s.Id, out var cover) ? cover : null, a.Playtime, a.LastPlayed);
        });
        cards = filter switch
        {
            "favorite" => cards.Where(s => s.Favorite),
            "following" => cards.Where(s => s.Following),
            "iconic" => cards.Where(s => s.Iconic),
            "owned" => cards.Where(s => s.Owned > 0),
            "missing" => cards.Where(s => s.Owned < s.Games),
            "pc" => cards.Where(s => s.Platforms.Contains("pc")),
            _ => cards,
        };
        if (!string.IsNullOrEmpty(platform)) cards = cards.Where(s => s.Platforms.Contains(platform));
        if (!string.IsNullOrEmpty(company)) cards = cards.Where(s => s.Platforms.Any(p => companies.GetValueOrDefault(p) == company));
        var byName = cards.OrderBy(s => s.Id == 0).ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase);
        return (sort switch
        {
            "iconic" => cards.OrderBy(s => s.Id == 0).ThenByDescending(s => s.Iconic).ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase),
            "played" => cards.OrderBy(s => s.Id == 0).ThenByDescending(s => s.Playtime).ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase),
            "activity" => cards.OrderBy(s => s.Id == 0).ThenByDescending(s => s.LastPlayed ?? "").ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => byName,
        }).ToList();
    }

    public static SagaDetail? Saga(Database db, long id)
    {
        var card = Sagas(db).FirstOrDefault(s => s.Id == id);
        if (card is null) return null;
        using var c = db.Open();
        var games = Games(db, c, id == 0 ? "WHERE g.saga_id IS NULL ORDER BY g.year IS NULL, g.year, g.name_es" : "WHERE g.saga_id = @id ORDER BY g.year IS NULL, g.year, g.name_es", new { id });
        return new SagaDetail(card, games);
    }

    public static bool SetSagaFlags(Database db, long id, bool? favorite, bool? following)
    {
        using var c = db.Open();
        return c.Execute("UPDATE saga SET favorite = coalesce(@favorite, favorite), following = coalesce(@following, following) WHERE id = @id",
            new { id, favorite, following }) > 0;
    }

    public static List<ConsoleCount> Consoles(Database db)
    {
        using var c = db.Open();
        return c.Query<(string Id, string Name, string? Company, long Games, long Owned)>("""
            SELECT c.id, c.name, c.company, count(DISTINCT v.game_id), count(DISTINCT CASE WHEN o.id IS NOT NULL THEN v.game_id END)
            FROM console c LEFT JOIN version v ON v.platform = c.id LEFT JOIN source o ON o.version_id = v.id
            GROUP BY c.id ORDER BY c.rowid
            """).Select(r => new ConsoleCount(r.Id, r.Name, r.Company, (int)r.Games, (int)r.Owned)).ToList();
    }

    /// <param name="filter">owned, missing o pc (tiene versión de PC).</param>
    /// <param name="sort">saga, name o year.</param>
    public static CollectionPage Collection(Database db, string platform, string? filter = null, string? sort = null)
    {
        using var c = db.Open();
        var where = """
            WHERE EXISTS (SELECT 1 FROM version v WHERE v.game_id = g.id AND v.platform = @platform)
            """;
        where += filter switch
        {
            "owned" => " AND EXISTS (SELECT 1 FROM version v JOIN source o ON o.version_id = v.id WHERE v.game_id = g.id AND v.platform = @platform)",
            "missing" => " AND NOT EXISTS (SELECT 1 FROM version v JOIN source o ON o.version_id = v.id WHERE v.game_id = g.id AND v.platform = @platform)",
            "pc" => " AND EXISTS (SELECT 1 FROM version v WHERE v.game_id = g.id AND v.platform = 'pc')",
            _ => "",
        };
        where += sort switch
        {
            "name" => " ORDER BY g.name_es",
            "year" => " ORDER BY g.year IS NULL, g.year, g.name_es",
            _ => " ORDER BY s.name IS NULL, s.name, g.year IS NULL, g.year, g.name_es",
        };
        var games = Games(db, c, where, new { platform });
        var unlinked = platform == "pc" && filter != "missing"
            ? c.Query<StoreEntry>("SELECT store, name, playtime, last_played AS LastPlayed FROM source WHERE kind = 'store' AND version_id IS NULL ORDER BY name").ToList()
            : [];
        return new CollectionPage(games, unlinked);
    }

    public static GameDetail? Game(Database db, long id)
    {
        using var c = db.Open();
        var g = c.QuerySingleOrDefault<(long Id, string NameEs, string? OriginalName, long? Year, long? SagaId, string? SagaName)>(
            "SELECT g.id, g.name_es, g.original_name, g.year, g.saga_id, s.name FROM game g LEFT JOIN saga s ON s.id = g.saga_id WHERE g.id = @id", new { id });
        if (g.Id == 0) return null;

        var stores = c.Query<(long VersionId, string Store, string? Name, long? Playtime, string? LastPlayed)>("""
            SELECT o.version_id, o.store, o.name, o.playtime, o.last_played FROM source o JOIN version v ON v.id = o.version_id
            WHERE v.game_id = @id AND o.kind = 'store'
            """, new { id }).ToList();
        var order = c.Query<string>("SELECT id FROM console ORDER BY rowid").ToList();
        var versions = c.Query<(long Id, string Platform, string Kind, long? Year)>("SELECT id, platform, kind, year FROM version WHERE game_id = @id", new { id })
            .OrderBy(v => order.IndexOf(v.Platform)).ThenBy(v => v.Year)
            .Select(v => new VersionRow(v.Id, v.Platform, v.Kind, (int?)v.Year,
                stores.Where(s => s.VersionId == v.Id).Select(s => new StoreEntry(s.Store, s.Name, s.Playtime, s.LastPlayed)).ToList()))
            .ToList();

        var fields = c.Query<(string Field, string? Value, string Source, bool Manual)>(
                "SELECT field, value, source, manual FROM field_value WHERE entity = 'game' AND entity_id = @id", new { id })
            .GroupBy(f => f.Field)
            .SelectMany(group => group.OrderBy(f => f.Value is null && !f.Manual).ThenBy(f => FieldValues.Rank(f.Field, f.Source, f.Manual)).Select((f, i) => new FieldRow(f.Field, f.Value, f.Source, f.Manual, i == 0)))
            .ToList();

        var p = c.QuerySingleOrDefault<(string Status, string? StartedAt, string? FinishedAt, long? Rating, string? Notes, long Favorite)?>(
            "SELECT status, started_at, finished_at, rating, notes, favorite FROM progress WHERE game_id = @id", new { id });
        var progress = p is { } x ? new ProgressInfo(x.Status, x.StartedAt, x.FinishedAt, (int?)x.Rating, x.Notes, x.Favorite != 0)
            : new ProgressInfo("pending", null, null, null, null, false);

        return new GameDetail(g.Id, g.NameEs, g.OriginalName, (int?)g.Year, g.SagaId, g.SagaName,
            SteamGridDb.Find(db, id, "cover") is not null, SteamGridDb.Find(db, id, "hero") is not null,
            versions, fields, progress, stores.Sum(s => s.Playtime ?? 0), stores.Max(s => s.LastPlayed));
    }

    public static bool SaveProgress(Database db, long id, ProgressInfo p)
    {
        using var c = db.Open();
        if (c.ExecuteScalar<long>("SELECT count(*) FROM game WHERE id = @id", new { id }) == 0) return false;
        c.Execute("""
            INSERT INTO progress (game_id, status, started_at, finished_at, rating, notes, favorite)
            VALUES (@id, @Status, @StartedAt, @FinishedAt, @Rating, @Notes, @Favorite)
            ON CONFLICT (game_id) DO UPDATE SET status = excluded.status, started_at = excluded.started_at, finished_at = excluded.finished_at,
                rating = excluded.rating, notes = excluded.notes, favorite = excluded.favorite
            """, new { id, p.Status, p.StartedAt, p.FinishedAt, p.Rating, p.Notes, p.Favorite });
        return true;
    }

    /// <summary>Busca sagas y juegos por nombre (en español u original). Sin distinguir mayúsculas.</summary>
    public static SearchResults Search(Database db, string q)
    {
        var like = $"%{q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        var sagas = Sagas(db).Where(s => s.Id != 0 && s.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).Take(8).ToList();
        using var c = db.Open();
        var games = Games(db, c, """
            WHERE g.name_es LIKE @like ESCAPE '\' OR g.original_name LIKE @like ESCAPE '\'
            ORDER BY g.name_es LIKE @start ESCAPE '\' DESC, g.year IS NULL, g.year LIMIT 20
            """, new { like, start = like[1..] });
        return new SearchResults(sagas, games);
    }
}
