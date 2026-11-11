using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Otteretro.App;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#282: compartir un perfil en un .zip y cargarlo en otra carpeta de datos, sin que se cuele nada privado.</summary>
[Collection("Installs.Handler")]
public class ProfileShareTests
{
    const string Totk = "0100F2C0115B6000";
    const string Zrif = "KO5ifR1dQ+eHBlOi1dcmVnaXN0ZXJlZA==";

    static Fixture Game(bool owned)
    {
        var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["E"], "E:");
        using var c = f.Db.Open();
        c.Execute("""
            INSERT INTO game (id, name_es) VALUES (10, 'Tears of the Kingdom');
            INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'switch');
            """);
        if (owned) c.Execute("INSERT INTO source (kind, library_id, relative_path, version_id, component, title_id) VALUES ('file', 'd', 'juego', 20, 'BASE', @Totk)", new { Totk });
        return f;
    }

    static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>Eden instalado a mano desde una carpeta que trae lo de la persona: keys, NAND, BIOS y su configuración global.</summary>
    static async Task InstallEden(Fixture f, string version, string exe, bool personal)
    {
        var src = f.Tmp[$"eden-{version}-{exe}"];
        Write(Path.Combine(src, @"Eden\eden.exe"), exe);
        if (personal)
        {
            Write(Path.Combine(src, @"Eden\user\keys\prod.keys"), "llaves");
            Write(Path.Combine(src, @"Eden\user\nand\system\save\0000"), "partida");
            Write(Path.Combine(src, @"Eden\user\config\qt-config.ini"), @"ruta=C:\Users\persona");
            Write(Path.Combine(src, @"Eden\ps2-bios.bin"), "bios");
        }
        var zip = f.Tmp[$"eden-{version}-{exe}.zip"];
        ZipFile.CreateFromDirectory(src, zip);
        var (task, error) = Installs.Enqueue(f.Db, new InstallJob("eden", version, Path: zip, Name: "Eden"));
        Assert.Null(error);
        Assert.Equal("done", (await f.Run(task!.Value)).Status);
    }

    static string Sha(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));

    static Dictionary<string, string> Hashes(string folder) => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
        .ToDictionary(p => Path.GetRelativePath(folder, p).Replace('\\', '/'), p => Sha(File.ReadAllBytes(p)));

    static byte[] Bytes(ZipArchiveEntry e)
    {
        using var s = e.Open();
        using var m = new MemoryStream();
        s.CopyTo(m);
        return m.ToArray();
    }

    [Fact]
    public async Task ExportAndLoadElsewhereGivesTheSameProfileWithoutPrivateDataNorReplacingVersions()
    {
        // Equipo A: el perfil, con su configuración, sus mods y sus ajustes; el juego tiene su licencia guardada.
        await using var a = Game(owned: true);
        await InstallEden(a, "1.0", "eden de a", personal: true);
        var (id, error) = Profiles.Create(a.Db, 20, "eden", "60 fps");
        Assert.Null(error);
        var store = Profiles.StoreOf(a.Db, id!.Value);
        Write(Path.Combine(store, $@"user\config\custom\{Totk}.ini"), "resolucion=2x");
        Write(Path.Combine(store, $@"user\load\{Totk}\60fps\exefs\p.ips"), "60");
        using (var c = a.Db.Open())
        {
            c.Execute("UPDATE profile SET settings = '{\"resolution\":\"2x\"}' WHERE id = @id", new { id });
            c.Execute("INSERT INTO version_license (version_id, zrif, updated_at) VALUES (20, @Zrif, '')", new { Zrif });
        }

        var package = a.Tmp["60fps.zip"];
        Assert.Equal("profile.share.badPath", ProfileShare.Export(a.Db, id.Value, "relativo.zip")?.Code);
        Assert.Null(ProfileShare.Export(a.Db, id.Value, package));
        Assert.Equal("profile.share.exists", ProfileShare.Export(a.Db, id.Value, package)?.Code); // nunca pisa

        // Lo que lleva: el emulador limpio, los archivos del perfil y el manifiesto. Nada privado, en ningún lado.
        using (var zip = ZipFile.OpenRead(package))
        {
            var names = zip.Entries.Select(e => e.FullName).Order().ToList();
            Assert.Equal(["emulador/eden-1.0-eden de a.zip", "otteretro-perfil.json", $"perfil/user/config/custom/{Totk}.ini", $"perfil/user/load/{Totk}/60fps/exefs/p.ips"], names);
            using var inner = new ZipArchive(new MemoryStream(Bytes(zip.GetEntry(names[0])!)));
            Assert.Equal(["Eden/eden.exe"], inner.Entries.Select(e => e.FullName.Replace('\\', '/')));
            var all = zip.Entries.Concat(inner.Entries).SelectMany(e => new[] { e.FullName, Encoding.UTF8.GetString(Bytes(e)) }).ToList();
            foreach (var secret in new[] { Zrif, "llaves", "partida", "persona", "bios", "keys", "nand" })
                Assert.DoesNotContain(all, text => text.Contains(secret));
            var manifest = Encoding.UTF8.GetString(Bytes(zip.GetEntry("otteretro-perfil.json")!));
            Assert.Contains(Totk, manifest);
            Assert.Contains("\"versionId\": 20", manifest);
        }

        // Equipo B: ya tiene Eden 1.0 (otro) y no tiene el juego.
        await using var b = Game(owned: false);
        await InstallEden(b, "1.0", "eden de b", personal: false);
        var (task, loadError) = ProfileShare.EnqueueImport(b.Db, package);
        Assert.Null(loadError);
        var run = await b.Run(task!.Value);
        Assert.Equal("done", run.Status);
        Assert.Contains(TaskQueue.ReadLog(b.Db, task.Value), l => l.Message.Code == "profile.import.log.notOwned");

        // La copia del paquete, al lado de la que ya había, que no se toca; y en su librería con el mismo hash.
        Assert.Equal(["1.0", "1.0-compartido"], Installs.List(b.Db).Select(i => i.Version));
        Assert.Equal("eden de b", File.ReadAllText(b.Tmp[@"E\Emuladores\Eden\1.0\eden.exe"]));
        Assert.Equal("eden de a", File.ReadAllText(b.Tmp[@"E\Emuladores\Eden\1.0-compartido\eden.exe"]));
        using (var zip = ZipFile.OpenRead(package))
            Assert.Equal(Sha(Bytes(zip.GetEntry("emulador/eden-1.0-eden de a.zip")!)),
                Sha(File.ReadAllBytes(b.Tmp[@"E\Libreria\Emuladores\eden\1.0-compartido\eden-1.0-eden de a.zip"])));

        // El mismo perfil: nombre, ajustes y archivos con los mismos hashes, para esa versión del juego y esa copia del emulador.
        using var cb = b.Db.Open();
        var loaded = cb.QuerySingle<(long Id, string Emulator, string Version, long Game, string Name, string Settings)>(
            "SELECT id, emulator_id, emulator_version, game_version_id, name, settings FROM profile WHERE game_version_id IS NOT NULL");
        Assert.Equal(("eden", "1.0-compartido", 20L, "60 fps", "{\"resolution\":\"2x\"}"), (loaded.Emulator, loaded.Version, loaded.Game, loaded.Name, loaded.Settings));
        Assert.Equal(Hashes(store), Hashes(Profiles.StoreOf(b.Db, loaded.Id)));

        // Cargarlo otra vez suma otra copia más: nunca pisa.
        Assert.Equal("done", (await b.Run(ProfileShare.EnqueueImport(b.Db, package).Task!.Value)).Status);
        Assert.Equal(["1.0", "1.0-compartido", "1.0-compartido-2"], Installs.List(b.Db).Select(i => i.Version));
    }

    [Fact]
    public async Task TheLicenseNeverLeavesAndAnUnknownGameIsSaid()
    {
        await using var a = Game(owned: true);
        await InstallEden(a, "1.0", "eden", personal: false);
        var (id, _) = Profiles.Create(a.Db, 20, "eden", "con licencia");
        Write(Path.Combine(Profiles.StoreOf(a.Db, id!.Value), "notas.txt"), "zrif: " + Zrif);
        using (var c = a.Db.Open()) c.Execute("INSERT INTO version_license (version_id, zrif, updated_at) VALUES (20, @Zrif, '')", new { Zrif });
        Assert.Equal("profile.share.private", ProfileShare.Export(a.Db, id.Value, a.Tmp["x.zip"])?.Code);
        Assert.False(File.Exists(a.Tmp["x.zip"]));

        File.Delete(Path.Combine(Profiles.StoreOf(a.Db, id.Value), "notas.txt"));
        Assert.Null(ProfileShare.Export(a.Db, id.Value, a.Tmp["x.zip"]));
        // En un catálogo sin ese juego: lo dice y no instala nada.
        await using var b = new Fixture();
        Disk.Create(b.Db, b.Tmp["E"], "E:");
        Assert.Equal("profile.import.noGame", ProfileShare.EnqueueImport(b.Db, a.Tmp["x.zip"]).Error?.Code);
        Assert.Equal("profile.import.badPackage", ProfileShare.EnqueueImport(b.Db, a.Tmp[@"E\Libreria\Emuladores\eden\1.0\eden-1.0-eden.zip"]).Error?.Code);
        Assert.Empty(Installs.List(b.Db));
    }

    static async Task InstallDusklight(Fixture f, string version, string exe)
    {
        var src = f.Tmp[$"dusklight-{version}"];
        Write(Path.Combine(src, "dusklight.exe"), exe);
        var zip = f.Tmp[$"dusklight-{version}.zip"];
        ZipFile.CreateFromDirectory(src, zip);
        var (task, error) = Installs.Enqueue(f.Db, new InstallJob("dusklight", version, Path: zip));
        Assert.Null(error);
        Assert.Equal("done", (await f.Run(task!.Value)).Status);
    }

    /// <summary>#316: cargar el perfil de un port deja una sola versión, la del paquete, sin perder las partidas que ya había.</summary>
    [Fact]
    public async Task LoadingAPortProfileLeavesOneVersionAndKeepsTheSaves()
    {
        await using var a = Game(owned: true);
        await InstallDusklight(a, "1.0", "de a");
        var (id, _) = Profiles.Create(a.Db, 20, "dusklight", "60 fps");
        var package = a.Tmp["dusklight.zip"];
        Assert.Null(ProfileShare.Export(a.Db, id!.Value, package));

        await using var b = Game(owned: true);
        await InstallDusklight(b, "2.0", "de b");
        var port = b.Tmp[@"E\Ports\Dusklight"];
        Write(Path.Combine(port, @"data\EUR\partida.sav"), "partida");
        Assert.Equal("done", (await b.Run(ProfileShare.EnqueueImport(b.Db, package).Task!.Value)).Status);

        Assert.Equal(["1.0"], Recipes.Installed(Disk.ToolRoots(b.Db)).Where(t => t.Kind == "port").Select(t => t.Version));
        Assert.Equal("de a", File.ReadAllText(Path.Combine(port, "dusklight.exe")));
        Assert.Equal("partida", File.ReadAllText(Path.Combine(port, @"data\EUR\partida.sav")));
        Assert.Equal([("2.0", false), ("1.0", true)], Installs.List(b.Db).Select(i => (i.Version, i.Path is not null)));
    }
}
