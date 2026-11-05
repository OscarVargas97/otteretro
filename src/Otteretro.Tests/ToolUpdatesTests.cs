using System.Net;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>Avisos de versiones (decisión 294) con un servidor de releases falso: sin red.</summary>
public class ToolUpdatesTests
{
    sealed class Fake(Dictionary<string, string> answers) : HttpMessageHandler
    {
        public Dictionary<string, string> Answers { get; } = answers;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var key = Answers.Keys.FirstOrDefault(request.RequestUri!.ToString().Contains);
            return Task.FromResult(key is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Answers[key]) });
        }
    }

    static string GitHub(string tag, string body = "Notas") =>
        $$"""[{"tag_name":"{{tag}}-rc","body":"rc","draft":false,"prerelease":true},{"tag_name":"{{tag}}","body":"{{body}}","draft":false,"prerelease":false,"published_at":"2026-09-30T07:06:44Z","html_url":"https://github.com/TwilitRealm/dusklight/releases/tag/{{tag}}"}]""";

    static string Dolphin(string rev, string date) => $$"""{"shortrev":"{{rev}}","date":"{{date}}","changelog_html":"<p>Arreglos &amp; mejoras</p>"}""";

    static void Install(Fixture f, string group, string console, string name, string version) =>
        Directory.CreateDirectory(Path.Combine(DiskTasks.ToolsOf(f.Db), group, console, name, version));

    static async Task Check(Fixture f, Fake fake, bool manual = false)
    {
        ToolUpdates.Handler = fake;
        try { Assert.Equal("done", (await f.Run(ToolUpdates.Enqueue(f.Db, manual))).Status); }
        finally { ToolUpdates.Handler = null; }
    }

    [Fact]
    public async Task ANewVersionIsNoticedUntilItIsInstalledBesideTheOldOne()
    {
        await using var f = new Fixture();
        Install(f, "Ports", "GameCube", "Dusklight", "2.0.2");
        await Check(f, new(new() { ["api.github.com/repos/TwilitRealm/dusklight/releases"] = GitHub("v2.0.3") }));

        var u = Assert.Single(ToolUpdates.List(f.Db));
        Assert.Equal(("dusklight", "stable", "2.0.3", "Notas"), (u.RecipeId, u.Channel, u.Version, u.Notes));
        Assert.Equal(["2.0.2"], u.Installed);

        Install(f, "Ports", "GameCube", "Dusklight", "2.0.3"); // al lado de la vieja
        Assert.Empty(ToolUpdates.List(f.Db));
    }

    [Fact]
    public async Task SkippingAVersionHidesItUntilAnotherComesOut()
    {
        await using var f = new Fixture();
        Install(f, "Ports", "GameCube", "Dusklight", "2.0.2");
        var fake = new Fake(new() { ["repos/TwilitRealm/dusklight/releases"] = GitHub("v2.0.3") });
        await Check(f, fake);
        Assert.True(ToolUpdates.Skip(f.Db, "dusklight", "stable"));
        Assert.Empty(ToolUpdates.List(f.Db));

        await Check(f, fake);
        Assert.Empty(ToolUpdates.List(f.Db));

        fake.Answers["repos/TwilitRealm/dusklight/releases"] = GitHub("v2.0.4");
        await Check(f, fake);
        Assert.Equal("2.0.4", Assert.Single(ToolUpdates.List(f.Db)).Version);
    }

    [Fact]
    public async Task DevelopmentVersionsNoticeAtMostOnceAWeekUnlessAskedByHand()
    {
        await using var f = new Fixture();
        Install(f, "Emuladores", "GameCube", "Dolphin", "2026.10.01");
        var fake = new Fake(new()
        {
            ["update/latest/beta"] = Dolphin("2609a", "2026-10-08T00:57:28Z"),
            ["update/latest/dev"] = Dolphin("2609-100", "2026-11-01T00:00:00Z"),
        });
        await Check(f, fake);
        var list = ToolUpdates.List(f.Db);
        Assert.Equal(["2609a", "2609-100"], list.Select(u => u.Version));
        Assert.Equal("Arreglos & mejoras", list[0].Notes);

        // Al día siguiente sale otra de desarrollo: sola no cambia el aviso dentro de la semana.
        fake.Answers["update/latest/dev"] = Dolphin("2609-120", "2026-11-02T00:00:00Z");
        await Check(f, fake);
        Assert.Equal("2609-100", ToolUpdates.List(f.Db).Single(u => u.Channel == "dev").Version);

        // «Revisar ahora» la trae igual.
        await Check(f, fake, manual: true);
        Assert.Equal("2609-120", ToolUpdates.List(f.Db).Single(u => u.Channel == "dev").Version);

        // Pasada la semana, la revisión diaria también.
        ToolUpdates.Remember(f.Db, "dolphin", "dev", new("2609-130", null, null, "2026-11-03T00:00:00Z"), manual: false, DateTime.UtcNow.AddDays(8));
        Assert.Equal("2609-130", ToolUpdates.List(f.Db).Single(u => u.Channel == "dev").Version);

        // Lo instalado más nuevo que lo publicado (por fecha) no avisa.
        Install(f, "Emuladores", "GameCube", "Dolphin", "2026.11.04");
        Assert.Empty(ToolUpdates.List(f.Db));
    }

    [Fact]
    public async Task ANoticeCarriesItsDownloadAndTheGamesThatUseIt()
    {
        await using var f = new Fixture();
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO game (id, name_es) VALUES (10, 'Metroid Fusion'), (11, 'Minish Cap'), (12, 'Sin ROM');
                INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'gba'), (21, 11, 'gba'), (22, 12, 'gba');
                INSERT INTO source (kind, library_id, relative_path, version_id) VALUES ('file', 'd', 'a.gba', 20), ('file', 'd', 'b.gba', 21);
                """);
        // Minish Cap tiene un perfil activo en otro emulador: ya no usa el base de GBA.
        Profiles.AddEmulator(f.Db, "otro", "emulator", "Otro");
        Profiles.Activate(f.Db, Profiles.Add(f.Db, 21, "otro", "El suyo"));
        const string releases = """[{"tag_name":"0.10.5","draft":false,"prerelease":false,"published_at":"2026-10-01T00:00:00Z","assets":[{"name":"mGBA-0.10.5-macos.dmg","browser_download_url":"https://x/mac.dmg"},{"name":"mGBA-0.10.5-win64.7z","browser_download_url":"https://x/mGBA-0.10.5-win64.7z"}]},{"tag_name":"0.10.4","draft":false,"prerelease":false,"published_at":"2025-10-01T00:00:00Z","assets":[]}]""";
        var fake = new Fake(new() { ["repos/mgba-emu/mgba/releases"] = releases });
        await Check(f, fake);

        var u = Assert.Single(ToolUpdates.List(f.Db), x => x.RecipeId == "mgba");
        Assert.Equal(("0.10.5", "https://x/mGBA-0.10.5-win64.7z"), (u.Version, u.Download));
        Assert.Equal(["Metroid Fusion"], u.Games); // mGBA es el base de GBA (`baseFor`); el juego sin ROM no cuenta
        Assert.Equal(["Minish Cap"], ToolUpdates.Games(f.Db)["otro"]);

        // Para elegir la versión al instalar: solo las que traen el archivo de Windows.
        ToolUpdates.Handler = fake;
        try { Assert.Equal(["0.10.5"], (await ToolUpdates.Installable(Recipes.Find("mgba")!, default)).Select(r => r.Version)); }
        finally { ToolUpdates.Handler = null; }
    }

    [Fact]
    public async Task WhatIsNotInstalledShowsItsLatestVersion()
    {
        await using var f = new Fixture();
        var fake = new Fake(new() { ["repos/TwilitRealm/dusklight/releases"] = GitHub("v2.0.3") });
        await Check(f, fake);
        var u = Assert.Single(ToolUpdates.List(f.Db), x => x.RecipeId == "dusklight");
        Assert.Equal("2.0.3", u.Version);
        Assert.Empty(u.Installed);

        Assert.True(ToolUpdates.Skip(f.Db, "dusklight", "stable"));
        Assert.DoesNotContain(ToolUpdates.List(f.Db), x => x.RecipeId == "dusklight");
    }

    [Fact]
    public async Task FixedTagsAndUnorderedListsUseTheTitleOrTheDate()
    {
        await using var f = new Fixture();
        // Como RPCS3: tags build-<hash> sin orden; la versión está en el título y la última es la de fecha más nueva.
        var rpcs3 = """[{"tag_name":"build-fff0c96","name":"0.0.18-12817","draft":false,"prerelease":false,"published_at":"2021-09-24T18:32:52Z"},{"tag_name":"build-ffeb16f","name":"0.0.37-18146","draft":false,"prerelease":false,"published_at":"2025-09-19T09:11:48Z"},{"tag_name":"build-ffea52d","name":"0.0.12-10987","draft":false,"prerelease":false,"published_at":"2020-10-08T16:42:34Z"}]""";
        var fake = new Fake(new() { ["repos/TwilitRealm/dusklight/releases"] = rpcs3 });
        await Check(f, fake);
        Assert.Equal("0.0.37-18146", Assert.Single(ToolUpdates.List(f.Db), x => x.RecipeId == "dusklight").Version);

        // Como DuckStation o Vita3K: un tag fijo que se vuelve a publicar; sin versión en el tag ni en el título, vale la fecha.
        fake.Answers["repos/TwilitRealm/dusklight/releases"] = """[{"tag_name":"v0.1-12083","name":"v0.1-12083","draft":false,"prerelease":false,"published_at":"2026-10-13T05:31:37Z"},{"tag_name":"latest","name":"Latest Rolling Release","draft":false,"prerelease":false,"published_at":"2026-10-25T02:59:23Z"}]""";
        await Check(f, fake);
        Assert.Equal("2026.10.25", Assert.Single(ToolUpdates.List(f.Db), x => x.RecipeId == "dusklight").Version);
    }

    [Fact]
    public async Task DevPatternAndDevRepoMarkDevelopmentVersions()
    {
        var fake = new Fake(new()
        {
            // Como Eden: un -rc publicado como release normal es de desarrollo.
            ["repos/eden-emu/eden/releases"] = """[{"tag_name":"v0.2.0-rc1","draft":false,"prerelease":false,"published_at":"2026-11-01T00:00:00Z"},{"tag_name":"v0.1.0","draft":false,"prerelease":false,"published_at":"2026-10-01T00:00:00Z"}]""",
            // Como Ryubing: las Canary viven en otro repositorio.
            ["repos/projects/Ryubing/releases"] = """[{"tag_name":"1.3.3","draft":false,"prerelease":false,"published_at":"2026-10-01T00:00:00Z"}]""",
            ["repos/projects/Ryubing-Canary/releases"] = """[{"tag_name":"1.3.200","draft":false,"prerelease":false,"published_at":"2026-11-03T00:00:00Z"}]""",
        });
        using var http = new HttpClient(fake);
        var eden = new RecipeUpdates("forgejo", "eden-emu/eden", "git.eden-emu.dev", DevPattern: "-rc");
        Assert.Equal("0.1.0", (await ToolUpdates.Latest(http, eden, "stable", default))!.Version);
        Assert.Equal("0.2.0-rc1", (await ToolUpdates.Latest(http, eden, "dev", default))!.Version);

        var ryubing = new RecipeUpdates("forgejo", "projects/Ryubing", "git.ryujinx.app", DevRepo: "projects/Ryubing-Canary");
        Assert.Equal(["stable"], ToolUpdates.ChannelsOf(ryubing, manual: false)); // la Canary se pide a mano
        Assert.Equal(["stable", "dev"], ToolUpdates.ChannelsOf(ryubing, manual: true));
        Assert.Equal("1.3.3", (await ToolUpdates.Latest(http, ryubing, "stable", default))!.Version);
        Assert.Equal("1.3.200", (await ToolUpdates.Latest(http, ryubing, "dev", default))!.Version);
    }
}
