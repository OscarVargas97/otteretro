using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#267: emuladores compartidos, el base de cada consola, perfiles por juego con uno activo y varias ROMs.</summary>
public class ProfilesTests
{
    static Fixture Switch()
    {
        var f = new Fixture();
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO game (id, name_es) VALUES (10, 'Tears of the Kingdom'), (11, 'Age of Calamity');
                INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'switch'), (21, 11, 'switch');
                """);
        Profiles.AddEmulator(f.Db, "eden", "emulator", "Eden", "Switch");
        Profiles.AddEmulator(f.Db, "ryubing", "emulator", "Ryubing", "Switch");
        Profiles.SetBase(f.Db, "switch", "ryubing");
        Profiles.SetBase(f.Db, "switch", "eden");
        return f;
    }

    [Fact]
    public async Task GameWithoutProfileUsesTheBaseEmulatorDefault()
    {
        await using var f = Switch();
        var effective = Profiles.Effective(f.Db, 21);
        Assert.NotNull(effective);
        Assert.Equal("eden", effective.EmulatorId);
        Assert.Null(effective.GameVersionId);
        // Ryubing quedó de alternativo, y una consola tiene un solo base.
        using var c = f.Db.Open();
        Assert.Equal([("eden", 1L), ("ryubing", 0L)], c.Query<(string, long)>("SELECT emulator_id, is_base FROM console_emulator ORDER BY emulator_id"));
        Assert.Throws<SqliteException>(() => c.Execute("UPDATE console_emulator SET is_base = 1"));
    }

    [Fact]
    public async Task OnlyOneActiveProfilePerGame()
    {
        await using var f = Switch();
        var x2 = Profiles.Add(f.Db, 20, "eden", "2x");
        var plain = Profiles.Add(f.Db, 20, "ryubing", "Ryubing");
        var ryu = Profiles.Add(f.Db, 21, "ryubing", "Ryubing");
        Assert.True(Profiles.Activate(f.Db, x2));
        Assert.True(Profiles.Activate(f.Db, ryu));
        Assert.Equal(x2, Profiles.Effective(f.Db, 20)!.Id);
        Assert.Equal("ryubing", Profiles.Effective(f.Db, 21)!.EmulatorId);

        Assert.True(Profiles.Activate(f.Db, plain));
        Assert.Equal(plain, Profiles.Effective(f.Db, 20)!.Id);
        Assert.Equal(ryu, Profiles.Effective(f.Db, 21)!.Id); // el otro juego no cambia

        using var c = f.Db.Open();
        Assert.Throws<SqliteException>(() => c.Execute("UPDATE profile SET active = 1 WHERE id = @x2", new { x2 }));
        // El perfil por defecto no se activa: es de todos los juegos sin perfil.
        var fallback = c.ExecuteScalar<long>("SELECT id FROM profile WHERE emulator_id = 'eden' AND game_version_id IS NULL");
        Assert.False(Profiles.Activate(f.Db, fallback));
        Assert.Throws<SqliteException>(() => c.Execute("INSERT INTO profile (emulator_id, name, created_at) VALUES ('eden', 'otro', '')"));
    }

    [Fact]
    public async Task ProfilePicksAModifiedRomAndForgetsItWhenTheFileGoes()
    {
        await using var f = Switch();
        long rom;
        using (var c = f.Db.Open())
        {
            c.Execute("INSERT INTO source (kind, library_id, relative_path, version_id, component) VALUES ('file', 'd', 'base.nsp', 20, 'BASE')");
            rom = c.ExecuteScalar<long>("""
                INSERT INTO source (kind, library_id, relative_path, version_id, component, modified, label)
                VALUES ('file', 'd', 'traduccion.nsp', 20, 'BASE', 1, 'Traducción') RETURNING id
                """);
        }
        var profile = Profiles.Add(f.Db, 20, "eden", "Traducido", source: rom);
        Assert.True(Profiles.Activate(f.Db, profile));
        Assert.Equal(rom, Profiles.Effective(f.Db, 20)!.SourceId);

        // Quitar el archivo (un escaneo que ya no lo ve) no rompe: el perfil vuelve a la base.
        using (var c = f.Db.Open()) c.Execute("DELETE FROM source WHERE id = @rom", new { rom });
        Assert.Null(Profiles.Effective(f.Db, 20)!.SourceId);
    }
}
