using Dapper;
using Otteretro.Libraries;
using Otteretro.Play;

namespace Otteretro.Tests;

/// <summary>#471: un DSiWare se juega con RetroArch y melonDS DS, que lo instala en la NAND; melonDS solo no lo arranca.</summary>
[Collection(nameof(PlayTests))]
public class DsiWareTests
{
    /// <summary>Una cabecera de DS: con DSiWare, unit code con DSi y title id alto 0x00030004, como los lee melonDS.</summary>
    internal static void Nds(string path, bool dsiware)
    {
        var head = new byte[0x1000];
        "KQ9E"u8.CopyTo(head.AsSpan(0x0C));
        if (dsiware) { head[0x12] = 3; head[0x234] = 0x04; head[0x236] = 0x03; }
        File.WriteAllBytes(path, head);
    }

    [Fact]
    public async Task TheHeaderSaysWhetherItIsDsiWare()
    {
        await using var f = new Fixture();
        var (dsi, cart, tiny) = (f.Tmp["a.nds"], f.Tmp["b.nds"], f.Tmp["c.nds"]);
        Nds(dsi, true);
        Nds(cart, false);
        File.WriteAllBytes(tiny, new byte[0x20]);
        Assert.True(GameIds.IsDsiWare(dsi));
        Assert.False(GameIds.IsDsiWare(cart));
        Assert.False(GameIds.IsDsiWare(tiny));
        Assert.False(GameIds.IsDsiWare(f.Tmp["no-existe.nds"]));
    }

    /// <summary>Four Swords: «Cómo jugar» elige melonDS DS (aunque falte instalarlo) y es la única opción; un .nds normal sigue con melonDS.</summary>
    [Fact]
    public async Task DsiWareRecommendsRetroArchAndNotMelonDs()
    {
        await using var f = Game(out var rom, dsiware: true);
        ProfileSettingsTests.Install(f, "melonds", "emulator", "melonDS", "melonDS.exe", "1.1");
        Profiles.SetBase(f.Db, "ds", "melonds");

        var play = PlayChoice.OfGame(f.Db, 10).Single();
        Assert.Equal("emulator:melonds-ds", play.Chosen);
        Assert.False(Assert.Single(play.Options).Installed); // melonDS ni se ofrece

        Nds(rom, dsiware: false);
        Assert.Equal("emulator:melonds", PlayChoice.OfGame(f.Db, 10).Single().Chosen);
    }

    /// <summary>Jugar abre el RetroArch instalado con el core y la ROM, y le deja los archivos de sistema en `system\` sin pisar los suyos.</summary>
    [Fact]
    public async Task PlayingOpensRetroArchWithTheCoreAndCopiesTheSystemFiles()
    {
        await using var f = Game(out var rom, dsiware: true);
        var core = ProfileSettingsTests.Install(f, "melonds-ds", "emulator", "melonDS DS (RetroArch)", "melondsds_libretro.dll", "1.4.0");
        Directory.CreateDirectory(Path.Combine(core, "cores"));
        File.WriteAllText(Path.Combine(core, "cores", "melondsds_libretro.dll"), "");

        // Sin RetroArch no está para jugar: «Instalar» instala RetroArch.
        var option = Assert.Single(PlayChoice.OfGame(f.Db, 10).Single().Options);
        Assert.Equal((false, "retroarch"), (option.Installed, option.Install));
        var shown = Run(f, out _);
        Assert.Contains("RetroArch", Assert.Single(shown));

        var retroarch = ProfileSettingsTests.Install(f, "retroarch", "emulator", "RetroArch", "retroarch.exe", "1.22.2");
        Assert.Null(await SystemFiles.Save(f.Db, "ds", "bios7.bin", new MemoryStream("bios"u8.ToArray()), default));
        Assert.Null(await SystemFiles.Save(f.Db, "ds", "dsi_nand.bin", new MemoryStream("nand"u8.ToArray()), default));
        Assert.True(Assert.Single(PlayChoice.OfGame(f.Db, 10).Single().Options).Installed);
        var system = Path.Combine(retroarch, "system");
        Directory.CreateDirectory(system);
        File.WriteAllText(Path.Combine(system, "dsi_nand.bin"), "la del core");

        Assert.Empty(Run(f, out var launched));
        Assert.Equal(Path.Combine(retroarch, "retroarch.exe"), launched!.Value.Exe);
        Assert.Equal(["-L", $"{core}/cores/melondsds_libretro.dll", rom], launched.Value.Args);
        Assert.Equal("bios", File.ReadAllText(Path.Combine(system, "bios7.bin")));
        Assert.Equal("la del core", File.ReadAllText(Path.Combine(system, "dsi_nand.bin")));
        Assert.Empty(Directory.GetFiles(system, "*.tmp"));
    }

    static Fixture Game(out string rom, bool dsiware)
    {
        var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["E"], "E:");
        rom = Path.Combine(f.Tmp["E"], "juego.nds");
        Nds(rom, dsiware);
        using var c = f.Db.Open();
        c.Execute("""
            INSERT INTO game (id, name_es) VALUES (10, 'Four Swords Anniversary Edition');
            INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'ds');
            INSERT INTO source (kind, library_id, relative_path, console, version_id, component, title_id) VALUES ('file', @disk, 'juego.nds', 'ds', 20, 'BASE', 'KQ9E');
            """, new { disk });
        return f;
    }

    static List<string> Run(Fixture f, out (string Exe, List<string> Args)? launched)
    {
        (string, List<string>)? seen = null;
        var shown = new List<string>();
        var (launch, show) = (GameLauncher.Launch, GameLauncher.ShowError);
        GameLauncher.ShowError = shown.Add;
        GameLauncher.Launch = (exe, args, _) => seen = (exe, [.. args]);
        try { GameLauncher.Run(f.Db, f.Config, 20); }
        finally { (GameLauncher.Launch, GameLauncher.ShowError) = (launch, show); }
        launched = seen;
        return shown;
    }
}
