using Dapper;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Tests;

public class DataTests
{
    [Fact]
    public void MigrationsAreIdempotent()
    {
        using var tmp = new TempDir();
        var db = new Database(tmp["otteretro.db"]);
        Assert.True(Migrations.Apply(db) > 0);
        Assert.Equal(0, Migrations.Apply(db));

        using var c = db.Open();
        Assert.Equal("wal", c.ExecuteScalar<string>("PRAGMA journal_mode"));
        Assert.Equal(1, c.ExecuteScalar<int>("SELECT count(*) FROM sqlite_master WHERE name = 'machine'"));
    }

    /// <summary>F1.4: las 24 plataformas quedan cargadas y la saga no lleva plataforma (#131).</summary>
    [Fact]
    public void CatalogHasThe24Platforms()
    {
        using var tmp = new TempDir();
        var db = new Database(tmp["otteretro.db"]);
        Migrations.Apply(db);
        using var c = db.Open();
        Assert.Equal(26, c.ExecuteScalar<int>("SELECT count(*) FROM console"));
        Assert.Equal(24, c.ExecuteScalar<int>("SELECT count(*) FROM console WHERE company IS NOT NULL"));
        Assert.Equal(0, c.ExecuteScalar<int>("SELECT count(*) FROM pragma_table_info('saga') WHERE name = 'platform'"));

        // Una saga con versiones de PC y de consola, sin duplicar la saga.
        c.Execute("""
            INSERT INTO saga (id, name) VALUES (1, 'Half-Life');
            INSERT INTO game (id, saga_id, name_es) VALUES (1, 1, 'Half-Life');
            INSERT INTO version (game_id, platform) VALUES (1, 'pc'), (1, 'ps2');
            INSERT INTO progress (game_id, status, rating) VALUES (1, 'finished', 9);
            """);
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => c.Execute("INSERT INTO version (game_id, platform) VALUES (1, 'dreamcast')"));
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => c.Execute("UPDATE progress SET rating = 11 WHERE game_id = 1"));
    }

    [Fact]
    public void RegisteringTheMachineTwiceDoesNotDuplicate()
    {
        using var tmp = new TempDir();
        var db = new Database(tmp["otteretro.db"]);
        Migrations.Apply(db);
        Migrations.RegisterMachine(db, "pc-1");
        Migrations.RegisterMachine(db, "pc-1");
        using var c = db.Open();
        Assert.Equal(1, c.ExecuteScalar<int>("SELECT count(*) FROM machine"));
    }

    /// <summary>Una base tal como la dejaba F0 (tablas en español) se actualiza sin perder lo que tenía (#139).</summary>
    [Fact]
    public void DatabaseFromF0IsUpgraded()
    {
        using var tmp = new TempDir();
        var db = new Database(tmp["otteretro.db"]);
        using (var c = db.Open())
            c.Execute("""
                CREATE TABLE migracion (nombre TEXT PRIMARY KEY, aplicada TEXT NOT NULL);
                CREATE TABLE equipo (id TEXT PRIMARY KEY, nombre TEXT NOT NULL, so TEXT NOT NULL);
                CREATE TABLE ajuste (clave TEXT NOT NULL, valor TEXT NOT NULL, equipo_id TEXT NOT NULL DEFAULT '', PRIMARY KEY (clave, equipo_id));
                INSERT INTO migracion VALUES ('0001_equipo.sql', '2026-10-01T00:00:00Z');
                INSERT INTO equipo VALUES ('pc-viejo', 'PC', 'Windows');
                INSERT INTO ajuste VALUES ('tema', 'oscuro', '');
                """);

        // Se aplican todas menos la 0001, que ya estaba.
        var total = typeof(Migrations).Assembly.GetManifestResourceNames().Count(r => r.EndsWith(".sql", StringComparison.Ordinal));
        Assert.Equal(total - 1, Migrations.Apply(db));

        using var after = db.Open();
        Assert.Equal("PC", after.ExecuteScalar<string>("SELECT name FROM machine WHERE id = 'pc-viejo'"));
        Assert.Equal("oscuro", after.ExecuteScalar<string>("SELECT value FROM setting WHERE name = 'tema' AND machine_id = ''"));
        Assert.Equal(total, after.ExecuteScalar<int>("SELECT count(*) FROM migration"));
        Assert.Equal(0, after.ExecuteScalar<int>("SELECT count(*) FROM sqlite_master WHERE name IN ('migracion', 'equipo', 'ajuste')"));
    }

    [Fact]
    public void DataFolderCreatesItsSubfolders()
    {
        using var tmp = new TempDir();
        var path = tmp["Otteretro"];
        Assert.Null(DataFolder.Prepare(path));
        Assert.True(DataFolder.IsReady(path));
        Assert.True(Directory.Exists(Path.Combine(path, "cache", "imagenes")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relativa\\carpeta")]
    public void DataFolderRejectsPartialPaths(string? path) =>
        Assert.NotNull(DataFolder.Prepare(path));

    [Fact]
    public void DataFolderRejectsAFile()
    {
        using var tmp = new TempDir();
        var file = tmp["archivo.txt"];
        File.WriteAllText(file, "x");
        Assert.NotNull(DataFolder.Prepare(file));
    }

    [Fact]
    public void ConfigIsSavedAndLoaded()
    {
        using var tmp = new TempDir();
        var path = tmp["config.json"];
        var c = AppConfig.Load(path);
        c.DataFolder = "E:\\Otteretro";
        c.Save(path);
        var loaded = AppConfig.Load(path);
        Assert.Equal("E:\\Otteretro", loaded.DataFolder);
        Assert.Equal(c.MachineId, loaded.MachineId);
    }

    /// <summary>El config.json que escribía F0 se sigue leyendo: sus claves no cambian.</summary>
    [Fact]
    public void ConfigFromF0IsRead()
    {
        using var tmp = new TempDir();
        var path = tmp["config.json"];
        File.WriteAllText(path, """{ "carpetaDatos": "E:\\Otteretro", "origenActualizaciones": "D:\\releases", "equipoId": "pc-viejo" }""");
        var c = AppConfig.Load(path);
        Assert.Equal("E:\\Otteretro", c.DataFolder);
        Assert.Equal("D:\\releases", c.UpdateSource);
        Assert.Equal("pc-viejo", c.MachineId);
        // Sin `language`, sigue al sistema (#140).
        Assert.Null(c.Language);
        Assert.Equal(AppConfig.SystemLanguage(), c.EffectiveLanguage);
    }

    [Fact]
    public void UnknownLanguageFallsBackToTheSystem()
    {
        var c = new AppConfig { Language = "xx" };
        Assert.Contains(c.EffectiveLanguage, AppConfig.Languages);
    }
}
