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
        Assert.Equal("install.backupIsDefault", Installs.SetBackupDisk(f.Db, star)?.Code);
        Assert.Null(Installs.SetBackupDisk(f.Db, other));
        var zip = Zip(f.Tmp, "v1", "uno");

        var job = new InstallJob("eden", "0.1", Path: zip, Name: "Eden");
        Assert.Equal("done", (await Install(f, job)).Status);
        await Backup(f);

        // La carpeta de la release se aplana: el ejecutable queda en la raíz de su versión.
        Assert.Equal("uno", File.ReadAllText(f.Tmp[@"E\Emuladores\Eden\0.1\eden.exe"]));
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip)));
        foreach (var disk in new[] { "E", "F" })
            Assert.Equal(sha, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f.Tmp[$@"{disk}\Libreria\Emuladores\eden\0.1\v1.zip"]))));
        var installed = Assert.Single(Installs.List(f.Db));
        Assert.Equal(("upload", sha, other, "0.1"), (installed.Origin, installed.Sha256, installed.BackupLibraryId, installed.Version));
        Assert.True(Disk.Same(f.Tmp[@"E\Emuladores\Eden\0.1"], installed.Path!));
        Assert.False(Directory.Exists(f.Tmp[@"E\Libreria\.trabajo"]) && Directory.EnumerateFileSystemEntries(f.Tmp[@"E\Libreria\.trabajo"]).Any());

        // La misma versión no se vuelve a instalar; otra convive con la vieja, que no se toca.
        Assert.Equal("install.exists", Installs.Enqueue(f.Db, job with { Path = Zip(f.Tmp, "v1b", "otro") }).Error?.Code);
        Assert.Equal("done", (await Install(f, job with { Version = "0.2", Path = Zip(f.Tmp, "v2", "dos") })).Status);
        Assert.Equal("uno", File.ReadAllText(f.Tmp[@"E\Emuladores\Eden\0.1\eden.exe"]));
        Assert.Equal("dos", File.ReadAllText(f.Tmp[@"E\Emuladores\Eden\0.2\eden.exe"]));
        Assert.Equal(["0.1", "0.2"], Installs.List(f.Db).Select(i => i.Version));
        // El emulador queda registrado una vez, con su perfil por defecto.
        using var c = f.Db.Open();
        Assert.Equal(1, c.ExecuteScalar<long>("SELECT count(*) FROM profile WHERE emulator_id = 'eden' AND game_version_id IS NULL"));
    }

    /// <summary>La segunda copia, si la cola no la hizo ya mientras corría la instalación.</summary>
    static async Task Backup(Fixture f)
    {
        if (Installs.EnqueueBackupIfPending(f.Db) is { } task) Assert.Equal("done", (await f.Run(task)).Status);
        Assert.Equal(0, Installs.Backup(f.Db).Pending);
    }

    [Fact]
    public async Task BackupWaitsForItsDiskAndReinstallComesFromTheLibrary()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["E"], "E:");
        var (other, _) = Disk.Create(f.Db, f.Tmp["F"], "F:");
        Installs.SetBackupDisk(f.Db, other);
        Directory.Move(f.Tmp["F"], f.Tmp["F-fuera"]); // el disco de la copia, desconectado

        Assert.Equal("done", (await Install(f, new InstallJob("eden", "0.1", Path: Zip(f.Tmp, "v1", "uno"), Name: "Eden"))).Status);
        Assert.Equal(1, Installs.Backup(f.Db).Pending);
        Assert.Null(Installs.EnqueueBackupIfPending(f.Db));

        Directory.Move(f.Tmp["F-fuera"], f.Tmp["F"]); // vuelve: se copia sola
        await Backup(f);
        Assert.True(File.Exists(f.Tmp[@"F\Libreria\Emuladores\eden\0.1\v1.zip"]));
        Assert.Equal(other, Assert.Single(Installs.List(f.Db)).BackupLibraryId);

        // Una versión que perdió su carpeta vuelve desde la librería, sin bajar nada.
        var reinstall = new InstallJob("eden", "0.1", Reinstall: true);
        Assert.Equal("install.exists", Installs.Enqueue(f.Db, reinstall).Error?.Code);
        Links.DeleteTree(f.Tmp[@"E\Emuladores\Eden\0.1"]); // con la NAND enlazada a lo común (#280)
        Assert.Null(Assert.Single(Installs.List(f.Db)).Path);
        Assert.Equal("done", (await Install(f, reinstall)).Status);
        Assert.Equal("uno", File.ReadAllText(f.Tmp[@"E\Emuladores\Eden\0.1\eden.exe"]));
        Assert.NotNull(Assert.Single(Installs.List(f.Db)).Path);

        // Si el archivo de la librería cambió, no se reinstala.
        Links.DeleteTree(f.Tmp[@"E\Emuladores\Eden\0.1"]);
        File.Copy(Zip(f.Tmp, "otro", "otro"), f.Tmp[@"E\Libreria\Emuladores\eden\0.1\v1.zip"], overwrite: true);
        var bad = await Install(f, reinstall);
        Assert.Equal(("error", "install.badHash"), (bad.Status, bad.Detail?.Code));
        Assert.False(Directory.Exists(f.Tmp[@"E\Emuladores\Eden\0.1"]));
        Assert.Equal("install.notInstalled", Installs.Enqueue(f.Db, new InstallJob("eden", "9", Reinstall: true)).Error?.Code);
    }

    [Fact]
    public void ReadsOneFolderPerEmulatorAndTheOldLayout()
    {
        using var tmp = new TempDir();
        foreach (var exe in new[] { @"Emuladores\Eden\0.1\eden.exe", @"Emuladores\Switch\Viejo\1.0\viejo.exe", @"Emuladores\Dolphin\2506\Dolphin.exe" })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tmp[exe])!);
            File.WriteAllText(tmp[exe], "");
        }
        var found = Recipes.Installed(tmp.Path).Select(t => (t.Folder, t.Name, t.Version, t.RecipeId)).ToList();
        Assert.Equal([("GameCube", "Dolphin", "2506", "dolphin"), ("Switch", "Eden", "0.1", "eden"), ("Switch", "Viejo", "1.0", null)], found.OrderBy(t => t.Name)); // Eden ya tiene receta (#273)
    }

    [Fact]
    public void VersionsShareTheirSavesAndNothingIsDeleted()
    {
        using var tmp = new TempDir();
        var dolphin = Recipes.Find("dolphin")!;
        void Release(string version, string file, string text)
        {
            Directory.CreateDirectory(tmp[$@"Emuladores\Dolphin\{version}\User\GC"]);
            File.WriteAllText(tmp[$@"Emuladores\Dolphin\{version}\User\GC\{file}"], text);
            File.WriteAllText(tmp[$@"Emuladores\Dolphin\{version}\Dolphin.exe"], "");
        }
        Release("2506", "card.raw", "partida");
        Installs.ShareSaves(dolphin, tmp[@"Emuladores\Dolphin\2506"], "2506");
        // Lo que traía la primera versión pasa a lo común, y la versión apunta ahí.
        Assert.Equal("partida", File.ReadAllText(tmp[@"Emuladores\Dolphin\_compartido\User\GC\card.raw"]));
        Assert.True(Links.IsLink(tmp[@"Emuladores\Dolphin\2506\User\GC"]));

        Release("2512", "default.raw", "de la release");
        Installs.ShareSaves(dolphin, tmp[@"Emuladores\Dolphin\2512"], "2512");
        // Antes de usarla, una copia puntual de lo común; lo que traía la versión nueva queda al lado.
        Assert.Equal("partida", File.ReadAllText(Assert.Single(Directory.GetFiles(tmp[@"Emuladores\Dolphin\_respaldos"], "card.raw", SearchOption.AllDirectories))));
        Assert.Equal("de la release", File.ReadAllText(tmp[@"Emuladores\Dolphin\2512\User\GC.antes-de-otteretro\default.raw"]));

        // Las dos versiones ven la misma partida.
        File.WriteAllText(tmp[@"Emuladores\Dolphin\2512\User\GC\nueva.sav"], "guardada en 2512");
        Assert.Equal("guardada en 2512", File.ReadAllText(tmp[@"Emuladores\Dolphin\2506\User\GC\nueva.sav"]));
        Assert.Equal("partida", File.ReadAllText(tmp[@"Emuladores\Dolphin\2512\User\GC\card.raw"]));
        // Repetirlo no pierde nada, y lo común no cuenta como una versión.
        Installs.ShareSaves(dolphin, tmp[@"Emuladores\Dolphin\2512"], "2512");
        Assert.Equal("guardada en 2512", File.ReadAllText(tmp[@"Emuladores\Dolphin\_compartido\User\GC\nueva.sav"]));
        Assert.Equal(["2506", "2512"], Recipes.Installed(tmp.Path).Select(t => t.Version));
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
            var job = new InstallJob("citron", "1.0", Url: "https://example.com/releases/citron-windows.exe", Name: "Citron");
            Assert.Equal("done", (await Install(f, job)).Status);
        }
        finally { Installs.Handler = null; }
        Assert.Equal(exe, File.ReadAllBytes(f.Tmp[@"E\Emuladores\Citron\1.0\citron-windows.exe"]));
        Assert.Equal(exe, File.ReadAllBytes(f.Tmp[@"E\Libreria\Emuladores\citron\1.0\citron-windows.exe"]));
        var citron = Assert.Single(Installs.List(f.Db));
        Assert.Equal(("url", "https://example.com/releases/citron-windows.exe", (string?)null), (citron.Origin, citron.Url, citron.BackupLibraryId));

        // Adoptar uno instalado a mano lo copia: el original sigue donde estaba.
        var manual = f.Tmp["a-mano"];
        Directory.CreateDirectory(manual);
        File.WriteAllText(Path.Combine(manual, "Ryujinx.exe"), "ryu");
        Assert.Equal("done", (await Install(f, new InstallJob("ryubing", "1.3", Path: manual, Adopt: true, Name: "Ryubing"))).Status);
        Assert.Equal("ryu", File.ReadAllText(Path.Combine(manual, "Ryujinx.exe")));
        Assert.Equal("ryu", File.ReadAllText(f.Tmp[@"E\Emuladores\Ryubing\1.3\Ryujinx.exe"]));
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
        Assert.Equal("install.badUrl", Installs.Enqueue(f.Db, new InstallJob("x", "1", Url: "http://x/y.zip", Name: "X")).Error?.Code);
        Assert.Equal("install.noDownload", Installs.Enqueue(f.Db, new InstallJob("dolphin", "5.0")).Error?.Code);
        Assert.Equal("install.noVersion", Installs.Enqueue(f.Db, new InstallJob("x", Url: "https://x/y.zip", Name: "X")).Error?.Code);
        Assert.Equal("install.noVersion", Installs.Enqueue(f.Db, new InstallJob("x", "..", Url: "https://x/y.zip", Name: "X")).Error?.Code);
        Assert.Equal("path.notFound", Installs.Enqueue(f.Db, new InstallJob("x", "1", Path: f.Tmp["nada.zip"], Name: "X")).Error?.Code);
    }

    sealed class Fake(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }
}
