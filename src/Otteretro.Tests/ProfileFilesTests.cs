using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#269: activar un perfil guarda lo del que estaba, lo quita del emulador y carga el nuevo; volver lo restaura tal cual.</summary>
public class ProfileFilesTests
{
    const string Totk = "0100F2C0115B6000";

    /// <summary>Una versión instalada a mano en el disco ★, registrada como las que instala la app.</summary>
    static string Install(Fixture f, string id, string kind, string name, string exe, string version = "1.0")
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

    static Fixture Game(string platform, string code, string name = "Tears of the Kingdom")
    {
        var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["E"], "E:");
        using var c = f.Db.Open();
        c.Execute("""
            INSERT INTO game (id, name_es) VALUES (10, @name);
            INSERT INTO version (id, game_id, platform) VALUES (20, 10, @platform);
            INSERT INTO source (kind, library_id, relative_path, version_id, component, title_id) VALUES ('file', 'd', 'juego', 20, 'BASE', @code);
            """, new { name, platform, code });
        return f;
    }

    static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    static long Create(Fixture f, string emulator, string name, string from = "empty", long? copy = null)
    {
        var (id, error) = Profiles.Create(f.Db, 20, emulator, name, from, copy);
        Assert.Null(error);
        return id!.Value;
    }

    static SaveNotice? Activate(Fixture f, long? profile)
    {
        var (error, saves) = Profiles.Switch(f.Db, 20, profile);
        Assert.Null(error);
        return saves;
    }

    [Fact]
    public async Task EdenSwitchesConfigAndModsAndGoingBackRestoresEverything()
    {
        await using var f = Game("switch", Totk.ToLowerInvariant()); // Eden lo escribe en mayúsculas (idCase)
        var eden = Install(f, "eden", "emulator", "Eden", "eden.exe");
        var ini = Path.Combine(eden, $@"user\config\custom\{Totk}.ini");
        var mods = Path.Combine(eden, $@"user\load\{Totk}");
        // Lo que la persona ya tenía, sin perfil.
        Write(ini, "suelto");
        Write(Path.Combine(mods, @"Mio\romfs\a.bin"), "mod suelto");

        var x2 = Create(f, "eden", "2x");
        Activate(f, x2);
        Assert.False(File.Exists(ini)); // un perfil vacío: el juego usa la global
        Assert.True(Links.IsLink(mods));
        Assert.Empty(Directory.EnumerateFileSystemEntries(mods));

        // Lo que se cambia en el emulador queda en el perfil.
        Write(ini, "2x");
        Write(Path.Combine(mods, @"60fps\exefs\p.ips"), "60");
        Assert.True(File.Exists(Path.Combine(Profiles.StoreOf(f.Db, x2), $@"user\load\{Totk}\60fps\exefs\p.ips")));

        var copy = Create(f, "eden", "2x sin mods", "copy", x2); // duplica el activo con lo último del emulador
        Activate(f, copy);
        Assert.Equal("2x", File.ReadAllText(ini));
        Assert.Equal("2x", File.ReadAllText(Path.Combine(Profiles.StoreOf(f.Db, x2), $@"user\config\custom\{Totk}.ini")));
        File.Delete(Path.Combine(mods, @"60fps\exefs\p.ips"));
        Write(ini, "copia");

        Activate(f, x2);
        Assert.Equal("2x", File.ReadAllText(ini));
        Assert.Equal("60", File.ReadAllText(Path.Combine(mods, @"60fps\exefs\p.ips")));
        Assert.Equal("copia", File.ReadAllText(Path.Combine(Profiles.StoreOf(f.Db, copy), $@"user\config\custom\{Totk}.ini")));
        Assert.Equal(x2, Profiles.Effective(f.Db, 20)!.Id);

        // Sin perfil: vuelve lo de antes, como carpeta real.
        Activate(f, null);
        Assert.Equal("suelto", File.ReadAllText(ini));
        Assert.False(Links.IsLink(mods));
        Assert.Equal("mod suelto", File.ReadAllText(Path.Combine(mods, @"Mio\romfs\a.bin")));
        Assert.Null(Profiles.Effective(f.Db, 20)!.GameVersionId);
        Activate(f, x2);
        Assert.Equal("2x", File.ReadAllText(ini));
    }

    [Fact]
    public async Task RyubingProfileStartsFromTheGlobalAndKeepsItsFiles()
    {
        await using var f = Game("switch", Totk);
        Install(f, "eden", "emulator", "Eden", "eden.exe"); // el base de Switch
        var ryu = Install(f, "ryubing", "emulator", "Ryubing", "Ryujinx.exe");
        var id = Totk.ToLowerInvariant();
        Write(Path.Combine(ryu, @"portable\Config.json"), """{"res_scale":1}""");
        var game = Path.Combine(ryu, $@"portable\games\{id}");
        Write(Path.Combine(game, "mods.json"), "suelto");

        var p = Create(f, "ryubing", "Ryubing"); // full: nace de la global, como el emulador
        Activate(f, p);
        Assert.Equal("""{"res_scale":1}""", File.ReadAllText(Path.Combine(game, "Config.json")));
        Assert.False(File.Exists(Path.Combine(game, "mods.json")));
        Assert.True(Links.IsLink(Path.Combine(ryu, $@"portable\mods\contents\{id}")));
        Write(Path.Combine(game, "Config.json"), """{"res_scale":2}""");
        Write(Path.Combine(game, "dlc.json"), "dlc");

        Activate(f, null); // vuelve a Eden: lo de Ryubing se guarda en el perfil y lo suelto de Ryubing vuelve
        Assert.Equal("suelto", File.ReadAllText(Path.Combine(game, "mods.json")));
        Assert.False(File.Exists(Path.Combine(game, "Config.json")));
        Activate(f, p);
        Assert.Equal("""{"res_scale":2}""", File.ReadAllText(Path.Combine(game, "Config.json")));
        Assert.Equal("dlc", File.ReadAllText(Path.Combine(game, "dlc.json")));
    }

    [Fact]
    public async Task DolphinTurnsOnHiresTexturesWhenTheProfileHasMods()
    {
        await using var f = Game("gc", "GZ2E01", "The Legend of Zelda: Twilight Princess");
        var dolphin = Install(f, "dolphin", "emulator", "Dolphin", "Dolphin.exe");
        var gfx = Path.Combine(dolphin, @"User\Config\GFX.ini");
        Write(gfx, "[Settings]\nInternalResolution = 3\n");
        var plain = Create(f, "dolphin", "Sin texturas");
        Activate(f, plain);
        Assert.DoesNotContain("HiresTextures", File.ReadAllText(gfx));

        var hd = Create(f, "dolphin", "Texturas HD");
        Write(Path.Combine(Profiles.StoreOf(f.Db, hd), @"User\Load\Textures\GZ2E01\link.png"), "png");
        Write(Path.Combine(Profiles.StoreOf(f.Db, hd), @"User\GameSettings\GZ2E01.ini"), "[Video_Settings]");
        Activate(f, hd);
        Assert.Contains("HiresTextures = True", File.ReadAllText(gfx));
        Assert.Contains("InternalResolution = 3", File.ReadAllText(gfx));
        Assert.Equal("png", File.ReadAllText(Path.Combine(dolphin, @"User\Load\Textures\GZ2E01\link.png")));
        Assert.Equal("[Video_Settings]", File.ReadAllText(Path.Combine(dolphin, @"User\GameSettings\GZ2E01.ini")));
        Activate(f, plain);
        Assert.False(File.Exists(Path.Combine(dolphin, @"User\GameSettings\GZ2E01.ini")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(dolphin, @"User\Load\Textures\GZ2E01")));
    }

    [Fact]
    public async Task DusklightPassesModsAndSwapsTheGlobalAndRestoresIt()
    {
        await using var f = Game("gc", "GZ2E01", "The Legend of Zelda: Twilight Princess");
        var dusk = Install(f, "dusklight", "port", "Dusklight", "dusklight.exe");
        var config = Path.Combine(dusk, @"data\config.json");
        Write(config, "global");
        // Un port aparece en la ficha del juego que cubre aunque no sea de su consola.
        Assert.Contains(Profiles.OfGame(f.Db, 10).Single().Emulators, e => e.Id == "dusklight");

        var p = Create(f, "dusklight", "60 fps");
        Activate(f, p);
        Assert.Equal("global", File.ReadAllText(config)); // swap: no se toca al activar, solo al lanzar
        var args = Profiles.LaunchArgs(f.Db, p);
        Assert.Equal("--mods", args[0]);
        Assert.True(Disk.Same(Path.Combine(Profiles.StoreOf(f.Db, p), @"data\mods"), args[1]));

        Profiles.SwapIn(f.Db, p);
        Assert.Equal("global", File.ReadAllText(config)); // perfil vacío: usa la global
        Write(config, "perfil"); // lo que cambia el juego mientras corre es del perfil
        Profiles.SwapOut(f.Db, "dusklight");
        Assert.Equal("global", File.ReadAllText(config));
        Profiles.SwapIn(f.Db, p);
        Assert.Equal("perfil", File.ReadAllText(config));
        Profiles.SwapIn(f.Db, p); // una marca que quedó (la app se cayó) se restaura antes de volver a cambiar
        Profiles.SwapOut(f.Db, "dusklight");
        Assert.Equal("global", File.ReadAllText(config));
    }

    [Fact]
    public async Task NothingMovesWhenTheProfileCannotBeApplied()
    {
        await using var f = Game("switch", Totk);
        using (var c = f.Db.Open()) c.Execute("UPDATE source SET title_id = NULL");
        var eden = Install(f, "eden", "emulator", "Eden", "eden.exe");
        Write(Path.Combine(eden, @"user\config\custom\x.ini"), "x");
        var p = Create(f, "eden", "2x");
        Assert.Equal("profile.noGameKey", Profiles.Switch(f.Db, 20, p).Error?.Code);
        Assert.Null(Profiles.Effective(f.Db, 20)!.GameVersionId);
        Assert.Equal("profile.badRom", Profiles.Update(f.Db, p, "2x", 999)?.Code);
    }

    // ── Partidas al cambiar de emulador (#270) ─────────────────────────────────────────────────────────────────────────────

    const string User = "00112233445566778899AABBCCDDEEFF";

    /// <summary>Un índice `imkvdb.arc` de Ryujinx con la partida de cuenta de cada juego.</summary>
    static void Index(string ryu, params (string TitleId, ulong SaveId)[] entries)
    {
        using var data = new MemoryStream();
        using var w = new BinaryWriter(data);
        w.Write("IMKV"u8);
        w.Write(0);
        w.Write(entries.Length);
        foreach (var (title, save) in entries)
        {
            w.Write("IMEN"u8);
            w.Write(0x40);
            w.Write(0x40);
            var key = new byte[0x40];
            BitConverter.GetBytes(Convert.ToUInt64(title, 16)).CopyTo(key, 0);
            key[0x20] = 1; // cuenta
            w.Write(key);
            var value = new byte[0x40];
            BitConverter.GetBytes(save).CopyTo(value, 0);
            w.Write(value);
        }
        w.Flush();
        var file = Path.Combine(ryu, @"portable\bis\system\save\8000000000000000\0\imkvdb.arc");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, data.ToArray());
    }

    [Fact]
    public async Task SwitchSavesGoBetweenEdenAndRyubingWithABackupAndTheOriginalUntouched()
    {
        await using var f = Game("switch", Totk);
        var eden = Install(f, "eden", "emulator", "Eden", "eden.exe");
        var ryu = Install(f, "ryubing", "emulator", "Ryubing", "Ryujinx.exe");
        var edenSave = Path.Combine(eden, $@"user\nand\user\save\0000000000000000\{User}\{Totk}");
        Write(Path.Combine(edenSave, "progress.sav"), "eden");
        Index(ryu, ("0100000000010000", 4), (Totk, 5));
        var ryuSave = Path.Combine(ryu, @"portable\bis\user\save\0000000000000005\0");
        Write(Path.Combine(ryuSave, "progress.sav"), "ryu vieja");
        Write(Path.Combine(ryuSave, "otro.sav"), "ryu otro");

        var notice = Activate(f, Create(f, "ryubing", "Ryubing"));
        Assert.Equal("saves.copied", notice?.Code);
        Assert.Equal("eden", File.ReadAllText(Path.Combine(ryuSave, "progress.sav")));
        Assert.False(File.Exists(Path.Combine(ryuSave, "otro.sav"))); // se reemplaza, no se mezcla
        Assert.Equal("eden", File.ReadAllText(Path.Combine(edenSave, "progress.sav"))); // el origen no se toca
        Assert.Equal("ryu vieja", File.ReadAllText(Path.Combine(notice!.Backup!, @"0\progress.sav")));
        Assert.Equal("ryu otro", File.ReadAllText(Path.Combine(notice.Backup!, @"0\otro.sav")));

        // Se juega en Ryubing y se vuelve a Eden: llega lo nuevo, y lo de Eden queda respaldado.
        File.WriteAllText(Path.Combine(ryuSave, "progress.sav"), "ryu nueva");
        notice = Activate(f, null);
        Assert.Equal("saves.copied", notice?.Code);
        Assert.Equal("ryu nueva", File.ReadAllText(Path.Combine(edenSave, "progress.sav")));
        Assert.Equal("eden", File.ReadAllText(Path.Combine(notice!.Backup!, "progress.sav")));
    }

    [Fact]
    public async Task SwitchSavesWarnWhenTheOtherEmulatorHasNoPlaceYet()
    {
        await using var f = Game("switch", Totk);
        var eden = Install(f, "eden", "emulator", "Eden", "eden.exe");
        Install(f, "ryubing", "emulator", "Ryubing", "Ryujinx.exe");
        var citron = Install(f, "citron", "emulator", "Citron", "citron.exe");
        Write(Path.Combine(eden, $@"user\nand\user\save\0000000000000000\{User}\{Totk}\progress.sav"), "eden");

        // Ryubing sin el juego en su índice: hay que abrirlo una vez ahí. El perfil se cambia igual.
        var ryubing = Create(f, "ryubing", "Ryubing");
        Assert.Equal("saves.needsFirstRun", Activate(f, ryubing)?.Code);
        Assert.Equal(ryubing, Profiles.Effective(f.Db, 20)!.Id);

        // Ryubing no tiene partida: a Citron no llega nada. Desde Eden, Citron la recibe con el mismo usuario.
        var c = Create(f, "citron", "Citron");
        Assert.Equal("saves.none", Activate(f, c)?.Code);
        Activate(f, null);
        Assert.Equal("saves.copied", Activate(f, c)?.Code);
        Assert.Equal("eden", File.ReadAllText(Path.Combine(citron, $@"user\nand\user\save\0000000000000000\{User}\{Totk}\progress.sav")));
    }

    [Fact]
    public async Task GameCubeSavesToDusklightAreOnlyAWarning()
    {
        await using var f = Game("gc", "GZ2E01", "The Legend of Zelda: Twilight Princess");
        var dolphin = Install(f, "dolphin", "emulator", "Dolphin", "Dolphin.exe");
        var dusk = Install(f, "dusklight", "port", "Dusklight", "dusklight.exe");
        Write(Path.Combine(dolphin, @"User\GC\USA\Card A\01-GZ2E-gczelda2.gci"), "gci");
        Assert.Equal("saves.untested", Activate(f, Create(f, "dusklight", "Dusklight"))?.Code);
        Assert.False(Directory.Exists(Path.Combine(dusk, @"data\USA")));
    }
}
