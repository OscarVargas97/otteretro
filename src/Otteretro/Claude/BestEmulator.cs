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

    public static List<EmulatorPick> Picks(Database db, long gameId)
    {
        using var c = db.Open();
        return c.Query<EmulatorPick>("""
            SELECT console, recipe_id AS RecipeId, repo, source, reason, origin, searched_at AS SearchedAt
            FROM emulator_pick WHERE game_id = @gameId ORDER BY console
            """, new { gameId }).ToList();
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
