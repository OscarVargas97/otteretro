using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>
/// «Convertir» (#469): un port que pide el juego de Switch extraído (SkyRecomp, LASRecomp) lo encuentra en `exefs\` y `romfs\`, al
/// lado de los NSP. Se sacan con hactool, la base con su última actualización aplicada, usando las prod.keys de Sistema y las llaves de
/// título de los tickets. Se extrae a un temporal dentro de la carpeta y recién al terminar bien se mueve; los NSP no se tocan.
/// </summary>
public static partial class SwitchExtract
{
    public const string Kind = "library.extract";
    public const string Tool = "hactool.exe";
    const string Work = ".otteretro-extraer";

    public record Job(long Version);

    /// <summary>Para las pruebas: corre hactool con HOME en la carpeta dada (ahí busca `.switch\title.keys`). Devuelve el código de salida.</summary>
    public static Func<string, string[], string, CancellationToken, Task<int>> Launch { get; set; } = Run;

    record Nsp(string Path, string? Component);

    static List<Nsp> Nsps(Database db, long version)
    {
        using var c = db.Open();
        return c.Query<(string LibraryId, string Relative, string? Component)>(
                "SELECT library_id, relative_path, component FROM source WHERE kind = 'file' AND version_id = @version AND lower(relative_path) LIKE '%.nsp'", new { version })
            .Select(r => Disk.Get(db, r.LibraryId) is { Connected: true } d ? new Nsp(Path.Combine(d.Path, r.Relative), r.Component) : null)
            .Where(n => n is not null && File.Exists(n.Path)).Select(n => n!).ToList();
    }

    static Nsp? BaseOf(List<Nsp> nsps) => nsps.FirstOrDefault(n => n.Component is null or "BASE");

    /// <summary>La carpeta del juego (la del NSP base) si falta extraerlo; null si ya está o no hay NSP base.</summary>
    public static string? Pending(Database db, long version)
    {
        if (BaseOf(Nsps(db, version)) is not { } b) return null;
        var folder = Path.GetDirectoryName(b.Path)!;
        return File.Exists(Path.Combine(folder, "exefs", "main")) && Directory.Exists(Path.Combine(folder, "romfs")) ? null : folder;
    }

    /// <summary>Las prod.keys más nuevas de Sistema › Switch; null si no hay.</summary>
    public static string? Keys(Database db) =>
        SystemFiles.List(db).FirstOrDefault(k => k.Id == "switch")?.Files
            .Where(f => f.Name.Equals("prod.keys", StringComparison.OrdinalIgnoreCase)).OrderByDescending(f => f.Modified).FirstOrDefault()?.Path;

    public static (long? Task, ApiError? Error) Enqueue(Database db, long version)
    {
        if (Pending(db, version) is null) return (null, ApiError.Of("extract.notNeeded"));
        if (Keys(db) is null) return (null, ApiError.Of("extract.noKeys"));
        if (Converter.Tool(db, Tool) is null) return (null, ApiError.Of("extract.noTool"));
        return (TaskQueue.Enqueue(db, Kind, JsonSerializer.Serialize(new Job(version), JsonSerializerOptions.Web)), null);
    }

    public static async Task Run(TaskRun run, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<Job>(run.Data!, JsonSerializerOptions.Web)!;
        var nsps = Nsps(run.Db, job.Version);
        if (Pending(run.Db, job.Version) is not { } folder || BaseOf(nsps) is not { } b) return;
        var keys = Keys(run.Db) ?? throw new TaskException(ApiError.Of("extract.noKeys"));
        var tool = Converter.Tool(run.Db, Tool) ?? throw new TaskException(ApiError.Of("extract.noTool"));
        foreach (var dir in new[] { "exefs", "romfs" })
            if (Directory.Exists(Path.Combine(folder, dir))) throw new TaskException(ApiError.Of("extract.exists", ("path", Path.Combine(folder, dir))));
        // La de número de versión más alto ([v65536] > [v0]); sin número, la última por nombre.
        var update = nsps.Where(n => n.Component == "UPD").OrderBy(n => VersionIn().Match(Path.GetFileName(n.Path)) is { Success: true } m ? long.Parse(m.Groups[1].Value) : -1)
            .ThenBy(n => n.Path, StringComparer.OrdinalIgnoreCase).LastOrDefault();

        var work = Path.Combine(folder, Work);
        if (Directory.Exists(work)) Directory.Delete(work, true);
        var switchDir = Directory.CreateDirectory(Path.Combine(work, ".switch")).FullName;
        try
        {
            // hactool aborta con llaves de largo raro: solo las de 32 o 64 dígitos.
            var keyFile = Path.Combine(switchDir, "prod.keys");
            File.WriteAllLines(keyFile, File.ReadLines(keys).Select(l => l.Trim()).Where(l => KeyLine().IsMatch(l)));
            var titleKeys = new List<string>();
            var ncas = new List<string>();
            var parts = update is null ? [b] : new[] { b, update };
            for (var i = 0; i < parts.Length; i++)
            {
                run.Report(i * 40 / parts.Length, ApiError.Of("extract.unpacking", ("path", parts[i].Path)));
                var dir = Path.Combine(work, i.ToString());
                await Hactool(tool, ["-k", keyFile, "-t", "pfs0", $"--outdir={dir}", parts[i].Path], work, ct);
                // El ticket trae el rights id (0x2A0) y la llave del título (0x180).
                if (Directory.EnumerateFiles(dir, "*.tik").FirstOrDefault() is { } tik)
                {
                    var t = File.ReadAllBytes(tik);
                    if (t.Length >= 0x2B0) titleKeys.Add($"{Convert.ToHexStringLower(t, 0x2A0, 16)} = {Convert.ToHexStringLower(t, 0x180, 16)}");
                }
                // El NCA del programa es el más grande.
                ncas.Add(Directory.EnumerateFiles(dir, "*.nca").MaxBy(f => new FileInfo(f).Length) ?? throw new TaskException(ApiError.Of("extract.failed", ("path", parts[i].Path))));
            }
            File.WriteAllLines(Path.Combine(switchDir, "title.keys"), titleKeys);
            run.Report(40, ApiError.Of("extract.extracting", ("path", folder)));
            var exefs = Path.Combine(work, "out", "exefs");
            var romfs = Path.Combine(work, "out", "romfs");
            await Hactool(tool, update is null
                ? ["-k", keyFile, $"--exefsdir={exefs}", $"--romfsdir={romfs}", ncas[0]]
                : ["-k", keyFile, $"--basenca={ncas[0]}", $"--exefsdir={exefs}", $"--romfsdir={romfs}", ncas[1]], work, ct);
            if (!File.Exists(Path.Combine(exefs, "main")) || !Directory.Exists(romfs) || !Directory.EnumerateFileSystemEntries(romfs).Any())
                throw new TaskException(ApiError.Of("extract.failed", ("path", folder)));
            Directory.Move(exefs, Path.Combine(folder, "exefs"));
            Directory.Move(romfs, Path.Combine(folder, "romfs"));
            run.Log(ApiError.Of("extract.log.done", ("path", folder)));
        }
        finally
        {
            // Las llaves y los NCA descifrables no quedan en disco.
            if (Directory.Exists(work)) Directory.Delete(work, true);
        }
    }

    static async Task Hactool(string tool, string[] args, string home, CancellationToken ct)
    {
        if (await Launch(tool, args, home, ct) != 0) throw new TaskException(ApiError.Of("extract.failed", ("path", args[^1])));
    }

    static async Task<int> Run(string tool, string[] args, string home, CancellationToken ct)
    {
        var start = new ProcessStartInfo(tool) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) start.ArgumentList.Add(a);
        start.Environment["HOME"] = home;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        try { await process.WaitForExitAsync(ct); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        await Task.WhenAll(output, error);
        return process.ExitCode;
    }

    [GeneratedRegex(@"\[v(\d+)\]")]
    private static partial Regex VersionIn();

    [GeneratedRegex("^[A-Za-z0-9_]+ *= *([0-9A-Fa-f]{32}|[0-9A-Fa-f]{64})$")]
    private static partial Regex KeyLine();
}
