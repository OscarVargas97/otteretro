using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;
using Otteretro.Libraries;

namespace Otteretro.Play;

/// <summary>Una sesión de juego. EndedAt y Duration: null mientras sigue abierta.</summary>
public record PlaySession(long VersionId, string Platform, string Launcher, string StartedAt, string? EndedAt, long? Duration, bool Playing);

/// <summary>
/// `otteretro jugar &lt;id&gt;` (§10, T7, #399): prepara, lanza el emulador o port en el juego, espera a que se cierre todo y anota la
/// sesión en `play_session`. Solo corre mientras juegas. Si algo falla, lo dice en una ventana pequeña; si no, no muestra nada.
/// </summary>
public static class GameLauncher
{
    /// <summary>Lanza y espera a que termine el juego (y lo que abrió); falla con <see cref="Win32Exception"/> si no arranca.</summary>
    public static Action<string, IReadOnlyList<string>, string> Launch { get; set; } = OperatingSystem.IsWindows() ? WindowsGame.Run : WaitLaunched;

    /// <summary>La ventana pequeña con el error ya traducido.</summary>
    public static Action<string> ShowError { get; set; } = OperatingSystem.IsWindows() ? WindowsGame.ShowError : _ => { };

    /// <summary>Juega la versión y devuelve el código de salida: 0 si se jugó, 1 si falló (con la ventana del error).</summary>
    public static int Run(Database db, AppConfig config, long version)
    {
        if (Play(db, config.MachineId, version) is not { } error) return 0;
        Fail(error, config.EffectiveLanguage);
        return 1;
    }

    public static void Fail(ApiError error, string language)
    {
        FileLog.Write(LogLevel.Warning, "Play", $"No se pudo jugar: {error.Code} {error.Params?.GetValueOrDefault("detail")}");
        ShowError(Text(error, language));
    }

    static ApiError? Play(Database db, string machine, long version)
    {
        // shortcut: lanza el perfil efectivo; cuando esté la regla de cómo jugar (#398), la opción que elija.
        if (Profiles.Effective(db, version) is not { } profile) return ApiError.Of("play.noEmulator");
        var (exe, args, inGame, error) = EmulatorSettings.OpenArgs(db, profile.Id, version);
        if (error is not null) return error;
        if (!inGame) return ApiError.Of("play.noGame");

        var swap = profile.GameVersionId is not null && Recipes.Find(profile.EmulatorId)?.PerGame?.Apply == "swap";
        if (swap) Profiles.SwapIn(db, profile.Id);
        var started = DateTime.UtcNow;
        // La sesión queda abierta (sin ended_at) mientras se juega: así la ficha muestra «Jugando…» (#400).
        long session;
        using (var c = db.Open())
            session = c.ExecuteScalar<long>("""
                INSERT INTO play_session (version_id, machine_id, launcher, started_at) VALUES (@version, @machine, 'otteretro', @start) RETURNING id
                """, new { version, machine, start = started.ToString("O") });
        try { Launch(exe!, args, Path.GetDirectoryName(exe)!); }
        catch (Exception e) when (e is Win32Exception or IOException)
        {
            using var c = db.Open();
            c.Execute("DELETE FROM play_session WHERE id = @session", new { session });
            return ApiError.Of("play.launchFailed", ("detail", e.Message));
        }
        finally { if (swap) Profiles.SwapOut(db, profile.EmulatorId); }
        var ended = DateTime.UtcNow;

        using (var c = db.Open())
            c.Execute("UPDATE play_session SET ended_at = @end, duration = @duration WHERE id = @session",
                new { session, end = ended.ToString("O"), duration = (long)(ended - started).TotalSeconds });
        return null;
    }

    /// <summary>Las últimas sesiones del juego (#400, #410), la más nueva primero. Playing: abierta en este momento.</summary>
    /// <remarks>shortcut: una sesión abierta de hace más de un día se da por cortada (el proceso murió sin cerrarla); si molesta,
    /// guardar el pid y comprobarlo.</remarks>
    public static List<PlaySession> Recent(Database db, long gameId)
    {
        using var c = db.Open();
        var since = DateTime.UtcNow.AddDays(-1);
        return c.Query<(long VersionId, string Platform, string Launcher, string StartedAt, string? EndedAt, long? Duration)>("""
                SELECT s.version_id, v.platform, s.launcher, s.started_at, s.ended_at, s.duration
                FROM play_session s JOIN version v ON v.id = s.version_id
                WHERE v.game_id = @gameId ORDER BY s.started_at DESC LIMIT 5
                """, new { gameId })
            .Select(s => new PlaySession(s.VersionId, s.Platform, s.Launcher, s.StartedAt, s.EndedAt, s.Duration,
                s.EndedAt is null && DateTime.Parse(s.StartedAt, null, System.Globalization.DateTimeStyles.RoundtripKind) > since))
            .ToList();
    }

    // shortcut: en Linux espera solo al proceso lanzado (no a lo que abra); pidfd y sus hijos llegan en la fase L.
    static void WaitLaunched(string exe, IReadOnlyList<string> args, string folder)
    {
        using var process = Process.Start(new ProcessStartInfo(exe, args) { WorkingDirectory = folder })!;
        process.WaitForExit();
    }

    /// <summary>El texto de `errors.*` de web/messages (el mismo que usa la interfaz), con sus parámetros.</summary>
    public static string Text(ApiError error, string language)
    {
        using var stream = typeof(GameLauncher).Assembly.GetManifestResourceStream($"messages/{language}.json")
            ?? typeof(GameLauncher).Assembly.GetManifestResourceStream("messages/es.json")!;
        var node = JsonNode.Parse(stream)?["errors"];
        foreach (var part in error.Code.Split('.')) node = node?[part];
        var text = node is JsonValue v && v.TryGetValue<string>(out var s) ? s : error.Code;
        foreach (var (key, value) in error.Params ?? []) text = text.Replace($"{{{key}}}", value);
        return text;
    }
}
