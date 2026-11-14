using System.Text.Json.Nodes;
using Dapper;
using Otteretro.Catalog;

namespace Otteretro.Tests;

/// <summary>#401: exportar a Steam con Steam ROM Manager, sin Steam ni SRM de verdad (todo en carpetas temporales).</summary>
[Collection("FakeHttp")]
public class SteamExportTests
{
    const string LoginUsers = """
        "users"
        {
            "76561190000000002"
            {
                "AccountName"   "yo"
                "MostRecent"    "1"
            }
        }
        """;

    const string Exe = @"C:\Otteretro\Otteretro.exe";

    /// <summary>Zelda (saga, NES), Tetris (sin saga, GB), Half-Life (PC, comprado en Steam). Marcadas las tres.</summary>
    static Fixture Prepare()
    {
        var f = new Fixture();
        Steam.LoginUsersPath = f.Tmp[@"Steam\config\loginusers.vdf"];
        Directory.CreateDirectory(Path.GetDirectoryName(Steam.LoginUsersPath)!);
        File.WriteAllText(Steam.LoginUsersPath, LoginUsers);
        SteamExport.SrmPath = null;
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO catalog.saga (id, name) VALUES (1, 'The Legend of Zelda'), (2, 'Half-Life');
                INSERT INTO catalog.game (id, saga_id, name_es) VALUES (1, 1, 'The Legend of Zelda'), (2, NULL, 'Tetris'), (3, 2, 'Half-Life');
                INSERT INTO catalog.version (id, game_id, platform, steam_app_id) VALUES (10, 1, 'nes', NULL), (20, 2, 'gb', NULL), (30, 3, 'pc', 70);
                INSERT INTO source (kind, store, store_id, name, version_id) VALUES ('store', 'steam', '70', 'Half-Life', 30);
                """);
        foreach (var id in new long[] { 10, 20, 30 }) SteamExport.Mark(f.Db, id, true);
        return f;
    }

    /// <summary>El caso de las pruebas de SRM (generate-app-id): crc32("123456789") = 0xCBF43926 y el acceso -873187034 en shortcuts.vdf.</summary>
    [Fact]
    public void AppIdMatchesSteamRomManager()
    {
        var id = SteamExport.AppId("", "123456789");
        Assert.Equal(0xCBF43926u, id);
        Assert.Equal(id, SteamExport.AppId("1234", "56789"));
        Assert.Equal(-873187034, (int)id);
        Assert.Equal((0xCBF43926ul << 32) | 0x02000000, SteamExport.RunGameId(id));
    }

    [Fact]
    public async Task ManifestHasNameCategoryLaunchAndArtAndSkipsWhatIsInSteam()
    {
        await using var f = Prepare();
        var images = SteamGridDb.Folder(f.Db);
        Directory.CreateDirectory(images);
        File.WriteAllText(Path.Combine(images, "1-cover.webp"), "portada");

        var entries = SteamExport.Entries(f.Db, Exe);
        Assert.Equal(["Tetris (Game Boy)", "The Legend of Zelda (NES)"], entries.Select(e => e.Title));
        Assert.Equal([SteamExport.NoSaga, "The Legend of Zelda"], entries.Select(e => e.Category));
        Assert.Equal(SteamExport.AppId($"\"{Exe}\"", "Tetris (Game Boy)"), (uint)entries[0].AppId);

        var folder = f.Tmp["exportar"];
        var parsers = await SteamExport.Write(f.Db, folder, Exe, entries, [.. entries.Select(e => e.Category)], f.Tmp["Steam"], "yo");
        Assert.Equal(2, parsers.Count);
        var zelda = parsers.Single(p => p!["steamCategories"]![0]!.GetValue<string>() == "The Legend of Zelda")!;
        Assert.Equal("Manual", zelda["parserType"]!.GetValue<string>());
        Assert.Equal("yo", zelda["userAccounts"]!["specifiedAccounts"]![0]!.GetValue<string>());

        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(zelda["parserInputs"]!["manualManifests"]!.GetValue<string>(), "otteretro.json")))!.AsArray();
        var entry = Assert.Single(manifest)!;
        Assert.Equal("The Legend of Zelda (NES)", entry["title"]!.GetValue<string>());
        Assert.Equal(Exe, entry["target"]!.GetValue<string>());
        Assert.Equal("jugar 10", entry["launchOptions"]!.GetValue<string>());
        Assert.Equal("portada", File.ReadAllText(Path.Combine(entry["startIn"]!.GetValue<string>(), "tall.webp")));
        Assert.True(File.Exists(Path.Combine(folder, "userConfigurations.json")));
    }

    [Fact]
    public async Task ChangesDetectAddedChangedAndRemoved()
    {
        await using var f = Prepare();
        var machine = f.Config.MachineId;
        var first = SteamExport.Changes(f.Db, machine, Exe);
        Assert.Equal(2, first.Added.Count);
        SteamExport.Save(f.Db, machine, first.Entries);
        Assert.Empty(SteamExport.Changes(f.Db, machine, Exe).Added);

        // Tetris entra a una saga (cambia), Zelda se desmarca (sale) y Mario se marca (entra).
        using (var c = f.Db.Open())
            c.Execute("""
                UPDATE catalog.game SET saga_id = 1 WHERE id = 2;
                INSERT INTO catalog.game (id, saga_id, name_es) VALUES (4, NULL, 'Super Mario Bros.');
                INSERT INTO catalog.version (id, game_id, platform) VALUES (40, 4, 'nes');
                """);
        SteamExport.Mark(f.Db, 10, false);
        SteamExport.Mark(f.Db, 40, true);
        var changes = SteamExport.Changes(f.Db, machine, Exe);
        Assert.Equal(40, Assert.Single(changes.Added).VersionId);
        Assert.Equal(20, Assert.Single(changes.Changed).VersionId);
        Assert.Equal(10, Assert.Single(changes.Removed).VersionId);
        // Otro equipo no tiene nada exportado.
        Assert.Equal(2, SteamExport.Changes(f.Db, "otro-equipo", Exe).Added.Count);
    }

    [Fact]
    public async Task ExportRunsSrmKeepsOtherParsersAndPlayGoesThroughSteam()
    {
        await using var f = Prepare();
        var srm = f.Tmp[@"srm\Steam-ROM-Manager.exe"];
        var config = f.Tmp[@"srm\userData\userConfigurations.json"];
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllText(config, """[{ "parserId": "mio", "configTitle": "Lo mío" }, { "parserId": "otteretro-viejo" }]""");
        string? ran = null;
        SteamExport.SrmPath = srm;
        SteamExport.RunSrm = (exe, _) => { ran = exe; return Task.FromResult(0); };
        try
        {
            var task = await f.Run(SteamExport.Enqueue(f.Db, f.Config.MachineId, Exe));
            Assert.Equal("done", task.Status);
            Assert.Equal("tasks.steam.exported", task.Detail!.Code);
            Assert.Equal(srm, ran);
        }
        finally { SteamExport.SrmPath = null; }

        var ids = JsonNode.Parse(File.ReadAllText(config))!.AsArray().Select(p => p!["parserId"]!.GetValue<string>()).ToList();
        Assert.Contains("mio", ids);
        Assert.DoesNotContain("otteretro-viejo", ids);
        Assert.Equal(3, ids.Count);
        var tetris = SteamExport.Entries(f.Db, Exe).Single(e => e.VersionId == 20);
        Assert.Equal(SteamExport.RunGameId(tetris.AppId), SteamExport.Exported(f.Db, f.Config.MachineId, 20));
        Assert.Null(SteamExport.Exported(f.Db, f.Config.MachineId, 30));
    }

    [Fact]
    public async Task WithoutSrmTheManifestsStayAndNothingIsRecorded()
    {
        await using var f = Prepare();
        var task = await f.Run(SteamExport.Enqueue(f.Db, f.Config.MachineId, Exe));
        Assert.Equal(("done", "tasks.steam.noSrm"), (task.Status, task.Detail?.Code));
        Assert.True(File.Exists(Path.Combine(f.Config.DataFolder!, "steam", f.Config.MachineId, "userConfigurations.json")));
        Assert.Empty(SteamExport.LastExport(f.Db, f.Config.MachineId));
    }

    /// <summary>#402: la pantalla ve la cuenta, si está SRM y lo que tienes sin marcar (no lo de Steam ni lo ya marcado).</summary>
    [Fact]
    public async Task StatusHasAccountSrmAndWhatYouOwnUnmarked()
    {
        await using var f = Prepare();
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO catalog.game (id, saga_id, name_es) VALUES (4, NULL, 'Super Mario Bros.');
                INSERT INTO catalog.version (id, game_id, platform) VALUES (40, 4, 'nes');
                INSERT INTO source (kind, name, version_id) VALUES ('file', 'smb.nes', 40), ('file', 'zelda.nes', 10);
                """);
        SteamExport.Mark(f.Db, 30, false);
        var status = SteamExport.Status(f.Db, Exe);
        Assert.Equal(("yo", false), (status.Account, status.Srm));
        Assert.Equal([40L], status.Available.Select(e => e.VersionId));
    }
}
