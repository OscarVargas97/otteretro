using System.IO.Compression;
using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Data;

namespace Otteretro.Catalog;

/// <summary>
/// El catálogo que trae la app (#211, decisión 250): quien la instala tiene las sagas y los juegos desde el primer momento, sin claves ni días de carga.
/// Solo sale de `catalogo.db`, nunca de la base propia, y lo que no esté en <see cref="Shared"/> no se publica: una tabla nueva queda fuera hasta decidirlo.
/// </summary>
public static class InitialCatalog
{
    /// <summary>El archivo que viene con la app. Sin él (las pruebas, con OTTERETRO_E2E) no se copia nada.</summary>
    public static string? BundledFile { get; set; } = Environment.GetEnvironmentVariable("OTTERETRO_E2E") is null
        ? Path.Combine(AppContext.BaseDirectory, "catalogo", "catalogo-inicial.db.gz")
        : null;

    /// <summary>Lo que se publica, con cada tabla antes de las que apuntan a ella. `claude_fingerprint` evita que Claude vuelva a juzgar lo ya juzgado: son hashes.</summary>
    static readonly string[] Shared = ["console", "console_platform", "saga", "game", "version", "version_criterion", "field_value", "route", "route_step", "claude_fingerprint"];

    /// <summary>Escribe una copia comprimida del catálogo, sin nada propio: ni tus correcciones a mano, ni tus reglas para Claude, ni qué consolas habilitaste.</summary>
    public static void Export(Database db, string path)
    {
        var temp = path + ".tmp";
        File.Delete(temp);
        using (var c = db.Open()) c.Execute("VACUUM catalog INTO @temp", new { temp });
        using (var copy = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp, Pooling = false }.ToString()))
        {
            copy.Open();
            foreach (var table in copy.Query<string>("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'").ToList())
                if (!Shared.Contains(table)) copy.Execute($"DROP TABLE \"{table}\"");
            copy.Execute("""
                DELETE FROM field_value WHERE manual = 1;
                UPDATE console SET enabled = 1, refreshed_at = NULL;
                VACUUM;
                """);
        }
        using (var input = File.OpenRead(temp))
        using (var output = new GZipStream(File.Create(path), CompressionLevel.SmallestSize))
            input.CopyTo(output);
        File.Delete(temp);
    }

    /// <summary>
    /// Llena un catálogo vacío con el que trae la app. Uno con juegos no se toca: tus datos apuntan a sus ids. Todo o nada, y solo las columnas que
    /// existen en los dos lados: el archivo puede ser de una versión anterior.
    /// </summary>
    /// <returns>Si lo copió.</returns>
    public static bool Seed(Database db)
    {
        if (BundledFile is not { } file || !File.Exists(file)) return false;
        using var c = db.Open();
        if (c.ExecuteScalar<long>("SELECT count(*) FROM catalog.game") > 0) return false;
        var temp = Path.Combine(Path.GetDirectoryName(db.CatalogPath)!, "catalogo-inicial.tmp");
        using (var input = new GZipStream(File.OpenRead(file), CompressionMode.Decompress))
        using (var output = File.Create(temp))
            input.CopyTo(output);
        c.Execute("ATTACH DATABASE @temp AS initial", new { temp });
        try
        {
            using var t = c.BeginTransaction();
            foreach (var table in Shared)
            {
                var columns = string.Join(", ", c.Query<string>($"SELECT name FROM pragma_table_info('{table}', 'catalog') WHERE name IN (SELECT name FROM pragma_table_info('{table}', 'initial'))", transaction: t));
                // Las consolas ya vienen de las migraciones.
                if (columns.Length > 0) c.Execute($"INSERT OR IGNORE INTO catalog.{table} ({columns}) SELECT {columns} FROM initial.{table}", transaction: t);
            }
            t.Commit();
        }
        finally
        {
            c.Execute("DETACH DATABASE initial");
            File.Delete(temp);
        }
        return true;
    }
}
