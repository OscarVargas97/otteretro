using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>
/// Formatos livianos (§9.7, #231 a #233): GameCube y Wii a RVZ con DolphinTool; PS1 y PS2 a CHD con chdman. Se convierte a un temporal,
/// se verifica y recién entonces se reemplaza el original: un RVZ tiene que dar el mismo SHA-1 que su ISO, y un CHD pasar `chdman verify`.
/// </summary>
public static partial class Converter
{
    public const string Kind = "library.convert";
    public static readonly string[] Consoles = ["gc", "wii", "ps1", "ps2"];
    const string Partial = ".convirtiendo";

    public record Job(string Console, long After = 0);

    /// <summary>Para las pruebas: corre la herramienta. Devuelve el código de salida y lo que imprimió.</summary>
    public static Func<string, string[], CancellationToken, Task<(int Code, string Output)>> Launch { get; set; } = Run;

    /// <summary>Dónde vienen las herramientas con la app (#232).</summary>
    public static string BundledFolder { get; set; } = Path.Combine(AppContext.BaseDirectory, "herramientas");

    public static (long? Task, ApiError? Error) Enqueue(Database db, string console) =>
        Consoles.Contains(console)
            ? (TaskQueue.Enqueue(db, Kind, JsonSerializer.Serialize(new Job(console), JsonSerializerOptions.Web)), null)
            : (null, ApiError.Of("convert.notConvertible"));

    /// <summary>El botón por consola (#233): convierte lo que tiene. Punto de control: el último archivo terminado.</summary>
    public static async Task RunTask(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<Job>(run.Data!, JsonSerializerOptions.Web)!;
        List<long> ids;
        using (var c = run.Db.Open())
            ids = c.Query<long>("SELECT id FROM source WHERE kind = 'file' AND console = @Console AND id > @After ORDER BY id", job).ToList();
        var converted = 0;
        for (var i = 0; i < ids.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            run.Report(i * 100 / ids.Count, ApiError.Of("convert.file", ("console", job.Console)));
            if (await Convert(run, ids[i], ct)) converted++;
            run.Report((i + 1) * 100 / ids.Count, null, JsonSerializer.Serialize(job with { After = ids[i] }, JsonSerializerOptions.Web));
        }
        run.Log(ApiError.Of("convert.log.summary", ("converted", converted.ToString())));
    }

    record Row(long Id, string LibraryId, string RelativePath, string Console, string? Sha1, long? VersionId);

    /// <summary>Convierte una fila de <c>source</c>, si corresponde. Nunca hace fallar la tarea: si no se puede, el original queda como estaba.</summary>
    public static async Task<bool> Convert(TaskRun run, long sourceId, CancellationToken ct)
    {
        Row? row;
        using (var c = run.Db.Open())
            row = c.QuerySingleOrDefault<Row>(
                "SELECT id, library_id AS LibraryId, relative_path AS RelativePath, console, sha1, version_id AS VersionId FROM source WHERE id = @sourceId", new { sourceId });
        if (row is null || Disk.Get(run.Db, row.LibraryId) is not { Connected: true } disk) return false;
        var file = Path.Combine(disk.Path, row.RelativePath);
        if (!File.Exists(file) || TargetOf(row.Console, file) is not { } target) return false;
        if (PortForbids(run.Db, row.VersionId, target))
        {
            run.Log(ApiError.Of("convert.log.portKeeps", ("path", file)));
            return false;
        }
        if (Tool(run.Db, target == ".rvz" ? "DolphinTool.exe" : "chdman.exe") is not { } tool)
        {
            run.Log(ApiError.Of("convert.log.noTool", ("path", file)), "warn");
            return false;
        }
        var final = Path.ChangeExtension(file, target);
        var temp = Path.ChangeExtension(file, Partial + target);
        if (File.Exists(final))
        {
            run.Log(ApiError.Of("import.log.conflict", ("path", file), ("to", final)), "warn");
            return false;
        }
        try
        {
            run.Report(0, ApiError.Of("convert.converting", ("path", file)));
            File.Delete(temp);
            var original = target == ".rvz" ? row.Sha1 ?? await Dats.Sha1(file, ct) : null;
            var work = Path.Combine(Path.GetTempPath(), "otteretro-dolphintool");
            var (code, _) = await Launch(tool, target == ".rvz"
                ? ["convert", "-u", work, "-i", file, "-o", temp, "-f", "rvz", "-b", "131072", "-c", "zstd", "-l", "5"]
                : [Path.GetExtension(file).Equals(".cue", StringComparison.OrdinalIgnoreCase) ? "createcd" : "createdvd", "-i", file, "-o", temp], ct);
            if (code != 0 || !File.Exists(temp) || !await Verified(tool, target, temp, original, ct))
            {
                File.Delete(temp);
                run.Log(ApiError.Of("convert.log.failed", ("path", file)), "warn");
                return false;
            }
            FolderMove.FlushToDisk(temp);
            var companions = Import.Companions(file);
            File.Move(temp, final);
            foreach (var old in companions.Append(file)) File.Delete(old);
            using (var c = run.Db.Open())
                c.Execute("UPDATE source SET relative_path = @relative, size = @size, sha1 = coalesce(sha1, @original), verified = verified WHERE id = @Id",
                    new { relative = Path.GetRelativePath(disk.Path, final), size = new FileInfo(final).Length, original, row.Id });
            // Lo que apuntaba al ISO (Dusklight) ahora apunta al RVZ.
            DiskTasks.RewriteInstalled(run.Db, file, final);
            run.Log(ApiError.Of("convert.log.done", ("path", final)));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            if (File.Exists(temp)) File.Delete(temp);
            run.Log(ApiError.Of("convert.log.failed", ("path", file)), "warn");
            return false;
        }
    }

    /// <summary>El formato al que va ese archivo; null si se queda como está.</summary>
    public static string? TargetOf(string console, string file) => (console, Path.GetExtension(file).ToLowerInvariant()) switch
    {
        ("gc" or "wii", ".iso" or ".gcm" or ".gcz" or ".wbfs" or ".ciso" or ".wia") => ".rvz",
        ("ps1" or "ps2", ".cue") => ".chd",
        ("ps2", ".iso") => ".chd",
        _ => null,
    };

    /// <summary>Lo que pide el port manda (§9.7): su receta dice qué formatos acepta para su juego.</summary>
    public static bool PortForbids(Database db, long? version, string target)
    {
        if (version is null) return false;
        using var c = db.Open();
        var game = c.QuerySingleOrDefault<(string Es, string? Original)>(
            "SELECT g.name_es, g.original_name FROM version v JOIN game g ON g.id = v.game_id WHERE v.id = @version", new { version });
        return Recipes.All.Any(r => r.Kind == "port" && r.Formats.Count > 0 && !r.Formats.Contains(target[1..], StringComparer.OrdinalIgnoreCase)
            && r.Game is not null && (Scan.Normalize(r.Game) == Scan.Normalize(game.Es ?? "") || Scan.Normalize(r.Game) == Scan.Normalize(game.Original ?? "")));
    }

    /// <summary>La herramienta: la que viene con la app o, si no, la del emulador instalado (#232).</summary>
    public static string? Tool(Database db, string name)
    {
        var bundled = Path.Combine(BundledFolder, name);
        if (File.Exists(bundled)) return bundled;
        return Recipes.Installed(Disk.ToolRoots(db)).Select(t => Path.Combine(t.Path, name)).FirstOrDefault(File.Exists);
    }

    static async Task<bool> Verified(string tool, string target, string file, string? original, CancellationToken ct)
    {
        if (target == ".chd") return (await Launch(tool, ["verify", "-i", file], ct)).Code == 0;
        return await ToolSha1(tool, file, ct) is { } sha1 && string.Equals(sha1, original, StringComparison.OrdinalIgnoreCase);
    }

    static async Task<string?> ToolSha1(string tool, string file, CancellationToken ct)
    {
        var (code, output) = await Launch(tool, ["verify", "-u", Path.Combine(Path.GetTempPath(), "otteretro-dolphintool"), "-i", file, "-a", "sha1"], ct);
        return code == 0 && Sha1Text().Match(output) is { Success: true } m ? m.Value.ToUpperInvariant() : null;
    }

    /// <summary>El SHA-1 con el que se compara contra los DAT: el de su ISO si es un RVZ (#231); si no, el del archivo.</summary>
    public static async Task<string> Sha1Of(Database db, string file, CancellationToken ct) =>
        Path.GetExtension(file).Equals(".rvz", StringComparison.OrdinalIgnoreCase) && Tool(db, "DolphinTool.exe") is { } tool && await ToolSha1(tool, file, ct) is { } sha1
            ? sha1
            : await Dats.Sha1(file, ct);

    /// <summary>Un CHD que llega hecho no se puede comparar con Redump: se comprueba que esté sano (#231). Null sin chdman.</summary>
    public static async Task<bool?> ChdIntact(Database db, string file, CancellationToken ct) =>
        Tool(db, "chdman.exe") is { } tool ? (await Launch(tool, ["verify", "-i", file], ct)).Code == 0 : null;

    static async Task<(int, string)> Run(string tool, string[] args, CancellationToken ct)
    {
        var start = new ProcessStartInfo(tool) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        try { await process.WaitForExitAsync(ct); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        await error;
        return (process.ExitCode, await output);
    }

    [GeneratedRegex("[0-9a-fA-F]{40}")]
    private static partial Regex Sha1Text();
}
