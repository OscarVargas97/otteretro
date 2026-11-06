using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Otteretro.App;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#268: instalar desde una URL, un zip o una carpeta, la librería de una sola copia y las versiones que conviven.</summary>
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
    public async Task ZipGoesToItsVersionAndToTheLibrary()
    {
        await using var f = new Fixture();
        var (star, _) = Disk.Create(f.Db, f.Tmp["E"], "E:");
        var (other, _) = Disk.Create(f.Db, f.Tmp["F"], "F:");
        var zip = Zip(f.Tmp, "v1", "uno");

        var job = new InstallJob("eden", "0.1", Path: zip, Name: "Eden");
        Assert.Equal("done", (await Install(f, job)).Status);

        // La carpeta de la release se aplana: el ejecutable queda en la raíz de su versión.
        Assert.Equal("uno", File.ReadAllText(f.Tmp[@"E\Emuladores\Eden\0.1\eden.exe"]));
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip)));
        Assert.Equal(sha, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f.Tmp[@"E\Libreria\Emuladores\eden\0.1\v1.zip"]))));
        Assert.False(Directory.Exists(f.Tmp[@"F\Libreria"])); // sin segunda copia (decision 335)
        var installed = Assert.Single(Installs.List(f.Db));
        Assert.Equal(("upload", sha, "0.1"), (installed.Origin, installed.Sha256, installed.Version));
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

    [Fact]
    public async Task ReinstallComesFromTheLibrary()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["E"], "E:");

        Assert.Equal("done", (await Install(f, new InstallJob("eden", "0.1", Path: Zip(f.Tmp, "v1", "uno"), Name: "Eden"))).Status);

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
        Assert.Equal(("url", "https://example.com/releases/citron-windows.exe"), (citron.Origin, citron.Url));

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
    public async Task ToolGoesToHerramientasWithTheNameItsRecipeLooksFor()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["E"], "E:");
        Installs.Handler = new Fake("srm"u8.ToArray());
        try
        {
            var job = new InstallJob("steam-rom-manager", "2.6.2", Url: "https://example.com/Steam-ROM-Manager-portable-2.6.2.exe");
            Assert.Equal("done", (await Install(f, job)).Status);
        }
        finally { Installs.Handler = null; }
        // `saveAs`: el .exe queda como lo busca `detect`; la librería guarda el original con su nombre.
        Assert.Equal("srm", File.ReadAllText(f.Tmp[@"E\Herramientas\Steam ROM Manager\2.6.2\Steam-ROM-Manager.exe"]));
        Assert.True(File.Exists(f.Tmp[@"E\Libreria\Herramientas\steam-rom-manager\2.6.2\Steam-ROM-Manager-portable-2.6.2.exe"]));
        var tool = Assert.Single(Installs.List(f.Db));
        Assert.Equal(("tool", "url"), (tool.Kind, tool.Origin));
        Assert.NotNull(tool.Path);
        Assert.Contains(Recipes.Installed(Disk.ToolRoots(f.Db)), t => t is { Kind: "tool", RecipeId: "steam-rom-manager", Version: "2.6.2" });
    }

    [Fact]
    public void ToolKindMigrationKeepsEmulatorsTheirBaseAndProfiles()
    {
        using var tmp = new TempDir();
        var folder = tmp["datos"];
        DataFolder.Prepare(folder);
        var db = DataFolder.Open(folder);
        Otteretro.Data.Migrations.Apply(db, until: "0033_emulator_profile.sql");
        using (var c = db.Open())
            c.Execute("""
                INSERT INTO emulator (id, kind, name) VALUES ('eden', 'emulator', 'Eden');
                INSERT INTO emulator_install (emulator_id, version, origin, installed_at) VALUES ('eden', '0.1', 'upload', 'hoy');
                INSERT INTO console_emulator (console_id, emulator_id, is_base) VALUES ('switch', 'eden', 1);
                INSERT INTO profile (emulator_id, name, created_at) VALUES ('eden', 'Eden', 'hoy');
                """);
        Assert.True(Otteretro.Data.Migrations.Apply(db) > 0);
        using var after = db.Open();
        Assert.Equal(1, after.ExecuteScalar<long>("SELECT count(*) FROM console_emulator WHERE emulator_id = 'eden' AND is_base = 1"));
        Assert.Equal(1, after.ExecuteScalar<long>("SELECT count(*) FROM emulator_install WHERE emulator_id = 'eden'"));
        Assert.Equal(1, after.ExecuteScalar<long>("SELECT count(*) FROM profile WHERE emulator_id = 'eden'"));
        Assert.Empty(after.Query("PRAGMA main.foreign_key_check"));
        after.Execute("INSERT INTO emulator (id, kind, name) VALUES ('steam-rom-manager', 'tool', 'Steam ROM Manager')");
        // La cascada sigue: quitar el emulador quita sus consolas.
        after.Execute("DELETE FROM profile; DELETE FROM emulator_install; DELETE FROM emulator WHERE id = 'eden'");
        Assert.Equal(0, after.ExecuteScalar<long>("SELECT count(*) FROM console_emulator"));
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

    static string Package(TempDir tmp, string name, params (string Path, string Text)[] files)
    {
        var folder = tmp[name + "-src"];
        foreach (var (path, text) in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(folder, path))!);
            File.WriteAllText(Path.Combine(folder, path), text);
        }
        ZipFile.CreateFromDirectory(folder, tmp[name + ".zip"]);
        return tmp[name + ".zip"];
    }

    /// <summary>#315 (decisiones 324 y 325): un port sin receta tiene una sola versión y lo que creó la persona se queda.</summary>
    [Fact]
    public async Task PortWithoutRecipeIsReplacedInPlaceAndKeepsYourFiles()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["E"], "E:");
        var job = new InstallJob("port-2", "1.0", Path: Package(f.Tmp, "v1", ("ship.exe", "uno"), ("viejo.dll", "solo en la 1.0")), Name: "Ship", Kind: "port");
        Assert.Equal("done", (await Install(f, job)).Status);
        Assert.Equal("uno", File.ReadAllText(f.Tmp[@"E\Ports\Ship\ship.exe"]));
        File.WriteAllText(f.Tmp[@"E\Ports\Ship\mio.cfg"], "de la persona");

        Assert.Equal("done", (await Install(f, job with { Version = "2.0", Path = Package(f.Tmp, "v2", ("ship.exe", "dos")) })).Status);
        Assert.Equal("dos", File.ReadAllText(f.Tmp[@"E\Ports\Ship\ship.exe"]));
        Assert.Equal("de la persona", File.ReadAllText(f.Tmp[@"E\Ports\Ship\mio.cfg"]));
        Assert.Equal("de la persona", File.ReadAllText(Assert.Single(Directory.GetFiles(f.Tmp[@"E\Ports\Ship\_respaldos"], "mio.cfg", SearchOption.AllDirectories))));
        Assert.False(Directory.Exists(f.Tmp[@"E\Ports\Ship.anterior"]));
        // Una sola instalada; la otra queda como archivo de la librería.
        Assert.Equal([("1.0", false), ("2.0", true)], Installs.List(f.Db).Select(i => (i.Version, i.Path is not null)));
        Assert.Equal(["2.0"], Recipes.Installed(Disk.ToolRoots(f.Db)).Where(t => t.Kind == "port").Select(t => t.Version));

        // «Reinstalar» la vieja la deja como la única, sin perder lo de la persona.
        Assert.Equal("done", (await Install(f, new InstallJob("port-2", "1.0", Reinstall: true))).Status);
        Assert.Equal("uno", File.ReadAllText(f.Tmp[@"E\Ports\Ship\ship.exe"]));
        Assert.Equal("de la persona", File.ReadAllText(f.Tmp[@"E\Ports\Ship\mio.cfg"]));
        Assert.Equal([("1.0", true), ("2.0", false)], Installs.List(f.Db).Select(i => (i.Version, i.Path is not null)));
    }

    /// <summary>#315 (decisiones 325 y 326): Dusklight se actualiza en su carpeta con sus partidas, su configuración y sus mods.</summary>
    [Fact]
    public async Task PortWithRecipeKeepsSavesConfigAndModsAndProfilesUseTheNewOne()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["E"], "E:");
        var job = new InstallJob("dusklight", "1.0", Path: Package(f.Tmp, "v1", ("dusklight.exe", "uno"), ("data/config.json", "{}")));
        Assert.Equal("done", (await Install(f, job)).Status);
        var root = f.Tmp[@"E\Ports\Dusklight"];
        foreach (var region in new[] { "EUR", "USA", "JAP" }) File.WriteAllText(Path.Combine(root, "data", region, "partida.sav"), region);
        File.WriteAllText(Path.Combine(root, @"data\config.json"), "{\"mia\":1}");
        Directory.CreateDirectory(Path.Combine(root, @"data\mods"));
        File.WriteAllText(Path.Combine(root, @"data\mods\mod.txt"), "mod");

        Assert.Equal("done", (await Install(f, job with { Version = "1.1", Path = Package(f.Tmp, "v2", ("dusklight.exe", "dos"), ("data/config.json", "{\"paquete\":1}")) })).Status);
        Assert.Equal("dos", File.ReadAllText(Path.Combine(root, "dusklight.exe")));
        foreach (var region in new[] { "EUR", "USA", "JAP" }) Assert.Equal(region, File.ReadAllText(Path.Combine(root, "data", region, "partida.sav")));
        Assert.Equal("{\"mia\":1}", File.ReadAllText(Path.Combine(root, @"data\config.json")));
        Assert.Equal("mod", File.ReadAllText(Path.Combine(root, @"data\mods\mod.txt")));
        // Un perfil de la versión vieja juega con la nueva.
        Assert.True(Disk.Same(root, Profiles.InstallOf(f.Db, "dusklight", "1.0")!));
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
