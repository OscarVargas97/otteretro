using System.ComponentModel;
using System.Diagnostics;
using Dapper;
using Otteretro.Libraries;
using Otteretro.Play;

namespace Otteretro.Tests;

/// <summary>#399: `otteretro jugar` prepara, lanza (aquí, un lanzador simulado), espera y anota la sesión; si falla, lo dice.</summary>
public class PlayTests
{
    /// <summary>Switch con Eden como emulador base y la ROM en el disco: sin perfil propio, juega con el perfil por defecto.</summary>
    static Fixture Ready(out string rom)
    {
        var f = ProfileSettingsTests.Game("switch", "0100F2C0115B6000", out rom);
        ProfileSettingsTests.Install(f, "eden", "emulator", "Eden", "eden.exe", "0.2.1");
        Profiles.SetBase(f.Db, "switch", "eden");
        return f;
    }

    static List<string> Shown(Action play)
    {
        var shown = new List<string>();
        var (launch, show) = (GameLauncher.Launch, GameLauncher.ShowError);
        GameLauncher.ShowError = shown.Add;
        try { play(); }
        finally { (GameLauncher.Launch, GameLauncher.ShowError) = (launch, show); }
        return shown;
    }

    [Fact]
    public async Task PlayingLaunchesInTheGameWaitsAndRecordsTheSession()
    {
        await using var f = Ready(out var rom);
        (string Exe, List<string> Args)? launched = null;
        var shown = Shown(() =>
        {
            GameLauncher.Launch = (exe, args, _) =>
            {
                launched = (exe, [.. args]);
                // Mientras se juega, la sesión está abierta: la ficha dice «Jugando…» (#400).
                var open = Assert.Single(GameLauncher.Recent(f.Db, 10));
                Assert.True(open.Playing);
                Assert.Null(open.EndedAt);
                Thread.Sleep(1100);
            };
            Assert.Equal(0, GameLauncher.Run(f.Db, f.Config, 20));
        });
        var closed = Assert.Single(GameLauncher.Recent(f.Db, 10));
        Assert.False(closed.Playing);
        Assert.Equal("switch", closed.Platform);

        Assert.Empty(shown); // un lanzamiento normal no muestra ventana
        Assert.EndsWith("eden.exe", launched!.Value.Exe);
        Assert.Equal(["-g", rom], launched.Value.Args);
        using var c = f.Db.Open();
        var session = c.QuerySingle<(long VersionId, string MachineId, string Launcher, string StartedAt, string EndedAt, long Duration)>(
            "SELECT version_id, machine_id, launcher, started_at, ended_at, duration FROM play_session");
        Assert.Equal((20, f.Config.MachineId, "otteretro"), (session.VersionId, session.MachineId, session.Launcher));
        Assert.True(session.Duration >= 1);
        Assert.True(DateTime.Parse(session.EndedAt) > DateTime.Parse(session.StartedAt));
    }

    [Fact]
    public async Task AFailureShowsTheReasonAndLeavesNoSession()
    {
        await using var f = ProfileSettingsTests.Game("switch", "0100F2C0115B6000", out _);
        f.Config.Language = "es";
        var shown = Shown(() => Assert.Equal(1, GameLauncher.Run(f.Db, f.Config, 20)));
        Assert.Equal([GameLauncher.Text(Api.ApiError.Of("play.noEmulator"), "es")], shown);
        Assert.StartsWith("Ese juego no tiene con qué jugarse", shown[0]);

        ProfileSettingsTests.Install(f, "eden", "emulator", "Eden", "eden.exe", "0.2.1");
        Profiles.SetBase(f.Db, "switch", "eden");
        f.Config.Language = "en";
        shown = Shown(() =>
        {
            GameLauncher.Launch = (_, _, _) => throw new Win32Exception("no arranca");
            Assert.Equal(1, GameLauncher.Run(f.Db, f.Config, 20));
        });
        Assert.Equal(["Couldn't open the game: no arranca"], shown);

        // #440: si falta el ejecutable, dice cuál y dónde, sin intentar lanzarlo.
        var folder = f.Tmp[@"E\Emuladores\Eden\0.2.1"];
        File.Delete(Path.Combine(folder, "eden.exe"));
        f.Config.Language = "es";
        shown = Shown(() =>
        {
            GameLauncher.Launch = (_, _, _) => Assert.Fail("no debería lanzar");
            Assert.Equal(1, GameLauncher.Run(f.Db, f.Config, 20));
        });
        Assert.Equal([$"No está «eden.exe» en {folder}: la instalación quedó incompleta o la receta no coincide con lo que se bajó. Reinstálalo desde Herramientas."], shown);
        using var c = f.Db.Open();
        Assert.Equal(0, c.ExecuteScalar<long>("SELECT count(*) FROM play_session"));
    }

    static string? Launched(Fixture f, long version)
    {
        string? exe = null;
        var shown = Shown(() =>
        {
            GameLauncher.Launch = (e, _, _) => exe = e;
            Assert.Equal(0, GameLauncher.Run(f.Db, f.Config, version));
        });
        Assert.Empty(shown);
        return exe;
    }

    /// <summary>#400: Jugar abre lo que nombra la ficha, la opción elegida en «Cómo jugar», no el perfil efectivo.</summary>
    [Fact]
    public async Task PlayingLaunchesTheChosenEmulator()
    {
        await using var f = Ready(out _);
        // El perfil efectivo es el de Citron (el base), que no abre en el juego; se elige Eden a mano.
        ProfileSettingsTests.Install(f, "citron", "emulator", "Citron", "citron.exe", "2026.01.01");
        Profiles.SetBase(f.Db, "switch", "citron");
        Assert.True(PlayChoice.SetManual(f.Db, 10, 20, "emulator:eden"));
        Assert.EndsWith("eden.exe", Launched(f, 20));
    }

    [Fact]
    public async Task PlayingLaunchesTheChosenPort()
    {
        await using var f = ProfileSettingsTests.Game("gc", "GZ2E01", out _);
        ProfileSettingsTests.Install(f, "dolphin", "emulator", "Dolphin", "Dolphin.exe", "2512");
        Profiles.SetBase(f.Db, "gc", "dolphin");
        ProfileSettingsTests.Install(f, "dusklight", "port", "Dusklight", "dusklight.exe", "1.0");
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO port (id, console, game, project, kind, status, link, source, added, game_id) VALUES
                  (1, 'gc', 'Twilight Princess', 'Dusklight', 'decomp', 'complete', 'https://github.com/TwilitRealm/dusklight', 'recompendium', '2026-01-01', 10)
                """);
        // El port al 100 % gana por la regla, aunque el perfil efectivo sea el de Dolphin.
        Assert.Equal("port:1", PlayChoice.OfGame(f.Db, 10)[0].Chosen);
        Assert.EndsWith("dusklight.exe", Launched(f, 20));
        Assert.True(PlayChoice.SetManual(f.Db, 10, 20, "emulator:dolphin"));
        Assert.EndsWith("Dolphin.exe", Launched(f, 20));
    }

    /// <summary>#400 paso 2: lo que tienes en Steam se abre con `steam://rungameid/` y, si el juego arranca, se anota la sesión.</summary>
    [Fact]
    public async Task SteamGamesOpenThroughSteamAndRecordTheSessionWhenSeen()
    {
        await using var f = new Fixture();
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO game (id, name_es) VALUES (10, 'Hollow Knight');
                INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'pc');
                INSERT INTO source (kind, store, store_id, name, version_id) VALUES ('store', 'steam', '367520', 'Hollow Knight', 20);
                """);
        var (open, find) = (GameLauncher.OpenUrl, GameLauncher.FindSteamGame);
        var opened = new List<string>();
        try
        {
            GameLauncher.OpenUrl = url => { opened.Add(url); return true; };
            // No arrancó en los primeros segundos: sin sesión.
            GameLauncher.FindSteamGame = _ => null;
            Assert.Empty(Shown(() => Assert.Equal(0, GameLauncher.Run(f.Db, f.Config, 20))));
            Assert.Empty(GameLauncher.Recent(f.Db, 10));

            string? watched = null;
            GameLauncher.FindSteamGame = app =>
            {
                watched = app;
                return () => Assert.True(Assert.Single(GameLauncher.Recent(f.Db, 10)).Playing);
            };
            Assert.Empty(Shown(() => Assert.Equal(0, GameLauncher.Run(f.Db, f.Config, 20))));
            Assert.Equal("367520", watched);
            Assert.Equal(["steam://rungameid/367520", "steam://rungameid/367520"], opened);
            var session = Assert.Single(GameLauncher.Recent(f.Db, 10));
            Assert.Equal(("steam", false), (session.Launcher, session.Playing));
            Assert.NotNull(session.EndedAt);

            // Sin Steam: lo dice.
            GameLauncher.OpenUrl = _ => false;
            f.Config.Language = "es";
            Assert.Equal([GameLauncher.Text(Api.ApiError.Of("steam.notInstalled"), "es")], Shown(() => Assert.Equal(1, GameLauncher.Run(f.Db, f.Config, 20))));
        }
        finally { (GameLauncher.OpenUrl, GameLauncher.FindSteamGame) = (open, find); }
    }

    [Fact]
    public void ArgumentsAreQuotedAsWindowsReadsThem()
    {
        Assert.Equal("-g", WindowsGame.Quote("-g"));
        Assert.Equal("\"\"", WindowsGame.Quote(""));
        Assert.Equal("\"E:\\Juegos de Switch\\\\\"", WindowsGame.Quote(@"E:\Juegos de Switch\"));
        Assert.Equal("\"a \\\"b\\\"\"", WindowsGame.Quote("a \"b\""));
    }

    static readonly string Cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");

    [Fact]
    public void WaitsForTheLaunchedProcess()
    {
        if (!OperatingSystem.IsWindows()) return;
        var clock = Stopwatch.StartNew();
        WindowsGame.Run(Cmd, ["/c", "ping", "-n", "3", "127.0.0.1", ">nul"], Environment.SystemDirectory);
        Assert.True(clock.Elapsed > TimeSpan.FromSeconds(1.5), $"terminó en {clock.Elapsed}");
    }

    [Fact]
    public void WaitsForWhatALauncherLeavesOpen()
    {
        if (!OperatingSystem.IsWindows()) return;
        WindowsGame.Poll = TimeSpan.FromMilliseconds(200);
        // cmd abre ping aparte y se cierra al instante: se sigue esperando hasta que ping termina.
        var clock = Stopwatch.StartNew();
        WindowsGame.Run(Cmd, ["/c", "start", "", "/b", "ping", "-n", "4", "127.0.0.1", ">nul"], Environment.SystemDirectory);
        Assert.True(clock.Elapsed > TimeSpan.FromSeconds(2.5), $"terminó en {clock.Elapsed}");
    }
}
