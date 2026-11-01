using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#197: la vista previa de un megapack, una versión por juego, sin extras, y la regla de duplicados.</summary>
[Collection("Dats.Handler")]
public class MegapackTests
{
    static readonly string[] Order = Megapack.DefaultRegions;

    [Theory]
    [InlineData("Metroid Fusion (Europe) (En,Fr,De,Es,It)", 0)]
    [InlineData("Metroid Fusion (Spain)", 0)]
    [InlineData("Metroid Fusion (USA) [T+Spa1.0_Traductor]", 0)]
    [InlineData("Metroid Fusion (Europe) (En,Fr,De)", 1)]
    [InlineData("Metroid Fusion (USA)", 2)]
    [InlineData("Metroid Fusion (Japan)", 3)]
    [InlineData("Metroid Fusion", 5)]
    public void RanksSpanishFirst(string name, int rank) => Assert.Equal(rank, Megapack.Rank(name, Order));

    [Theory]
    [InlineData("Juego (USA) (Beta)", true)]
    [InlineData("Juego (USA) (Demo)", true)]
    [InlineData("Juego (Unl)", true)]
    [InlineData("Juego [h1 Algo]", true)]
    [InlineData("Juego [b1]", true)]
    [InlineData("Juego (USA) [T+Spa]", false)]
    [InlineData("Zelda [01006BB00C6F0000][v0]", false)]
    [InlineData("Juego [BASE]", false)]
    public void TellsExtrasApart(string name, bool extra) => Assert.Equal(extra, Megapack.IsExtra(name));

    static void Write(string path, string text = "rom")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public async Task PreviewPicksOneVersionPerGameAndMovesNothing()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO saga (id, name) VALUES (1, 'Metroid');
                INSERT INTO game (id, name_es, saga_id) VALUES (10, 'Metroid Fusion', 1);
                INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'gba');
                """);
        var pack = f.Tmp["pack"];
        Write(Path.Combine(pack, "Metroid Fusion (USA).gba"));
        Write(Path.Combine(pack, "Metroid Fusion (Europe) (En,Fr,De,Es,It).gba"));
        Write(Path.Combine(pack, "Metroid Fusion (Europe) (Beta).gba"));
        Write(Path.Combine(pack, "Otro (Disc 1).gba"));
        Write(Path.Combine(pack, "Otro (Disc 2).gba"));
        Write(Path.Combine(pack, "leeme.txt"));
        using (var zip = ZipFile.Open(Path.Combine(pack, "Comprimido (USA).zip"), ZipArchiveMode.Create))
        using (var w = new StreamWriter(zip.CreateEntry("Comprimido (USA).nds").Open())) w.Write("rom");

        var items = Megapack.Preview(f.Db, pack, Order).Items.ToDictionary(i => i.Name);

        Assert.Equal(6, items.Count);
        var chosen = items["Metroid Fusion (Europe) (En,Fr,De,Es,It)"];
        Assert.True(chosen.Selected);
        Assert.Equal(("gba", 10L, "Metroid"), (chosen.Console!, chosen.GameId!.Value, chosen.Saga!));
        Assert.Equal("region", items["Metroid Fusion (USA)"].Reason);
        Assert.Equal("extra", items["Metroid Fusion (Europe) (Beta)"].Reason);
        Assert.True(items["Otro (Disc 1)"].Selected && items["Otro (Disc 2)"].Selected); // cada disco cuenta
        Assert.Equal("ds", items["Comprimido (USA)"].Console); // la consola sale de adentro del comprimido
        Assert.True(File.Exists(Path.Combine(pack, "Metroid Fusion (USA).gba")));
    }

    [Fact]
    public async Task ImportsOnlyWhatStaysMarked()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        var pack = f.Tmp["pack"];
        Write(Path.Combine(pack, "A (USA).gba"));
        Write(Path.Combine(pack, "B (USA).gba"));
        Dats.Handler = new NoNetwork();
        try { Assert.Equal("done", (await f.Run(Import.Enqueue(f.Db, pack, ["B (USA).gba"]).Task!.Value)).Status); }
        finally { Dats.Handler = null; }
        Assert.Equal(["B (USA).gba"], Directory.GetFiles(pack).Select(Path.GetFileName));
    }

    /// <summary>§9.4: si ya lo tienes se salta, salvo que el nuevo esté verificado y el tuyo no: el tuyo va a la papelera.</summary>
    [Fact]
    public async Task ADuplicateIsSkippedUnlessTheNewOneIsVerified()
    {
        await using var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        var mine = @"Juegos\Game Boy Advance\Metroid Fusion\mio.gba";
        Write(f.Tmp[$@"Otteretro\{mine}"], "viejo");
        var good = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes("bueno")));
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO game (id, name_es) VALUES (10, 'Metroid Fusion');
                INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'gba');
                INSERT INTO source (kind, library_id, relative_path, console, size, version_id, verified) VALUES ('file', @disk, @mine, 'gba', 5, 20, 0);
                INSERT INTO dat_entry (console, source, name, sha1) VALUES ('gba', 'no-intro', 'Metroid Fusion (Europe)', @good);
                """, new { disk, mine, good });
        var pack = f.Tmp["pack"];
        Write(Path.Combine(pack, "Metroid Fusion (USA).gba"), "sin verificar");
        var trashed = new List<string>();
        App.Trash.Fake = path => { trashed.Add(path); File.Delete(path); return true; };
        try
        {
            await Run(f, pack);
            Assert.True(File.Exists(Path.Combine(pack, "Metroid Fusion (USA).gba"))); // sin verificar: se salta
            Assert.Empty(trashed);

            Write(Path.Combine(pack, "Metroid Fusion (Europe).gba"), "bueno");
            File.Delete(Path.Combine(pack, "Metroid Fusion (USA).gba"));
            await Run(f, pack);
            Assert.Equal(["mio.gba"], trashed.Select(Path.GetFileName));
            Assert.False(File.Exists(Path.Combine(pack, "Metroid Fusion (Europe).gba")));
            Assert.Equal("bueno", File.ReadAllText(f.Tmp[@"Otteretro\Juegos\Game Boy Advance\Metroid Fusion\Metroid Fusion (Europe).gba"]));
            using var c = f.Db.Open();
            Assert.Equal(1, c.ExecuteScalar<long>("SELECT count(*) FROM source WHERE version_id = 20"));
            Assert.Equal(1, c.ExecuteScalar<long>("SELECT verified FROM source WHERE version_id = 20"));
        }
        finally { App.Trash.Fake = null; }

        static async Task Run(Fixture f, string pack) => Assert.Equal("done", (await f.Run(Import.Enqueue(f.Db, pack).Task!.Value)).Status);
    }

    sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new HttpRequestException("sin red");
    }
}
