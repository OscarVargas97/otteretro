using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Otteretro.Libraries;

/// <param name="Type">crc32, md5, sha1, sha256 o sha512.</param>
public record Expected(string Type, string Hex);

/// <summary>Los hashes que trae un paquete, como los lee JDownloader: archivos `.sfv`, `.md5` y `.sha*`, o `[ABCD1234]` en el nombre (#202).</summary>
public static partial class Checksums
{
    static string? TypeOf(int length) => length switch { 8 => "crc32", 32 => "md5", 40 => "sha1", 64 => "sha256", 128 => "sha512", _ => null };

    /// <summary>Por nombre de archivo, lo que dicen los archivos de hashes de la carpeta.</summary>
    public static Dictionary<string, Expected> In(string folder)
    {
        var found = new Dictionary<string, Expected>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(folder).Where(f => Kind().IsMatch(f) && new FileInfo(f).Length < 512 * 1024))
            foreach (var line in File.ReadLines(file))
            {
                if (line.StartsWith(';') || Line().Match(line.Trim()) is not { Success: true } m) continue;
                // `.sfv`: nombre y CRC al final; `.md5` y `.sha*`: hash y nombre, a veces con `*`.
                var (hex, name) = m.Groups["h1"].Success ? (m.Groups["h1"].Value, m.Groups["n1"].Value) : (m.Groups["h2"].Value, m.Groups["n2"].Value);
                if (TypeOf(hex.Length) is { } type) found[Path.GetFileName(name.Trim().TrimStart('*').Replace('\\', '/'))] = new(type, hex.ToLowerInvariant());
            }
        return found;
    }

    public static Expected? InName(string name) => InBrackets().Match(name) is { Success: true } m ? new("crc32", m.Groups[1].Value.ToLowerInvariant()) : null;

    public static bool Matches(string path, Expected expected)
    {
        using var stream = File.OpenRead(path);
        var hex = expected.Type switch
        {
            "crc32" => Archives.Crc32(stream).ToString("x8"),
            "md5" => Convert.ToHexStringLower(MD5.HashData(stream)),
            "sha1" => Convert.ToHexStringLower(SHA1.HashData(stream)),
            "sha256" => Convert.ToHexStringLower(SHA256.HashData(stream)),
            _ => Convert.ToHexStringLower(SHA512.HashData(stream)),
        };
        return hex == expected.Hex;
    }

    [GeneratedRegex(@"\.(sfv|md5|sha1|sha256|sha512)$", RegexOptions.IgnoreCase)]
    private static partial Regex Kind();

    [GeneratedRegex(@"^(?:(?<h1>[0-9A-Fa-f]{32,128})\s+(?<n1>.+)|(?<n2>.+?)\s+(?<h2>[0-9A-Fa-f]{8}))$")]
    private static partial Regex Line();

    [GeneratedRegex(@"\[([0-9A-Fa-f]{8})\]")]
    private static partial Regex InBrackets();
}
