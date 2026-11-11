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
            GameLauncher.Launch = (exe, args, _) => { launched = (exe, [.. args]); Thread.Sleep(1100); };
            Assert.Equal(0, GameLauncher.Run(f.Db, f.Config, 20));
        });

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
        using var c = f.Db.Open();
        Assert.Equal(0, c.ExecuteScalar<long>("SELECT count(*) FROM play_session"));
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
