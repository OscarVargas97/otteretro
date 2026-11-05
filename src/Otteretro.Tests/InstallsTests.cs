using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Otteretro.App;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#268: instalar desde una URL, un zip o una carpeta, la librería en dos discos y las versiones que conviven.</summary>
[Collection("Installs.Handler")]
public class InstallsTests
{
    static string Zip(TempDir tmp, string name, string content)
    {
        var folder = tmp[name + "-src"];
        Directory.CreateDirectory(Path.Combine(folder, "Eden"));
        File.WriteAllText(Path.Combine(folder, "Eden", "eden.exe"), content);
        var zip = tmp[name + ".zip"];
        ZipFile.CreateFromDirectory(folder, zip);
        return zip;
    }

    static async Task<TaskInfo> Install(Fixture f, InstallJob job)
    {
        var (task, error) = Installs.Enqueue(f.Db, job);
        Assert.Null(error);
        return await f.Run(task!.Value);
    }

    [Fact]
    public async Task ZipGoesToItsVersionAndToTheLibraryOnTwoDisks()
    {
        await using var f = new Fixture();
        var (star, _) = Disk.Create(f.Db, f.Tmp["E"], "E:");
        var (other, _) = Disk.Create(f.Db, f.Tmp["F"], "F:");
        var zip = Zip(f.Tmp, "v1", "uno");

        var job = new InstallJob("eden", "0.1", Path: zip, Name: "Eden", Folder: "Switch");
        Assert.Equal("done", (await Install(f, job)).Status);

        // La carpeta de la release se aplana: el ejecutable queda en la raíz de su versión.
        Assert.Equal("uno", File.ReadAllText(f.Tmp[@"E\Emuladores\Switch\Eden\0.1\eden.exe"]));
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip)));
        foreach (var disk in new[] { "E", "F" })
            Assert.Equal(sha, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f.Tmp[$@"{disk}\Libreria\Emuladores\eden\0.1\v1.zip"]))));
        var installed = Assert.Single(Installs.List(f.Db));
        Assert.Equal(("upload", sha, other, "0.1"), (installed.Origin, installed.Sha256, installed.BackupLibraryId, installed.Version));
        Assert.True(Disk.Same(f.Tmp[@"E\Emuladores\Switch\Eden\0.1"], installed.Path!));
        Assert.False(Directory.Exists(f.Tmp[@"E\Libreria\.trabajo"]) && Directory.EnumerateFileSystemEntries(f.Tmp[@"E\Libreria\.trabajo"]).Any());
        Assert.NotNull(star);

        // La misma versión no se vuelve a instalar; otra convive con la vieja, que no se toca.
        Assert.Equal("install.exists", Installs.Enqueue(f.Db, job with { Path = Zip(f.Tmp, "v1b", "otro") }).Error?.Code);
        Assert.Equal("done", (await Install(f, job with { Version = "0.2", Path = Zip(f.Tmp, "v2", "dos") })).Status);
        Assert.Equal("uno", File.ReadAllText(f.Tmp[@"E\Emuladores\Switch\Eden\0.1\eden.exe"]));
        Assert.Equal("dos", File.ReadAllText(f.Tmp[@"E\Emuladores\Switch\Eden\0.2\eden.exe"]));
        Assert.Equal(["0.1", "0.2"], Installs.List(f.Db).Select(i => i.Version));
        // El emulador queda registrado una vez, con su perfil por defecto.
        using var c = f.Db.Open();
        Assert.Equal(1, c.ExecuteScalar<long>("SELECT count(*) FROM profile WHERE emulator_id = 'eden' AND game_version_id IS NULL"));
    }

    [Fact]
    public async Task ReleaseUrlIsDownloadedAndAdoptedFolderIsCopied()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["E"], "E:");
        var exe = Encoding.UTF8.GetBytes("binario");
        Installs.Handler = new Fake(exe);
        try
        {
            var job = new InstallJob("citron", "1.0", Url: "https://example.com/releases/citron-windows.exe", Name: "Citron", Folder: "Switch");
            Assert.Equal("done", (await Install(f, job)).Status);
        }
        finally { Installs.Handler = null; }
        Assert.Equal(exe, File.ReadAllBytes(f.Tmp[@"E\Emuladores\Switch\Citron\1.0\citron-windows.exe"]));
        Assert.Equal(exe, File.ReadAllBytes(f.Tmp[@"E\Libreria\Emuladores\citron\1.0\citron-windows.exe"]));
        var citron = Assert.Single(Installs.List(f.Db));
        Assert.Equal(("url", "https://example.com/releases/citron-windows.exe", (string?)null), (citron.Origin, citron.Url, citron.BackupLibraryId));

        // Adoptar uno instalado a mano lo copia: el original sigue donde estaba.
        var manual = f.Tmp["a-mano"];
        Directory.CreateDirectory(manual);
        File.WriteAllText(Path.Combine(manual, "Ryujinx.exe"), "ryu");
        Assert.Equal("done", (await Install(f, new InstallJob("ryubing", "1.3", Path: manual, Adopt: true, Name: "Ryubing", Folder: "Switch"))).Status);
        Assert.Equal("ryu", File.ReadAllText(Path.Combine(manual, "Ryujinx.exe")));
        Assert.Equal("ryu", File.ReadAllText(f.Tmp[@"E\Emuladores\Switch\Ryubing\1.3\Ryujinx.exe"]));
        Assert.Equal("adopted", Installs.List(f.Db).Single(i => i.EmulatorId == "ryubing").Origin);
    }

    [Fact]
    public void LibraryIsNeverOverwrittenAndRecipeHashIsSri()
    {
        using var tmp = new TempDir();
        File.WriteAllText(tmp["a.zip"], "a");
        Directory.CreateDirectory(tmp["b"]);
        File.WriteAllText(tmp[@"b\a.zip"], "b");
        var shelf = tmp["estante"];
        Installs.Store(tmp["a.zip"], shelf);
        Installs.Store(tmp["a.zip"], shelf); // el mismo: no hace nada
        Assert.Equal("install.libraryTaken", Assert.Throws<TaskException>(() => Installs.Store(tmp[@"b\a.zip"], shelf)).Error.Code);
        Assert.Equal("a", File.ReadAllText(Path.Combine(shelf, "a.zip")));

        var sha = Convert.ToHexStringLower(SHA256.HashData("a"u8.ToArray()));
        Assert.True(Installs.MatchesSri(sha, "sha256-" + Convert.ToBase64String(SHA256.HashData("a"u8.ToArray()))));
        Assert.False(Installs.MatchesSri(sha, "sha256-" + Convert.ToBase64String(SHA256.HashData("b"u8.ToArray()))));
    }

    [Fact]
    public async Task RejectsWhatCannotBeInstalled()
    {
        await using var f = new Fixture();
        Assert.Equal("install.unknownEmulator", Installs.Enqueue(f.Db, new InstallJob("nadie", "1", Url: "https://x/y.zip")).Error?.Code);
        Assert.Equal("install.badUrl", Installs.Enqueue(f.Db, new InstallJob("x", "1", Url: "http://x/y.zip", Name: "X", Folder: "Switch")).Error?.Code);
        Assert.Equal("install.noDownload", Installs.Enqueue(f.Db, new InstallJob("dolphin", "5.0")).Error?.Code);
        Assert.Equal("install.noVersion", Installs.Enqueue(f.Db, new InstallJob("x", Url: "https://x/y.zip", Name: "X", Folder: "Switch")).Error?.Code);
        Assert.Equal("install.noVersion", Installs.Enqueue(f.Db, new InstallJob("x", "..", Url: "https://x/y.zip", Name: "X", Folder: "Switch")).Error?.Code);
        Assert.Equal("path.notFound", Installs.Enqueue(f.Db, new InstallJob("x", "1", Path: f.Tmp["nada.zip"], Name: "X", Folder: "Switch")).Error?.Code);
    }

    sealed class Fake(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }
}
