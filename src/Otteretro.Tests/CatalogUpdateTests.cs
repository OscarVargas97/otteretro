using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Catalog;

namespace Otteretro.Tests;

/// <summary>#213 y #222: buscar no aplica nada; aceptar aplica sin perder lo de la persona, y si algo falla sigue el anterior.</summary>
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

    static Fake Answers(byte[] data, string? sha = null, string tag = "catalogo-20261101", object? changes = null)
    {
        var assets = new List<object>
        {
            new { name = "catalogo.db.gz", browser_download_url = "https://x.test/catalogo.db.gz" },
            new { name = "catalogo.db.gz.json", browser_download_url = "https://x.test/manifest.json" },
        };
        if (changes is not null) assets.Add(new { name = "cambios.json", browser_download_url = "https://x.test/cambios.json" });
        var answers = new Dictionary<string, byte[]>
        {
            ["releases?"] = JsonSerializer.SerializeToUtf8Bytes(new object[]
            {
                new { tag_name = "imagenes-1", draft = false, assets = new[] { new { name = "x", browser_download_url = "https://x.test/mal" } } },
                new { tag_name = tag, draft = false, assets },
            }),
            ["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(new { sha256 = sha ?? Convert.ToHexStringLower(SHA256.HashData(data)) }),
            ["catalogo.db.gz"] = data,
        };
        if (changes is not null) answers["cambios.json"] = JsonSerializer.SerializeToUtf8Bytes(changes);
        return new(answers);
    }

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

    /// <summary>Buscar y, si quedó una versión esperando, aceptarla.</summary>
    static async Task<App.TaskInfo> CheckAndAccept(Fixture f)
    {
        var task = await f.Run(CatalogUpdate.Enqueue(f.Db));
        if (task.Status == "done" && CatalogUpdate.Waiting(f.Db) is not null) Assert.Null(CatalogUpdate.Accept(f.Db));
        return task;
    }

    async Task<App.TaskInfo> Update(Fixture f, Fake fake)
    {
        CatalogUpdate.Handler = fake;
        try { return await CheckAndAccept(f); }
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
        Assert.Null(CatalogUpdate.Waiting(f.Db));
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

    /// <summary>#222 (decisión 259): buscar deja la versión esperando sin tocar nada; rechazar la descarta y aceptar la aplica entera.</summary>
    [Fact]
    public async Task CheckingAppliesNothingUntilItIsAccepted()
    {
        await using var f = new Fixture();
        Mine(f);
        CatalogUpdate.Handler = Answers(Compiled(f, Catalog2));
        try
        {
            Assert.Equal("done", (await f.Run(CatalogUpdate.Enqueue(f.Db))).Status);
            Assert.Equal("catalogo-20261101", CatalogUpdate.Waiting(f.Db)?.Tag);
            using var c = f.Db.Open();
            string Name() => c.ExecuteScalar<string>("SELECT name_es FROM catalog.game WHERE id = 10")!;
            Assert.Equal("Viejo Fusion", Name());

            CatalogUpdate.Reject(f.Db);
            Assert.Null(CatalogUpdate.Waiting(f.Db));
            Assert.Equal("Viejo Fusion", Name());
            Assert.Equal("catalog.nothingPending", CatalogUpdate.Accept(f.Db)?.Code);

            Assert.Equal("done", (await f.Run(CatalogUpdate.Enqueue(f.Db))).Status);
            Assert.Null(CatalogUpdate.Accept(f.Db));
            Assert.Equal("Metroid Fusion", Name());
            Assert.Equal("catalogo-20261101", CatalogUpdate.Version(f.Db));
            Assert.Null(CatalogUpdate.Waiting(f.Db));
            Assert.False(File.Exists(CatalogUpdate.PendingFile(f.Db)));
        }
        finally { CatalogUpdate.Handler = null; }
    }

    /// <summary>#217 con #222: guardar un fork solo cambia el origen; no baja ni aplica nada.</summary>
    [Fact]
    public async Task SavingAForkOnlyChangesTheSource()
    {
        await using var f = new Fixture();
        Mine(f);
        var fake = new Fake(Repo("alguien/otteretro-catalogo", "b", Compiled(f, Catalog2)));
        CatalogUpdate.Handler = fake;
        try
        {
            Assert.Null(await CatalogUpdate.SetSource(f.Db, "alguien/otteretro-catalogo"));
            Assert.Equal(0, fake.DataDownloads);
            Assert.Equal(0, f.Db.Open().ExecuteScalar<long>("SELECT count(*) FROM task WHERE kind = @Kind", new { CatalogUpdate.Kind }));
            Assert.Null(CatalogUpdate.Waiting(f.Db));
        }
        finally { CatalogUpdate.Handler = null; }
    }

    /// <summary>
    /// #223 (decisiones 260 y 262): qué cambia, con lo que toca tus datos primero; sin tu versión en el historial publicado vale la comparación
    /// propia; con ella, lo publicado se verifica y lo que no coincide se avisa.
    /// </summary>
    [Fact]
    public async Task TheChangesComeFirstWithWhatIsYoursAndArePublishedAndVerified()
    {
        await using var f = new Fixture();
        Mine(f);
        CatalogUpdate.Handler = Answers(Compiled(f, Catalog2));
        try
        {
            Assert.Equal("done", (await f.Run(CatalogUpdate.Enqueue(f.Db))).Status);
            var changes = CatalogDiff.Of(f.Db)!;
            Assert.False(changes.FromHistory); // tu catálogo no dice su versión
            Assert.Equal(("game", "merged", 11L, "Metroid Fusion", 4), (changes.Items[0].Kind, changes.Items[0].Change, changes.Items[0].Id, changes.Items[0].IntoName, changes.Items[0].Yours)); // progreso, enlace y sus dos archivos
            Assert.Contains(changes.Items, x => x is { Kind: "game", Change: "renamed", Id: 10, From: "Viejo Fusion", Name: "Metroid Fusion" });
            Assert.Contains(changes.Items, x => x is { Kind: "saga", Change: "renamed", Id: 1, From: "Viejo", Name: "Metroid" });
            Assert.Contains(changes.Items, x => x is { Kind: "game", Change: "added", Id: 12 });
            Assert.Null(CatalogUpdate.Accept(f.Db));
        }
        finally { CatalogUpdate.Handler = null; }

        // Dos versiones después, con el historial publicado: lo que coincide no se avisa y lo distinto, sí.
        const string Catalog4 = """
            INSERT INTO catalog.saga (id, name) VALUES (1, 'Metroid');
            INSERT INTO catalog.game (id, saga_id, name_es) VALUES (10, 1, 'Metroid Fusion'), (12, 1, 'Super Metroid (SNES)'), (13, 1, 'Metroid Dread');
            INSERT INTO catalog.version (id, game_id, platform) VALUES (20, 10, 'gba'), (22, 12, 'snes'), (23, 13, 'switch');
            """;
        object History(string change13) => new
        {
            versions = new[] { "catalogo-20261101", "catalogo-20261102", "catalogo-20261103" },
            entries = new object[]
            {
                new { version = "catalogo-20261101", kind = "game", change = "merged", id = 11, into = 10 },
                new { version = "catalogo-20261102", kind = "game", change = "renamed", id = 12, name = "Super Metroid (SNES)", from = "Super Metroid" },
                new { version = "catalogo-20261103", kind = "game", change = change13, id = 13, name = "Metroid Dread" },
            },
        };
        foreach (var (change13, mismatches) in new[] { ("added", 0), ("removed", 1) })
        {
            CatalogUpdate.Handler = Answers(Compiled(f, Catalog4), tag: "catalogo-20261103", changes: History(change13));
            try
            {
                Assert.Equal("done", (await f.Run(CatalogUpdate.Enqueue(f.Db))).Status);
                var changes = CatalogDiff.Of(f.Db)!;
                Assert.True(changes.FromHistory);
                Assert.Equal(("catalogo-20261101", "catalogo-20261103"), (changes.From, changes.To));
                Assert.Equal(mismatches, changes.Mismatch.Count);
                CatalogUpdate.Reject(f.Db);
            }
            finally { CatalogUpdate.Handler = null; }
        }
    }

    /// <summary>
    /// #228: las versiones publicadas, de la más nueva a la más vieja, con los cambios de cada una; las que ya no se pueden bajar salen igual
    /// por el historial. Elegir una que no es la última la baja y espera confirmación, como cualquier otra.
    /// </summary>
    [Fact]
    public async Task VersionsListTheirChangesAndAnOlderOneCanBeChosen()
    {
        await using var f = new Fixture();
        Mine(f);
        var (older, newer) = (Compiled(f, Catalog2), Compiled(f, Catalog2.Replace("'Metroid Fusion'", "'Metroid Fusion (GBA)'")));
        var history = new
        {
            versions = new[] { "catalogo-20261030", "catalogo-20261101", "catalogo-20261102" },
            entries = new object[]
            {
                new { version = "catalogo-20261101", kind = "game", change = "merged", id = 11, name = "Duplicado", into = 10 },
                new { version = "catalogo-20261102", kind = "game", change = "renamed", id = 10, name = "Metroid Fusion (GBA)", from = "Metroid Fusion" },
            },
        };
        object Release(string tag, string letter, bool changes = false) => new
        {
            tag_name = tag, draft = false, published_at = "2026-11-02T10:00:00Z",
            assets = new[]
            {
                new { name = "catalogo.db.gz", browser_download_url = $"https://{letter}.test/catalogo.db.gz" },
                new { name = "catalogo.db.gz.json", browser_download_url = $"https://{letter}.test/manifest.json" },
            }.Concat(changes ? [new { name = "cambios.json", browser_download_url = $"https://{letter}.test/cambios.json" }] : []).ToArray(),
        };
        CatalogUpdate.Handler = new Fake(new()
        {
            ["releases?"] = JsonSerializer.SerializeToUtf8Bytes(new[] { Release("catalogo-20261102", "n", changes: true), Release("catalogo-20261101", "o") }),
            ["n.test/cambios.json"] = JsonSerializer.SerializeToUtf8Bytes(history),
            ["n.test/manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(new { sha256 = Convert.ToHexStringLower(SHA256.HashData(newer)) }),
            ["n.test/catalogo.db.gz"] = newer,
            ["o.test/manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(new { sha256 = Convert.ToHexStringLower(SHA256.HashData(older)) }),
            ["o.test/catalogo.db.gz"] = older,
        });
        try
        {
            // #230: una instalación de antes de #222 solo guardó el hash de lo aplicado; la lista encuentra su versión y la recuerda.
            using (var c = f.Db.Open())
                c.Execute("INSERT INTO setting (machine_id, name, value) VALUES ('', 'catalog.published', @sha)", new { sha = Convert.ToHexStringLower(SHA256.HashData(older)) });
            var page = await CatalogUpdate.Versions(f.Db);
            Assert.Equal("catalogo-20261101", page.Current);
            Assert.Equal("catalogo-20261101", CatalogUpdate.Version(f.Db));
            Assert.True(page.Versions.Single(v => v.Tag == "catalogo-20261101").Current);
            Assert.Equal(["catalogo-20261102", "catalogo-20261101", "catalogo-20261030"], page.Versions.Select(v => v.Tag));
            Assert.Equal([true, true, false], page.Versions.Select(v => v.Available)); // la del 30 ya no se puede bajar
            Assert.Equal(1, page.Versions[0].Counts["renamed"]);
            Assert.Equal("Metroid Fusion", page.Versions[0].Changes.Single().From);

            Assert.Equal("done", (await f.Run(CatalogUpdate.Enqueue(f.Db, "catalogo-20261101"))).Status);
            Assert.Null(CatalogUpdate.Waiting(f.Db)); // ya es la tuya: no queda nada esperando
            Assert.True((await CatalogUpdate.Versions(f.Db)).Versions[1].Current);

            Assert.Equal("catalog.versionGone", (await f.Run(CatalogUpdate.Enqueue(f.Db, "catalogo-20261030"))).Detail?.Code);
        }
        finally { CatalogUpdate.Handler = null; }
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
            async Task Apply() => Assert.Equal("done", (await CheckAndAccept(f)).Status);
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
            Assert.Equal("done", (await CheckAndAccept(f)).Status);
            Assert.EndsWith("10-cover.jpg", await ImageRefs.File(f.Db, 10, "cover"));
            Assert.EndsWith("10-cover.jpg", await ImageRefs.File(f.Db, 10, "cover"));
            Assert.Single(downloads, d => d.Contains("vieja"));

            foreach (var (k, v) in Repo(CatalogUpdate.Official, "a", Compiled(f, WithImage("https://cdn2.steamgriddb.com/grid/nueva.jpg")))) answers[k] = v;
            Assert.Equal("done", (await CheckAndAccept(f)).Status);
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
