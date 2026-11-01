using System.Text.Json;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;
using Otteretro.Libraries;

namespace Otteretro.Claude;

/// <summary>
/// Claude identifica lo ambiguo (§9.4, #180): lo que el escaneo no reconoció por nombre, TitleID, hash ni alias va a Claude
/// por lotes. Lo que nombra y está en el catálogo queda asignado y en Revisar › Dudosas, y su nombre, como alias para el
/// próximo escaneo; si no puede, va a Sin identificar y la app avisa. Lo ya preguntado no se repite mientras no cambie.
/// </summary>
public static class Identify
{
    public const string Kind = "claude.identify", Prompt = "identificar-juego";

    /// <summary>El escaneo la encola al terminar. Las pruebas lo apagan: no deben lanzar `claude`.</summary>
    public static bool AfterScan { get; set; } = true;

    /// <summary>Archivos por llamada.</summary>
    public static int PerCall { get; set; } = 25;

    public record FileName(long Id, string? Name, string Reason);
    public record Reply(List<FileName> Files);

    record Pending(long Id, string Console, string RelativePath, string? TitleId, string? Component);

    /// <summary>Encola la identificación si hay algo sin identificar y no hay una esperando.</summary>
    public static long? EnqueueIfNeeded(Database db)
    {
        using var c = db.Open();
        if (c.ExecuteScalar<long>("SELECT count(*) FROM task WHERE kind = @Kind AND status IN ('queued', 'paused')", new { Kind }) > 0) return null;
        if (Load(db).Count == 0) return null;
        return TaskQueue.Enqueue(db, Kind);
    }

    /// <summary>
    /// Lo que espera a Claude: archivos sin juego que no están ya en Revisar. De Switch va uno por juego (el TitleID base): lo
    /// que se decide para él vale para sus UPD y DLC.
    /// </summary>
    static List<Pending> Load(Database db)
    {
        using var c = db.Open();
        var files = c.Query<Pending>("""
            SELECT o.id, o.console, o.relative_path AS RelativePath, o.title_id AS TitleId, o.component FROM source o
            WHERE o.kind = 'file' AND o.version_id IS NULL AND o.console IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM review_item r WHERE r.entity = 'source' AND r.entity_id = o.id AND r.status = 'open')
            ORDER BY o.component <> 'BASE', o.id
            """).ToList();
        return files.Where(f => f.Component is null).Concat(files.Where(f => f.Component is not null)
            .GroupBy(f => Scan.BaseOf(f.TitleId!)).Select(g => g.First())).ToList();
    }

    record FileInput(long Id, string Console, string File, string? Folder, string? TitleId);

    static FileInput Input(Pending p) =>
        new(p.Id, p.Console, Path.GetFileName(p.RelativePath), Path.GetDirectoryName(p.RelativePath) is { } folder ? Path.GetFileName(folder) : null, p.TitleId);

    /// <summary>La huella va por archivo y no por id: si se vuelve a escanear, el archivo es el mismo.</summary>
    static string Key(Pending p) => $"file:{p.Console}:{p.RelativePath}";

    public static async Task Run(TaskRun run, CancellationToken ct)
    {
        var pending = Load(run.Db);
        var todo = pending.Where(p => ClaudeFingerprint.Changed(run.Db, Prompt, Key(p), Input(p) with { Id = 0 })).ToList();
        // Lo que Claude ya vio con los mismos datos y no pudo (o lo que deshiciste) vuelve a Sin identificar sin preguntarle otra vez.
        foreach (var p in pending.Except(todo)) Apply(run, p, null);
        var text = ClaudeRunner.Prompt(Prompt);
        var done = 0;
        foreach (var batch in todo.Chunk(PerCall))
        {
            run.Report(done * 100 / Math.Max(1, todo.Count), ApiError.Of("tasks.claude.identify", ("done", done.ToString()), ("total", todo.Count.ToString())));
            var reply = await ClaudeRunner.Run<Reply>(run.Db, run.Id, Prompt, text, new { items = batch.Select(Input) }, ct, "sonnet");
            int doubtful = 0, unidentified = 0;
            foreach (var p in batch)
            {
                var answer = reply.Value.Files.FirstOrDefault(f => f.Id == p.Id);
                if (Apply(run, p, answer)) doubtful++;
                else unidentified++;
                ClaudeFingerprint.Save(run.Db, Prompt, Key(p), Input(p) with { Id = 0 });
            }
            run.Log(ApiError.Of("tasks.log.identify", ("doubtful", doubtful.ToString()), ("unidentified", unidentified.ToString())));
            done += batch.Length;
        }
    }

    /// <summary>Asigna el juego que nombró Claude (y a los UPD y DLC del mismo TitleID base) o lo deja en Sin identificar.</summary>
    static bool Apply(TaskRun run, Pending p, FileName? answer)
    {
        var version = answer?.Name is { } name ? Scan.MatchName(run.Db, p.Console, name) : null;
        using var c = run.Db.Open();
        using var t = c.BeginTransaction();
        var now = DateTime.UtcNow.ToString("O");
        var ids = p.Component is null ? [p.Id] : c.Query<(long Id, string TitleId)>(
                "SELECT id, title_id FROM source WHERE kind = 'file' AND version_id IS NULL AND console = @Console AND component IS NOT NULL", p, t)
            .Where(r => Scan.BaseOf(r.TitleId) == Scan.BaseOf(p.TitleId!)).Select(r => r.Id).ToList();
        var data = JsonSerializer.Serialize(new { prompt = Prompt, name = answer?.Name, reason = answer?.Reason, versionId = version }, JsonSerializerOptions.Web);
        foreach (var id in ids)
        {
            if (version is not null) c.Execute("UPDATE source SET version_id = @version WHERE id = @id", new { id, version }, t);
            c.Execute("INSERT INTO review_item (kind, entity, entity_id, data, created_at) VALUES (@kind, 'source', @id, @data, @now)",
                new { kind = version is null ? "unidentified" : "doubtful", id, data, now }, t);
        }
        if (version is not null)
        {
            var gameId = c.ExecuteScalar<long>("SELECT game_id FROM version WHERE id = @version", new { version }, t);
            AddAlias(c, t, Scan.AliasText(p.RelativePath), p.Console, gameId, "claude", run.Id);
        }
        t.Commit();
        return version is not null;
    }

    /// <summary>Un alias (§11.1): ese nombre de archivo, en esa consola, es ese juego. El escaneo lo usa antes que Claude.</summary>
    public static void AddAlias(System.Data.IDbConnection c, System.Data.IDbTransaction? t, string text, string console, long gameId, string origin, long? taskId)
    {
        c.Execute("UPDATE learning SET undone = 1 WHERE kind = 'alias' AND undone = 0 AND json_extract(content, '$.text') = @text AND json_extract(content, '$.console') = @console",
            new { text, console }, t);
        c.Execute("INSERT INTO learning (kind, content, created_at, task_id) VALUES ('alias', @content, @now, @taskId)", new
        {
            content = JsonSerializer.Serialize(new { kind = "alias", text, console, gameId, origin }, JsonSerializerOptions.Web),
            now = DateTime.UtcNow.ToString("O"),
            taskId,
        }, t);
    }
}
