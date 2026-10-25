using System.Security.Cryptography;
using System.Text;
using Dapper;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Tests;

public class CredentialsTests
{
    // En Linux, una llave fija en vez del llavero; en Windows se usa DPAPI de verdad.
    static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);
    static SecretBox Box() => new(() => Key);

    static Database CreateDatabase(TempDir tmp)
    {
        var db = new Database(tmp["otteretro.db"]);
        Migrations.Apply(db);
        Migrations.RegisterMachine(db, "pc-a");
        Migrations.RegisterMachine(db, "pc-b");
        return db;
    }

    [Fact]
    public void SavesEncryptedAndSurvivesARestart()
    {
        using var tmp = new TempDir();
        var db = CreateDatabase(tmp);
        new Credentials(db, "pc-a", Box()).Save("steam.apiKey", "clave-de-prueba-123");

        using (var c = db.Open())
        {
            var stored = c.ExecuteScalar<byte[]>("SELECT value FROM credential WHERE id = 'steam.apiKey'");
            Assert.DoesNotContain("clave-de-prueba-123", Encoding.UTF8.GetString(stored!));
        }
        // Otra instancia (como al reiniciar la app) la lee igual.
        var again = new Credentials(db, "pc-a", Box());
        Assert.Equal("clave-de-prueba-123", again.Read("steam.apiKey"));
        Assert.True(again.List().Single(s => s.Id == "steam.apiKey").Saved);
        Assert.False(again.List().Single(s => s.Id == "github.token").Saved);
    }

    [Fact]
    public void EachMachineHasItsOwnKeys()
    {
        using var tmp = new TempDir();
        var db = CreateDatabase(tmp);
        new Credentials(db, "pc-a", Box()).Save("github.token", "token-a");

        var other = new Credentials(db, "pc-b", Box());
        Assert.Null(other.Read("github.token"));
        Assert.All(other.List(), s => Assert.False(s.Saved));
    }

    [Fact]
    public void DeleteLeavesItMissing()
    {
        using var tmp = new TempDir();
        var credentials = new Credentials(CreateDatabase(tmp), "pc-a", Box());
        credentials.Save("igdb.clientSecret", "x");
        credentials.Delete("igdb.clientSecret");
        Assert.Null(credentials.Read("igdb.clientSecret"));
    }

    [Fact]
    public void LinuxSealRejectsAValueMovedToAnotherField()
    {
        var plain = Encoding.UTF8.GetBytes("secreto");
        var sealedValue = LinuxSecrets.Seal(Key, plain, "steam.apiKey"u8.ToArray());
        Assert.Equal(plain, LinuxSecrets.Open(Key, sealedValue, "steam.apiKey"u8.ToArray()));
        Assert.ThrowsAny<CryptographicException>(() => LinuxSecrets.Open(Key, sealedValue, "github.token"u8.ToArray()));
    }
}
