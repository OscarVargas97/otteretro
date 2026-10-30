using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Tests;

public class TaskQueueTests
{
    static (TaskQueue Queue, Database Db) NewQueue(TempDir tmp)
    {
        var config = new AppConfig { DataFolder = tmp["datos"] };
        Assert.Null(DataFolder.Prepare(config.DataFolder));
        var db = Startup.PrepareData(config);
        var session = new AppSession(config, tmp["config.json"], "k");
        var events = new Events();
        return (new TaskQueue(session, events, new FolderMoveService(session, events), new SecretBox()) { Poll = TimeSpan.FromMilliseconds(20) }, db);
    }

    static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 250 && !condition(); i++) await Task.Delay(20);
        Assert.True(condition(), string.Join(" | ", Directory.GetFiles(System.IO.Path.GetTempPath() + "otteretro-pruebas", "*.log", SearchOption.AllDirectories).Select(File.ReadAllText)));
    }

    static string Status(Database db, long id)
    {
        using var c = db.Open();
        return c.ExecuteScalar<string>("SELECT status FROM task WHERE id = @id", new { id })!;
    }

    /// <summary>Listo cuando de F1.2: una tarea cortada por un cierre de la app sigue desde su punto de control.</summary>
    [Fact]
    public async Task InterruptedTaskResumesAfterRestart()
    {
        var starts = new List<string?>();
        TaskQueue.Handlers["test-resume"] = async (run, ct) =>
        {
            lock (starts) starts.Add(run.Data);
            for (var step = int.Parse(run.Data ?? "0"); step < 4; step++)
            {
                run.Report(step * 25, null, step.ToString());
                await Task.Delay(step == 2 && run.Data == "2" && starts.Count == 1 ? Timeout.Infinite : 10, ct);
            }
        };
        using var tmp = new TempDir();
        var (first, db) = NewQueue(tmp);
        var id = TaskQueue.Enqueue(db, "test-resume");

        await first.StartAsync(CancellationToken.None);
        await Until(() => starts.Count == 1 && Status(db, id) == "running" && first.List()[0].Progress == 50);
        await first.StopAsync(CancellationToken.None);
        Assert.Equal("running", Status(db, id));

        var (second, _) = NewQueue(tmp);
        await second.StartAsync(CancellationToken.None);
        await Until(() => Status(db, id) == "done");
        await second.StopAsync(CancellationToken.None);

        Assert.Equal([null, "2"], starts);
        Assert.Equal(100, second.List()[0].Progress);
    }

    [Fact]
    public async Task OneTaskAtATimePerKindAndErrorsAreKept()
    {
        int now = 0, max = 0;
        TaskQueue.Handlers["test-serial"] = async (run, ct) =>
        {
            max = Math.Max(max, Interlocked.Increment(ref now));
            await Task.Delay(50, ct);
            Interlocked.Decrement(ref now);
            if (run.Id % 2 == 0) throw new InvalidOperationException("sin red");
        };
        using var tmp = new TempDir();
        var (queue, db) = NewQueue(tmp);
        var ids = Enumerable.Range(0, 3).Select(_ => TaskQueue.Enqueue(db, "test-serial")).ToList();

        await queue.StartAsync(CancellationToken.None);
        await Until(() => ids.All(id => Status(db, id) is "done" or "error"));
        await queue.StopAsync(CancellationToken.None);

        Assert.Equal(1, max);
        var failed = queue.List().Single(t => t.Id == ids[1]);
        Assert.Equal("error", failed.Status);
        Assert.Equal(ApiError.Of("tasks.failed", ("detail", "sin red")).Code, failed.Detail!.Code);
        Assert.Equal("sin red", failed.Detail.Params!["detail"]);
    }
}
