using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#178: el escaneo de Colección por nombre y TitleID, con BASE, UPD y DLC.</summary>
public class ScanTests
{
    /// <summary>Tus 7 juegos de Switch, con los nombres de tus archivos.</summary>
    static readonly string[] SwitchFiles =
    [
        @"Hyrule Warriors - Age of Calamity\Hyrule Warriors - Age of Calamity [BASE] [01002B00111A2000][v0].nsp",
        @"Hyrule Warriors - Age of Calamity\Hyrule Warriors - Age of Calamity [DLC] [01002B00111A3001][v0] Digital Version Preorder Bonus.nsp",
        @"Hyrule Warriors - Age of Calamity\Hyrule Warriors - Age of Calamity [DLC] [01002B00111A3002][v0] Expansion Pass - Purchase Bonus.nsp",
        @"Hyrule Warriors - Age of Calamity\Hyrule Warriors - Age of Calamity [DLC] [01002B00111A3003][v0] Pulse of the Ancients.nsp",
        @"Hyrule Warriors - Age of Calamity\Hyrule Warriors - Age of Calamity [DLC] [01002B00111A3004][v0] Guardian of Remembrance.nsp",
        @"Hyrule Warriors - Age of Calamity\Hyrule Warriors - Age of Calamity [UPD] [01002B00111A2800][v327680][1.3.0].nsp",
        @"Hyrule Warriors - Age of Calamity\Hyrule Warriors - Age of Calamity [UPD] [01002B00111A2800][v458752][1.4.1].nsp",
        @"Hyrule Warriors - Definitive Edition\Hyrule Warriors - Definitive Edition [BASE] [0100AE00096EA000][v0].nsp",
        @"Hyrule Warriors - Definitive Edition\Hyrule Warriors - Definitive Edition [UPD] [0100AE00096EA800][v65536][1.0.1].nsp",
        @"The Legend of Zelda Breath of the Wild\The Legend of Zelda Breath of the Wild [BASE] [01007EF00011E000][v0].nsp",
        @"The Legend of Zelda Breath of the Wild\The Legend of Zelda Breath of the Wild [DLC] [01007EF00011F001][v196608] DLC Pack 1.nsp",
        @"The Legend of Zelda Breath of the Wild\The Legend of Zelda Breath of the Wild [DLC] [01007EF00011F002][v196608] DLC Pack 2.nsp",
        @"The Legend of Zelda Breath of the Wild\The Legend of Zelda Breath of the Wild [UPD] [01007EF00011E800][v1114112][1.9.0].nsp",
        @"The Legend of Zelda Echoes of Wisdom\The Legend of Zelda Echoes of Wisdom [BASE] [01008CF01BAAC000][v0].nsp",
        @"The Legend of Zelda Echoes of Wisdom\The Legend of Zelda Echoes of Wisdom [UPD] [01008CF01BAAC800][v196608][1.1.0].nsp",
        @"The Legend of Zelda Link’s Awakening\The Legend of Zelda Link’s Awakening [BASE] [01006BB00C6F0000][v0].nsp",
        @"The Legend of Zelda Link’s Awakening\The Legend of Zelda Link’s Awakening [UPD] [01006BB00C6F0800][v131072][1.1.0].nsp",
        @"The Legend of Zelda Skyward Sword HD\The Legend of Zelda Skyward Sword HD [BASE] [01002DA013484000][v0].nsp",
        @"The Legend of Zelda Skyward Sword HD\The Legend of Zelda Skyward Sword HD [UPD] [01002DA013484800][v65536][1.0.1].nsp",
        @"The Legend of Zelda Tears of the Kingdom\The Legend of Zelda Tears of the Kingdom [BASE] [0100F2C0115B6000][v0].nsp",
        @"The Legend of Zelda Tears of the Kingdom\The Legend of Zelda Tears of the Kingdom [UPD] [0100F2C0115B6800][v393216][1.2.1].nsp",
        @"The Legend of Zelda Tears of the Kingdom\The Legend of Zelda Tears of the Kingdom [UPD] [0100F2C0115B6800][v589824][1.4.1].nsp",
        @"The Legend of Zelda Tears of the Kingdom\The Legend of Zelda Tears of the Kingdom [UPD] [0100F2C0115B6800][v655360][1.4.2].nsp",
        @"The Legend of Zelda Tears of the Kingdom\The Legend of Zelda Tears of the Kingdom [UPD] [0100F2C0115B6800][v720896][1.4.3].nsp",
    ];

    [Fact]
    public async Task YourSwitchGamesComeOutWithBaseUpdAndDlc()
    {
        await using var f = new Fixture();
        var (disk, _, _) = Library.Adopt(f.Db, Directory.CreateDirectory(f.Tmp[@"e\Otteretro"]).FullName, "E:", "disk");
        var games = f.Tmp[@"e\Otteretro\Juegos"];
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO game (id, name_es) VALUES (1, 'Hyrule Warriors: La era del cataclismo'), (2, 'Hyrule Warriors: Definitive Edition'),
                    (3, 'The Legend of Zelda: Breath of the Wild'), (4, 'The Legend of Zelda: Echoes of Wisdom'), (5, 'The Legend of Zelda: Link''s Awakening'),
                    (6, 'The Legend of Zelda: Skyward Sword HD'), (7, 'The Legend of Zelda: Tears of the Kingdom'), (8, 'The Legend of Zelda: Twilight Princess'),
                    (9, 'Doble'), (10, 'Doble');
                UPDATE game SET original_name = 'Hyrule Warriors: Age of Calamity' WHERE id = 1;
                INSERT INTO version (id, game_id, platform) VALUES (11, 1, 'switch'), (12, 2, 'switch'), (13, 3, 'switch'), (14, 4, 'switch'), (15, 5, 'switch'),
                    (16, 6, 'switch'), (17, 7, 'switch'), (18, 8, 'gc'), (19, 3, 'wiiu'), (20, 9, 'gc'), (21, 10, 'gc');
                """);
        foreach (var file in SwitchFiles) Write(Path.Combine(games, "Switch", file));
        Write(Path.Combine(games, @"Switch\Raro\Raro [BASE] [0100AAAA00000000][v0].nsp"));
        Write(Path.Combine(games, @"Switch\Raro\léeme.txt"));
        Write(Path.Combine(games, @"GameCube\The Legend of Zelda Twilight Princess\The Legend of Zelda Twilight Princess (Europe) (En,Fr,De,Es,It).iso"), GameCubeHeader("GZ2P01"));
        Write(Path.Combine(games, @"GameCube\Doble (USA).iso"), GameCubeHeader("XXXE01"));

        var scanned = await f.Run(Scan.Enqueue(f.Db));
        Assert.True(scanned.Status == "done", System.Text.Json.JsonSerializer.Serialize(scanned.Detail));

        using (var c = f.Db.Open())
        {
            var rows = c.Query<(string Path, string Console, string? Component, string? TitleId, long? Version, long Size)>(
                "SELECT relative_path, console, component, title_id, version_id, size FROM source WHERE kind = 'file'").ToList();
            Assert.Equal(SwitchFiles.Length + 3, rows.Count);
            var expected = new Dictionary<string, (long Version, int Base, int Upd, int Dlc)>
            {
                ["Hyrule Warriors - Age of Calamity"] = (11, 1, 2, 4),
                ["Hyrule Warriors - Definitive Edition"] = (12, 1, 1, 0),
                ["The Legend of Zelda Breath of the Wild"] = (13, 1, 1, 2),
                ["The Legend of Zelda Echoes of Wisdom"] = (14, 1, 1, 0),
                ["The Legend of Zelda Link’s Awakening"] = (15, 1, 1, 0),
                ["The Legend of Zelda Skyward Sword HD"] = (16, 1, 1, 0),
                ["The Legend of Zelda Tears of the Kingdom"] = (17, 1, 4, 0),
            };
            foreach (var (game, (version, b, u, d)) in expected)
            {
                var mine = rows.Where(r => r.Path.Contains($@"\Switch\{game}\")).ToList();
                Assert.All(mine, r => Assert.Equal(version, r.Version));
                Assert.Equal((b, u, d), (mine.Count(r => r.Component == "BASE"), mine.Count(r => r.Component == "UPD"), mine.Count(r => r.Component == "DLC")));
            }
            var unknown = Assert.Single(rows, r => r.Path.Contains("Raro"));
            Assert.Equal(("switch", "BASE", "0100AAAA00000000", (long?)null), (unknown.Console, unknown.Component, unknown.TitleId, unknown.Version));
            var tp = Assert.Single(rows, r => r.Path.Contains("Twilight"));
            Assert.Equal(("gc", "GZ2P01", (long?)18, @"Juegos\GameCube\The Legend of Zelda Twilight Princess\The Legend of Zelda Twilight Princess (Europe) (En,Fr,De,Es,It).iso"),
                (tp.Console, tp.TitleId, tp.Version, tp.Path));
            Assert.Null(Assert.Single(rows, r => r.Path.Contains("Doble")).Version); // dos juegos con ese nombre: queda para Claude

            // Una versión puesta a mano no se pisa al volver a escanear.
            c.Execute("UPDATE source SET version_id = 20 WHERE relative_path LIKE '%Doble%'");
        }
        File.Delete(Path.Combine(games, "Switch", SwitchFiles[^1]));
        Assert.Equal("done", (await f.Run(Scan.Enqueue(f.Db))).Status);
        using (var c = f.Db.Open())
        {
            Assert.Equal(SwitchFiles.Length + 2, c.ExecuteScalar<long>("SELECT count(*) FROM source"));
            Assert.Equal(20, c.ExecuteScalar<long>("SELECT version_id FROM source WHERE relative_path LIKE '%Doble%'"));
            Assert.Equal(disk, c.ExecuteScalar<string>("SELECT DISTINCT library_id FROM source"));
        }
        Assert.True(File.Exists(Path.Combine(games, @"Switch\Raro\léeme.txt"))); // nunca borra archivos
    }

    [Fact]
    public void ComponentsAndBaseTitleIds()
    {
        Assert.Equal(("BASE", "01007EF00011E000"), (Scan.ComponentOf("01007EF00011E000"), Scan.BaseOf("01007EF00011E000")));
        Assert.Equal(("UPD", "01007EF00011E000"), (Scan.ComponentOf("01007EF00011E800"), Scan.BaseOf("01007EF00011E800")));
        Assert.Equal(("DLC", "01007EF00011E000"), (Scan.ComponentOf("01007EF00011F001"), Scan.BaseOf("01007EF00011F001")));
        Assert.Equal("the legend of zelda links awakening", Scan.Normalize("The Legend of Zelda: Link’s Awakening (Europe) [BASE]"));
        Assert.Equal("pokemon edicion amarilla", Scan.Normalize("Pokémon: Edición Amarilla"));
    }

    /// <summary>IEEE 1619, vector 1 (keys en cero, sector 0): el descifrado XTS da ceros.</summary>
    [Fact]
    public void XtsMatchesTheStandardVector()
    {
        var data = Convert.FromHexString("917cf69ebd68b2ec9b9fe9a3eadda692cd43d2f59598ed858c02c2652fbf922e");
        SwitchTitle.XtsDecrypt(new byte[32], data);
        Assert.All(data, b => Assert.Equal(0, b));
    }

    /// <summary>#213: sin TitleID en el nombre, sale del ticket o, con tus prod.keys, de la cabecera del NCA.</summary>
    [Fact]
    public async Task ReadsTheTitleIdInsideTheFile()
    {
        await using var f = new Fixture();
        var key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        var withTicket = f.Tmp["ticket.nsp"];
        File.WriteAllBytes(withTicket, Pfs0(("0100F2C0115B60000000000000000000.tik", new byte[0x2C0])));
        Assert.Equal("0100F2C0115B6000", SwitchTitle.Read(withTicket, null));

        Library.Adopt(f.Db, Directory.CreateDirectory(f.Tmp[@"e\Otteretro"]).FullName, "E:", "disk");
        Write(f.Tmp[@"e\Otteretro\Sistema\Switch\Keys\prod.keys"], Encoding.ASCII.GetBytes($"master_key_00 = {new string('0', 32)}\nheader_key = {Convert.ToHexString(key)}\n"));
        File.WriteAllBytes(Directory.CreateDirectory(f.Tmp[@"e\Otteretro\Juegos\Switch\Sin nombre"]).FullName + @"\juego.nsp",
            Pfs0(("datos.nca", new byte[0x500]), ("abcd.cnmt.nca", NcaHeader(key, 0x01007EF00011E800))));
        Assert.Null(SwitchTitle.Read(f.Tmp[@"e\Otteretro\Juegos\Switch\Sin nombre\juego.nsp"], null));

        Assert.Equal("done", (await f.Run(Scan.Enqueue(f.Db))).Status);
        using var c = f.Db.Open();
        Assert.Equal(("01007EF00011E800", "UPD"), c.QuerySingle<(string, string)>("SELECT title_id, component FROM source"));
    }

    static void Write(string path, byte[]? content = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content ?? Encoding.UTF8.GetBytes(path));
    }

    static byte[] GameCubeHeader(string id)
    {
        var head = new byte[0x440];
        Encoding.ASCII.GetBytes(id).CopyTo(head, 0);
        new byte[] { 0xC2, 0x33, 0x9F, 0x3D }.CopyTo(head, 0x1C);
        return head;
    }

    static byte[] Pfs0(params (string Name, byte[] Data)[] files)
    {
        var names = new MemoryStream();
        var table = new MemoryStream();
        long offset = 0;
        foreach (var (name, data) in files)
        {
            var entry = new byte[0x18];
            BinaryPrimitives.WriteInt64LittleEndian(entry, offset);
            BinaryPrimitives.WriteInt64LittleEndian(entry.AsSpan(8), data.Length);
            BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(16), (int)names.Length);
            table.Write(entry);
            names.Write(Encoding.UTF8.GetBytes(name + "\0"));
            offset += data.Length;
        }
        var head = new byte[0x10];
        Encoding.ASCII.GetBytes("PFS0").CopyTo(head, 0);
        BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(4), files.Length);
        BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(8), (int)names.Length);
        return [.. head, .. table.ToArray(), .. names.ToArray(), .. files.SelectMany(x => x.Data)];
    }

    /// <summary>Una cabecera de NCA cifrada como la de Nintendo: «NCA3» en 0x200 y el TitleID en 0x210.</summary>
    static byte[] NcaHeader(byte[] key, ulong titleId)
    {
        var header = new byte[0xC00];
        Encoding.ASCII.GetBytes("NCA3").CopyTo(header, 0x200);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x210), titleId);
        using var dataKey = Aes.Create();
        dataKey.Key = key[..16];
        using var tweakKey = Aes.Create();
        tweakKey.Key = key[16..];
        for (var sector = 0; sector < header.Length / 0x200; sector++)
        {
            var tweak = new byte[16];
            BinaryPrimitives.WriteUInt64BigEndian(tweak.AsSpan(8), (ulong)sector);
            tweakKey.EncryptEcb(tweak, tweak, PaddingMode.None);
            for (var at = sector * 0x200; at < (sector + 1) * 0x200; at += 16)
            {
                var block = new byte[16];
                for (var i = 0; i < 16; i++) block[i] = (byte)(header[at + i] ^ tweak[i]);
                dataKey.EncryptEcb(block, block, PaddingMode.None);
                for (var i = 0; i < 16; i++) header[at + i] = (byte)(block[i] ^ tweak[i]);
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
        return header;
    }
}
