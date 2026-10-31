using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Otteretro.App;
using Otteretro.Claude;

namespace Otteretro.Tests;

/// <summary>F2.1 con un `claude` falso: valida, reintenta una vez y falla bien.</summary>
[Collection(nameof(ClaudeRunnerTests))] // Launch es estático: estas pruebas no corren en paralelo entre sí
public class ClaudeRunnerTests : IDisposable
{
    public record Double(int Value, string? Note);

    readonly List<(ProcessStartInfo Start, string Stdin)> calls = [];

    readonly Func<ProcessStartInfo, string, CancellationToken, Task<ProcessOutput>> real = ClaudeRunner.Launch;

    public void Dispose() => ClaudeRunner.Launch = real;

    /// <summary>Un `claude` que responde, en orden, lo que se le pasa.</summary>
    void Fake(params ProcessOutput[] outputs) => ClaudeRunner.Launch = (start, stdin, ct) =>
    {
        calls.Add((start, stdin));
        return Task.FromResult(outputs[calls.Count - 1]);
    };

    static ProcessOutput Reply(object? structured, bool isError = false, string? result = null) =>
        new(0, JsonSerializer.Serialize(new
        {
            type = "result",
            is_error = isError,
            result,
            structured_output = structured,
            usage = new { input_tokens = 10, output_tokens = 5, cache_read_input_tokens = 100, cache_creation_input_tokens = 0, server_tool_use = new { } },
        }), "");

    [Fact]
    public async Task ValidReplyIsReadWithItsUsage()
    {
        Fake(Reply(new { value = 4, note = (string?)null }));

        var r = await ClaudeRunner.Run<Double>("Duplica n.", new { n = 2 }, CancellationToken.None, model: "haiku");

        Assert.Equal(new Double(4, null), r.Value);
        Assert.Equal(new ClaudeUsage(10, 5, 100, 0), r.Usage);
        Assert.Equal(1, r.Attempts);
        var (start, stdin) = Assert.Single(calls);
        var args = start.ArgumentList.ToList();
        Assert.Equal("{\"n\":2}", stdin);
        Assert.Equal("Duplica n.", args[args.IndexOf("-p") + 1]);
        Assert.Equal(ClaudeRunner.Tools, args[args.IndexOf("--tools") + 1]); // solo búsqueda y lectura web
        Assert.Contains("--restricted", args);
        Assert.Equal("haiku", args[args.IndexOf("--model") + 1]);
        var schema = JsonDocument.Parse(args[args.IndexOf("--json-schema") + 1]).RootElement;
        Assert.Equal(["value", "note"], schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("object", schema.GetProperty("type").GetString()); // claude rechaza ["object", "null"]
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public async Task InvalidReplyIsAskedAgainOnce()
    {
        Fake(Reply(new { value = "cuatro", note = "x" }), Reply(new { value = 4, note = "x" }));

        var r = await ClaudeRunner.Run<Double>("p", new { }, CancellationToken.None);

        Assert.Equal(4, r.Value.Value);
        Assert.Equal(2, r.Attempts);
        Assert.Equal(20, r.Usage.InputTokens); // se cuentan los dos intentos
    }

    [Theory]
    [InlineData("""{"value":4,"note":"x","extra":1}""")] // sobra un campo
    [InlineData("""{"value":4}""")]                      // falta uno
    [InlineData("null")]                                 // sin salida estructurada
    public async Task TwoInvalidRepliesFail(string structured)
    {
        var bad = Reply(JsonSerializer.Deserialize<JsonElement>(structured));
        Fake(bad, bad, bad);

        var e = await Assert.ThrowsAsync<TaskException>(() => ClaudeRunner.Run<Double>("p", new { }, CancellationToken.None));

        Assert.Equal("tasks.claude.invalidOutput", e.Error.Code);
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task ClaudeErrorFailsWithoutRetrying()
    {
        Fake(Reply(null, isError: true, result: "algo salió mal"), Reply(new { value = 4, note = "x" }));

        var e = await Assert.ThrowsAsync<TaskException>(() => ClaudeRunner.Run<Double>("p", new { }, CancellationToken.None));

        Assert.Equal("tasks.claude.failed", e.Error.Code);
        Assert.Equal("algo salió mal", e.Error.Params!["detail"]);
        Assert.Single(calls);
    }

    [Fact]
    public async Task CrashWithoutJsonReportsStderr()
    {
        Fake(new ProcessOutput(1, "", "no autenticado\n"));

        var e = await Assert.ThrowsAsync<TaskException>(() => ClaudeRunner.Run<Double>("p", new { }, CancellationToken.None));

        Assert.Equal("no autenticado", e.Error.Params!["detail"]);
    }

    [Fact]
    public async Task MissingClaudeIsReported()
    {
        ClaudeRunner.Launch = (_, _, _) => throw new Win32Exception(2);

        var e = await Assert.ThrowsAsync<TaskException>(() => ClaudeRunner.Run<Double>("p", new { }, CancellationToken.None));

        Assert.Equal("tasks.claude.notInstalled", e.Error.Code);
    }
}
