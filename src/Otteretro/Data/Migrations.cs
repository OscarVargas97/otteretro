using System.Runtime.InteropServices;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Otteretro.Data;

/// <summary>Aplica en orden los Data/Migrations/NNNN_nombre.sql que aún no se aplicaron (A1).</summary>
public static class Migrations
{
    const string Marker = ".Migrations.";

    /// <summary>Devuelve cuántas migraciones aplicó.</summary>
    public static int Apply(Database db)
    {
        using var c = db.Open();
        RenameLegacyLog(c);
        c.Execute("CREATE TABLE IF NOT EXISTS migration (name TEXT PRIMARY KEY, applied_at TEXT NOT NULL)");
        var done = c.Query<string>("SELECT name FROM migration").ToHashSet();
        var assembly = typeof(Migrations).Assembly;
        var applied = 0;

        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(r => r.Contains(Marker) && r.EndsWith(".sql", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var name = resource[(resource.IndexOf(Marker, StringComparison.Ordinal) + Marker.Length)..];
            if (done.Contains(name)) continue;

            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            using var t = c.BeginTransaction();
            c.Execute(reader.ReadToEnd(), transaction: t);
            c.Execute("INSERT INTO migration (name, applied_at) VALUES (@name, @date)",
                new { name, date = DateTime.UtcNow.ToString("O") }, t);
            t.Commit();
            applied++;
        }
        return applied;
    }

    /// <summary>
    /// Las bases creadas con F0 llevan el registro de migraciones en `migracion (nombre, aplicada)`.
    /// Se renombra antes de leerlo; los nombres de las migraciones ya aplicadas no cambian (#139).
    /// </summary>
    static void RenameLegacyLog(SqliteConnection c)
    {
        if (c.ExecuteScalar<int>("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'migracion'") == 0) return;
        using var t = c.BeginTransaction();
        c.Execute("""
            ALTER TABLE migracion RENAME TO migration;
            ALTER TABLE migration RENAME COLUMN nombre TO name;
            ALTER TABLE migration RENAME COLUMN aplicada TO applied_at;
            """, transaction: t);
        t.Commit();
    }

    /// <summary>Registra o actualiza este equipo (lo que depende de cada PC se guarda por equipo, §7.6).</summary>
    public static void RegisterMachine(Database db, string id)
    {
        using var c = db.Open();
        c.Execute("""
            INSERT INTO machine (id, name, os) VALUES (@id, @name, @os)
            ON CONFLICT(id) DO UPDATE SET name = excluded.name, os = excluded.os
            """, new { id, name = Environment.MachineName, os = RuntimeInformation.OSDescription });
    }
}
