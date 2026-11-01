using System.Net;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#179: DAT de No-Intro y Redump y verificación por hash.</summary>
public class DatTests
{
    const string GameCubeDat = """
        clrmamepro (
        	name "Nintendo - GameCube"
        	version "2026.10.07"
        )

        game (
        	name "Legend of Zelda, The - Twilight Princess (Europe) (En,Fr,De,Es,It)"
        	region "Europe"
        	serial "DL-DOL-GZ2P-EUR"
        	rom ( name "Legend of Zelda, The - Twilight Princess (Europe) (En,Fr,De,Es,It).iso" size 1459978240 crc 61FE14DA md5 F9C47B0C8A34D345C06061F24E0DB277 sha1 2601822A488EEB86FB89DB16CA8F29C2C953E1CA serial "DL-DOL-GZ2P-EUR" )
        )
        game (
        	name "Legend of Zelda, The - Twilight Princess (USA)"
        	region "USA"
        	rom ( name "Legend of Zelda, The - Twilight Princess (USA).iso" size 1459978240 crc 0AB54D43 sha1 75edd3ddff41f125d1b4ce1a40378f1b565519e7 )
        )
        """;

    [Fact]
    public void ReadsAClrmameproDat()
    {
        var entries = Dats.Parse(new StringReader(GameCubeDat)).ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal(new Dats.Entry("Legend of Zelda, The - Twilight Princess (Europe) (En,Fr,De,Es,It)", "2601822A488EEB86FB89DB16CA8F29C2C953E1CA",
            "61FE14DA", 1459978240, "DL-DOL-GZ2P-EUR", "Europe"), entries[0]);
        Assert.Equal(("75EDD3DDFF41F125D1B4CE1A40378F1B565519E7", null, "USA"), (entries[1].Sha1, entries[1].Serial, entries[1].Region));
        Assert.Equal(Scan.Normalize("The Legend of Zelda: Twilight Princess"), Scan.Normalize(entries[0].Name));
    }

    [Fact]
    public async Task TwilightPrincessComesOutVerifiedByRedump()
    {
        await using var f = new Fixture();
        Library.Adopt(f.Db, Directory.CreateDirectory(f.Tmp[@"e\Otteretro"]).FullName, "E:", "disk");
        var tp = Disc("GZ2P01", 1);
        var copy = Disc("GZ2P01", 2); // otro volcado: mismo serial, distinto hash
        Write(f.Tmp[@"e\Otteretro\Juegos\GameCube\The Legend of Zelda Twilight Princess\The Legend of Zelda Twilight Princess (Europe) (En,Fr,De,Es,It).iso"], tp);
        Write(f.Tmp[@"e\Otteretro\Juegos\GameCube\copia\tp.iso"], copy);
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO game (id, name_es) VALUES (8, 'The Legend of Zelda: Twilight Princess');
                INSERT INTO version (id, game_id, platform) VALUES (18, 8, 'gc');
                INSERT INTO dat_entry (console, source, name, sha1, serial) VALUES
                    ('gc', 'redump', 'Legend of Zelda, The - Twilight Princess (Europe) (En,Fr,De,Es,It)', @sha1, 'DL-DOL-GZ2P-EUR');
                """, new { sha1 = Convert.ToHexString(SHA1.HashData(tp)) });

        Assert.Equal("done", (await f.Run(Scan.Enqueue(f.Db))).Status);
        using (var c = f.Db.Open()) // «tp.iso» no dice su nombre: sale por el serial del disco en el DAT
            Assert.Equal([18L, 18L], c.Query<long>("SELECT version_id FROM source ORDER BY relative_path"));

        var task = await f.Run(Dats.EnqueueVerify(f.Db, null, 8));
        Assert.Equal("done", task.Status);
        using (var c = f.Db.Open())
        {
            var rows = c.Query<(string Path, string Sha1, bool Verified)>("SELECT relative_path, sha1, verified FROM source ORDER BY relative_path").ToList();
            Assert.Equal((Convert.ToHexString(SHA1.HashData(tp)), true), (rows.Single(r => r.Path.Contains("Europe")).Sha1, rows.Single(r => r.Path.Contains("Europe")).Verified));
            Assert.False(rows.Single(r => r.Path.Contains("tp.iso")).Verified);
        }
        Assert.Contains(Otteretro.Catalog.CatalogQueries.Game(f.Db, 8)!.Versions.Single().Files, x => x.Verified && x.Path.EndsWith(".iso"));

        // Volver a escanear con el mismo archivo conserva el hash y lo verificado.
        Assert.Equal("done", (await f.Run(Scan.Enqueue(f.Db))).Status);
        using (var c = f.Db.Open()) Assert.Equal(1, c.ExecuteScalar<long>("SELECT count(*) FROM source WHERE verified = 1"));
    }

    [Fact]
    public async Task DownloadsTheDatsFromLibretro()
    {
        await using var f = new Fixture();
        var asked = new List<string>();
        Dats.Handler = new Fake(asked);
        try
        {
            Assert.Equal("done", (await f.Run(Dats.EnqueueUpdate(f.Db))).Status);
        }
        finally { Dats.Handler = null; }
        Assert.Contains("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/redump/Nintendo%20-%20GameCube.dat", asked);
        using var c = f.Db.Open();
        Assert.Equal(2 * Dats.Files.Values.Sum(v => v.Length), c.ExecuteScalar<long>("SELECT count(*) FROM dat_entry"));
    }

    sealed class Fake(List<string> asked) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (asked) asked.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(GameCubeDat) });
        }
    }

    static byte[] Disc(string id, byte fill)
    {
        var data = Enumerable.Repeat(fill, 0x8000).ToArray();
        Encoding.ASCII.GetBytes(id).CopyTo(data, 0);
        new byte[] { 0xC2, 0x33, 0x9F, 0x3D }.CopyTo(data, 0x1C);
        return data;
    }

    static void Write(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }
}
