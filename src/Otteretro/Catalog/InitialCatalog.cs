using System.IO.Compression;
using Dapper;
using Otteretro.Data;

namespace Otteretro.Catalog;

/// <summary>
/// El catálogo que trae la app (#211, decisión 250): quien la instala tiene las sagas y los juegos desde el primer momento, sin claves ni días de carga.
/// Lo genera y publica `otteretro-catalogo`; después, <see cref="CatalogUpdate"/> lo mantiene al día.
/// </summary>
public static class InitialCatalog
{
    /// <summary>El archivo que viene con la app. Sin él (las pruebas, con OTTERETRO_E2E) no se copia nada.</summary>
    public static string? BundledFile { get; set; } = Environment.GetEnvironmentVariable("OTTERETRO_E2E") is null
        ? Path.Combine(AppContext.BaseDirectory, "catalogo", "catalogo-inicial.db.gz")
        : null;

    /// <summary>Lo que se copia, con cada tabla antes de las que apuntan a ella.</summary>
    static readonly string[] Shared = ["console", "console_platform", "saga", "game", "version", "version_criterion", "field_value", "route", "route_step", "image", "version_code"];

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
            c.Execute("UPDATE cache_generation SET value = value + 1", transaction: t); // Sagas guardado ya no vale (#225)
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
