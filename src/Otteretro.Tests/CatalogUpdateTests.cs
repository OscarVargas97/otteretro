using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Catalog;

namespace Otteretro.Tests;

/// <summary>#213: la app toma el catálogo publicado sin perder lo de la persona, y si algo falla sigue el anterior.</summary>
[Collection("Dats.Handler")]
public class CatalogUpdateTests
{
    sealed class Fake(Dictionary<string, byte[]> answers) : HttpMessageHandler
    {
        public int DataDownloads;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            if (url.EndsWith("catalogo.db.gz", StringComparison.Ordinal)) DataDownloads++;
            var key = answers.Keys.FirstOrDefault(url.Contains);
            return Task.FromResult(key is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(answers[key]) });
        }
    }

    /// <summary>Un catálogo publicado de verdad: una base con el esquema de la app, comprimida.</summary>
    static byte[] Compiled(Fixture f, string sql)
    {
        Directory.CreateDirectory(f.Tmp["pub"]);
        var db = new Data.Database(f.Tmp["pub/propia.db"], f.Tmp["pub/catalogo.db"]);
        Data.Migrations.Apply(db);
        var plain = f.Tmp[$"pub/{Guid.NewGuid():N}.db"];
        using (var c = db.Open())
        {
            c.Execute("DELETE FROM catalog.game; DELETE FROM catalog.saga; CREATE TABLE IF NOT EXISTS catalog.replaced (kind TEXT, old_id INTEGER, new_id INTEGER); DELETE FROM catalog.replaced; DELETE FROM catalog.image;");
            c.Execute(sql);
            c.Execute("VACUUM catalog INTO @plain", new { plain });
        }
        SqliteConnection.ClearAllPools();
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        using (var input = File.OpenRead(plain))
            input.CopyTo(gz);
        return output.ToArray();
    }

    const string Catalog2 = """
        INSERT INTO catalog.saga (id, name) VALUES (1, 'Metroid');
        INSERT INTO catalog.game (id, saga_id, name_es) VALUES (10, 1, 'Metroid Fusion'), (12, 1, 'Super Metroid');
        INSERT INTO catalog.version (id, game_id, platform) VALUES (20, 10, 'gba'), (22, 12, 'snes');
        INSERT INTO catalog.replaced (kind, old_id, new_id) VALUES ('game', 11, 10), ('version', 21, 20), ('version', 29, NULL);
        """;

    static Fake Answers(byte[] data, string? sha = null, string tag = "catalogo-20261101") => new(new()
    {
        ["releases?"] = JsonSerializer.SerializeToUtf8Bytes(new object[]
        {
            new { tag_name = "imagenes-1", draft = false, assets = new[] { new { name = "x", browser_download_url = "https://x.test/mal" } } },
            new { tag_name = tag, draft = false, assets = new[]
            {
                new { name = "catalogo.db.gz", browser_download_url = "https://x.test/catalogo.db.gz" },
                new { name = "catalogo.db.gz.json", browser_download_url = "https://x.test/manifest.json" },
            } },
        }),
        ["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(new { sha256 = sha ?? Convert.ToHexStringLower(SHA256.HashData(data)) }),
        ["catalogo.db.gz"] = data,
    });

    static void Mine(Fixture f)
    {
        using var c = f.Db.Open();
        c.Execute("""
            INSERT INTO catalog.saga (id, name) VALUES (1, 'Viejo');
            INSERT INTO catalog.game (id, saga_id, name_es) VALUES (10, 1, 'Viejo Fusion'), (11, 1, 'Duplicado');
            INSERT INTO catalog.version (id, game_id, platform) VALUES (20, 10, 'gba'), (21, 11, 'gba'), (29, 11, 'gb');
            INSERT INTO progress (game_id, status, notes) VALUES (11, 'finished', 'Lo terminé');
            INSERT INTO download_link (url, name, package, game_id, version_id, created_at) VALUES ('https://sitio.test/a', 'a', 'a', 11, 21, '2026-01-01');
            INSERT INTO library (id, name, kind, path, created_at, type) VALUES ('d', 'D', 'local', 'E:\Otteretro', '2026-01-01', 'disk');
            INSERT INTO source (kind, library_id, relative_path, console, version_id) VALUES ('file', 'd', 'a.gba', 'gba', 21), ('file', 'd', 'b.gb', 'gb', 29);
            """);
    }

    async Task<App.TaskInfo> Update(Fixture f, Fake fake)
    {
        CatalogUpdate.Handler = fake;
        try { return await f.Run(CatalogUpdate.Enqueue(f.Db)); }
        finally { CatalogUpdate.Handler = null; }
    }

    [Fact]
    public async Task APublishedVersionReplacesTheCatalogAndKeepsWhatIsYours()
    {
        await using var f = new Fixture();
        Mine(f);
        var done = await Update(f, Answers(Compiled(f, Catalog2)));
        Assert.True(done.Status == "done", JsonSerializer.Serialize(done.Detail));

        using var c = f.Db.Open();
        Assert.Equal("Metroid Fusion", c.ExecuteScalar<string>("SELECT name_es FROM catalog.game WHERE id = 10"));
        Assert.Equal(0, c.ExecuteScalar<long>("SELECT count(*) FROM catalog.game WHERE id = 11"));
        // Lo tuyo pasó al juego que queda.
        Assert.Equal("Lo terminé", c.ExecuteScalar<string>("SELECT notes FROM progress WHERE game_id = 10"));
        Assert.Equal((10L, 20L), c.QuerySingle<(long, long)>("SELECT game_id, version_id FROM download_link"));
        Assert.Equal(20, c.ExecuteScalar<long>("SELECT version_id FROM source WHERE relative_path = 'a.gba'"));
        // Quitado sin reemplazo: el archivo sigue, sin identificar.
        Assert.Null(c.ExecuteScalar<long?>("SELECT version_id FROM source WHERE relative_path = 'b.gb'"));
    }

    /// <summary>Una versión que desaparece sin figurar en `replaced` no deja un archivo apuntando a nada.</summary>
    [Fact]
    public async Task AVersionThatDisappearsLeavesItsFileUnidentified()
    {
        await using var f = new Fixture();
        Mine(f);
        const string WithoutReplacements = """
            INSERT INTO catalog.saga (id, name) VALUES (1, 'Metroid');
            INSERT INTO catalog.game (id, saga_id, name_es) VALUES (10, 1, 'Metroid Fusion');
            INSERT INTO catalog.version (id, game_id, platform) VALUES (20, 10, 'gba');
            """;
        Assert.Equal("done", (await Update(f, Answers(Compiled(f, WithoutReplacements)))).Status);
        using var c = f.Db.Open();
        Assert.Null(c.ExecuteScalar<long?>("SELECT version_id FROM source WHERE relative_path = 'a.gba'"));
    }

    [Fact]
    public async Task TheSameVersionIsNotDownloadedTwice()
    {
        await using var f = new Fixture();
        var fake = Answers(Compiled(f, Catalog2));
        Assert.Equal("done", (await Update(f, fake)).Status);
        Assert.Equal("done", (await Update(f, fake)).Status);
        Assert.Equal(1, fake.DataDownloads);
    }

    [Fact]
    public async Task ABadHashLeavesTheCatalogIntact()
    {
        await using var f = new Fixture();
        Mine(f);
        var done = await Update(f, Answers(Compiled(f, Catalog2), sha: new string('0', 64)));
        Assert.Equal(("error", "catalog.badHash"), (done.Status, done.Detail?.Code));
        using var c = f.Db.Open();
        Assert.Equal("Viejo Fusion", c.ExecuteScalar<string>("SELECT name_es FROM catalog.game WHERE id = 10"));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(f.Db.CatalogPath)!, "catalogo-nuevo*"));
    }

    [Fact]
    public async Task AFileThatIsNotADatabaseOrIsEmptyLeavesTheCatalogIntact()
    {
        await using var f = new Fixture();
        Mine(f);
        using var bad = new MemoryStream();
        using (var gz = new GZipStream(bad, CompressionLevel.Fastest, leaveOpen: true)) gz.Write("esto no es una base"u8);
        Assert.Equal("catalog.badFile", (await Update(f, Answers(bad.ToArray()))).Detail?.Code);
        Assert.Equal("catalog.badFile", (await Update(f, Answers(Compiled(f, "SELECT 1")))).Detail?.Code);
        using var c = f.Db.Open();
        Assert.Equal(2, c.ExecuteScalar<long>("SELECT count(*) FROM catalog.game"));
    }

    /// <summary>#217: un repo por fork, con su versión publicada en `https://&lt;letra&gt;.test/`.</summary>
    static Dictionary<string, byte[]> Repo(string repo, string letter, byte[] data) => new()
    {
        [$"repos/{repo}/releases?"] = JsonSerializer.SerializeToUtf8Bytes(new[] { new { tag_name = "catalogo-1", draft = false, assets = new[]
        {
            new { name = "catalogo.db.gz", browser_download_url = $"https://{letter}.test/catalogo.db.gz" },
            new { name = "catalogo.db.gz.json", browser_download_url = $"https://{letter}.test/manifest.json" },
        } } }),
        [$"{letter}.test/manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(new { sha256 = Convert.ToHexStringLower(SHA256.HashData(data)) }),
        [$"{letter}.test/catalogo.db.gz"] = data,
    };

    [Fact]
    public async Task AForkAndBackKeepsWhatIsYours()
    {
        await using var f = new Fixture();
        Mine(f);
        const string ForkCatalog = """
            INSERT INTO catalog.saga (id, name) VALUES (1, 'Metroid');
            INSERT INTO catalog.game (id, saga_id, name_es) VALUES (12, 1, 'Super Metroid del fork');
            INSERT INTO catalog.version (id, game_id, platform) VALUES (22, 12, 'snes');
            """;
        var answers = Repo(CatalogUpdate.Official, "a", Compiled(f, Catalog2));
        foreach (var (k, v) in Repo("alguien/otteretro-catalogo", "b", Compiled(f, ForkCatalog))) answers[k] = v;
        CatalogUpdate.Handler = new Fake(answers);
        try
        {
            async Task Apply() => Assert.Equal("done", (await f.Run(CatalogUpdate.Enqueue(f.Db))).Status);
            await Apply();
            using var c = f.Db.Open();
            long? Version() => c.ExecuteScalar<long?>("SELECT version_id FROM source WHERE relative_path = 'a.gba'");
            Assert.Equal(20, Version());

            Assert.Null(await CatalogUpdate.SetSource(f.Db, "https://github.com/alguien/otteretro-catalogo/"));
            Assert.Equal("alguien/otteretro-catalogo", CatalogUpdate.Source(f.Db));
            await Apply();
            Assert.Equal("Super Metroid del fork", c.ExecuteScalar<string>("SELECT name_es FROM catalog.game WHERE id = 12"));
            Assert.Null(Version()); // sin identificar mientras el fork no tenga su versión
            Assert.Equal("Lo terminé", c.ExecuteScalar<string>("SELECT notes FROM progress WHERE game_id = 10"));

            Assert.Null(await CatalogUpdate.SetSource(f.Db, null)); // volver al oficial
            Assert.Equal(CatalogUpdate.Official, CatalogUpdate.Source(f.Db));
            await Apply();
            Assert.Equal(20, Version());
        }
        finally { CatalogUpdate.Handler = null; }
    }

    [Fact]
    public async Task ARepoWithoutAPublishedCatalogIsNotUsed()
    {
        await using var f = new Fixture();
        CatalogUpdate.Handler = new Fake(new() { ["repos/alguien/vacio/releases?"] = "[]"u8.ToArray() });
        try
        {
            Assert.Equal("catalog.sourceName", (await CatalogUpdate.SetSource(f.Db, "esto no es un repo"))?.Code);
            Assert.Equal("catalog.sourceEmpty", (await CatalogUpdate.SetSource(f.Db, "alguien/vacio"))?.Code);
            Assert.Equal("catalog.sourceUnreachable", (await CatalogUpdate.SetSource(f.Db, "alguien/no-existe"))?.Code);
            Assert.Equal(CatalogUpdate.Official, CatalogUpdate.Source(f.Db));
        }
        finally { CatalogUpdate.Handler = null; }
    }

    /// <summary>#215: la imagen se baja sin clave de su referencia la primera vez; si la referencia cambia, se baja la nueva; lo tuyo sin referencia sigue.</summary>
    [Fact]
    public async Task ImagesComeFromTheirReferenceAndARenewedOneIsDownloadedAgain()
    {
        await using var f = new Fixture();
        var folder = SteamGridDb.Folder(f.Db);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "12-cover.jpg"), "la bajaste con tu clave");
        string WithImage(string url) => Catalog2 + $"INSERT INTO catalog.image (game_id, kind, url) VALUES (10, 'cover', '{url}');";
        var downloads = new List<string>();
        var answers = Repo(CatalogUpdate.Official, "a", Compiled(f, WithImage("https://cdn2.steamgriddb.com/grid/vieja.jpg")));
        answers["cdn2.steamgriddb.com"] = "no es una imagen"u8.ToArray();
        var fake = new Fake(answers);
        CatalogUpdate.Handler = ImageRefs.Handler = new Spy(fake, downloads);
        try
        {
            Assert.Equal("done", (await f.Run(CatalogUpdate.Enqueue(f.Db))).Status);
            Assert.EndsWith("10-cover.jpg", await ImageRefs.File(f.Db, 10, "cover"));
            Assert.EndsWith("10-cover.jpg", await ImageRefs.File(f.Db, 10, "cover"));
            Assert.Single(downloads, d => d.Contains("vieja"));

            foreach (var (k, v) in Repo(CatalogUpdate.Official, "a", Compiled(f, WithImage("https://cdn2.steamgriddb.com/grid/nueva.jpg")))) answers[k] = v;
            Assert.Equal("done", (await f.Run(CatalogUpdate.Enqueue(f.Db))).Status);
            Assert.Null(SteamGridDb.Find(f.Db, 10, "cover")); // la vieja se borró
            Assert.NotNull(await ImageRefs.File(f.Db, 10, "cover"));
            Assert.Single(downloads, d => d.Contains("nueva"));
            Assert.Equal("la bajaste con tu clave", File.ReadAllText(Path.Combine(folder, "12-cover.jpg")));
            Assert.Null(await ImageRefs.File(f.Db, 22, "cover")); // sin referencia: el color de la consola
        }
        finally { CatalogUpdate.Handler = ImageRefs.Handler = null; }
    }

    sealed class Spy(HttpMessageHandler inner, List<string> downloads) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.Host.EndsWith("steamgriddb.com", StringComparison.Ordinal)) lock (downloads) downloads.Add(request.RequestUri.ToString());
            return base.SendAsync(request, ct);
        }
    }

    [Fact]
    public async Task WithoutAPublishedVersionNothingHappens()
    {
        await using var f = new Fixture();
        Mine(f);
        var done = await Update(f, new Fake(new() { ["releases?"] = "[]"u8.ToArray() }));
        Assert.Equal("done", done.Status);
        using var c = f.Db.Open();
        Assert.Equal(2, c.ExecuteScalar<long>("SELECT count(*) FROM catalog.game"));
    }
}
