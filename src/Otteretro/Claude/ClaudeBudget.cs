using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.Data;

namespace Otteretro.Claude;

/// <summary>
/// Una tarea de Claude que tiene que esperar (límite de la suscripción, tope por ventana u horario). La cola la
/// devuelve a la espera hasta <see cref="Until"/> con su punto de control, sin contarla como un intento fallido.
/// </summary>
public sealed class ClaudePausedException(DateTime until, ApiError reason) : Exception(reason.Code)
{
    public DateTime Until { get; } = until;
    public ApiError Reason { get; } = reason;
}

/// <summary>
/// Cuándo puede llamar Otteretro a Claude sin quitarte cuota (F2.2, decisiones #154 y #155):
/// en el horario (00:00 a 08:00 por defecto, en la hora del equipo), sin pasar del tope de cada ventana de 5 h
/// (50 % por defecto) y nunca con el límite de la suscripción alcanzado. Los ajustes van en la tabla `setting`.
/// </summary>
public static partial class ClaudeBudget
{
    public static Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

    public static readonly TimeSpan Window = TimeSpan.FromHours(5);

    // Ajustes (tabla `setting`, globales). Los valores por defecto valen mientras no se cambian en Ajustes › Claude.
    public const string ScheduleStart = "claude.scheduleStart", ScheduleEnd = "claude.scheduleEnd", WindowShare = "claude.windowShare";
    // Estado que lleva la app.
    const string WindowStart = "claude.windowStart", WindowCost = "claude.windowCost", PausedUntil = "claude.pausedUntil", RunNowUntil = "claude.runNowUntil";

    /// <summary>Antes de cada llamada: lanza <see cref="ClaudePausedException"/> si ahora no se puede.</summary>
    public static void Check(Database db)
    {
        var now = UtcNow();
        using var c = db.Open();
        var s = c.Query<(string Name, string Value)>("SELECT name, value FROM setting WHERE machine_id = '' AND name LIKE 'claude.%'")
            .ToDictionary(r => r.Name, r => r.Value);

        if (Date(s, PausedUntil) is { } paused && paused > now)
            throw new ClaudePausedException(paused, ApiError.Of("tasks.claude.limit", ("at", paused.ToString("O"))));

        if (!InSchedule(s, now) && !(Date(s, RunNowUntil) > now))
        {
            var next = NextScheduleStart(s, now);
            throw new ClaudePausedException(next, ApiError.Of("tasks.claude.schedule", ("at", next.ToString("O"))));
        }

        // La ventana empieza con la primera llamada después de que terminó la anterior.
        var start = Date(s, WindowStart);
        if (start is null || start + Window <= now)
        {
            Set(c, WindowStart, now.ToString("O"));
            return;
        }
        // Tope: solo cuando ya se sabe cuánto entra en una ventana (se aprende al tocar el límite).
        // ponytail: se mide lo que gasta Otteretro; tus sesiones de Claude Code en la misma ventana no se ven.
        if (Number(s, WindowCost) is { } capacity && capacity > 0)
        {
            var used = c.ExecuteScalar<double>("SELECT COALESCE(SUM(cost_usd), 0) FROM claude_usage WHERE created_at >= @from",
                new { from = start.Value.ToString("O") });
            var share = Number(s, WindowShare) ?? 50;
            if (used >= capacity * share / 100)
            {
                var end = start.Value + Window;
                throw new ClaudePausedException(end, ApiError.Of("tasks.claude.windowCap", ("share", share.ToString(CultureInfo.InvariantCulture)), ("at", end.ToString("O"))));
            }
        }
    }

    /// <summary>
    /// Si el texto es el aviso de límite de `claude` («You've hit your session limit · resets 8:10pm (America/Santiago)»),
    /// guarda hasta cuándo esperar y lo devuelve. Con el límite de sesión (5 h) además aprende cuánto entra en una ventana.
    /// </summary>
    public static ClaudePausedException? LimitReached(Database db, string text)
    {
        if (!LimitText().IsMatch(text)) return null;
        var now = UtcNow();
        // ponytail: si la hora no se entiende, se espera una hora y se prueba de nuevo (volverá a avisar si sigue).
        var until = ParseReset(text, now) ?? now.AddHours(1);
        using var c = db.Open();
        Set(c, PausedUntil, until.ToString("O"));
        if (SessionLimit().IsMatch(text))
        {
            var start = until - Window;
            var used = c.ExecuteScalar<double>("SELECT COALESCE(SUM(cost_usd), 0) FROM claude_usage WHERE created_at >= @from",
                new { from = start.ToString("O") });
            if (used > 0) Set(c, WindowCost, used.ToString(CultureInfo.InvariantCulture));
            Set(c, WindowStart, start.ToString("O"));
        }
        return new ClaudePausedException(until, ApiError.Of("tasks.claude.limit", ("at", until.ToString("O"))));
    }

    /// <summary>«Ejecutar ahora»: salta el horario hasta que empiece el siguiente. El límite y el tope siguen valiendo.</summary>
    public static DateTime RunNow(Database db)
    {
        var now = UtcNow();
        using var c = db.Open();
        var s = c.Query<(string Name, string Value)>("SELECT name, value FROM setting WHERE machine_id = '' AND name LIKE 'claude.%'")
            .ToDictionary(r => r.Name, r => r.Value);
        var until = NextScheduleStart(s, now);
        Set(c, RunNowUntil, until.ToString("O"));
        // Lo que esperaba el horario sale ya.
        c.Execute("UPDATE task SET resume_at = NULL WHERE status = 'queued' AND kind LIKE 'claude.%' AND detail LIKE '%tasks.claude.schedule%'");
        return until;
    }

    /// <summary>La hora de «resets …» en UTC: «8:10pm», «3pm» o con fecha, «Nov 3, 9am», en la zona que trae entre paréntesis.</summary>
    public static DateTime? ParseReset(string text, DateTime nowUtc)
    {
        var m = ResetText().Match(text);
        if (!m.Success) return null;
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(m.Groups["zone"].Value); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { zone = TimeZoneInfo.Local; }
        var hour = int.Parse(m.Groups["h"].Value) % 12 + (m.Groups["ampm"].Value.Equals("pm", StringComparison.OrdinalIgnoreCase) ? 12 : 0);
        var minute = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value) : 0;
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
        DateTime at;
        if (m.Groups["date"].Success)
        {
            if (!DateTime.TryParseExact(m.Groups["date"].Value.Trim(), ["MMM d", "MMM d,"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                return null;
            at = new DateTime(local.Year, day.Month, day.Day, hour, minute, 0);
            if (at < local.AddDays(-1)) at = at.AddYears(1); // «Jan 2» dicho en diciembre
        }
        else
        {
            at = local.Date.AddHours(hour).AddMinutes(minute);
            if (at <= local) at = at.AddDays(1);
        }
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(at, DateTimeKind.Unspecified), zone);
    }

    static bool InSchedule(Dictionary<string, string> s, DateTime nowUtc)
    {
        var (start, end) = Schedule(s);
        var t = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZoneInfo.Local));
        return start == end || (start < end ? t >= start && t < end : t >= start || t < end); // 22:00–06:00 cruza la medianoche
    }

    static DateTime NextScheduleStart(Dictionary<string, string> s, DateTime nowUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZoneInfo.Local);
        var at = local.Date + Schedule(s).Start.ToTimeSpan();
        if (at <= local) at = at.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(at, DateTimeKind.Unspecified), TimeZoneInfo.Local);
    }

    static (TimeOnly Start, TimeOnly End) Schedule(Dictionary<string, string> s) =>
        (Time(s, ScheduleStart) ?? new TimeOnly(0, 0), Time(s, ScheduleEnd) ?? new TimeOnly(8, 0));

    static TimeOnly? Time(Dictionary<string, string> s, string key) =>
        s.TryGetValue(key, out var v) && TimeOnly.TryParseExact(v, "HH:mm", out var t) ? t : null;

    static DateTime? Date(Dictionary<string, string> s, string key) =>
        s.TryGetValue(key, out var v) && DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    static double? Number(Dictionary<string, string> s, string key) =>
        s.TryGetValue(key, out var v) && double.TryParse(v, CultureInfo.InvariantCulture, out var n) ? n : null;

    public static void Set(System.Data.IDbConnection c, string name, string value) =>
        c.Execute("INSERT INTO setting (name, value, machine_id) VALUES (@name, @value, '') ON CONFLICT (name, machine_id) DO UPDATE SET value = excluded.value",
            new { name, value });

    [GeneratedRegex(@"hit your .*limit|usage limit reached", RegexOptions.IgnoreCase)]
    private static partial Regex LimitText();

    [GeneratedRegex(@"session limit|5-hour limit", RegexOptions.IgnoreCase)]
    private static partial Regex SessionLimit();

    [GeneratedRegex(@"resets\s+(?:(?<date>[A-Z][a-z]{2} \d{1,2},?)\s+)?(?<h>\d{1,2})(?::(?<m>\d{2}))?\s*(?<ampm>am|pm)\s*\((?<zone>[^)]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ResetText();
}
