using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;
using Otteretro.Libraries;

namespace Otteretro.Claude;

/// <param name="RecipeId">La receta recomendada; null si el emulador no tiene receta y entonces <paramref name="Repo"/> dice cuál es.</param>
/// <param name="Origin">claude (lo encontró «Buscar el mejor emulador») o manual (lo elegiste a mano).</param>
/// <param name="SearchedAt">Cuándo se buscó o se eligió.</param>
public record EmulatorPick(string Console, string? RecipeId, string? Repo, string? Source, string? Reason, string Origin, string SearchedAt);

/// <summary>Un juego en el selector de «Buscar el mejor emulador» (#374).</summary>
/// <param name="SearchedAt">Cuándo se buscó por última vez; null si nunca.</param>
public record BestEmulatorGame(long Id, string Name, int? Year, string[] Platforms, string? SearchedAt);

/// <summary>La búsqueda de un juego en su ficha (#375).</summary>
/// <param name="Task">La tarea que la busca ahora (en cola, corriendo o en pausa); null si no hay.</param>
/// <param name="SearchedAt">Cuándo se buscó por última vez; null si nunca.</param>
public record BestEmulatorStatus(long? Task, string? SearchedAt);

/// <param name="RecipeId">La receta elegida a mano.</param>
public record EmulatorPickRequest(string RecipeId);

/// <summary>
/// «Buscar el mejor emulador» (#373, #369): Sonnet con búsqueda web recomienda un emulador por cada consola donde existe un juego,
/// con su fuente y su motivo. Queda solo local (no se envía al catálogo): una fila de `emulator_pick` por juego y consola.
/// </summary>
public static class BestEmulator
{
    public const string Kind = "claude.bestEmulator", Prompt = "mejor-emulador";

    /// <param name="Recipe">El id de la receta; vacío si no tiene.</param>
    /// <param name="Repo">El repo cuando no hay receta; vacío si la hay.</param>
    public record Pick(string Console, string Recipe, string Repo, string Source, string Reason);
    public record Reply(List<Pick> Picks);

    /// <summary>Encola la búsqueda de un juego (la que ya espera, si hay una); null si el juego no existe.</summary>
    public static long? Enqueue(Database db, long gameId)
    {
        using var c = db.Open();
        if (c.ExecuteScalar<long>("SELECT count(*) FROM game WHERE id = @gameId", new { gameId }) == 0) return null;
        return c.ExecuteScalar<long?>("SELECT id FROM task WHERE kind = @Kind AND status IN ('queued', 'paused', 'running') AND data = @data",
            new { Kind, data = gameId.ToString() }) ?? TaskQueue.Enqueue(db, Kind, gameId.ToString());
    }

    /// <summary>Encola una búsqueda por cada juego elegido (#374); devuelve las tareas. Nunca recorre el catálogo: solo lo que llega.</summary>
    public static List<long> Enqueue(Database db, IEnumerable<long> gameIds) =>
        gameIds.Distinct().Select(id => Enqueue(db, id)).OfType<long>().ToList();

    /// <summary>Los juegos de una saga que se pueden buscar (los que salen en alguna consola), con la fecha de su última búsqueda.</summary>
    public static List<BestEmulatorGame> SagaGames(Database db, long sagaId)
    {
        var games = Catalog.CatalogQueries.SagaGames(db, sagaId, 0, int.MaxValue).Items.Where(g => g.Platforms.Length > 0).ToList();
        using var c = db.Open();
        var searched = c.Query<(long GameId, string SearchedAt)>("SELECT game_id, searched_at FROM emulator_search").ToDictionary(r => r.GameId, r => r.SearchedAt);
        return games.Select(g => new BestEmulatorGame(g.Id, g.Name, g.Year, g.Platforms, searched.GetValueOrDefault(g.Id))).ToList();
    }

    public static List<EmulatorPick> Picks(Database db, long gameId)
    {
        using var c = db.Open();
        return c.Query<EmulatorPick>("""
            SELECT console, recipe_id AS RecipeId, repo, source, reason, origin, searched_at AS SearchedAt
            FROM emulator_pick WHERE game_id = @gameId ORDER BY console
            """, new { gameId }).ToList();
    }

    public static BestEmulatorStatus Status(Database db, long gameId)
    {
        using var c = db.Open();
        return new(
            c.ExecuteScalar<long?>("SELECT id FROM task WHERE kind = @Kind AND status IN ('queued', 'paused', 'running') AND data = @data",
                new { Kind, data = gameId.ToString() }),
            c.ExecuteScalar<string?>("SELECT searched_at FROM emulator_search WHERE game_id = @gameId", new { gameId }));
    }

    /// <summary>
    /// «Elegir a mano» (#375): la receta tiene que ser un emulador que corra esa consola, y el juego tiene que salir en ella.
    /// Reemplaza lo que hubiera para el juego y la consola (de Claude o a mano). false si algo no calza.
    /// </summary>
    public static bool Choose(Database db, long gameId, string console, string recipeId)
    {
        if (Recipes.Find(recipeId) is not { Kind: "emulator" } recipe || !recipe.Consoles.Contains(console)) return false;
        using var c = db.Open();
        if (c.ExecuteScalar<long>("SELECT count(*) FROM version WHERE game_id = @gameId AND platform = @console", new { gameId, console }) == 0) return false;
        c.Execute("""
            INSERT INTO emulator_pick (game_id, console, recipe_id, origin, searched_at) VALUES (@gameId, @console, @recipeId, 'manual', @now)
            ON CONFLICT (game_id, console) DO UPDATE SET recipe_id = @recipeId, repo = NULL, source = NULL, reason = NULL, origin = 'manual', searched_at = @now
            """, new { gameId, console, recipeId, now = DateTime.UtcNow.ToString("O") });
        return true;
    }

    /// <summary>«Volver al del catálogo» (#375): borra lo local del juego en esa consola, sea de Claude o elegido a mano.</summary>
    public static void Forget(Database db, long gameId, string console)
    {
        using var c = db.Open();
        c.Execute("DELETE FROM emulator_pick WHERE game_id = @gameId AND console = @console", new { gameId, console });
    }

    public static async Task Run(TaskRun run, CancellationToken ct)
    {
        var gameId = long.Parse(run.Data!);
        (string Name, string? OriginalName, long? Year) game;
        List<string> consoles;
        using (var c = run.Db.Open())
        {
            game = c.QuerySingle<(string, string?, long?)>("SELECT name_es, original_name, year FROM game WHERE id = @gameId", new { gameId });
            consoles = c.Query<string>("SELECT DISTINCT platform FROM version WHERE game_id = @gameId ORDER BY platform", new { gameId }).ToList();
        }
        var recipes = Recipes.All.Where(r => r.Kind == "emulator" && r.Consoles.Intersect(consoles).Any()).Select(r => new
        {
            r.Id,
            r.Name,
            repo = r.Updates?.Repo is { } repo ? $"https://{r.Updates.Host ?? "github.com"}/{repo}" : null,
            consoles = r.Consoles,
            r.ForkOf,
        });
        run.Report(0, ApiError.Of("tasks.claude.bestEmulator", ("game", game.Name)));
        var reply = await ClaudeRunner.Run<Reply>(run.Db, run.Id, Prompt, ClaudeRunner.Prompt(Prompt),
            new { game = new { id = gameId, name = game.Name, game.OriginalName, game.Year }, consoles, recipes }, ct, "sonnet");
        var saved = Save(run.Db, gameId, consoles, reply.Value.Picks);
        run.Log(ApiError.Of("tasks.log.bestEmulator", ("count", saved.ToString()), ("consoles", consoles.Count.ToString())));
    }

    /// <summary>
    /// Reemplaza lo que dejó Claude para el juego por lo nuevo, sin tocar lo elegido a mano. Descarta lo que no sirve: consolas que
    /// no son del juego, recetas que no existen o no corren esa consola, y lo que viene sin fuente o sin receta ni repo.
    /// </summary>
    public static int Save(Database db, long gameId, IReadOnlyCollection<string> consoles, IEnumerable<Pick> picks)
    {
        var valid = picks
            .Where(p => consoles.Contains(p.Console) && p.Source.Trim() != ""
                && (p.Recipe.Trim() != "" ? Recipes.Find(p.Recipe) is { } r && r.Consoles.Contains(p.Console) : p.Repo.Trim() != ""))
            .DistinctBy(p => p.Console).ToList();
        var now = DateTime.UtcNow.ToString("O");
        using var c = db.Open();
        using var t = c.BeginTransaction();
        c.Execute("DELETE FROM emulator_pick WHERE game_id = @gameId AND origin = 'claude'", new { gameId }, t);
        c.Execute("INSERT INTO emulator_search (game_id, searched_at) VALUES (@gameId, @now) ON CONFLICT (game_id) DO UPDATE SET searched_at = @now",
            new { gameId, now }, t);
        var saved = valid.Sum(p => c.Execute("""
            INSERT INTO emulator_pick (game_id, console, recipe_id, repo, source, reason, origin, searched_at)
            VALUES (@gameId, @Console, @recipe, @repo, @Source, @Reason, 'claude', @now) ON CONFLICT DO NOTHING
            """, new
        {
            gameId,
            p.Console,
            recipe = p.Recipe.Trim() is { Length: > 0 } id ? id : null,
            repo = p.Recipe.Trim() == "" ? p.Repo.Trim() : null,
            p.Source,
            p.Reason,
            now,
        }, t));
        t.Commit();
        return saved;
    }
}
