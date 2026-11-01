using System.Security.Cryptography;
using Dapper;
using Otteretro.Api;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <param name="Version">La carpeta de esa versión; null si el archivo está suelto.</param>
public record SystemFile(string Name, string? Version, long Size, string Modified, string Disk, string Path);

/// <param name="Missing">Lo que le falta para jugar lo que tienes de esta consola.</param>
public record SystemConsole(string Id, string Name, List<SystemFile> Files, List<string> Missing);

/// <summary>
/// BIOS, keys y firmware (§8.5, #199): los subes tú y quedan en `Sistema\&lt;Consola&gt;\&lt;archivo&gt;\&lt;versión&gt;\` del disco ★. La app no los
/// descarga ni los comparte. Subir otra versión nunca reemplaza la anterior (P7).
/// </summary>
public static partial class SystemFiles
{
    /// <summary>Lo que hace falta para jugar, por consola. Se completa con las recetas de cada emulador en la F5.</summary>
    static readonly Dictionary<string, string[]> Needed = new()
    {
        ["switch"] = ["prod.keys"],
    };

    public static List<SystemConsole> List(Database db)
    {
        using var c = db.Open();
        var consoles = c.Query<(string Id, string Name)>("SELECT id, name FROM console").ToDictionary(k => k.Id, k => k.Name);
        var owned = c.Query<string>("SELECT DISTINCT console FROM source WHERE kind = 'file' AND console IS NOT NULL").ToHashSet();
        var files = new Dictionary<string, List<SystemFile>>();
        foreach (var folder in Disk.ConsoleFolders(db, "system"))
        {
            if (Disk.Get(db, folder.LibraryId) is not { } disk) continue;
            var root = System.IO.Path.Combine(disk.Path, folder.Relative);
            foreach (var file in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true }))
            {
                var info = new FileInfo(file);
                var version = System.IO.Path.GetDirectoryName(file)!;
                // `<archivo>\<versión>\<archivo>`: su carpeta lleva el mismo nombre dos niveles arriba.
                var versioned = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(version)) == info.Name;
                (files.TryGetValue(folder.Console, out var list) ? list : files[folder.Console] = [])
                    .Add(new(info.Name, versioned ? System.IO.Path.GetFileName(version) : null, info.Length, info.LastWriteTimeUtc.ToString("O"), disk.Name, file));
            }
        }
        return consoles.Where(k => files.ContainsKey(k.Key) || (owned.Contains(k.Key) && Needed.ContainsKey(k.Key)))
            .Select(k =>
            {
                var mine = files.GetValueOrDefault(k.Key) ?? [];
                var missing = owned.Contains(k.Key)
                    ? Needed.GetValueOrDefault(k.Key, []).Where(n => !mine.Any(f => string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase))).ToList()
                    : [];
                return new SystemConsole(k.Key, k.Value, mine.OrderBy(f => f.Name).ThenByDescending(f => f.Modified).ToList(), missing);
            }).OrderBy(k => k.Name).ToList();
    }

    /// <summary>Guarda lo subido en una carpeta de versión nueva. Si ya tienes ese mismo archivo, no suma nada.</summary>
    public static async Task<ApiError?> Save(Database db, string console, string name, Stream content, CancellationToken ct)
    {
        using (var c = db.Open())
            if (c.ExecuteScalar<long>("SELECT count(*) FROM console WHERE id = @console", new { console }) == 0) return ApiError.Of("system.badConsole");
        name = System.IO.Path.GetFileName(name ?? "");
        if (name.Length == 0 || name.StartsWith('.') || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) return ApiError.Of("system.badName");
        if (Disk.DefaultDisk(db) is not { Connected: true } disk) return ApiError.Of("library.notConnected");

        var root = System.IO.Path.Combine(disk.Path, Disk.DiskFolders["system"], ConsoleFolder(db, disk, console), name);
        Directory.CreateDirectory(root);
        var temp = System.IO.Path.Combine(root, System.IO.Path.GetRandomFileName());
        try
        {
            string hash;
            await using (var file = File.Create(temp))
            {
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[1 << 20];
                for (int n; (n = await content.ReadAsync(buffer, ct)) > 0;)
                {
                    sha.AppendData(buffer, 0, n);
                    await file.WriteAsync(buffer.AsMemory(0, n), ct);
                }
                hash = Convert.ToHexStringLower(sha.GetHashAndReset())[..12];
            }
            App.FolderMove.FlushToDisk(temp);
            var version = Directory.GetDirectories(root, $"*-{hash}").FirstOrDefault();
            if (version is not null) return null; // ya lo tenías
            version = System.IO.Path.Combine(root, $"{DateTime.Now:yyyy-MM-dd}-{hash}");
            Directory.CreateDirectory(version);
            File.Move(temp, System.IO.Path.Combine(version, name));
            return null;
        }
        catch (IOException e)
        {
            return ApiError.Of("folder.createFailed", ("detail", e.Message));
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    static string ConsoleFolder(Database db, DiskInfo disk, string console)
    {
        if (Disk.ConsoleFolders(db, "system").FirstOrDefault(f => f.LibraryId == disk.Id && f.Console == console) is { } found) return System.IO.Path.GetFileName(found.Relative);
        using var c = db.Open();
        var name = c.ExecuteScalar<string>("SELECT name FROM console WHERE id = @console", new { console })!;
        return name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 ? console : name;
    }
}
