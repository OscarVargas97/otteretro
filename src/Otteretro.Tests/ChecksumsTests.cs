using System.Security.Cryptography;
using System.Text;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#202, decisión 249: cada archivo se compara con su tamaño y con el hash que traiga el paquete, como JDownloader.</summary>
public class ChecksumsTests
{
    static readonly byte[] Rom = Encoding.ASCII.GetBytes("una rom de prueba");

    static string Folder()
    {
        var folder = Directory.CreateTempSubdirectory("otteretro-hash").FullName;
        File.WriteAllBytes(Path.Combine(folder, "juego.gba"), Rom);
        return folder;
    }

    static string Crc()
    {
        using var s = new MemoryStream(Rom);
        return Archives.Crc32(s).ToString("X8");
    }

    static (List<int> Bad, string? Check) Verify(string folder, long? size = null, string name = "juego.gba")
    {
        var checks = new List<string?> { null };
        return (Downloads.Verify(folder, [name], [size], checks), checks[0]);
    }

    [Fact]
    public void ASfvThatMatchesVerifiesAndOneThatDoesNotFails()
    {
        var folder = Folder();
        File.WriteAllText(Path.Combine(folder, "paquete.sfv"), $"; generado\r\njuego.gba {Crc()}\r\n");
        var (bad, check) = Verify(folder);
        Assert.Empty(bad);
        Assert.Equal("crc32", check);
        File.WriteAllText(Path.Combine(folder, "paquete.sfv"), "juego.gba 00000000\n");
        (bad, check) = Verify(folder);
        Assert.Equal([0], bad);
        Assert.Null(check);
    }

    [Fact]
    public void Md5AndShaFilesAndTheCrcInTheNameCount()
    {
        var folder = Folder();
        File.WriteAllText(Path.Combine(folder, "x.md5"), $"{Convert.ToHexStringLower(MD5.HashData(Rom))} *juego.gba\n");
        Assert.Equal("md5", Verify(folder).Check);
        File.Delete(Path.Combine(folder, "x.md5"));
        File.WriteAllText(Path.Combine(folder, "x.sha256"), $"{Convert.ToHexString(SHA256.HashData(Rom))}  juego.gba\n");
        Assert.Equal("sha256", Verify(folder).Check);
        File.Delete(Path.Combine(folder, "x.sha256"));

        var named = $"juego [{Crc()}].gba";
        File.Move(Path.Combine(folder, "juego.gba"), Path.Combine(folder, named));
        Assert.Equal("crc32", Verify(folder, name: named).Check);
    }

    [Fact]
    public void WithoutHashesTheSizeIsAllThatIsChecked()
    {
        var folder = Folder();
        Assert.Equal("size", Verify(folder, Rom.Length).Check);
        var (bad, check) = Verify(folder, Rom.Length + 1);
        Assert.Equal([0], bad);
        Assert.Null(check);
        Assert.Null(Verify(folder).Check);
    }
}
