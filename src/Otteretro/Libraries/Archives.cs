using System.Text.RegularExpressions;
using Otteretro.Api;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Otteretro.Libraries;

/// <summary>Descomprimir (§9.5, #230): zip, 7z y rar, en varias partes y con contraseña, dentro de la app, sin 7-Zip.</summary>
public static partial class Archives
{
    /// <summary>Es comprimido, sea la parte que abre el conjunto o una de las demás.</summary>
    public static bool IsArchive(string file) => AnyPart().IsMatch(Path.GetFileName(file));

    /// <summary>Es la parte por la que se abre: `x.zip`, `x.7z`, `x.rar`, `x.part1.rar` o `x.7z.001`.</summary>
    public static bool IsMain(string file) => IsArchive(file) && !LaterPart().IsMatch(Path.GetFileName(file));

    /// <summary>El primer archivo de juego de adentro, solo con el índice; null si no se puede leer sin contraseña.</summary>
    public static string? FirstGame(string main)
    {
        try
        {
            using var archive = ArchiveFactory.OpenArchive(Parts(main).Select(p => new FileInfo(p)).ToList(), new ReaderOptions());
            return archive.Entries.Where(e => !e.IsDirectory).Select(e => e.Key)
                .FirstOrDefault(k => k is not null && Scan.GameExtensions.Contains(Path.GetExtension(k)) && !IsArchive(k));
        }
        catch (Exception e) when (e is not OperationCanceledException) { return null; }
    }

    /// <summary>Todas las partes del comprimido, empezando por la que lo abre.</summary>
    public static List<string> Parts(string main)
    {
        try { return ArchiveFactory.GetFileParts(main).ToList(); }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException) { return [main]; }
    }

    /// <summary>
    /// Extrae todo en <paramref name="target"/>, que se vacía antes de cada intento. Sin contraseña y, si está cifrado, con cada una
    /// de la lista. Cada archivo se compara con su tamaño y su CRC. Devuelve null si salió bien; si no, el error y target queda vacío.
    /// </summary>
    public static ApiError? Extract(string main, string target, IReadOnlyList<string> passwords, CancellationToken ct)
    {
        ApiError failure = ApiError.Of("import.log.archiveFailed", ("path", main));
        foreach (var password in passwords.Prepend(null))
        {
            ct.ThrowIfCancellationRequested();
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            Directory.CreateDirectory(target);
            var encrypted = false;
            try
            {
                var parts = Parts(main).Select(p => new FileInfo(p)).ToList();
                using var archive = ArchiveFactory.OpenArchive(parts, new ReaderOptions { Password = password });
                encrypted = archive.IsEncrypted;
                foreach (var (entry, open) in Entries(archive))
                {
                    ct.ThrowIfCancellationRequested();
                    encrypted |= entry.IsEncrypted;
                    if (entry.IsDirectory || entry.LinkTarget is not null) continue;
                    var to = Path.GetFullPath(Path.Combine(target, entry.Key!));
                    // Un nombre con `..` no sale de la carpeta.
                    if (!Disk.Inside(to, target)) throw new InvalidDataException(entry.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                    var crc = 0xFFFFFFFFu;
                    long length = 0;
                    using (var from = open())
                    using (var file = File.Create(to))
                    {
                        var buffer = new byte[1 << 20];
                        for (int n; (n = from.Read(buffer)) > 0; length += n)
                        {
                            ct.ThrowIfCancellationRequested();
                            crc = Update(crc, buffer.AsSpan(0, n));
                            file.Write(buffer, 0, n);
                        }
                    }
                    if ((entry.Size > 0 && entry.Size != length) || (entry.Crc != 0 && entry.Crc != (long)~crc))
                        throw new InvalidDataException(entry.Key);
                }
                return null;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failure = ApiError.Of(encrypted ? "import.log.password" : "import.log.archiveFailed", ("path", main));
                if (!encrypted) break; // sin cifrar, otra contraseña no lo arregla
            }
        }
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        return failure;
    }

    /// <summary>Lo sólido y los 7z se leen de corrido; lo demás no lo permite y se lee entrada por entrada.</summary>
    static IEnumerable<(IEntry Entry, Func<Stream> Open)> Entries(IArchive archive)
    {
        if (!archive.IsSolid && archive.Type != ArchiveType.SevenZip)
        {
            foreach (var entry in archive.Entries) yield return (entry, entry.OpenEntryStream);
            yield break;
        }
        using var reader = archive.ExtractAllEntries();
        while (reader.MoveToNextEntry()) yield return (reader.Entry, reader.OpenEntryStream);
    }

    static readonly uint[] Table = Enumerable.Range(0, 256).Select(i =>
    {
        var c = (uint)i;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    /// <summary>El CRC32 de todo un archivo (para los `.sfv`, #202).</summary>
    public static uint Crc32(Stream stream)
    {
        var crc = 0xFFFFFFFFu;
        var buffer = new byte[1 << 20];
        for (int n; (n = stream.Read(buffer)) > 0;) crc = Update(crc, buffer.AsSpan(0, n));
        return ~crc;
    }

    static uint Update(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    [GeneratedRegex(@"\.(zip|7z|rar|7z\.\d{3}|r\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex AnyPart();

    /// <summary>`x.part2.rar`, `x.r00`, `x.7z.002`: se abren desde la primera.</summary>
    [GeneratedRegex(@"(\.part0*([2-9]|[1-9]\d+)\.rar|\.r\d{2}|\.7z\.(?!0*1$)\d{3})$", RegexOptions.IgnoreCase)]
    private static partial Regex LaterPart();
}
