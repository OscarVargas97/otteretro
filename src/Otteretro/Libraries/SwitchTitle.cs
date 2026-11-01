using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Otteretro.Libraries;

/// <summary>
/// El TitleID de un NSP/NSZ o XCI/XCZ que no lo trae en el nombre (#213). Primero el ticket (`&lt;rights id&gt;.tik`, sin keys);
/// si no hay, la cabecera del NCA de metadatos, cifrada con la `header_key` de tus `prod.keys` (AES-128-XTS).
/// </summary>
public static class SwitchTitle
{
    /// <summary>La `header_key` del primer `prod.keys` que haya en esas carpetas; null si no hay.</summary>
    public static byte[]? HeaderKey(IEnumerable<string> folders)
    {
        foreach (var folder in folders.Where(Directory.Exists))
            foreach (var file in Directory.EnumerateFiles(folder, "prod.keys", new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive }))
                foreach (var line in File.ReadLines(file))
                {
                    var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 && parts[0] == "header_key" && parts[1].Length == 64) return Convert.FromHexString(parts[1]);
                }
        return null;
    }

    /// <summary>El TitleID en 16 dígitos hex; null si el archivo no tiene la forma esperada o falta la key.</summary>
    public static string? Read(string path, byte[]? headerKey)
    {
        try
        {
            using var file = File.OpenRead(path);
            var entries = Path.GetExtension(path).ToLowerInvariant() is ".xci" or ".xcz" ? Secure(file) : Partition(file, 0, "PFS0");
            if (entries is null) return null;
            if (entries.FirstOrDefault(e => e.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase) && e.Name.Length == 36) is { } ticket)
                return ticket.Name[..16].ToUpperInvariant();
            if (headerKey is null) return null;
            var nca = entries.FirstOrDefault(e => e.Name.Contains(".cnmt.nc", StringComparison.OrdinalIgnoreCase))
                ?? entries.FirstOrDefault(e => e.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase));
            if (nca is null) return null;
            var header = ReadAt(file, nca.Offset, 0x400);
            if (header is null) return null;
            XtsDecrypt(headerKey, header);
            if (Encoding.ASCII.GetString(header, 0x200, 3) != "NCA") return null;
            return BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(0x210)).ToString("X16");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public record Entry(string Name, long Offset, long Size);

    /// <summary>Los archivos de una partición PFS0 (NSP) o HFS0 (XCI) que empieza en <paramref name="start"/>, con su posición absoluta.</summary>
    static List<Entry>? Partition(Stream file, long start, string magic)
    {
        var head = ReadAt(file, start, 0x10);
        if (head is null || Encoding.ASCII.GetString(head, 0, 4) != magic) return null;
        var count = BinaryPrimitives.ReadInt32LittleEndian(head.AsSpan(4));
        var names = BinaryPrimitives.ReadInt32LittleEndian(head.AsSpan(8));
        var size = magic == "PFS0" ? 0x18 : 0x40;
        if (count is < 0 or > 10000 || names is < 0 or > 1 << 20) return null;
        var table = ReadAt(file, start + 0x10, count * size + names);
        if (table is null) return null;
        var data = start + 0x10 + count * size + names;
        var list = new List<Entry>();
        for (var i = 0; i < count; i++)
        {
            var e = table.AsSpan(i * size);
            var nameAt = count * size + BinaryPrimitives.ReadInt32LittleEndian(e[16..]);
            var end = Array.IndexOf(table, (byte)0, nameAt);
            list.Add(new(Encoding.UTF8.GetString(table, nameAt, (end < 0 ? table.Length : end) - nameAt),
                data + BinaryPrimitives.ReadInt64LittleEndian(e), BinaryPrimitives.ReadInt64LittleEndian(e[8..])));
        }
        return list;
    }

    /// <summary>La partición «secure» de un XCI, donde están los NCA del juego.</summary>
    static List<Entry>? Secure(Stream file)
    {
        var head = ReadAt(file, 0x100, 0x38);
        if (head is null || Encoding.ASCII.GetString(head, 0, 4) != "HEAD") return null;
        var root = Partition(file, BinaryPrimitives.ReadInt64LittleEndian(head.AsSpan(0x30)), "HFS0");
        return root?.FirstOrDefault(e => e.Name == "secure") is { } secure ? Partition(file, secure.Offset, "HFS0") : null;
    }

    static byte[]? ReadAt(Stream file, long offset, int length)
    {
        if (offset < 0 || offset + length > file.Length) return null;
        file.Position = offset;
        var buffer = new byte[length];
        file.ReadExactly(buffer);
        return buffer;
    }

    /// <summary>
    /// AES-128-XTS en sectores de 0x200 con el número de sector en big endian, como lo usa Nintendo (el estándar lo pone en
    /// little endian; para el sector 0 da igual). La primera mitad de la key cifra los datos y la segunda, el tweak.
    /// </summary>
    public static void XtsDecrypt(byte[] key, byte[] data, int sectorSize = 0x200)
    {
        using var dataKey = Aes.Create();
        dataKey.Key = key[..16];
        using var tweakKey = Aes.Create();
        tweakKey.Key = key[16..32];
        var tweak = new byte[16];
        var block = new byte[16];
        for (var sector = 0; sector * sectorSize < data.Length; sector++)
        {
            Array.Clear(tweak);
            BinaryPrimitives.WriteUInt64BigEndian(tweak.AsSpan(8), (ulong)sector);
            tweakKey.EncryptEcb(tweak, tweak, PaddingMode.None);
            for (var at = sector * sectorSize; at < Math.Min(data.Length, (sector + 1) * sectorSize); at += 16)
            {
                for (var i = 0; i < 16; i++) block[i] = (byte)(data[at + i] ^ tweak[i]);
                dataKey.DecryptEcb(block, block, PaddingMode.None);
                for (var i = 0; i < 16; i++) data[at + i] = (byte)(block[i] ^ tweak[i]);
                // tweak × α en GF(2^128), little endian.
                var carry = 0;
                for (var i = 0; i < 16; i++)
                {
                    var next = tweak[i] >> 7;
                    tweak[i] = (byte)((tweak[i] << 1) | carry);
                    carry = next;
                }
                if (carry != 0) tweak[0] ^= 0x87;
            }
        }
    }
}
