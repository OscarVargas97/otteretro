using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#240: lo que cada juego dice de sí mismo, leído de imágenes mínimas armadas acá, y reconocer sin IA con eso.</summary>
[Collection("Dats.Handler")]
public class GameIdsTests
{
    /// <summary>Un PARAM.SFO con sus claves de texto.</summary>
    static byte[] Sfo(params (string Key, string Value)[] entries)
    {
        var keys = new MemoryStream();
        var values = new MemoryStream();
        var index = new List<byte[]>();
        foreach (var (key, value) in entries)
        {
            var entry = new byte[16];
            BinaryPrimitives.WriteUInt16LittleEndian(entry, (ushort)keys.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(2), 0x0204);
            var data = Encoding.UTF8.GetBytes(value + "\0");
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(4), (uint)data.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(8), (uint)data.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(12), (uint)values.Length);
            keys.Write(Encoding.ASCII.GetBytes(key + "\0"));
            values.Write(data);
            index.Add(entry);
        }
        var head = new byte[0x14];
        "\0PSF"u8.CopyTo(head);
        var keyStart = 0x14 + index.Count * 16;
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(0x08), (uint)keyStart);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(0x0C), (uint)(keyStart + keys.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(0x10), (uint)index.Count);
        return [.. head, .. index.SelectMany(e => e), .. keys.ToArray(), .. values.ToArray()];
    }

    /// <summary>Una imagen ISO 9660 mínima: archivos en la raíz y en una carpeta de un nivel («PSP_GAME/PARAM.SFO»).</summary>
    static byte[] Iso(params (string Path, byte[] Data)[] files)
    {
        var sectors = new List<byte[]>();
        for (var i = 0; i < 20; i++) sectors.Add(new byte[2048]);
        byte[] Record(string name, long lba, long size, bool folder)
        {
            var id = Encoding.ASCII.GetBytes(name);
            var record = new byte[33 + id.Length + (id.Length % 2 == 0 ? 1 : 0)];
            record[0] = (byte)record.Length;
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(2), (uint)lba);
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(10), (uint)size);
            record[25] = (byte)(folder ? 2 : 0);
            record[32] = (byte)id.Length;
            id.CopyTo(record, 33);
            return record;
        }
        long Add(byte[] data)
        {
            var lba = sectors.Count;
            for (var at = 0; at < Math.Max(1, data.Length); at += 2048)
            {
                var sector = new byte[2048];
                Array.Copy(data, at, sector, 0, Math.Min(2048, data.Length - at));
                sectors.Add(sector);
            }
            return lba;
        }
        byte[] Directory(IEnumerable<byte[]> records) => [.. records.SelectMany(r => r)];
        var rootRecords = new List<byte[]>();
        foreach (var group in files.GroupBy(f => f.Path.Contains('/') ? f.Path.Split('/')[0] : ""))
            if (group.Key == "") foreach (var (path, data) in group) rootRecords.Add(Record(path + ";1", Add(data), data.Length, false));
            else
            {
                var inner = Directory(group.Select(f => Record(f.Path.Split('/')[1] + ";1", Add(f.Data), f.Data.Length, false)));
                rootRecords.Add(Record(group.Key, Add(inner), 2048, true));
            }
        var root = Directory(rootRecords);
        var rootLba = Add(root);
        var pvd = sectors[16];
        pvd[0] = 1;
        "CD001"u8.CopyTo(pvd.AsSpan(1));
        Record("\0", rootLba, 2048, true).CopyTo(pvd, 156);
        return [.. sectors.SelectMany(s => s)];
    }

    /// <summary>La misma imagen en crudo, como un .bin de PS1 (Mode 2: 24 bytes antes de cada sector de datos).</summary>
    static byte[] Raw(byte[] iso)
    {
        var raw = new byte[iso.Length / 2048 * 2352];
        for (var i = 0; i < iso.Length / 2048; i++) Array.Copy(iso, i * 2048, raw, i * 2352 + 24, 2048);
        return raw;
    }

    /// <summary>La imagen comprimida como CSO: un bloque deflate por sector, con su índice.</summary>
    static byte[] Cso(byte[] iso)
    {
        var count = iso.Length / 2048;
        var blocks = Enumerable.Range(0, count).Select(i =>
        {
            using var m = new MemoryStream();
            using (var d = new DeflateStream(m, CompressionLevel.Optimal, true)) d.Write(iso, i * 2048, 2048);
            return m.ToArray();
        }).ToList();
        var head = new byte[0x18];
        "CISO"u8.CopyTo(head);
        BinaryPrimitives.WriteInt64LittleEndian(head.AsSpan(8), iso.Length);
        BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(0x10), 2048);
        var index = new byte[(count + 1) * 4];
        var at = 0x18 + index.Length;
        for (var i = 0; i <= count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(index.AsSpan(i * 4), (uint)at);
            if (i < count) at += blocks[i].Length;
        }
        return [.. head, .. index, .. blocks.SelectMany(b => b)];
    }

    static string Save(Fixture f, string name, byte[] data)
    {
        var path = f.Tmp[name];
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
        return path;
    }

    [Fact]
    public async Task PlayStationDiscsSayTheirSerial()
    {
        await using var f = new Fixture();
        var ps2 = Iso(("SYSTEM.CNF", "BOOT2 = cdrom0:\\SLUS_200.62;1\nVER = 1.00\n"u8.ToArray()));
        Assert.Equal(new GameId("ps2", "SLUS-20062", null), GameIds.FromFile(Save(f, "gta3.iso", ps2)));

        var ps1 = Raw(Iso(("SYSTEM.CNF", "BOOT = cdrom:\\SCUS_944.55;1\r\n"u8.ToArray())));
        Save(f, "ff7.bin", ps1);
        File.WriteAllText(f.Tmp["ff7.cue"], "FILE \"ff7.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        Assert.Equal(new GameId("ps1", "SCUS-94455", null), GameIds.FromFile(f.Tmp["ff7.cue"]));

        var psp = Iso(("UMD_DATA.BIN", "ULUS-10041|0001|G"u8.ToArray()), ("PSP_GAME/PARAM.SFO", Sfo(("DISC_ID", "ULUS10041"), ("TITLE", "Lumines"))));
        Assert.Equal(new GameId("psp", "ULUS-10041", "Lumines"), GameIds.FromFile(Save(f, "lumines.iso", psp)));
        Assert.Equal(new GameId("psp", "ULUS-10041", "Lumines"), GameIds.FromFile(Save(f, "lumines.cso", Cso(psp))));

        var ps3 = Iso(("PS3_GAME/PARAM.SFO", Sfo(("TITLE_ID", "BLUS30001"), ("TITLE", "Resistance: Fall of Man"))));
        Assert.Equal(new GameId("ps3", "BLUS-30001", "Resistance: Fall of Man"), GameIds.FromFile(Save(f, "resistance.iso", ps3)));

        Assert.Null(GameIds.FromFile(Save(f, "nada.iso", new byte[64 * 1024])));
    }

    [Fact]
    public async Task PackagesAndCartridgesSayTheirIds()
    {
        await using var f = new Fixture();
        // PS4: el param.sfo va en la tabla de entradas del .pkg.
        var sfo = Sfo(("TITLE_ID", "CUSA00001"), ("TITLE", "The Last of Us Remastered"));
        var pkg = new byte[0x200 + sfo.Length];
        BinaryPrimitives.WriteUInt32BigEndian(pkg, 0x7F434E54);
        BinaryPrimitives.WriteUInt32BigEndian(pkg.AsSpan(0x10), 1);
        BinaryPrimitives.WriteUInt32BigEndian(pkg.AsSpan(0x18), 0x100);
        Encoding.ASCII.GetBytes("UP9000-CUSA00001_00-THELASTOFUS00000").CopyTo(pkg, 0x40);
        BinaryPrimitives.WriteUInt32BigEndian(pkg.AsSpan(0x100), 0x1000);
        BinaryPrimitives.WriteUInt32BigEndian(pkg.AsSpan(0x110), 0x200);
        BinaryPrimitives.WriteUInt32BigEndian(pkg.AsSpan(0x114), (uint)sfo.Length);
        sfo.CopyTo(pkg, 0x200);
        Assert.Equal(new GameId("ps4", "CUSA-00001", "The Last of Us Remastered"), GameIds.FromFile(Save(f, "tlou.pkg", pkg)));

        // Vita: un .vpk es un zip con sce_sys/param.sfo.
        using (var zip = ZipFile.Open(f.Tmp["persona.vpk"], ZipArchiveMode.Create))
        using (var w = zip.CreateEntry("sce_sys/param.sfo").Open()) w.Write(Sfo(("TITLE_ID", "PCSE00120"), ("TITLE", "Persona 4 Golden")));
        Assert.Equal(new GameId("vita", "PCSE-00120", "Persona 4 Golden"), GameIds.FromFile(f.Tmp["persona.vpk"]));

        // 3DS: la partición del juego (NCCH) en 0x200 * el valor de 0x120.
        var cci = new byte[0x600];
        "NCSD"u8.CopyTo(cci.AsSpan(0x100));
        BinaryPrimitives.WriteUInt32LittleEndian(cci.AsSpan(0x120), 2);
        "NCCH"u8.CopyTo(cci.AsSpan(0x400 + 0x100));
        "CTR-P-AREE"u8.CopyTo(cci.AsSpan(0x400 + 0x150));
        Assert.Equal(new GameId("3ds", "CTR-P-AREE", null), GameIds.FromFile(Save(f, "mk7.3ds", cci)));

        var nds = new byte[0x200];
        "AMCE"u8.CopyTo(nds.AsSpan(0x0C));
        Assert.Equal(new GameId("ds", "AMCE", null), GameIds.FromFile(Save(f, "mk.nds", nds)));
        var gba = new byte[0x200];
        "AMKE"u8.CopyTo(gba.AsSpan(0xAC));
        Assert.Equal(new GameId("gba", "AMKE", null), GameIds.FromFile(Save(f, "mksc.gba", gba)));
        var z64 = new byte[0x40];
        z64[0] = 0x80;
        "NSME"u8.CopyTo(z64.AsSpan(0x3B));
        Assert.Equal(new GameId("n64", "NSME", null), GameIds.FromFile(Save(f, "sm64.z64", z64)));
    }

    [Fact]
    public async Task FolderGamesSayTheirNames()
    {
        await using var f = new Fixture();
        Save(f, @"juegos\Resistance\PS3_GAME\PARAM.SFO", Sfo(("TITLE_ID", "BLUS30001"), ("TITLE", "Resistance: Fall of Man")));
        Save(f, @"juegos\Resistance\PS3_GAME\USRDIR\EBOOT.BIN", [1]);
        Save(f, @"juegos\Astro\sce_sys\param.json", Encoding.UTF8.GetBytes("""
            { "titleId": "PPSA01325", "localizedParameters": { "defaultLanguage": "en-US", "en-US": { "titleName": "Astro's Playroom" } } }
            """));
        Save(f, @"juegos\Zelda WW\meta\meta.xml", Encoding.UTF8.GetBytes("""
            <?xml version="1.0"?><menu><title_id type="hexBinary">0005000010143400</title_id><longname_en type="string">The Legend of Zelda
            The Wind Waker HD</longname_en></menu>
            """));
        var found = FolderGame.Find(f.Tmp["juegos"]).ToDictionary(g => Path.GetFileName(g.Root), g => (g.Console, g.Name));
        Assert.Equal(("ps3", "Resistance: Fall of Man"), found["Resistance"]);
        Assert.Equal(("ps5", "Astro's Playroom"), found["Astro"]);
        Assert.Equal(("wiiu", "The Legend of Zelda The Wind Waker HD"), found["Zelda WW"]);
    }

    sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new HttpRequestException("sin red");
    }

    /// <summary>
    /// #249 (decisión 278): el código que publica el catálogo va primero y no necesita tablas ni red: un .nsp de Switch y un ISO de PS2
    /// con nombres que no dicen nada se reconocen por su TitleID y su serial.
    /// </summary>
    [Fact]
    public async Task TheCatalogCodeComesFirstWithoutTablesOrNetwork()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO game (id, name_es) VALUES (11, 'The Legend of Zelda: Breath of the Wild'), (30, 'Grand Theft Auto III');
                INSERT INTO version (id, game_id, platform) VALUES (21, 11, 'switch'), (40, 30, 'ps2');
                INSERT INTO version_code (version_id, code) VALUES (21, '01007EF00011E000'), (40, 'SLUS-20062');
                """);
        Save(f, @"Descargas\x [01007EF00011E000][v0].nsp", "juego"u8.ToArray());
        Save(f, @"Descargas\disco.iso", Iso(("SYSTEM.CNF", "BOOT2 = cdrom0:\\SLUS_200.62;1\n"u8.ToArray())));
        Dats.Handler = new NoNetwork();
        try { Assert.Equal("done", (await f.Run(Import.Enqueue(f.Db, f.Tmp["Descargas"]).Task!.Value)).Status); }
        finally { Dats.Handler = null; }

        using var db = f.Db.Open();
        Assert.Equal(21, db.ExecuteScalar<long>("SELECT version_id FROM source WHERE title_id = '01007EF00011E000'"));
        Assert.Equal(40, db.ExecuteScalar<long>("SELECT version_id FROM source WHERE title_id = 'SLUS-20062'"));
    }

    /// <summary>Un ISO de PS2 con un nombre que no dice nada se reconoce por su serial en el DAT, sin Claude.</summary>
    [Fact]
    public async Task ADiscIsRecognizedByItsSerialInTheDat()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO game (id, name_es) VALUES (30, 'Grand Theft Auto III'); INSERT INTO version (id, game_id, platform) VALUES (40, 30, 'ps2');
                INSERT INTO dat_entry (console, source, name, sha1, serial) VALUES ('ps2', 'redump', 'Grand Theft Auto III (USA)', 'otro', 'SLUS-20062');
                """);
        Save(f, @"Descargas\disco-1.iso", Iso(("SYSTEM.CNF", "BOOT2 = cdrom0:\\SLUS_200.62;1\n"u8.ToArray())));
        Dats.Handler = new NoNetwork();
        try { Assert.Equal("done", (await f.Run(Import.Enqueue(f.Db, f.Tmp["Descargas"]).Task!.Value)).Status); }
        finally { Dats.Handler = null; }

        Assert.True(File.Exists(f.Tmp[@"Otteretro\Juegos\PlayStation 2\Grand Theft Auto III\disco-1.iso"]));
        using var db = f.Db.Open();
        Assert.Equal((40L, "SLUS-20062"), db.QuerySingle<(long, string)>("SELECT version_id, title_id FROM source WHERE relative_path LIKE '%disco-1%'"));
    }
}
