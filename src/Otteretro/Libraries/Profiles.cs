using Dapper;
using Otteretro.Data;

namespace Otteretro.Libraries;

/// <param name="EmulatorVersion">La versión instalada que usa; null: la más nueva.</param>
/// <param name="GameVersionId">La versión del catálogo (el juego en su consola); null: el perfil por defecto del emulador.</param>
/// <param name="SourceId">La ROM que usa; null: la base.</param>
public record Profile(long Id, string EmulatorId, string? EmulatorVersion, long? GameVersionId, string Name, string Settings, long? SourceId);

/// <summary>
/// Emuladores compartidos y perfiles por juego (§8.0, #267): cada consola tiene un emulador base, cada emulador un perfil por
/// defecto y cada juego, a lo más, un perfil activo. Cambiar los archivos del emulador al activar otro perfil es aparte.
/// </summary>
public static class Profiles
{
    const string Columns = "id, emulator_id AS EmulatorId, emulator_version AS EmulatorVersion, game_version_id AS GameVersionId, name, settings, source_id AS SourceId";

    static string Now() => DateTime.UtcNow.ToString("O");

    /// <summary>Registra un emulador (si no estaba) con su perfil por defecto.</summary>
    public static void AddEmulator(Database db, string id, string kind, string name, string folder)
    {
        using var c = db.Open();
        using var t = c.BeginTransaction();
        if (c.Execute("INSERT OR IGNORE INTO emulator (id, kind, name, folder) VALUES (@id, @kind, @name, @folder)", new { id, kind, name, folder }, t) > 0)
            c.Execute("INSERT INTO profile (emulator_id, name, created_at) VALUES (@id, @name, @now)", new { id, name, now = Now() }, t);
        t.Commit();
    }

    /// <summary>El emulador base de la consola; el que lo era sigue sirviéndole como alternativo.</summary>
    public static void SetBase(Database db, string console, string emulator)
    {
        using var c = db.Open();
        using var t = c.BeginTransaction();
        c.Execute("UPDATE console_emulator SET is_base = 0 WHERE console_id = @console", new { console }, t);
        c.Execute("""
            INSERT INTO console_emulator (console_id, emulator_id, is_base) VALUES (@console, @emulator, 1)
            ON CONFLICT (console_id, emulator_id) DO UPDATE SET is_base = 1
            """, new { console, emulator }, t);
        t.Commit();
    }

    /// <summary>Un perfil nuevo de un juego, inactivo.</summary>
    public static long Add(Database db, long gameVersion, string emulator, string name, string? emulatorVersion = null, long? source = null)
    {
        using var c = db.Open();
        return c.ExecuteScalar<long>("""
            INSERT INTO profile (emulator_id, emulator_version, game_version_id, name, source_id, created_at)
            VALUES (@emulator, @emulatorVersion, @gameVersion, @name, @source, @now) RETURNING id
            """, new { emulator, emulatorVersion, gameVersion, name, source, now = Now() });
    }

    /// <summary>Deja ese perfil como el activo de su juego y apaga el que lo era. False si no existe o es un perfil por defecto.</summary>
    public static bool Activate(Database db, long id)
    {
        using var c = db.Open();
        using var t = c.BeginTransaction();
        if (c.ExecuteScalar<long?>("SELECT game_version_id FROM profile WHERE id = @id", new { id }, t) is not { } game) return false;
        c.Execute("UPDATE profile SET active = 0 WHERE game_version_id = @game AND active = 1", new { game }, t);
        c.Execute("UPDATE profile SET active = 1 WHERE id = @id", new { id }, t);
        t.Commit();
        return true;
    }

    /// <summary>
    /// Con qué se juega: el perfil activo del juego o, si no tiene, el perfil por defecto del emulador base de su consola. Null si
    /// la consola no tiene emulador base.
    /// </summary>
    public static Profile? Effective(Database db, long gameVersion)
    {
        using var c = db.Open();
        return c.QuerySingleOrDefault<Profile>($"SELECT {Columns} FROM profile WHERE game_version_id = @gameVersion AND active = 1", new { gameVersion })
            ?? c.QuerySingleOrDefault<Profile>($"""
                SELECT {Columns} FROM profile WHERE game_version_id IS NULL AND emulator_id = (
                    SELECT e.emulator_id FROM console_emulator e JOIN version v ON v.platform = e.console_id
                    WHERE v.id = @gameVersion AND e.is_base = 1)
                """, new { gameVersion });
    }
}
