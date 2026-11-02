using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Otteretro.Claude;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#180 con un `claude` falso: lo que el escaneo no reconoce va a Claude; lo que nombra queda en Dudosas y como alias.</summary>
[Collection(nameof(ClaudeRunnerTests))] // comparten ClaudeRunner.Launch y ClaudeBudget.UtcNow
public class IdentifyTests : IAsyncDisposable
{
    readonly Fixture f = new();
    readonly List<string> inputs = [];
    readonly Func<ProcessStartInfo, string, CancellationToken, Task<ProcessOutput>> real = ClaudeRunner.Launch;
    readonly Func<DateTime> realNow = ClaudeBudget.UtcNow;

    public IdentifyTests()
    {
        ClaudeBudget.UtcNow = () => TimeZoneInfo.ConvertTimeToUtc(DateTime.Today.AddHours(3), TimeZoneInfo.Local); // de noche
        ClaudeRunner.Launch = (start, stdin, ct) =>
        {
            inputs.Add(stdin);
            var items = JsonDocument.Parse(stdin).RootElement.GetProperty("items").EnumerateArray()
                .Select(i => (Id: i.GetProperty("id").GetInt64(), File: i.GetProperty("file").GetString()!)).ToList();
            var reply = new Identify.Reply(items.Select(i => new Identify.FileName(i.Id,
                i.File.StartsWith("juego raro") ? "The Legend of Zelda: Twilight Princess"
                : i.File.StartsWith("Sin nombre") ? "Hyrule Warriors: Age of Calamity" : null, "Por el nombre")).ToList());
            return Task.FromResult(new ProcessOutput(0, JsonSerializer.Serialize(new
            {
                is_error = false,
                structured_output = reply,
                total_cost_usd = 0.01,
                usage = new { input_tokens = 100, output_tokens = 10, cache_read_input_tokens = 0, cache_creation_input_tokens = 0 },
            }, JsonSerializerOptions.Web), ""));
        };
        using var c = f.Db.Open();
        c.Execute("""
            INSERT INTO game (id, name_es, original_name) VALUES (1, 'Hyrule Warriors: La era del cataclismo', 'Hyrule Warriors: Age of Calamity'),
                (8, 'The Legend of Zelda: Twilight Princess', NULL);
            INSERT INTO version (id, game_id, platform) VALUES (11, 1, 'switch'), (18, 8, 'gc');
            """);
    }

    public async ValueTask DisposeAsync()
    {
        ClaudeRunner.Launch = real;
        ClaudeBudget.UtcNow = realNow;
        await f.DisposeAsync();
    }

    [Fact]
    public async Task ClaudeIdentifiesWhatTheScanCouldNot()
    {
        Disk.Adopt(f.Db, Directory.CreateDirectory(f.Tmp[@"e\Otteretro"]).FullName, "E:", "disk");
        foreach (var file in new[] { @"GameCube\juego raro.iso", @"GameCube\basura.iso", @"Switch\x\Sin nombre [0100AAAA00000000][v0].nsp",
                     @"Switch\x\Sin nombre [0100AAAA00000800][v65536].nsp" })
            Write(f.Tmp[$@"e\Otteretro\Juegos\{file}"]);

        Assert.Equal("done", (await f.Run(Scan.Enqueue(f.Db))).Status);
        Assert.Equal("done", (await f.Run(Identify.EnqueueIfNeeded(f.Db)!.Value)).Status);

        using (var c = f.Db.Open())
        {
            var rows = c.Query<(string Path, long? Version)>("SELECT relative_path, version_id FROM source ORDER BY relative_path").ToList();
            Assert.Equal([(long?)null, 18, 11, 11], rows.Select(r => r.Version));
            var review = c.Query<(string Kind, long Source)>("SELECT kind, entity_id FROM review_item WHERE entity = 'source' ORDER BY entity_id").ToList();
            Assert.Equal(3, review.Count(r => r.Kind == "doubtful"));
            Assert.Equal(1, review.Count(r => r.Kind == "unidentified"));
            Assert.Equal(2, c.ExecuteScalar<long>("SELECT count(*) FROM learning WHERE kind = 'alias' AND undone = 0"));
        }
        // Un solo lote: el UPD va con su BASE, no se pregunta aparte.
        Assert.Equal(3, JsonDocument.Parse(Assert.Single(inputs)).RootElement.GetProperty("items").GetArrayLength());

        // El alias lo reconoce en el próximo escaneo, sin Claude.
        using (var c = f.Db.Open()) c.Execute("DELETE FROM source; DELETE FROM review_item;");
        Assert.Equal("done", (await f.Run(Scan.Enqueue(f.Db))).Status);
        using (var c = f.Db.Open())
            Assert.Equal([(long?)null, 18, 11, 11], c.Query<long?>("SELECT version_id FROM source ORDER BY relative_path"));
        // Lo de Sin identificar ya se preguntó con los mismos datos: no se vuelve a mandar.
        Assert.Equal("done", (await f.Run(Identify.EnqueueIfNeeded(f.Db)!.Value)).Status);
        Assert.Single(inputs);
        using (var c = f.Db.Open()) // pero vuelve a Sin identificar
            Assert.Equal(1, c.ExecuteScalar<long>("SELECT count(*) FROM review_item WHERE kind = 'unidentified' AND status = 'open'"));
    }

    /// <summary>#181: en Revisar se confirma lo de Claude o se elige el juego; la elección queda como alias y se puede deshacer.</summary>
    [Fact]
    public async Task ReviewConfirmsOrPicksTheGameAndLearnsAnAlias()
    {
        Disk.Adopt(f.Db, Directory.CreateDirectory(f.Tmp[@"e\Otteretro"]).FullName, "E:", "disk");
        foreach (var file in new[] { @"GameCube\juego raro.iso", @"GameCube\basura.iso", @"Switch\x\Sin nombre [0100AAAA00000000][v0].nsp",
                     @"Switch\x\Sin nombre [0100AAAA00000800][v65536].nsp" })
            Write(f.Tmp[$@"e\Otteretro\Juegos\{file}"]);
        Assert.Equal("done", (await f.Run(Scan.Enqueue(f.Db))).Status);
        Assert.Equal("done", (await f.Run(Identify.EnqueueIfNeeded(f.Db)!.Value)).Status);

        var page = ClaudeReview.Page(f.Db);
        Assert.Equal(4, page.Files.Count);
        Assert.Equal(4, ClaudeReview.PendingCount(f.Db));
        var junk = page.Files.Single(x => x.Kind == "unidentified");
        Assert.Equal("review.pickGame", Identify.Resolve(f.Db, junk.Id, null)?.Code);
        Assert.Equal("review.noVersion", Identify.Resolve(f.Db, junk.Id, 1)?.Code); // Hyrule Warriors no sale en GameCube
        Assert.Null(Identify.Resolve(f.Db, junk.Id, 8));
        // Confirmar el BASE de Switch resuelve también su UPD.
        Assert.Null(Identify.Resolve(f.Db, page.Files.First(x => x.Console == "switch").Id, null));

        page = ClaudeReview.Page(f.Db);
        Assert.Equal("juego raro.iso", Path.GetFileName(Assert.Single(page.Files).Path));
        using var c = f.Db.Open();
        Assert.Equal(18, c.ExecuteScalar<long>("SELECT version_id FROM source WHERE relative_path LIKE '%basura%'"));
        var alias = c.QuerySingle<(long Id, string Content)>("SELECT id, content FROM learning WHERE kind = 'alias' AND content LIKE '%basura%'");
        Assert.Contains("\"origin\":\"manual\"", alias.Content);
    }

    static void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, path);
    }
}
