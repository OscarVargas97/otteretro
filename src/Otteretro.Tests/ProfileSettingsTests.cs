using Dapper;
using Otteretro.Api;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#276: ajustes y mandos por perfil, con las claves de la versión del emulador que usa (decisión 298).</summary>
public class ProfileSettingsTests
{
    const string Totk = "0100F2C0115B6000";

    internal static string Install(Fixture f, string id, string kind, string name, string exe, string version)
    {
        var dir = f.Tmp[$@"E\{(kind == "port" ? "Ports" : "Emuladores")}\{name}\{version}"];
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, exe), "");
        Profiles.AddEmulator(f.Db, id, kind, name);
        using var c = f.Db.Open();
        c.Execute("INSERT INTO emulator_install (emulator_id, version, origin, installed_at) VALUES (@id, @version, 'adopted', @now)",
            new { id, version, now = DateTime.UtcNow.ToString("O") });
        return dir;
    }

    /// <summary>Un juego con su ROM en el disco ★.</summary>
    internal static Fixture Game(string platform, string code, out string rom)
    {
        var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["E"], "E:");
        rom = Path.Combine(f.Tmp["E"], "juego.nsp");
        File.WriteAllText(rom, "");
        using var c = f.Db.Open();
        c.Execute("""
            INSERT INTO game (id, name_es) VALUES (10, 'Tears of the Kingdom');
            INSERT INTO version (id, game_id, platform) VALUES (20, 10, @platform);
            INSERT INTO source (kind, library_id, relative_path, version_id, component, title_id) VALUES ('file', @disk, 'juego.nsp', 20, 'BASE', @code);
            """, new { platform, code, disk });
        return f;
    }

    static ProfileSettingsRequest Request(string? version, params (string, string)[] options) =>
        new(options.ToDictionary(o => o.Item1, o => o.Item2), [], version);

    static Recipe Versioned => new("eden", "Eden", "emulator", "Switch", "eden.exe")
    {
        PerGame = new("user/config/custom/{titleId}.ini", "file") { IdCase = "upper" },
        OptionsUpTo = "0.10",
        Options =
        [
            new("resolution", "user/config/qt-config.ini", "ini", "resolution_setup", "Renderer"),
            new("fps", "user/config/qt-config.ini", "ini", "frame_limit", "Renderer", Since: "0.10"),
            new("vsync", "user/config/qt-config.ini", "ini", "vsync", "Renderer"),
            new("vsync", "user/config/qt-config.ini", "ini", "vsync_mode", "Renderer", Since: "0.10"),
        ],
    };

    [Fact]
    public void EachVersionUsesTheKeysOfTheHighestSinceThatDoesNotPassIt()
    {
        Assert.True(EmulatorSettings.Compare("0.10", "0.9") > 0);
        Assert.True(EmulatorSettings.Compare("2026.06.01", "2026.5.30") > 0);
        Assert.Equal(0, EmulatorSettings.Compare("1.0", "1"));

        var at9 = EmulatorSettings.For(Versioned, "0.9");
        Assert.Equal(["resolution", "vsync"], at9.Select(o => o.Name).Order());
        Assert.Equal("vsync", at9.Single(o => o.Name == "vsync").Key);
        var at10 = EmulatorSettings.For(Versioned, "0.10.2");
        Assert.Equal("vsync_mode", at10.Single(o => o.Name == "vsync").Key);
        Assert.Contains(at10, o => o.Name == "fps");

        // Una versión más nueva que la descrita usa la anterior más cercana, con aviso.
        Assert.Equal("vsync_mode", EmulatorSettings.For(Versioned, "0.11").Single(o => o.Name == "vsync").Key);
        Assert.True(EmulatorSettings.Undescribed(Versioned, "0.11"));
        Assert.False(EmulatorSettings.Undescribed(Versioned, "0.10"));
    }

    [Fact]
    public async Task PassingAProfileToAnotherVersionTakesWhatExistsAndKeepsTheRest()
    {
        await using var f = Game("switch", Totk, out _);
        Install(f, "eden", "emulator", "Eden", "eden.exe", "0.10");
        Install(f, "eden", "emulator", "Eden", "eden.exe", "0.9");
        var (id, _) = Profiles.Create(f.Db, 20, "eden", "2x");
        var ini = Path.Combine(Profiles.StoreOf(f.Db, id!.Value), $@"user\config\custom\{Totk}.ini");
        Profile Get() { using var c = f.Db.Open(); return Profiles.Get(c, id.Value)!; }
        void Set(string version)
        {
            using (var c = f.Db.Open())
                c.Execute("""UPDATE profile SET emulator_version = @version, settings = '{"options":{"resolution":"3","fps":"60","vsync":"2"}}' WHERE id = @id""", new { version, id });
            Assert.Null(EmulatorSettings.Apply(f.Db, Get(), recipe: Versioned));
        }

        Set("0.10");
        var text = File.ReadAllText(ini);
        Assert.Contains("frame_limit = 60", text);
        Assert.Contains("vsync_mode = 2", text);

        Set("0.9"); // la 0.9 no tiene fps; vsync tiene otro nombre
        text = File.ReadAllText(ini);
        Assert.Contains("resolution_setup = 3", text);
        Assert.Contains("vsync = 2", text);
        Assert.DoesNotContain("vsync_mode", text);
        Assert.Equal("60", EmulatorSettings.Read(Get().Settings).Options["fps"]); // guardado por si vuelve

        Set("0.10");
        Assert.Contains("frame_limit = 60", File.ReadAllText(ini));
    }

    [Fact]
    public async Task EdenProfileSettingsGoToItsGameConfigWithTheYuzuFlags()
    {
        await using var f = Game("switch", Totk, out var rom);
        var eden = Install(f, "eden", "emulator", "Eden", "eden.exe", "0.2.1");
        var (id, _) = Profiles.Create(f.Db, 20, "eden", "2x");
        Assert.Null(EmulatorSettings.Save(f.Db, id!.Value, Request(null, ("resolution", "3"), ("speed", "200"))));
        var stored = File.ReadAllText(Path.Combine(Profiles.StoreOf(f.Db, id.Value), $@"user\config\custom\{Totk}.ini"));
        Assert.Contains(@"resolution_setup\use_global = false", stored);
        Assert.Contains(@"resolution_setup\default = false", stored);
        Assert.Contains("resolution_setup = 3", stored);
        Assert.Contains("speed_limit = 200", stored);

        // Al activarlo entra al emulador; con el perfil activo, guardar lo cambia también ahí.
        Assert.Null(Profiles.Switch(f.Db, 20, id).Error);
        var live = Path.Combine(eden, $@"user\config\custom\{Totk}.ini");
        Assert.Contains("resolution_setup = 3", File.ReadAllText(live));
        Assert.Null(EmulatorSettings.Save(f.Db, id.Value, Request(null, ("resolution", ""))));
        Assert.DoesNotContain("resolution_setup", File.ReadAllText(live));
        Assert.Contains("speed_limit = 200", File.ReadAllText(live));

        // Cambiar la versión de un perfil cargado no: primero se activa otro.
        Assert.Equal("settings.versionWhileActive", EmulatorSettings.Save(f.Db, id.Value, Request("0.2.1"))?.Code);
        Assert.Equal("settings.badValue", EmulatorSettings.Save(f.Db, id.Value, Request(null, ("resolution", "3\n[Core]")))?.Code);

        // Abrir la configuración: en el juego, con su ROM.
        var (_, args, inGame, error) = EmulatorSettings.OpenArgs(f.Db, id.Value);
        Assert.Null(error);
        Assert.True(inGame);
        Assert.Equal(["-g", rom], args);
        File.Delete(rom); // sin la ROM, se abre normal (y se avisa)
        Assert.False(EmulatorSettings.OpenArgs(f.Db, id.Value).InGame);
    }

    [Fact]
    public async Task DefaultProfileWritesTheGlobalConfig()
    {
        await using var f = Game("gc", "GZ2E01", out _);
        var dolphin = Install(f, "dolphin", "emulator", "Dolphin", "Dolphin.exe", "2512");
        long id;
        using (var c = f.Db.Open()) id = c.ExecuteScalar<long>("SELECT id FROM profile WHERE emulator_id = 'dolphin' AND game_version_id IS NULL");
        Assert.Null(EmulatorSettings.Save(f.Db, id, Request(null, ("resolution", "3"), ("renderer", "Vulkan"))));
        var gfx = File.ReadAllText(Path.Combine(dolphin, @"User\Config\GFX.ini"));
        Assert.Contains("[Settings]", gfx);
        Assert.Contains("InternalResolution = 3", gfx);
        Assert.Contains("GFXBackend = Vulkan", File.ReadAllText(Path.Combine(dolphin, @"User\Config\Dolphin.ini")));
        Assert.Equal("3", EmulatorSettings.View(f.Db, id)!.Options.Single(o => o.Name == "resolution").Current);
    }

    [Fact]
    public async Task DolphinControllersAndSettingsPerGame()
    {
        await using var f = Game("gc", "GZ2E01", out _);
        var dolphin = Install(f, "dolphin", "emulator", "Dolphin", "Dolphin.exe", "2512");
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(dolphin, @"User\Config\Profiles\GCPad")).FullName, "Mando Pro.ini"), "");
        var (id, _) = Profiles.Create(f.Db, 20, "dolphin", "Con mando");
        var view = EmulatorSettings.View(f.Db, id!.Value)!;
        Assert.Equal(["Mando Pro"], view.InputProfiles);
        Assert.Contains(view.Input, i => i.Name == "PadProfile1");
        Assert.Null(EmulatorSettings.Save(f.Db, id.Value, new(new() { ["resolution"] = "4" }, new() { ["PadProfile1"] = "Mando Pro" }, null)));
        var ini = File.ReadAllText(Path.Combine(Profiles.StoreOf(f.Db, id.Value), @"User\GameSettings\GZ2E01.ini"));
        Assert.Contains("[Video_Settings]", ini); // la sección del juego, no la de GFX.ini
        Assert.Contains("InternalResolution = 4", ini);
        Assert.Contains("[Controls]", ini);
        Assert.Contains("PadProfile1 = Mando Pro", ini);
        Assert.Equal("settings.badValue", EmulatorSettings.Save(f.Db, id.Value, new([], new() { ["--input-profile"] = "x" }, null))?.Code);

        // Inactivo no se abre en el juego: su configuración no está en el emulador.
        Assert.Equal("settings.notActive", EmulatorSettings.OpenArgs(f.Db, id.Value).Error?.Code);
    }

    [Fact]
    public async Task DusklightProfileStartsFromTheGlobalAndKeepsTypes()
    {
        await using var f = Game("gc", "GZ2E01", out _);
        var dusk = Install(f, "dusklight", "port", "Dusklight", "dusklight.exe", "1.0.0");
        Directory.CreateDirectory(Path.Combine(dusk, "data"));
        File.WriteAllText(Path.Combine(dusk, @"data\config.json"), """{ "backend.isoPath": "E:\\tp.iso" }""");
        var (id, _) = Profiles.Create(f.Db, 20, "dusklight", "60 fps");
        Assert.Null(EmulatorSettings.Save(f.Db, id!.Value, Request(null, ("fps", "60"), ("vsync", "false"), ("renderer", "vulkan"))));
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(Profiles.StoreOf(f.Db, id.Value), @"data\config.json")))!;
        Assert.Equal(@"E:\tp.iso", (string)json["backend.isoPath"]!); // nace de la global: `swap` la cambia entera
        Assert.Equal(60, (int)json["video.maxFrameRate"]!);
        Assert.False((bool)json["video.enableVsync"]!);
        Assert.Equal("vulkan", (string)json["backend.graphicsBackend"]!);
        // Un port es el juego: se abre en él sin estar activo (su configuración entra al lanzar).
        Assert.True(EmulatorSettings.OpenArgs(f.Db, id.Value).InGame);
    }
}
