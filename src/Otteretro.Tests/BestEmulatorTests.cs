using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Otteretro.Claude;

namespace Otteretro.Tests;

/// <summary>#373 con un `claude` falso: una recomendación por consola, sin lo que no existe, y lo elegido a mano no se toca.</summary>
[Collection(nameof(ClaudeRunnerTests))] // comparten ClaudeRunner.Launch
public class BestEmulatorTests : IAsyncDisposable
{
    readonly Fixture f = new();
    readonly List<string> inputs = [];
    readonly Func<ProcessStartInfo, string, CancellationToken, Task<ProcessOutput>> real = ClaudeRunner.Launch;
    List<BestEmulator.Pick> reply = [];

    public BestEmulatorTests()
    {
        ClaudeRunner.Launch = (start, stdin, ct) =>
        {
            inputs.Add(stdin);
            return Task.FromResult(new ProcessOutput(0, JsonSerializer.Serialize(new
            {
                is_error = false,
                structured_output = new BestEmulator.Reply(reply),
                total_cost_usd = 0.01,
                usage = new { input_tokens = 100, output_tokens = 10, cache_read_input_tokens = 0, cache_creation_input_tokens = 0 },
            }, JsonSerializerOptions.Web), ""));
        };
        using var c = f.Db.Open();
        c.Execute("""
            INSERT INTO game (id, name_es, original_name, year) VALUES (8, 'The Legend of Zelda: Twilight Princess', NULL, 2006);
            INSERT INTO version (id, game_id, platform) VALUES (18, 8, 'gc'), (19, 8, 'wii'), (20, 8, 'switch');
            """);
    }

    public async ValueTask DisposeAsync()
    {
        ClaudeRunner.Launch = real;
        await f.DisposeAsync();
    }

    [Fact]
    public async Task KeepsOnePickPerConsoleAndDropsWhatDoesNotExist()
    {
        // Una elección a mano en Wii: la búsqueda no la toca.
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO emulator_pick (game_id, console, recipe_id, origin, searched_at) VALUES (8, 'wii', 'dolphin', 'manual', '2026-01-01')");
        reply =
        [
            new("gc", "dolphin", "", "https://wiki.dolphin-emu.org/", "Corre perfecto."),
            new("gc", "pcsx2", "", "https://x", "Repetida: se queda la primera."),
            new("wii", "dolphin", "", "https://x", "Ya elegiste a mano."),
            new("switch", "no-existe", "", "https://x", "Receta inventada."),
            new("ps2", "pcsx2", "", "https://x", "Consola que no es del juego."),
        ];
        var task = BestEmulator.Enqueue(f.Db, 8)!.Value;
        Assert.Equal(task, BestEmulator.Enqueue(f.Db, 8)); // la que ya espera
        Assert.Null(BestEmulator.Enqueue(f.Db, 999));
        Assert.Equal("done", (await f.Run(task)).Status);

        var input = JsonDocument.Parse(Assert.Single(inputs)).RootElement;
        Assert.Equal(["gc", "switch", "wii"], input.GetProperty("consoles").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains(input.GetProperty("recipes").EnumerateArray(), r => r.GetProperty("id").GetString() == "dolphin");

        var picks = BestEmulator.Picks(f.Db, 8);
        Assert.Equal([("gc", "dolphin", "claude"), ("wii", "dolphin", "manual")], picks.Select(p => (p.Console, p.RecipeId!, p.Origin)));
        Assert.Equal("Corre perfecto.", picks[0].Reason);
        Assert.Equal("https://wiki.dolphin-emu.org/", picks[0].Source);

        // Una búsqueda nueva reemplaza lo de Claude: un repo sin receta, y GameCube sin nada esta vez.
        reply = [new("switch", "", "https://github.com/alguien/fork", "https://x", "Un fork que arregla este juego.")];
        Assert.Equal("done", (await f.Run(BestEmulator.Enqueue(f.Db, 8)!.Value)).Status);
        picks = BestEmulator.Picks(f.Db, 8);
        Assert.Equal([("switch", null, "https://github.com/alguien/fork", "claude"), ("wii", "dolphin", null, "manual")],
            picks.Select(p => (p.Console, p.RecipeId, p.Repo, p.Origin)));
    }
}
