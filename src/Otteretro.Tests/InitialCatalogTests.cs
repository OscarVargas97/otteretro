using System.IO.Compression;
using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Catalog;

namespace Otteretro.Tests;

/// <summary>#211: quien instala tiene el catálogo desde el primer momento.</summary>
[Collection("Dats.Handler")]
public class InitialCatalogTests
{
    /// <summary>Un archivo como el que trae la app, con el esquema real.</summary>
    static string Bundled(Fixture f)
    {
        using (var c = f.Db.Open())
        {
            c.Execute("""
                INSERT INTO catalog.saga (id, name) VALUES (1, 'Metroid');
                INSERT INTO catalog.game (id, saga_id, name_es) VALUES (10, 1, 'Metroid Fusion');
                INSERT INTO catalog.version (id, game_id, platform) VALUES (20, 10, 'gba');
                INSERT INTO catalog.image (game_id, kind, url) VALUES (10, 'cover', 'https://images.igdb.com/igdb/image/upload/t_cover_big/x.jpg');
                """);
            c.Execute("VACUUM catalog INTO @plain", new { plain = f.Tmp["copia.db"] });
        }
        SqliteConnection.ClearAllPools();
        var file = f.Tmp["catalogo-inicial.db.gz"];
        using (var input = File.OpenRead(f.Tmp["copia.db"]))
        using (var gz = new GZipStream(File.Create(file), CompressionLevel.Fastest))
            input.CopyTo(gz);
        return file;
    }

    [Fact]
    public async Task AFreshInstallGetsTheCatalog()
    {
        await using var source = new Fixture();
        var file = Bundled(source);
        await using var fresh = new Fixture();
        InitialCatalog.BundledFile = file;
        try { Assert.True(InitialCatalog.Seed(fresh.Db)); }
        finally { InitialCatalog.BundledFile = null; }
        using var c = fresh.Db.Open();
        Assert.Equal("Metroid Fusion", c.ExecuteScalar<string>("SELECT name_es FROM catalog.game WHERE id = 10"));
        Assert.Equal(20, c.ExecuteScalar<long>("SELECT id FROM catalog.version WHERE game_id = 10"));
        Assert.NotNull(c.ExecuteScalar<string>("SELECT url FROM catalog.image WHERE game_id = 10 AND kind = 'cover'")); // las portadas vienen con la app
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(fresh.Db.CatalogPath)!, "*.tmp"));
    }

    /// <summary>La copia inicial solo llena un catálogo vacío: lo demás es de la actualización.</summary>
    [Fact]
    public async Task AnInstallWithACatalogIsNotSeeded()
    {
        await using var source = new Fixture();
        var file = Bundled(source);
        await using var mine = new Fixture();
        using (var c = mine.Db.Open()) c.Execute("INSERT INTO catalog.game (id, name_es) VALUES (10, 'Mi juego')");
        InitialCatalog.BundledFile = file;
        try { Assert.False(InitialCatalog.Seed(mine.Db)); }
        finally { InitialCatalog.BundledFile = null; }
        using var check = mine.Db.Open();
        Assert.Equal("Mi juego", check.ExecuteScalar<string>("SELECT name_es FROM catalog.game WHERE id = 10"));
    }

    [Fact]
    public async Task WithoutTheFileNothingHappens()
    {
        await using var f = new Fixture();
        Assert.False(InitialCatalog.Seed(f.Db));
    }
}
