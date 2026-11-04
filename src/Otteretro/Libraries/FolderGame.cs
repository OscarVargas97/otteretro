using System.Buffers.Binary;
using System.Text.RegularExpressions;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>
/// Un juego de Switch o Switch 2 que llega ya extraído, como carpeta (#237): `exefs\main` y `romfs` al lado, con la base en `base\` y las
/// actualizaciones en `updates\&lt;versión&gt;\`. Es lo que cargan los emuladores de Switch 2: se guarda tal cual.
/// </summary>
/// <param name="Root">La carpeta del juego: la que tiene `base\`/`updates\`, o la que tiene `exefs\` si no hay esas.</param>
/// <param name="TitleId">El program id de `main.npdm`; null si no se pudo leer.</param>
/// <param name="Console">switch2 si el TitleID empieza con 04, switch si empieza con 01; null si no se sabe.</param>
public record SwitchDump(string Root, string? TitleId, string? Console)
{
    /// <summary>Las carpetas extraídas que hay dentro de <paramref name="folder"/>, una por juego.</summary>
    public static List<SwitchDump> Find(string folder)
    {
        if (!Directory.Exists(folder)) return [];
        var byRoot = new Dictionary<string, SwitchDump>(StringComparer.OrdinalIgnoreCase);
        foreach (var exefs in Directory.EnumerateDirectories(folder, "exefs", new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive }))
        {
            if (!File.Exists(Path.Combine(exefs, "main"))) continue;
            var root = RootOf(exefs);
            var titleId = ProgramId(Path.Combine(exefs, "main.npdm"));
            // La base manda: su TitleID es el del juego (el de una actualización también lo es, pero puede faltar el npdm).
            if (byRoot.TryGetValue(root, out var known) && known.TitleId is not null && !IsBase(exefs)) continue;
            byRoot[root] = new(root, titleId, ConsoleOf(titleId));
        }
        // Una carpeta dentro de otra ya encontrada no es otro juego.
        return byRoot.Values.Where(d => !byRoot.Keys.Any(r => !Disk.Same(r, d.Root) && d.Root.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(d => d.Root, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static bool IsBase(string exefs) => string.Equals(Path.GetFileName(Path.GetDirectoryName(exefs)), "base", StringComparison.OrdinalIgnoreCase);

    /// <summary>`…\Juego\base\exefs` y `…\Juego\updates\1.0\exefs` son `…\Juego`; un `…\Juego\exefs` suelto, también.</summary>
    static string RootOf(string exefs)
    {
        var parent = Path.GetDirectoryName(exefs)!;
        if (IsBase(exefs)) return Path.GetDirectoryName(parent)!;
        var grand = Path.GetDirectoryName(parent)!;
        return string.Equals(Path.GetFileName(grand), "updates", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(grand)! : parent;
    }

    static string? ConsoleOf(string? titleId) => titleId?[..2] switch { "04" => "switch2", "01" => "switch", _ => null };

    /// <summary>El program id de un `main.npdm` (cabecera META, sección ACI0), como 16 dígitos hexadecimales; null si no es uno.</summary>
    public static string? ProgramId(string npdm)
    {
        try
        {
            var data = File.ReadAllBytes(npdm);
            if (data.Length < 0x80 || !data.AsSpan(0, 4).SequenceEqual("META"u8)) return null;
            var aci = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x70));
            if (aci < 0 || aci + 0x18 > data.Length || !data.AsSpan(aci, 4).SequenceEqual("ACI0"u8)) return null;
            return BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(aci + 0x10)).ToString("X16");
        }
        catch (IOException) { return null; }
    }

    /// <summary>Lo que pesa la carpeta entera.</summary>
    public static long SizeOf(string root) =>
        Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true }).Sum(f => new FileInfo(f).Length);
}

