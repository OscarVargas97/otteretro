using System.Text;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#199: subir BIOS, keys y firmware con versiones, y avisar lo que falta.</summary>
public class SystemFilesTests
{
    static MemoryStream Text(string t) => new(Encoding.UTF8.GetBytes(t));

    [Fact]
    public async Task KeepsEveryVersionAndWarnsWhatIsMissing()
    {
        await using var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO source (kind, library_id, relative_path, console, size) VALUES ('file', @disk, 'Juegos\\Switch\\a.nsp', 'switch', 1)", new { disk });

        Assert.Equal(["prod.keys"], Assert.Single(SystemFiles.List(f.Db)).Missing);

        Assert.Null(await SystemFiles.Save(f.Db, "switch", "prod.keys", Text("uno"), default));
        Assert.Null(await SystemFiles.Save(f.Db, "switch", "prod.keys", Text("uno"), default)); // el mismo: no suma
        Assert.Null(await SystemFiles.Save(f.Db, "switch", "prod.keys", Text("dos"), default));

        var console = Assert.Single(SystemFiles.List(f.Db));
        Assert.Empty(console.Missing);
        Assert.Equal(2, console.Files.Count);
        Assert.All(console.Files, x => Assert.NotNull(x.Version));
        Assert.Equal(["dos", "uno"], console.Files.Select(x => File.ReadAllText(x.Path)).Order());
        Assert.Equal(2, Directory.GetDirectories(f.Tmp[@"Otteretro\Sistema\Switch\prod.keys"]).Length);
    }

    [Fact]
    public async Task RejectsBadNamesAndConsoles()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        Assert.Equal("system.badConsole", (await SystemFiles.Save(f.Db, "nada", "bios.bin", Text("x"), default))?.Code);
        Assert.Equal("system.badName", (await SystemFiles.Save(f.Db, "ps2", "", Text("x"), default))?.Code);
        // Una ruta en el nombre no se sale de la carpeta.
        Assert.Null(await SystemFiles.Save(f.Db, "ps2", @"..\..\fuga.bin", Text("x"), default));
        Assert.False(File.Exists(f.Tmp[@"Otteretro\fuga.bin"]));
        Assert.Equal("fuga.bin", Assert.Single(Assert.Single(SystemFiles.List(f.Db)).Files).Name);
    }
}
