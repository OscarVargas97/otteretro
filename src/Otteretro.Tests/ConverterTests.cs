using System.Security.Cryptography;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#196 con una herramienta falsa (los parámetros reales se comprobaron con DolphinTool): el original se borra solo si la copia se verifica.</summary>
[Collection("Dats.Handler")]
public class ConverterTests : IAsyncDisposable
{
    readonly Fixture f = new();
    readonly string bundled;
    readonly Func<string, string[], CancellationToken, Task<(int, string)>> real = Converter.Launch;
    readonly string realFolder = Converter.BundledFolder;
    readonly string disk;
    string? wrongHash;
    readonly List<string[]> calls = [];

    public ConverterTests()
    {
        bundled = Directory.CreateDirectory(f.Tmp["herramientas"]).FullName;
        File.WriteAllText(Path.Combine(bundled, "DolphinTool.exe"), "");
        File.WriteAllText(Path.Combine(bundled, "chdman.exe"), "");
        Converter.BundledFolder = bundled;
        Converter.Launch = (_, args, _) =>
        {
            calls.Add(args);
            string At(string flag) => args[Array.IndexOf(args, flag) + 1];
            if (args[0] is "convert" or "createcd" or "createdvd")
            {
                File.Copy(At("-i"), At("-o"));
                return Task.FromResult((0, ""));
            }
            // Un RVZ sano da el mismo SHA-1 que su ISO, que acá es el mismo contenido.
            var hash = wrongHash ?? Convert.ToHexStringLower(SHA1.HashData(File.ReadAllBytes(At("-i"))));
            return Task.FromResult((0, $"{hash}\n"));
        };
        disk = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:").Id!;
    }

    public async ValueTask DisposeAsync()
    {
        Converter.Launch = real;
        Converter.BundledFolder = realFolder;
        await f.DisposeAsync();
    }

    long Row(string relative, string console, string? sha1 = null, bool verified = false)
    {
        var full = f.Tmp[$@"Otteretro\{relative}"];
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        if (!File.Exists(full)) File.WriteAllText(full, "disco");
        using var c = f.Db.Open();
        return c.ExecuteScalar<long>("""
            INSERT INTO source (kind, library_id, relative_path, console, size, sha1, verified) VALUES ('file', @disk, @relative, @console, 5, @sha1, @verified) RETURNING id
            """, new { disk, relative, console, sha1, verified });
    }

    [Fact]
    public async Task AnIsoBecomesRvzAndStaysVerified()
    {
        var sha1 = Convert.ToHexString(SHA1.HashData("disco"u8));
        Row(@"Juegos\GameCube\TP\tp.iso", "gc", sha1, true);
        var install = Path.Combine(f.Session.Config.DataFolder!, @"Herramientas\Ports\GameCube\Dusklight\1.0");
        Directory.CreateDirectory(Path.Combine(install, "Datos"));
        File.WriteAllText(Path.Combine(install, "dusklight.exe"), "");
        var iso = f.Tmp[@"Otteretro\Juegos\GameCube\TP\tp.iso"];
        File.WriteAllText(Path.Combine(install, @"Datos\config.json"), $$"""{ "backend.isoPath": {{System.Text.Json.JsonSerializer.Serialize(iso)}} }""");

        Assert.Equal("done", (await f.Run(Converter.Enqueue(f.Db, "gc").Task!.Value)).Status);

        var rvz = f.Tmp[@"Otteretro\Juegos\GameCube\TP\tp.rvz"];
        Assert.True(File.Exists(rvz));
        Assert.False(File.Exists(iso));
        using var c = f.Db.Open();
        Assert.Equal((@"Juegos\GameCube\TP\tp.rvz", sha1, true), c.QuerySingle<(string, string, bool)>("SELECT relative_path, sha1, verified FROM source"));
        Assert.Contains("tp.rvz", File.ReadAllText(Path.Combine(install, @"Datos\config.json"))); // Dusklight apunta al archivo nuevo
        Assert.Equal(["convert", "verify"], calls.Select(x => x[0]));
    }

    [Fact]
    public async Task IfTheCopyDoesNotVerifyTheOriginalStays()
    {
        Row(@"Juegos\GameCube\TP\tp.iso", "gc");
        wrongHash = new string('0', 40);
        Assert.Equal("done", (await f.Run(Converter.Enqueue(f.Db, "gc").Task!.Value)).Status);

        Assert.Equal("disco", File.ReadAllText(f.Tmp[@"Otteretro\Juegos\GameCube\TP\tp.iso"]));
        Assert.Equal(["tp.iso"], Directory.GetFiles(f.Tmp[@"Otteretro\Juegos\GameCube\TP"]).Select(Path.GetFileName));
        using var c = f.Db.Open();
        Assert.Equal(@"Juegos\GameCube\TP\tp.iso", c.ExecuteScalar<string>("SELECT relative_path FROM source"));
    }

    [Fact]
    public async Task ACueBecomesChdWithItsBins()
    {
        var bin = f.Tmp[@"Otteretro\Juegos\PlayStation\Crash\Crash.bin"];
        Directory.CreateDirectory(Path.GetDirectoryName(bin)!);
        File.WriteAllText(bin, "datos");
        File.WriteAllText(f.Tmp[@"Otteretro\Juegos\PlayStation\Crash\Crash.cue"], "FILE \"Crash.bin\" BINARY\n");
        Row(@"Juegos\PlayStation\Crash\Crash.cue", "ps1");

        Assert.Equal("done", (await f.Run(Converter.Enqueue(f.Db, "ps1").Task!.Value)).Status);

        Assert.Equal(["Crash.chd"], Directory.GetFiles(f.Tmp[@"Otteretro\Juegos\PlayStation\Crash"]).Select(Path.GetFileName));
        Assert.Equal(["createcd", "verify"], calls.Select(x => x[0]));
    }

    [Fact]
    public async Task WithoutTheToolNothingChanges()
    {
        Converter.BundledFolder = f.Tmp["no-existe"];
        Row(@"Juegos\GameCube\TP\tp.iso", "gc");
        Assert.Equal("done", (await f.Run(Converter.Enqueue(f.Db, "gc").Task!.Value)).Status);
        Assert.True(File.Exists(f.Tmp[@"Otteretro\Juegos\GameCube\TP\tp.iso"]));
        Assert.Empty(calls);
    }

    [Fact]
    public async Task OnlyConvertsWhatHasALightFormat()
    {
        Assert.Equal(".rvz", Converter.TargetOf("wii", "x.wbfs"));
        Assert.Equal(".chd", Converter.TargetOf("ps2", "x.iso"));
        Assert.Null(Converter.TargetOf("gc", "x.rvz"));
        Assert.Null(Converter.TargetOf("psp", "x.iso"));
        Assert.Equal("convert.notConvertible", Converter.Enqueue(f.Db, "switch").Error?.Code);
    }

    /// <summary>La receta de un port manda (§9.7): Dusklight acepta RVZ, pero no CHD.</summary>
    [Fact]
    public async Task ThePortDecidesTheFormat()
    {
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO game (id, name_es) VALUES (8, 'The Legend of Zelda: Twilight Princess'); INSERT INTO version (id, game_id, platform) VALUES (18, 8, 'gc');");
        Assert.False(Converter.PortForbids(f.Db, 18, ".rvz"));
        Assert.True(Converter.PortForbids(f.Db, 18, ".chd"));
        Assert.False(Converter.PortForbids(f.Db, null, ".chd"));
    }
}
