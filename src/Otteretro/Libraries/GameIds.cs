using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Otteretro.Libraries;

/// <summary>Lo que un juego dice de sí mismo (#240): su consola, su serial (el de los DAT de libretro) y, si lo trae, su nombre.</summary>
public record GameId(string? Console, string? Serial, string? Name);

/// <summary>
/// Reconocer sin IA (#240, decisión 276): se lee el serial o el nombre que trae cada juego en su cabecera o en sus archivos de sistema,
/// para cruzarlo con el serial de los DAT (PS1, PS2, PSP, PS3, Vita, 3DS, DS, GBA, N64) o directamente con el catálogo (PS4, PS5, Wii U).
/// Lo que no se puede leer da null y sigue el camino de siempre (ScreenScraper, Claude, Revisar).
/// </summary>
public static partial class GameIds
{
    /// <summary>De un archivo: la ROM, la imagen de disco (.iso, .bin, .cue, .cso), el .pkg o el .vpk.</summary>
    public static GameId? FromFile(string path)
    {
        try
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".nds" => Cartridge(path, "ds", 0x0C),
                ".gba" => Cartridge(path, "gba", 0xAC),
                ".z64" or ".n64" or ".v64" => N64(path),
                ".3ds" or ".cci" => Ncsd(path),
                ".cia" => Cia(path),
                ".pkg" => Pkg(path),
                ".vpk" => Vpk(path),
                ".cso" => Disc(Cso.Open(path)),
                ".iso" => Disc(Sectors.Open(path)),
                ".bin" => Disc(Sectors.Open(path)),
                ".cue" => CueBin(path) is { } bin ? Disc(Sectors.Open(bin)) : null,
                _ => null,
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or JsonException) { return null; }
    }

    /// <summary>
    /// Juegos que vienen como carpeta, sin archivo que los diga: PS3 (`PS3_GAME\PARAM.SFO`), Vita y PS4 (`sce_sys\param.sfo`), PS5
    /// (`sce_sys\param.json`) y Wii U (`meta\meta.xml`). Cada uno con su carpeta raíz.
    /// </summary>
    public static List<(string Root, GameId Id)> Folders(string folder)
    {
        var found = new List<(string, GameId)>();
        if (!Directory.Exists(folder)) return found;
        var options = new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive };
        try
        {
            foreach (var sfo in Directory.EnumerateFiles(folder, "PARAM.SFO", options))
            {
                var parent = Path.GetDirectoryName(sfo)!;
                var name = Path.GetFileName(parent);
                var root = name.Equals("PS3_GAME", StringComparison.OrdinalIgnoreCase) || name.Equals("sce_sys", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetDirectoryName(parent)! : null;
                if (root is null || Sfo(File.ReadAllBytes(sfo)) is not { } id) continue;
                found.Add((root, id));
            }
            foreach (var json in Directory.EnumerateFiles(folder, "param.json", options))
                if (Path.GetFileName(Path.GetDirectoryName(json))!.Equals("sce_sys", StringComparison.OrdinalIgnoreCase) && Ps5(File.ReadAllText(json)) is { } id)
                    found.Add((Path.GetDirectoryName(Path.GetDirectoryName(json))!, id));
            foreach (var meta in Directory.EnumerateFiles(folder, "meta.xml", options))
                if (Path.GetFileName(Path.GetDirectoryName(meta))!.Equals("meta", StringComparison.OrdinalIgnoreCase) && WiiU(File.ReadAllText(meta)) is { } id)
                    found.Add((Path.GetDirectoryName(Path.GetDirectoryName(meta))!, id));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        // Una carpeta dentro de otra ya encontrada (la actualización de un juego, por ejemplo) no es otro juego.
        return found.Where(f => !found.Any(o => !Disk.Same(o.Item1, f.Item1) && f.Item1.StartsWith(o.Item1 + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(f => f.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // —— Cartuchos ——

    /// <summary>DS y GBA: el código de juego de 4 letras de la cabecera, como lo lleva el DAT («BKAJ»).</summary>
    static GameId? Cartridge(string path, string console, int at)
    {
        var head = Head(path, at + 4);
        return Code(head, at, 4) is { } code ? new(console, code, null) : null;
    }

    /// <summary>N64: «N» + el id de dos letras + la región, en 0x3B. Las .v64 y .n64 vienen con los bytes cambiados de lugar.</summary>
    static GameId? N64(string path)
    {
        var head = Head(path, 0x40);
        if (head.Length < 0x40) return null;
        if (head[0] == 0x37 && head[1] == 0x80) for (var i = 0; i + 1 < head.Length; i += 2) (head[i], head[i + 1]) = (head[i + 1], head[i]); // .v64
        else if (head[0] == 0x40 && head[1] == 0x12) for (var i = 0; i + 3 < head.Length; i += 4) Array.Reverse(head, i, 4); // .n64
        return head[0] == 0x80 && Code(head, 0x3B, 4) is { } code ? new("n64", code, null) : null;
    }

    /// <summary>3DS en .3ds/.cci: la partición del juego (NCCH) trae el código de producto («CTR-P-BP4J»), que es el serial del DAT.</summary>
    static GameId? Ncsd(string path)
    {
        using var s = File.OpenRead(path);
        var head = Read(s, 0, 0x200);
        if (Encoding.ASCII.GetString(head, 0x100, 4) != "NCSD") return null;
        var ncch = (long)BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(0x120)) * 0x200;
        return Ncch(Read(s, ncch, 0x200));
    }

    /// <summary>3DS en .cia: el primer contenido es el NCCH del juego, después de los encabezados alineados a 64 bytes.</summary>
    static GameId? Cia(string path)
    {
        using var s = File.OpenRead(path);
        var head = Read(s, 0, 0x20);
        static long Align(long n) => (n + 63) / 64 * 64;
        var sizes = Enumerable.Range(0, 5).Select(i => (long)BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(i == 0 ? 0 : 4 + i * 4))).ToArray();
        var (header, certs, ticket, tmd) = (sizes[0], sizes[1], sizes[2], sizes[3]);
        var content = Align(header) + Align(certs) + Align(ticket) + Align(tmd);
        return Ncch(Read(s, content, 0x200));
    }

    static GameId? Ncch(byte[] ncch) =>
        ncch.Length >= 0x160 && Encoding.ASCII.GetString(ncch, 0x100, 4) == "NCCH" && Code(ncch, 0x150, 16) is { } product ? new("3ds", product, null) : null;

    // —— PlayStation ——

    /// <summary>PS4 (.pkg, magia «\x7FCNT»): el `param.sfo` va sin cifrar en la tabla de entradas. PS3 (.pkg, «\x7FPKG»): el id del contenido.</summary>
    static GameId? Pkg(string path)
    {
        using var s = File.OpenRead(path);
        var head = Read(s, 0, 0x80);
        var magic = BinaryPrimitives.ReadUInt32BigEndian(head);
        if (magic == 0x7F504B47) // PS3: «UP0001-BLUS30001_00-…» en 0x30
            return Code(head, 0x30, 36) is { } content && content.Length > 16 ? new("ps3", Dashed(content[7..16]), null) : null;
        if (magic != 0x7F434E54) return null;
        var count = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(0x10));
        var table = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(0x18));
        var serial = Code(head, 0x40, 36) is { Length: > 16 } id ? Dashed(id[7..16]) : null;
        var entries = Read(s, table, (int)Math.Min(count, 4096) * 32);
        for (var i = 0; i + 32 <= entries.Length; i += 32)
            if (BinaryPrimitives.ReadUInt32BigEndian(entries.AsSpan(i)) == 0x1000) // param.sfo
            {
                var at = BinaryPrimitives.ReadUInt32BigEndian(entries.AsSpan(i + 16));
                var size = BinaryPrimitives.ReadUInt32BigEndian(entries.AsSpan(i + 20));
                if (size is > 0 and < 1 << 20 && Sfo(Read(s, at, (int)size)) is { } sfo) return sfo with { Serial = sfo.Serial ?? serial };
            }
        return serial is null ? null : new("ps4", serial, null);
    }

    /// <summary>Vita (.vpk, un zip): su `sce_sys/param.sfo`.</summary>
    static GameId? Vpk(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        if (zip.GetEntry("sce_sys/param.sfo") is not { } entry) return null;
        using var memory = new MemoryStream();
        using (var e = entry.Open()) e.CopyTo(memory);
        return Sfo(memory.ToArray());
    }

    /// <summary>
    /// Un `PARAM.SFO` (PS3, PSP, Vita, PS4): su TITLE_ID (o DISC_ID) y su TITLE. La consola sale del prefijo del id: CUSA/PLAS → PS4,
    /// PCS → Vita, UL/UC/NP?H/NP?G → PSP; lo demás (BL, BC, NP?B) → PS3.
    /// </summary>
    internal static GameId? Sfo(byte[] data)
    {
        if (data.Length < 0x14 || !data.AsSpan(0, 4).SequenceEqual("\0PSF"u8)) return null;
        var keys = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x08));
        var values = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x0C));
        var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x10));
        var found = new Dictionary<string, string>();
        for (var i = 0; i < count && 0x14 + i * 16 + 16 <= data.Length; i++)
        {
            var at = 0x14 + i * 16;
            var key = Zero(data, keys + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at)));
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 4));
            var offset = values + (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 12));
            if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at + 2)) != 0x0404 && offset + length <= data.Length)
                found[key] = Encoding.UTF8.GetString(data, offset, length).TrimEnd('\0').Trim();
        }
        var id = found.GetValueOrDefault("TITLE_ID") ?? found.GetValueOrDefault("DISC_ID");
        if (id is null) return null;
        // PSP de tienda: NP?H y NP?G; PS3 de tienda: NP?B y NP?A.
        var console = id.StartsWith("CUSA") || id.StartsWith("PLAS") ? "ps4"
            : id.StartsWith("PCS") ? "vita"
            : id.StartsWith("UL") || id.StartsWith("UC") || (id.StartsWith("NP") && id.Length > 3 && id[3] is 'H' or 'G') ? "psp"
            : "ps3";
        return new(console, Dashed(id), found.GetValueOrDefault("TITLE"));
    }

    /// <summary>PS5 en carpeta: `sce_sys\param.json`, con su titleId y el nombre en el idioma por defecto.</summary>
    static GameId? Ps5(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var id = root.TryGetProperty("titleId", out var t) ? t.GetString() : null;
        string? name = null;
        if (root.TryGetProperty("localizedParameters", out var local))
        {
            var language = local.TryGetProperty("defaultLanguage", out var l) ? l.GetString() : "en-US";
            foreach (var key in new[] { language, "en-US", "es-ES" })
                if (key is not null && local.TryGetProperty(key, out var p) && p.TryGetProperty("titleName", out var n)) { name = n.GetString(); break; }
        }
        return id is null && name is null ? null : new("ps5", id is null ? null : Dashed(id), name);
    }

    /// <summary>Wii U en carpeta: `meta\meta.xml` trae el nombre en inglés (o en español).</summary>
    static GameId? WiiU(string xml)
    {
        var name = Tag(xml, "longname_en") ?? Tag(xml, "longname_es") ?? Tag(xml, "shortname_en");
        return name is null ? null : new("wiiu", Tag(xml, "title_id"), name.Replace('\n', ' '));
    }

    static string? Tag(string xml, string tag) =>
        Regex.Match(xml, $@"<{tag}[^>]*>([^<]+)</{tag}>") is { Success: true } m ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : null;

    /// <summary>
    /// Discos de PlayStation (ISO 9660): PSP por `UMD_DATA.BIN`, PS3 por `PS3_GAME\PARAM.SFO`, PS1 y PS2 por `SYSTEM.CNF` (BOOT2 es PS2).
    /// GameCube y Wii no pasan por acá: los lee <see cref="Scan.Serial"/>.
    /// </summary>
    static GameId? Disc(Sectors? disc)
    {
        if (disc is null) return null;
        using (disc)
        {
            if (Iso.Find(disc, "UMD_DATA.BIN") is { } umd && Encoding.ASCII.GetString(umd).Split('|')[0] is { Length: >= 9 } psp)
            {
                var name = Iso.Find(disc, "PSP_GAME/PARAM.SFO") is { } sfo ? Sfo(sfo)?.Name : null;
                return new("psp", Dashed(psp.Replace("-", "")), name);
            }
            if (Iso.Find(disc, "PS3_GAME/PARAM.SFO") is { } ps3) return Sfo(ps3) is { } id ? id with { Console = "ps3" } : null;
            if (Iso.Find(disc, "SYSTEM.CNF") is not { } cnf) return null;
            var text = Encoding.ASCII.GetString(cnf);
            if (Boot().Match(text) is not { Success: true } m) return null;
            // «cdrom0:\SLUS_200.62;1» → «SLUS-20062»
            return new(m.Groups[1].Value == "BOOT2" ? "ps2" : "ps1", Dashed(m.Groups[2].Value.Replace("_", "").Replace(".", "")), null);
        }
    }

    [GeneratedRegex(@"(BOOT2?)\s*=\s*cdrom0?:\\?([A-Z]{4}[_-]?\d{3}\.?\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex Boot();

    /// <summary>El .bin que nombra un .cue (el primero).</summary>
    static string? CueBin(string cue)
    {
        var m = Regex.Match(File.ReadAllText(cue), "FILE\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return m.Success ? Path.Combine(Path.GetDirectoryName(cue)!, m.Groups[1].Value) : null;
    }

    // —— Ayudas ——

    /// <summary>«SLUS20062» → «SLUS-20062», como el serial de los DAT.</summary>
    static string Dashed(string id) => id.Length > 4 && char.IsAsciiLetter(id[3]) && char.IsAsciiDigit(id[4]) && !id.Contains('-') ? $"{id[..4]}-{id[4..]}" : id;

    static string? Code(byte[] data, int at, int length)
    {
        if (data.Length < at + length) return null;
        var code = Encoding.ASCII.GetString(data, at, length).TrimEnd('\0', ' ');
        return code.Length > 0 && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? code : null;
    }

    static string Zero(byte[] data, int at)
    {
        var end = Array.IndexOf(data, (byte)0, at);
        return Encoding.ASCII.GetString(data, at, (end < 0 ? data.Length : end) - at);
    }

    static byte[] Head(string path, int length)
    {
        using var s = File.OpenRead(path);
        return Read(s, 0, length);
    }

    static byte[] Read(Stream s, long at, int length)
    {
        var buffer = new byte[length];
        s.Seek(at, SeekOrigin.Begin);
        var read = s.ReadAtLeast(buffer, length, throwOnEndOfStream: false);
        return read == length ? buffer : buffer[..read];
    }

    /// <summary>Sectores de 2048 bytes de una imagen: .iso tal cual, .bin en crudo (2352 bytes, datos desde el byte 24 o 16), .cso comprimido.</summary>
    internal class Sectors : IDisposable
    {
        protected readonly Stream Stream;
        readonly int size, skip;

        protected Sectors(Stream stream, int size, int skip) => (Stream, this.size, this.skip) = (stream, size, skip);

        public static Sectors? Open(string path)
        {
            var s = File.OpenRead(path);
            // El descriptor primario está en el sector 16: «CD001» en 1. En crudo, Mode 2 (PS1, PS2 en CD) lleva 24 bytes antes; Mode 1, 16.
            foreach (var (size, skip) in new[] { (2048, 0), (2352, 24), (2352, 16) })
                if (Read(s, 16L * size + skip, 6) is { Length: 6 } d && Encoding.ASCII.GetString(d, 1, 5) == "CD001") return new(s, size, skip);
            s.Dispose();
            return null;
        }

        public virtual byte[] Sector(long lba) => Read(Stream, lba * size + skip, 2048);
        public void Dispose() => Stream.Dispose();
    }

    /// <summary>CSO (PSP): bloques comprimidos con deflate, con su índice al principio.</summary>
    sealed class Cso : Sectors
    {
        readonly uint[] index;
        readonly int shift;

        Cso(Stream stream, uint[] index, int shift) : base(stream, 2048, 0) => (this.index, this.shift) = (index, shift);

        public static new Sectors? Open(string path)
        {
            var s = File.OpenRead(path);
            var head = Read(s, 0, 0x18);
            if (head.Length < 0x18 || Encoding.ASCII.GetString(head, 0, 4) != "CISO") { s.Dispose(); return null; }
            var total = BinaryPrimitives.ReadInt64LittleEndian(head.AsSpan(8));
            var block = BinaryPrimitives.ReadInt32LittleEndian(head.AsSpan(0x10));
            var shift = head[0x15];
            if (block != 2048) { s.Dispose(); return null; }
            var count = (int)Math.Min(total / block + 1, 1 << 22);
            var raw = Read(s, 0x18, count * 4);
            var index = new uint[raw.Length / 4];
            for (var i = 0; i < index.Length; i++) index[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(i * 4));
            var cso = new Cso(s, index, shift);
            return cso.Sector(16) is { Length: 2048 } pvd && Encoding.ASCII.GetString(pvd, 1, 5) == "CD001" ? cso : null;
        }

        public override byte[] Sector(long lba)
        {
            if (lba + 1 >= index.Length) return [];
            var (start, end) = (index[lba] & 0x7FFFFFFF, index[lba + 1] & 0x7FFFFFFF);
            var raw = Read(Stream, (long)start << shift, (int)(((long)end << shift) - ((long)start << shift)));
            if ((index[lba] & 0x80000000) != 0) return raw[..Math.Min(raw.Length, 2048)]; // sin comprimir
            using var inflate = new DeflateStream(new MemoryStream(raw), CompressionMode.Decompress);
            var sector = new byte[2048];
            inflate.ReadAtLeast(sector, 2048, throwOnEndOfStream: false);
            return sector;
        }
    }

    /// <summary>Lo mínimo de ISO 9660 para leer un archivo chico por su ruta.</summary>
    static class Iso
    {
        public static byte[]? Find(Sectors disc, string path)
        {
            var pvd = disc.Sector(16);
            if (pvd.Length < 2048) return null;
            var (lba, size) = Extent(pvd, 156);
            foreach (var part in path.Split('/'))
            {
                if (Entry(disc, lba, size, part) is not { } next) return null;
                (lba, size) = next;
            }
            if (size > 1 << 20) return null; // solo archivos chicos (SYSTEM.CNF, PARAM.SFO, UMD_DATA.BIN)
            var data = new byte[size];
            for (var read = 0L; read < size; read += 2048)
            {
                var sector = disc.Sector(lba + read / 2048);
                Array.Copy(sector, 0, data, read, Math.Min(Math.Min(2048, sector.Length), size - read));
            }
            return data;
        }

        static (long Lba, long Size) Extent(byte[] record, int at) =>
            (BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(at + 2)), BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(at + 10)));

        static (long, long)? Entry(Sectors disc, long lba, long size, string name)
        {
            for (var read = 0L; read < size && read < 1 << 20; read += 2048)
            {
                var sector = disc.Sector(lba + read / 2048);
                for (var at = 0; at < sector.Length && sector[at] > 0; at += sector[at])
                {
                    var length = sector[at + 32];
                    if (at + 33 + length > sector.Length) break;
                    var id = Encoding.ASCII.GetString(sector, at + 33, length).Split(';')[0];
                    if (id.Equals(name, StringComparison.OrdinalIgnoreCase)) return Extent(sector, at);
                }
            }
            return null;
        }
    }
}
