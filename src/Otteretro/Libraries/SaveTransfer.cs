using System.Buffers.Binary;
using System.Text;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <summary>Qué pasó con las partidas al cambiar de emulador (decisión 288).</summary>
/// <param name="Code">saves.copied (llevadas, con respaldo de lo que había), saves.none (no hay partida que llevar),
/// saves.needsFirstRun (el otro emulador todavía no tiene dónde ponerla: abrir el juego una vez ahí), saves.chooseUser (Eden o Citron
/// tienen varios usuarios de consola: la persona elige a cuál, entre <paramref name="Users"/>; no se cambió nada), saves.untested (el formato
/// parece el mismo pero no está probado: se avisa y no se copia) o saves.unknown (no hay forma conocida).</param>
/// <param name="Backup">Dónde quedó lo que había en el emulador de destino antes de reemplazarlo.</param>
/// <param name="Users">Con saves.chooseUser: los usuarios de consola del emulador de destino.</param>
public record SaveNotice(string Code, string? Backup = null, List<string>? Users = null);

/// <summary>
/// Las partidas de un juego al pasar de un emulador a otro (decisión 288): se copian si el formato es el mismo, se llevan a su
/// lugar si hay forma conocida (Switch: Eden y Citron guardan como yuzu, Ryubing en `bis` con su índice `imkvdb.arc`), siempre con
/// respaldo de lo que se reemplaza, y si no se puede, se avisa. Nunca se toca la partida de origen.
/// </summary>
public static class SaveTransfer
{
    static readonly string[] Yuzu = ["eden", "citron"];
    static readonly string[] Ryujinx = ["ryubing", "ryujinx-ss4k"];
    const string YuzuSaves = "user/nand/user/save/0000000000000000";
    const string RyujinxSaves = "portable/bis/user/save";
    const string RyujinxIndex = "portable/bis/system/save/8000000000000000/0/imkvdb.arc";

    /// <summary>
    /// Lleva la partida de <paramref name="titleId"/> de un emulador al otro. Null si no hay nada que decir (el mismo emulador). Si Eden
    /// o Citron tienen más de un usuario de consola y <paramref name="user"/> no es uno de ellos, no toca nada y pide elegir; con uno
    /// solo, va ahí.
    /// </summary>
    public static SaveNotice? Transfer(Database db, string from, string? fromInstall, string to, string? toInstall, string? titleId, string? user = null)
    {
        if (from == to) return null;
        var gc = new[] { "dolphin", "dusklight" };
        if (gc.Contains(from) && gc.Contains(to)) return new("saves.untested"); // .gci en los dos: por probar con un archivo real
        bool Switch(string e) => Yuzu.Contains(e) || Ryujinx.Contains(e);
        if (!Switch(from) || !Switch(to) || titleId is null) return new("saves.unknown");
        if (fromInstall is null || Find(from, fromInstall, titleId) is not { } source) return new("saves.none");
        if (toInstall is not null && Yuzu.Contains(to) && Users(toInstall) is { Count: > 1 } users)
        {
            if (!users.Contains(user ?? "", StringComparer.OrdinalIgnoreCase)) return new("saves.chooseUser", Users: users);
            source = source with { User = users.First(u => u.Equals(user, StringComparison.OrdinalIgnoreCase)) };
        }
        if (toInstall is null || Place(to, toInstall, titleId, source.User) is not { } target) return new("saves.needsFirstRun");
        var backup = Directory.Exists(target) && Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories).Any()
            ? Path.Combine(Profiles.Root(db), Profiles.BackupsFolder, "partidas", $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{to}-{titleId}") : null;
        // Ryubing guarda en `<id>\0` (lo confirmado) y a veces `<id>\1` (lo de trabajo): se respaldan y se reemplazan los dos.
        var replace = Ryujinx.Contains(to) ? new[] { target, Path.Combine(Path.GetDirectoryName(target)!, "1") }.Where(d => d == target || Directory.Exists(d)).ToList() : [target];
        if (backup is not null) Profiles.CopyTree(Ryujinx.Contains(to) ? Path.GetDirectoryName(target)! : target, backup);
        foreach (var dir in replace)
        {
            if (Directory.Exists(dir)) Links.DeleteTree(dir);
            Profiles.CopyTree(source.Path, dir);
        }
        return new("saves.copied", backup);
    }

    /// <summary>La carpeta de la partida en el emulador de origen, con el usuario de la consola si es de yuzu; null si no hay.</summary>
    static (string Path, string? User)? Find(string emulator, string install, string titleId)
    {
        if (Yuzu.Contains(emulator))
        {
            var root = Path.Combine(install, YuzuSaves);
            if (!Directory.Exists(root)) return null;
            foreach (var user in Directory.EnumerateDirectories(root))
            {
                var dir = Path.Combine(user, titleId.ToUpperInvariant());
                if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Any()) return (dir, Path.GetFileName(user));
            }
            return null;
        }
        return SaveId(install, titleId) is { } id && Path.Combine(install, RyujinxSaves, id, "0") is var d
            && Directory.Exists(d) && Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories).Any() ? (d, null) : null;
    }

    /// <summary>Los usuarios de consola de Eden o Citron: las carpetas de su NAND con partidas.</summary>
    static List<string> Users(string install) => Path.Combine(install, YuzuSaves) is var root && Directory.Exists(root)
        ? Directory.GetDirectories(root).Select(d => Path.GetFileName(d)).Order().ToList() : [];

    /// <summary>
    /// Dónde va la partida en el emulador de destino. Eden y Citron: el usuario elegido o, si tiene uno solo, ese; sin ninguno, el
    /// mismo de la partida si viene de yuzu. Ryubing: la carpeta que su índice le asignó al juego. Null si el emulador todavía no la
    /// creó (abrir el juego una vez).
    /// </summary>
    static string? Place(string emulator, string install, string titleId, string? user)
    {
        if (Yuzu.Contains(emulator))
        {
            var users = Users(install);
            if (users.Count == 1) user = users[0];
            return user is null ? null : Path.Combine(install, YuzuSaves, user, titleId.ToUpperInvariant());
        }
        return SaveId(install, titleId) is { } id ? Path.Combine(install, RyujinxSaves, id, "0") : null;
    }

    /// <summary>
    /// El id de la partida de cuenta de un juego en el índice de Ryujinx (`imkvdb.arc`, el de LibHac): «IMKV», reservado, cantidad;
    /// cada entrada «IMEN», tamaño de la clave y del valor, la clave (SaveDataAttribute: ProgramId en 0, tipo en 0x20, 1 = cuenta) y
    /// el valor (SaveDataId en 0). Devuelve el nombre de su carpeta, en 16 hex minúsculas.
    /// </summary>
    /// <remarks>ponytail: el formato sale del código de LibHac y de ryujinx_compat.cpp de Eden; sin probar con un índice real.</remarks>
    public static string? SaveId(string install, string titleId)
    {
        var file = Path.Combine(install, RyujinxIndex);
        if (!File.Exists(file) || !ulong.TryParse(titleId, System.Globalization.NumberStyles.HexNumber, null, out var program)) return null;
        var data = File.ReadAllBytes(file);
        if (data.Length < 12 || Encoding.ASCII.GetString(data, 0, 4) != "IMKV") return null;
        var count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8));
        var at = 12;
        for (var i = 0; i < count && at + 12 <= data.Length; i++)
        {
            if (Encoding.ASCII.GetString(data, at, 4) != "IMEN") return null;
            var keySize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at + 4));
            var valueSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at + 8));
            var key = at + 12;
            if (key + keySize + valueSize > data.Length || keySize < 0x21 || valueSize < 8) return null;
            if (BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(key)) == program && data[key + 0x20] == 1)
                return BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(key + keySize)).ToString("x16");
            at = key + keySize + valueSize;
        }
        return null;
    }
}
