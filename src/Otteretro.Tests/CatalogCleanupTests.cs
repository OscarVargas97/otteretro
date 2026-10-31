using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Otteretro.Claude;

namespace Otteretro.Tests;

/// <summary>F2.7 con un `claude` falso: marca solo lo dudoso, aplica los cambios, no repite lo que no cambió y deshace.</summary>
[Collection(nameof(ClaudeRunnerTests))] // comparten ClaudeRunner.Launch y ClaudeBudget.UtcNow
public class CatalogCleanupTests : IAsyncDisposable
{
    readonly Fixture f = new();
    readonly List<List<string>> calls = [];
    readonly Func<ProcessStartInfo, string, CancellationToken, Task<ProcessOutput>> real = ClaudeRunner.Launch;
    readonly Func<DateTime> realNow = ClaudeBudget.UtcNow;

    public CatalogCleanupTests()
    {
        ClaudeBudget.UtcNow = () => TimeZoneInfo.ConvertTimeToUtc(DateTime.Today.AddHours(3), TimeZoneInfo.Local); // de noche
        using var c = f.Db.Open();
        c.Execute("""
            INSERT INTO saga (id, name) VALUES (1, 'The Legend of Zelda'), (2, 'Half-Life'), (3, 'Okami');
            INSERT INTO game (id, saga_id, name_es, original_name, year) VALUES
              (10, 1, 'The Legend of Zelda: Ocarina of Time', 'The Legend of Zelda: Ocarina of Time', 1998),
              (11, 1, 'The Legend of Zelda: Ocarina of Time 3D', 'The Legend of Zelda: Ocarina of Time 3D', 2011),
              (12, 1, 'Freshly-Picked Tingle''s Rosy Rupeeland', 'Freshly-Picked Tingle''s Rosy Rupeeland', 2006),
              (20, NULL, 'The Legend of Zelda: Echoes of Wisdom', 'The Legend of Zelda: Echoes of Wisdom', 2024),
              (21, NULL, 'Tetris', 'Tetris', 1984),
              (30, 2, 'Half-Life', 'Half-Life', 1998), (31, 2, 'Half-Life 2', 'Half-Life 2', 2004),
              (40, 3, 'Okami', 'Okami', 2006);
            INSERT INTO version (game_id, platform, year, igdb_id) VALUES
              (10, 'n64', 1998, 1), (11, '3ds', 2011, 2), (12, 'ds', 2006, 3), (20, 'switch', 2024, 4),
              (30, 'pc', 1998, 5), (31, 'pc', 2004, 6), (40, 'ps2', 2006, 7);
            INSERT INTO field_value (entity, entity_id, field, value, source) VALUES ('saga', 1, 'name', 'The Legend of Zelda', 'igdb');
            """);
    }

    public async ValueTask DisposeAsync()
    {
        ClaudeRunner.Launch = real;
        ClaudeBudget.UtcNow = realNow;
        await f.DisposeAsync();
    }

    /// <summary>Un `claude` que responde, en orden, estas respuestas.</summary>
    void Fake(params CatalogCleanup.Reply[] replies) => ClaudeRunner.Launch = (start, stdin, ct) =>
    {
        calls.Add([.. start.ArgumentList, stdin]);
        var reply = replies[calls.Count - 1];
        return Task.FromResult(new ProcessOutput(0, JsonSerializer.Serialize(new
        {
            is_error = false,
            structured_output = reply,
            total_cost_usd = 0.01,
            usage = new { input_tokens = 100, output_tokens = 10, cache_read_input_tokens = 0, cache_creation_input_tokens = 0 },
        }, JsonSerializerOptions.Web), ""));
    };

    T Scalar<T>(string sql)
    {
        using var c = f.Db.Open();
        return c.ExecuteScalar<T>(sql)!;
    }

    [Fact]
    public void OnlyDoubtfulSagasAreMarked()
    {
        var cases = CatalogCleanup.Cases(f.Db, null);

        var zelda = Assert.Single(cases); // Half-Life está bien y Okami tiene un solo juego
        Assert.Equal(1, zelda.Id);
        Assert.Equal(["similarNames", "looseCandidates"], zelda.Reasons);
        Assert.Equal("20|The Legend of Zelda: Echoes of Wisdom|2024|switch", Assert.Single(zelda.Loose).Line); // Tetris no
        Assert.Equal(1, CatalogCleanup.Measure(f.Db).Calls);
    }

    [Fact]
    public async Task ChangesAreAppliedNotRepeatedAndUndone()
    {
        Fake(new CatalogCleanup.Reply([
            new("merge", "El 3D es un remake del mismo juego", KeepGameId: 10, MergeGameIds: [11]),
            new("move", "Es de la saga", GameIds: [20], ToSagaId: 1),
            new("move", "Tingle tiene saga propia", GameIds: [12], NewSagaName: "Tingle"),
            new("rename", "Nombre corto", SagaId: 1, Name: "Zelda"),
            new("move", "Inventado", GameIds: [999], ToSagaId: 1), // fuera del lote: se descarta
            new("move", "Otra saga", GameIds: [30], ToSagaId: 1),  // Half-Life no venía en el lote
        ], []));

        var task = await f.Run(CatalogCleanup.Enqueue(f.Db));

        Assert.Equal("done", task.Status);
        Assert.Contains("haiku", calls[0]);
        Assert.Contains("Echoes of Wisdom", calls[0][^1]);
        Assert.Equal(0, Scalar<long>("SELECT count(*) FROM game WHERE id = 11"));
        Assert.Equal(10, Scalar<long>("SELECT game_id FROM version WHERE igdb_id = 2"));
        Assert.Equal(1, Scalar<long>("SELECT saga_id FROM game WHERE id = 20"));
        Assert.Equal("Tingle", Scalar<string>("SELECT s.name FROM game g JOIN saga s ON s.id = g.saga_id WHERE g.id = 12"));
        Assert.Equal("Zelda", Scalar<string>("SELECT name FROM saga WHERE id = 1"));
        Assert.Equal(2, Scalar<long>("SELECT saga_id FROM game WHERE id = 30"));
        Assert.Equal(4, Scalar<long>("SELECT count(*) FROM learning WHERE kind = 'rule' AND undone = 0"));

        // Sin cambios en los datos, otra pasada no llama a Claude.
        Assert.Equal("done", (await f.Run(CatalogCleanup.Enqueue(f.Db))).Status);
        Assert.Single(calls);

        foreach (var id in Enumerable.Range(1, 4).Reverse()) Assert.True(CatalogCleanup.Undo(f.Db, id));
        Assert.False(CatalogCleanup.Undo(f.Db, 1));

        Assert.Equal("The Legend of Zelda: Ocarina of Time 3D", Scalar<string>("SELECT name_es FROM game WHERE id = 11"));
        Assert.Equal(11, Scalar<long>("SELECT game_id FROM version WHERE igdb_id = 2"));
        Assert.Equal(0, Scalar<long>("SELECT count(*) FROM game WHERE id = 20 AND saga_id IS NOT NULL"));
        Assert.Equal(1, Scalar<long>("SELECT saga_id FROM game WHERE id = 12"));
        Assert.Equal(0, Scalar<long>("SELECT count(*) FROM saga WHERE name = 'Tingle'"));
        Assert.Equal("The Legend of Zelda", Scalar<string>("SELECT name FROM saga WHERE id = 1"));
    }

    [Fact]
    public async Task UnsureSagasGetASecondLookWithSonnet()
    {
        Fake(new CatalogCleanup.Reply([], [1]), new CatalogCleanup.Reply([], []));

        Assert.Equal("done", (await f.Run(CatalogCleanup.Enqueue(f.Db))).Status);

        Assert.Equal(2, calls.Count);
        Assert.Contains("haiku", calls[0]);
        Assert.Contains("sonnet", calls[1]);
    }
}
