using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#469 con un hactool falso (los parámetros reales se comprobaron a mano con Skyward Sword HD 1.0.1): la base con su última
/// actualización queda en exefs\ y romfs\ al lado de los NSP, y las llaves no quedan en disco.</summary>
[Collection("Dats.Handler")]
public class SwitchExtractTests : IAsyncDisposable
{
    readonly Fixture f = new();
    readonly string realFolder = Converter.BundledFolder;
    readonly Func<string, string[], string, CancellationToken, Task<int>> real = SwitchExtract.Launch;
    readonly string disk;
    readonly string game;
    readonly List<string[]> calls = [];
    string? titleKeys;
    int failAt = -1;

    public SwitchExtractTests()
    {
        var tools = Directory.CreateDirectory(f.Tmp["herramientas"]).FullName;
        File.WriteAllText(Path.Combine(tools, "hactool.exe"), "");
        Converter.BundledFolder = tools;
        SwitchExtract.Launch = (_, args, home, _) =>
        {
            calls.Add(args);
            if (calls.Count - 1 == failAt) return Task.FromResult(1);
            string? At(string flag) => args.FirstOrDefault(a => a.StartsWith($"--{flag}="))?[(flag.Length + 3)..];
            if (At("outdir") is { } dir)
            {
                // El ticket: rights id en 0x2A0 y llave en 0x180; el del update termina en 800.
                Directory.CreateDirectory(dir);
                var tik = new byte[0x2C0];
                var update = args[^1].Contains("UPD");
                Convert.FromHexString(update ? "01002DA013484800" + "0000000000000000" : "01002DA013484000" + "0000000000000000").CopyTo(tik, 0x2A0);
                Array.Fill(tik, update ? (byte)0xBB : (byte)0xAA, 0x180, 16);
                File.WriteAllBytes(Path.Combine(dir, "x.tik"), tik);
                File.WriteAllText(Path.Combine(dir, "control.nca"), "c");
                File.WriteAllText(Path.Combine(dir, update ? "upd.nca" : "base.nca"), "programa grande");
            }
            else
            {
                titleKeys = File.ReadAllText(Path.Combine(home, ".switch", "title.keys"));
                Directory.CreateDirectory(At("exefsdir")!);
                File.WriteAllText(Path.Combine(At("exefsdir")!, "main"), "");
                Directory.CreateDirectory(At("romfsdir")!);
                File.WriteAllText(Path.Combine(At("romfsdir")!, "data.arc"), "");
            }
            return Task.FromResult(0);
        };
        disk = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:").Id!;
        game = Directory.CreateDirectory(f.Tmp[@"Otteretro\Juegos\Switch\Skyward Sword"]).FullName;
        Directory.CreateDirectory(f.Tmp[@"Otteretro\Sistema\Switch\prod.keys\2026-11-14-abc"]);
        File.WriteAllText(f.Tmp[@"Otteretro\Sistema\Switch\prod.keys\2026-11-14-abc\prod.keys"],
            $"header_key = {new string('1', 64)}\nraro = {new string('2', 34)}\nkey_area_key_application_00 = {new string('3', 32)}\n");
        using var c = f.Db.Open();
        c.Execute("INSERT INTO game (id, name_es) VALUES (1, 'Skyward Sword'); INSERT INTO version (id, game_id, platform) VALUES (2, 1, 'switch');");
        foreach (var (name, component) in new[] { ("SS [01002DA013484000][v0] BASE.nsp", "BASE"), ("SS [01002DA013484800][v65536] UPD.nsp", "UPD"), ("SS [01002DA013484800][v131072] UPD.nsp", "UPD") })
        {
            File.WriteAllText(Path.Combine(game, name), "nsp");
            c.Execute("INSERT INTO source (kind, library_id, relative_path, console, size, version_id, component) VALUES ('file', @disk, @relative, 'switch', 3, 2, @component)",
                new { disk, relative = $@"Juegos\Switch\Skyward Sword\{name}", component });
        }
    }

    public async ValueTask DisposeAsync()
    {
        SwitchExtract.Launch = real;
        Converter.BundledFolder = realFolder;
        await f.DisposeAsync();
    }

    [Fact]
    public async Task TheBaseWithItsLatestUpdateLandsNextToTheNsps()
    {
        Assert.Equal(game, SwitchExtract.Pending(f.Db, 2));
        Assert.Equal("done", (await f.Run(SwitchExtract.Enqueue(f.Db, 2).Task!.Value)).Status);

        Assert.True(File.Exists(Path.Combine(game, @"exefs\main")));
        Assert.True(File.Exists(Path.Combine(game, @"romfs\data.arc")));
        Assert.Equal(3, Directory.GetFiles(game, "*.nsp").Length); // los NSP no se tocan
        Assert.False(Directory.Exists(Path.Combine(game, ".otteretro-extraer"))); // ni llaves ni NCA en disco
        Assert.Contains("[v131072]", calls[1][^1]); // la última actualización
        Assert.Contains($"--basenca=", string.Join(' ', calls[2]));
        Assert.EndsWith("upd.nca", calls[2][^1]);
        Assert.Equal($"01002da0134840000000000000000000 = {new string('a', 32)}\n01002da0134848000000000000000000 = {new string('b', 32)}",
            titleKeys!.Trim().ReplaceLineEndings("\n"));
        Assert.Null(SwitchExtract.Pending(f.Db, 2));
        Assert.Equal("extract.notNeeded", SwitchExtract.Enqueue(f.Db, 2).Error!.Code);
    }

    [Fact]
    public async Task AFailedRunLeavesNothingBehind()
    {
        failAt = 2;
        Assert.Equal("error", (await f.Run(SwitchExtract.Enqueue(f.Db, 2).Task!.Value)).Status);
        Assert.Equal(["SS [01002DA013484000][v0] BASE.nsp", "SS [01002DA013484800][v131072] UPD.nsp", "SS [01002DA013484800][v65536] UPD.nsp"],
            Directory.EnumerateFileSystemEntries(game).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void WithoutKeysItSaysSo()
    {
        Directory.Delete(f.Tmp[@"Otteretro\Sistema"], true);
        Assert.Equal("extract.noKeys", SwitchExtract.Enqueue(f.Db, 2).Error!.Code);
    }

    [Fact]
    public async Task TheScanKeepsTheNspsOfAConvertedFolder()
    {
        Directory.CreateDirectory(Path.Combine(game, "exefs"));
        File.WriteAllText(Path.Combine(game, @"exefs\main"), "");
        Directory.CreateDirectory(Path.Combine(game, "romfs"));
        await f.Run(Scan.Enqueue(f.Db));
        using var c = f.Db.Open();
        Assert.Equal(0, c.ExecuteScalar<long>("SELECT count(*) FROM source WHERE kind = 'file' AND relative_path NOT LIKE '%.nsp'"));
        Assert.Equal(3, c.ExecuteScalar<long>("SELECT count(*) FROM source WHERE kind = 'file' AND relative_path LIKE '%.nsp'"));
    }
}
