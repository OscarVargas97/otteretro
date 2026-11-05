using System.Net;
using System.Text;
using System.Text.Json;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#286 y #287: los ports del catálogo en la ficha y en Novedades, y cómo se instalan con su perfil.</summary>
[Collection("Installs.Handler")]
public class PortsTests
{
    static Fixture Catalog()
    {
        var f = new Fixture();
        using var c = f.Db.Open();
        c.Execute("""
            INSERT INTO saga (id, name) VALUES (1, 'Zelda'), (2, 'Mario');
            INSERT INTO game (id, saga_id, name_es) VALUES (10, 1, 'Twilight Princess'), (11, 2, 'Mario 64'), (12, NULL, 'Otro');
            INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'gc'), (21, 10, 'wii'), (22, 11, 'n64'), (23, 12, 'n64');
            INSERT INTO source (kind, library_id, relative_path, console, version_id) VALUES ('file', NULL, 'mario.z64', 'n64', 22);
            INSERT INTO saga_user (saga_id, favorite, following) VALUES (1, 0, 1);
            INSERT INTO port (id, console, game, project, kind, status, delisted, link, source, added, game_id) VALUES
              (1, 'gc', 'Twilight Princess', 'Dusklight', 'decomp', 'complete', 0, 'https://github.com/TwilitRealm/dusklight', 'recompendium', '2026-10-01', 10),
              (2, 'n64', 'Mario 64', 'Ship', 'decomp', 'playable', 0, 'https://example.com/ship', 'extra', '2026-11-01', 11),
              (3, 'n64', 'Mario 64', 'Viejo', 'port', 'beta', 1, 'https://example.com/viejo', 'extra', '2026-09-01', 11),
              (4, 'n64', 'Mario 64', 'Solo decomp', 'decomp', 'no-port', 0, 'https://example.com/decomp', 'extra', '2026-11-02', 11),
              (5, 'n64', 'Otro', 'Ajeno', 'port', 'complete', 0, 'https://example.com/ajeno', 'extra', '2026-11-03', 12);
            """);
        return f;
    }

    [Fact]
    public async Task GamePortsSayWhichAreFullyPlayableAndNewsOnlyShowOwnedOrFollowed()
    {
        await using var f = Catalog();
        var mario = Ports.OfGame(f.Db, 11);
        // Los ya no listados al final; «sin port todavía» después de los que se juegan.
        Assert.Equal(["Ship", "Solo decomp", "Viejo"], mario.Select(p => p.Project));
        Assert.True(mario.Single(p => p.Project == "Viejo").Delisted);
        var tp = Assert.Single(Ports.OfGame(f.Db, 10));
        Assert.True(tp.FullyPlayable);
        Assert.Equal("dusklight", tp.RecipeId); // por su repositorio
        Assert.False(mario[0].FullyPlayable);

        // Lo de un juego que no tienes ni sigues no es novedad.
        Assert.Equal([4L, 2L, 1L, 3L], Ports.News(f.Db).Select(p => p.Id));
        Ports.MarkSeen(f.Db);
        Assert.Empty(Ports.News(f.Db));
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO port (id, console, game, project, kind, status, link, source, added, game_id) VALUES (6, 'n64', 'Mario 64', 'Nuevo', 'recomp', 'experimental', 'https://example.com/n', 'extra', '2026-11-04', 11)");
        Assert.Equal([6L], Ports.News(f.Db).Select(p => p.Id));
    }

    [Fact]
    public async Task PortFromUrlGetsItsProfileAndRecipeOneIsOneClick()
    {
        await using var f = Catalog();
        Disk.Create(f.Db, f.Tmp["E"], "E:");
        Assert.Equal("ports.noPort", (await Ports.Install(f.Db, 4, new())).Error?.Code);
        Assert.Equal("ports.needsUrl", (await Ports.Install(f.Db, 2, new())).Error?.Code);
        Assert.Equal("ports.notFound", (await Ports.Install(f.Db, 99, new())).Error?.Code);

        var release = JsonSerializer.Serialize(new[] { new { tag_name = "v1.2.0", name = "1.2.0", draft = false, prerelease = false, published_at = "2026-11-01T00:00:00Z",
            assets = new[] { new { name = "Dusklight-v1.2.0-win32-x86_64.zip", browser_download_url = "https://example.com/dusklight.zip" } } } });
        Installs.Handler = new Fake(release, "binario");
        try
        {
            // Sin receta: con la URL; la versión sale del nombre del archivo.
            var (task, error) = await Ports.Install(f.Db, 2, new("https://example.com/ship-1.4.2.exe"));
            Assert.Null(error);
            Assert.Equal("done", (await f.Run(task!.Value)).Status);

            // Con receta: un clic, con la última release y su archivo de Windows.
            if (OperatingSystem.IsWindows())
            {
                (task, error) = await Ports.Install(f.Db, 1, new());
                Assert.Null(error);
                using var c = f.Db.Open();
                var job = JsonSerializer.Deserialize<InstallJob>(c.ExecuteScalar<string>("SELECT data FROM task WHERE id = @task", new { task })!, JsonSerializerOptions.Web)!;
                Assert.Equal(("dusklight", "1.2.0", "https://example.com/dusklight.zip", 1L), (job.Emulator, job.Version, job.Url, job.Port));
            }
        }
        finally { Installs.Handler = null; }

        Assert.Equal("binario", File.ReadAllText(f.Tmp[@"E\Ports\Ship\1.4.2\ship-1.4.2.exe"]));
        Assert.Equal(["1.4.2"], Ports.OfGame(f.Db, 11).Single(p => p.Id == 2).Installed);
        // Su juego tiene perfil con el port, inactivo, como con cualquier emulador; repetir no lo duplica.
        Ports.AddProfiles(f.Db, 2, "port-2", "Ship");
        using (var c = f.Db.Open())
            Assert.Equal([(22L, "Ship", 0L)], c.Query<(long, string, long)>("SELECT game_version_id, name, active FROM profile WHERE emulator_id = 'port-2' AND game_version_id IS NOT NULL"));
    }

    sealed class Fake(string releases, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.Host == "api.github.com" ? releases : body, Encoding.UTF8),
            });
    }
}
