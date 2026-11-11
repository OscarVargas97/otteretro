using System.Text.Json.Nodes;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#280: UPD y DLC dentro del emulador con el que se juega, con emuladores falsos (nunca se ejecuta uno real).</summary>
[Collection("Content.Runner")]
public class ContentTests
{
    /// <summary>Un disco ★ con un juego de esa consola, sus archivos en `Juegos\` y el emulador instalado (solo su ejecutable).</summary>
    static (Fixture F, string Install) Setup(string console, string emulator, string name, string exe, params (string File, string? Component)[] files)
    {
        var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["E"], "E:");
        var install = f.Tmp[$@"E\Emuladores\{name}\1.0"];
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, exe), "");
        Profiles.AddEmulator(f.Db, emulator, "emulator", name);
        using var c = f.Db.Open();
        c.Execute("""
            INSERT INTO game (id, name_es) VALUES (11, 'Age of Calamity');
            INSERT INTO version (id, game_id, platform) VALUES (21, 11, @console);
            INSERT INTO emulator_install (emulator_id, version, origin, installed_at) VALUES (@emulator, '1.0', 'adopted', '2026-11-05');
            """, new { console, emulator });
        foreach (var (file, component) in files)
        {
            var relative = Path.Combine("Juegos", console, "Age of Calamity", file);
            Directory.CreateDirectory(Path.GetDirectoryName(f.Tmp[@"E\" + relative])!);
            File.WriteAllText(f.Tmp[@"E\" + relative], file);
            c.Execute("INSERT INTO source (kind, library_id, relative_path, console, component, version_id) VALUES ('file', @disk, @relative, @console, @component, 21)",
                new { disk, relative, console, component });
        }
        return (f, install);
    }

    static readonly (string, string?)[] AgeOfCalamity =
    [
        ("Age of Calamity [01002B00111A2000][BASE].nsp", "BASE"),
        ("Age of Calamity [01002B00111A2800][v65536][UPD].nsp", "UPD"), ("Age of Calamity [01002B00111A2800][v131072][UPD].nsp", "UPD"),
        ("DLC 1 [01002B00111A3001][DLC].nsp", "DLC"), ("DLC 2 [01002B00111A3002][DLC].nsp", "DLC"),
        ("DLC 3 [01002B00111A3003][DLC].nsp", "DLC"), ("DLC 4 [01002B00111A3004][DLC].nsp", "DLC"),
    ];

    [Fact]
    public async Task EdenPointsToTheUpdatesAndDlcWithoutCopying()
    {
        var (f, install) = Setup("switch", "eden", "Eden", "eden.exe", AgeOfCalamity);
        await using var _ = f;
        var ini = Path.Combine(install, @"user\config\qt-config.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(ini)!);
        File.WriteAllText(ini, "[UI]\ntheme = default\n[Renderer]\nbackend = 1\n");

        var (steps, error) = Content.Prepare(f.Db, 21, "eden");
        Assert.Null(error);
        // Los 2 UPD y los 4 DLC, apuntados; la BASE se abre tal cual.
        Assert.Equal(6, steps!.Count(s => s.Action == "pointed"));
        var folder = f.Tmp[@"E\Juegos\switch\Age of Calamity"];
        var lines = File.ReadAllLines(ini);
        Assert.Contains($"Paths\\external_content_dirs\\1\\path = \"{folder.Replace('\\', '/')}\"", lines);
        Assert.Contains(@"Paths\external_content_dirs\size = 1", lines);
        Assert.True(Array.IndexOf(lines, @"Paths\external_content_dirs\size = 1") < Array.IndexOf(lines, "[Renderer]")); // en [UI]
        Assert.Contains("backend = 1", lines);
        // No se copió nada: los originales siguen en Juegos\ y la NAND es la común a las versiones (decisión 308).
        Assert.Equal(7, Directory.GetFiles(folder).Length);
        Assert.Empty(Directory.EnumerateFiles(install, "*.nsp", SearchOption.AllDirectories));
        Assert.True(Links.IsLink(Path.Combine(install, @"user\nand")));

        // Repetirlo no duplica la carpeta.
        Content.Prepare(f.Db, 21, null);
        Assert.Single(File.ReadAllLines(ini), l => l.Contains(@"external_content_dirs\") && l.Contains("path"));
    }

    [Fact]
    public async Task RyubingAddsTheFolderToAutoloadDirsAndCitronWarns()
    {
        var (f, install) = Setup("switch", "ryubing", "Ryubing", "Ryujinx.exe", AgeOfCalamity);
        await using var _ = f;
        var config = Path.Combine(install, @"portable\Config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllText(config, """{ "version": 70, "autoload_dirs": ["D:\\Otros"] }""");
        Assert.Null(Content.Prepare(f.Db, 21, "ryubing").Error);
        var json = JsonNode.Parse(File.ReadAllText(config))!;
        Assert.Equal(70, json["version"]!.GetValue<int>());
        Assert.Equal([@"D:\Otros", f.Tmp[@"E\Juegos\switch\Age of Calamity"]], json["autoload_dirs"]!.AsArray().Select(n => n!.GetValue<string>()));

        // Citron no apunta ni instala solo: aviso de cada UPD y DLC.
        var citron = f.Tmp[@"E\Emuladores\Citron\1.0"];
        Directory.CreateDirectory(citron);
        File.WriteAllText(Path.Combine(citron, "citron.exe"), "");
        Profiles.AddEmulator(f.Db, "citron", "emulator", "Citron");
        using (var c = f.Db.Open()) c.Execute("INSERT INTO emulator_install (emulator_id, version, origin, installed_at) VALUES ('citron', '1.0', 'adopted', '')");
        Assert.Equal(6, Content.Prepare(f.Db, 21, "citron").Steps!.Count(s => s.Action == "manual"));
    }

    [Fact]
    public async Task AzaharInstallsTheCiaOnceWithItsCommandLine()
    {
        var (f, install) = Setup("3ds", "azahar", "Azahar", "azahar.exe", ("Juego.cia", null), ("Juego.3ds", null));
        await using var _ = f;
        var calls = new List<string>();
        var exit = 0;
        Content.Runner = (exe, args) =>
        {
            calls.Add($"{exe} {string.Join(' ', args)}");
            return exit;
        };
        try
        {
            exit = 1;
            Assert.Equal("failed", Assert.Single(Content.Prepare(f.Db, 21, "azahar").Steps!).Action);
            exit = 0;
            var (task, error) = Content.Enqueue(f.Db, 21, null); // sin emulador: el que corre la consola
            Assert.Null(error);
            Assert.Equal("done", (await f.Run(task!.Value)).Status);
            Assert.Equal("already", Assert.Single(Content.Prepare(f.Db, 21, "azahar").Steps!).Action);
        }
        finally { Content.Runner = null; }
        var cia = f.Tmp[@"E\Juegos\3ds\Age of Calamity\Juego.cia"];
        Assert.Equal([$"{Path.Combine(install, "azahar.exe")} --install {cia}", $"{Path.Combine(install, "azahar.exe")} --install {cia}"], calls);
        Assert.True(File.Exists(cia));
    }

    [Fact]
    public async Task VitaInstallsThePkgWithTheLicenseFromTheGamePage()
    {
        var (f, install) = Setup("vita", "vita3k", "Vita3K", "Vita3K.exe", ("Juego.pkg", null));
        await using var _ = f;
        var calls = new List<IReadOnlyList<string>>();
        Content.Runner = (_, args) =>
        {
            calls.Add(args);
            return 0;
        };
        try
        {
            // Sin licencia: aviso, y no se ejecuta nada.
            Assert.Equal("license", Assert.Single(Content.Prepare(f.Db, 21, "vita3k").Steps!).Action);
            Assert.Empty(calls);
            Assert.Equal("content.badLicense", Content.SetLicense(f.Db, 21, "no es; una licencia")?.Code);
            Assert.Null(Content.SetLicense(f.Db, 21, " KO5ifR1dQ+eHBwcHB3ggAA== "));
            Assert.True(Content.HasLicense(f.Db, 21));
            Assert.Equal("installed", Assert.Single(Content.Prepare(f.Db, 21, "vita3k").Steps!).Action);
        }
        finally { Content.Runner = null; }
        var pkg = f.Tmp[@"E\Juegos\vita\Age of Calamity\Juego.pkg"];
        Assert.Equal(["--pkg", pkg, "--zrif", "KO5ifR1dQ+eHBwcHB3ggAA=="], Assert.Single(calls));
        // Se borra con una vacía, y solo se guarda en juegos de Vita.
        Assert.Null(Content.SetLicense(f.Db, 21, ""));
        Assert.False(Content.HasLicense(f.Db, 21));
        using (var c = f.Db.Open()) c.Execute("UPDATE version SET platform = 'ps3' WHERE id = 21");
        Assert.Equal("content.notVita", Content.SetLicense(f.Db, 21, "KO5i")?.Code);
    }

    [Fact]
    public async Task WiiUWarnsForEachUpdateAndDlc()
    {
        var (f, _) = Setup("wiiu", "cemu", "Cemu", "Cemu.exe");
        await using var __ = f;
        var disk = Disk.List(f.Db).Single().Id;
        foreach (var (folder, titleId) in new[] { ("Juego", "00050000101C9500"), ("Juego [UPD]", "0005000E101C9500"), ("Juego [DLC]", "0005000C101C9500") })
        {
            var relative = Path.Combine("Juegos", "wiiu", folder);
            Directory.CreateDirectory(f.Tmp[$@"E\{relative}\meta"]);
            using var c = f.Db.Open();
            c.Execute("INSERT INTO source (kind, library_id, relative_path, console, component, title_id, version_id) VALUES ('file', @disk, @relative, 'wiiu', 'BASE', @titleId, 21)",
                new { disk, relative, titleId });
        }
        var steps = Content.Prepare(f.Db, 21, "cemu").Steps!;
        Assert.Equal([("manual", "Juego [DLC]"), ("manual", "Juego [UPD]")], steps.Select(s => (s.Action, Path.GetFileName(s.File))));
    }

    [Fact]
    public async Task Ps4OnlyWarnsWhatToInstallInShadPs4()
    {
        var (f, _) = Setup("ps4", "shadps4", "shadPS4", "shadPS4.exe", ("CUSA00001-DLC.pkg", null));
        await using var __ = f;
        Content.Runner = (_, _) => throw new InvalidOperationException("no se ejecuta nada");
        try
        {
            Assert.Equal(new ContentStep("ps4", f.Tmp[@"E\Juegos\ps4\Age of Calamity\CUSA00001-DLC.pkg"]), Assert.Single(Content.Prepare(f.Db, 21, "shadps4").Steps!));
        }
        finally { Content.Runner = null; }
        Assert.Equal("content.notInstalled", Content.Prepare(f.Db, 21, "rpcs3").Error?.Code);
    }
}
