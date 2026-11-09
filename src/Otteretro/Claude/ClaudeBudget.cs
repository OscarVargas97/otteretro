using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.Data;

namespace Otteretro.Claude;

/// <summary>
/// Una tarea de Claude que tiene que esperar al límite de la suscripción. La cola la devuelve a la espera hasta
/// <see cref="Until"/> con su punto de control, sin contarla como un intento fallido.
/// </summary>
public sealed class ClaudePausedException(DateTime until, ApiError reason) : Exception(reason.Code)
{
    public DateTime Until { get; } = until;
    public ApiError Reason { get; } = reason;
}

/// <summary>Almacenamiento › Reconocer juegos (#251): si se usa Claude, lo del mes y si espera al límite.</summary>
/// <param name="UseClaude">Si lo que ningún script reconoce va a Claude (decisión 281); apagado, va directo a Revisar.</param>
/// <param name="MonthCalls">Llamadas para identificar juegos en el mes en curso (decisión 282).</param>
/// <param name="PausedUntil">Hasta cuándo espera por el límite de la suscripción (null = no espera).</param>
public record RecognizeStatus(bool UseClaude, int MonthCalls, double MonthCost, string? PausedUntil);

public record RecognizeRequest(bool UseClaude);

/// <summary>
/// Cuándo puede llamar Otteretro a Claude (decisión 280): siempre que haga falta, sin horario. Solo espera cuando la
/// suscripción llega a su límite, y sigue sola cuando se libera.
/// </summary>
public static partial class ClaudeBudget
{
    public static Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

    // Ajuste (tabla `setting`, global) y estado que lleva la app.
    public const string UseClaude = "claude.use";
    const string PausedUntil = "claude.pausedUntil";

    /// <summary>Antes de cada llamada: lanza <see cref="ClaudePausedException"/> si se está esperando al límite.</summary>
    public static void Check(Database db)
    {
        using var c = db.Open();
        if (Date(c, PausedUntil) is { } paused && paused > UtcNow())
            throw new ClaudePausedException(paused, ApiError.Of("tasks.claude.limit", ("at", paused.ToString("O"))));
    }

    /// <summary>Si se está esperando al límite (la cola prueba cada 15 minutos mientras tanto, #363).</summary>
    public static bool Paused(Database db)
    {
        using var c = db.Open();
        return Date(c, PausedUntil) is { } paused && paused > UtcNow();
    }

    /// <summary>La respuesta de la llamada mínima de <see cref="Retry"/>.</summary>
    public record Ping(bool Ok);

    /// <summary>
    /// «Probar ahora» (#363): deja de esperar y hace una llamada mínima. Si responde, las tareas que esperaban al límite
    /// (`tasks.claude.limit`) vuelven a la cola ya, con su punto de control, y se devuelven sus ids. Si el límite sigue, lanza
    /// <see cref="ClaudePausedException"/> con la hora nueva, que <see cref="LimitReached"/> ya guardó. Si falla por otra cosa,
    /// la espera queda como estaba.
    /// </summary>
    public static async Task<long[]> Retry(Database db, CancellationToken ct)
    {
        string? before;
        using (var c = db.Open())
        {
            before = c.ExecuteScalar<string?>("SELECT value FROM setting WHERE machine_id = '' AND name = @PausedUntil", new { PausedUntil });
            c.Execute("DELETE FROM setting WHERE machine_id = '' AND name = @PausedUntil", new { PausedUntil });
        }
        try { await ClaudeRunner.Run<Ping>(db, null, "retry", "Responde con ok = true.", new { }, ct, "haiku"); }
        catch (Exception e) when (e is not ClaudePausedException && before is not null)
        {
            using var c = db.Open();
            Set(c, PausedUntil, before);
            throw;
        }
        using (var c = db.Open())
            return c.Query<long>("UPDATE task SET resume_at = NULL, detail = NULL WHERE status = 'queued' AND detail LIKE '%tasks.claude.limit%' RETURNING id").ToArray();
    }

    /// <summary>
    /// Si el texto es el aviso de límite de `claude` («You've hit your session limit · resets 8:10pm (America/Santiago)»),
    /// guarda hasta cuándo esperar y lo devuelve.
    /// </summary>
    public static ClaudePausedException? LimitReached(Database db, string text)
    {
        if (!LimitText().IsMatch(text)) return null;
        // ponytail: si la hora no se entiende, se espera una hora y se prueba de nuevo (volverá a avisar si sigue).
        var until = ParseReset(text, UtcNow()) ?? UtcNow().AddHours(1);
        using var c = db.Open();
        Set(c, PausedUntil, until.ToString("O"));
        return new ClaudePausedException(until, ApiError.Of("tasks.claude.limit", ("at", until.ToString("O"))));
    }

    /// <summary>Si lo que ningún script reconoce va a Claude: sí, mientras no se apague.</summary>
    public static bool Enabled(Database db)
    {
        using var c = db.Open();
        return c.ExecuteScalar<string?>("SELECT value FROM setting WHERE machine_id = '' AND name = @UseClaude", new { UseClaude }) != "0";
    }

    public static RecognizeStatus Status(Database db)
    {
        var now = UtcNow();
        using var c = db.Open();
        var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O");
        var (calls, cost) = c.QuerySingle<(long, double)>(
            "SELECT count(*), COALESCE(SUM(cost_usd), 0.0) FROM claude_usage WHERE prompt = @Prompt AND created_at >= @from", new { Identify.Prompt, from });
        return new RecognizeStatus(Enabled(db), (int)calls, cost, Date(c, PausedUntil) is { } p && p > now ? p.ToString("O") : null);
    }

    public static void Save(Database db, RecognizeRequest request)
    {
        using var c = db.Open();
        Set(c, UseClaude, request.UseClaude ? "1" : "0");
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

    static DateTime? Date(System.Data.IDbConnection c, string name) =>
        c.ExecuteScalar<string?>("SELECT value FROM setting WHERE machine_id = '' AND name = @name", new { name }) is { } v
        && DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    public static void Set(System.Data.IDbConnection c, string name, string value) =>
        c.Execute("INSERT INTO setting (name, value, machine_id) VALUES (@name, @value, '') ON CONFLICT (name, machine_id) DO UPDATE SET value = excluded.value",
            new { name, value });

    [GeneratedRegex(@"hit your .*limit|usage limit reached", RegexOptions.IgnoreCase)]
    private static partial Regex LimitText();


    [GeneratedRegex(@"resets\s+(?:(?<date>[A-Z][a-z]{2} \d{1,2},?)\s+)?(?<h>\d{1,2})(?::(?<m>\d{2}))?\s*(?<ampm>am|pm)\s*\((?<zone>[^)]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ResetText();
}
