using Dapper;
using Otteretro.App;
using Otteretro.Data;
using Otteretro.Libraries;
using Otteretro.Play;

namespace Otteretro.Tests;

/// <summary>#398: la regla de cómo jugar (§8.3), su orden editable y la elección manual.</summary>
public class PlayChoiceTests
{
    static PlayOption Option(string key, string criterion, int? level = null, bool installed = true) =>
        new(key, key.Split(':')[0], key, criterion, level, installed);

    [Fact]
    public void RuleGoesPcThenFullPortThenPerfectEmulatorThenMostPlayable()
    {
        var order = PlayChoice.DefaultOrder.ToList();
        var pc = Option("pc:1", PlayChoice.Pc);
        var port = Option("port:2", PlayChoice.Port, 3, installed: false);
        var perfect = Option("emulator:dolphin", PlayChoice.Perfect, 3);
        var beta = Option("port:3", PlayChoice.Playable, 1);
        var unknown = Option("emulator:otro", PlayChoice.Playable);
        var playable = Option("port:4", PlayChoice.Playable, 2);
        var broken = Option("emulator:roto", PlayChoice.Playable, 0);

        Assert.Equal(("pc:1", "pc"), PlayChoice.Choose([perfect, port, pc, playable], order, null));
        Assert.Equal(("port:2", "port"), PlayChoice.Choose([perfect, playable, port], order, null));
        Assert.Equal(("emulator:dolphin", "perfect"), PlayChoice.Choose([playable, perfect], order, null));
        // Lo más jugable: 2 antes que 1, 1 antes que sin dato, y sin dato antes que «no funciona».
        Assert.Equal(("port:4", "playable"), PlayChoice.Choose([broken, unknown, beta, playable], order, null));
        Assert.Equal(("port:3", "playable"), PlayChoice.Choose([broken, unknown, beta], order, null));
        Assert.Equal(("emulator:otro", "playable"), PlayChoice.Choose([broken, unknown], order, null));
        Assert.Equal((null, null), PlayChoice.Choose([], order, null));

        // El orden editable cambia la elección.
        Assert.Equal(("emulator:dolphin", "perfect"), PlayChoice.Choose([pc, port, perfect], [PlayChoice.Perfect, PlayChoice.Pc, PlayChoice.Port, PlayChoice.Playable], null));
        // La manual gana mientras siga siendo una opción.
        Assert.Equal(("emulator:otro", "manual"), PlayChoice.Choose([pc, unknown], order, "emulator:otro"));
        Assert.Equal(("pc:1", "pc"), PlayChoice.Choose([pc], order, "emulator:otro"));
    }

    [Fact]
    public async Task GameOptionsComeFromWhatYouHaveAndManualChoicePersists()
    {
        await using var f = new Fixture();
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO game (id, name_es) VALUES (10, 'Twilight Princess');
                INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'gc'), (21, 10, 'wii'), (22, 10, 'pc');
                INSERT INTO source (kind, relative_path, console, version_id) VALUES ('file', 'tp.iso', 'gc', 20);
                INSERT INTO port (id, console, game, project, kind, status, link, source, added, emulator, emulator_level, game_id) VALUES
                  (1, 'gc', 'Twilight Princess', 'Dusklight', 'decomp', 'complete', 'https://github.com/TwilitRealm/Dusklight/', 'recompendium', '2026-01-01', 'Dolphin', 3, 10),
                  (2, 'gc', 'Twilight Princess', 'Sin receta', 'port', 'playable', 'https://example.com/x', 'extra', '2026-01-01', NULL, NULL, 10);
                """);
        Profiles.AddEmulator(f.Db, "dolphin", "emulator", "Dolphin");

        // Solo la versión que tienes; el port sin receta ni instalado no es opción. El port al 100 % gana al emulador perfecto.
        var gc = Assert.Single(PlayChoice.OfGame(f.Db, 10));
        Assert.Equal(20, gc.VersionId);
        Assert.Equal(["port:1", "emulator:dolphin"], gc.Options.Select(o => o.Key));
        Assert.Equal(PlayChoice.Perfect, gc.Options[1].Criterion);
        Assert.Equal(("port:1", "port"), (gc.Chosen, gc.Reason));

        // Con el orden cambiado, el emulador perfecto pasa adelante.
        Assert.False(PlayChoice.SetOrder(f.Db, ["perfect", "pc"]));
        Assert.True(PlayChoice.SetOrder(f.Db, ["perfect", "port", "pc", "playable"]));
        Assert.Equal("emulator:dolphin", PlayChoice.OfGame(f.Db, 10)[0].Chosen);
        Assert.True(PlayChoice.SetOrder(f.Db, [.. PlayChoice.DefaultOrder]));

        // A mano: persiste y gana; una opción que no es suya no se acepta; volver a la automática la recupera.
        Assert.False(PlayChoice.SetManual(f.Db, 10, 20, "port:2"));
        Assert.False(PlayChoice.SetManual(f.Db, 10, 21, "emulator:dolphin"));
        Assert.True(PlayChoice.SetManual(f.Db, 10, 20, "emulator:dolphin"));
        Assert.Equal(("emulator:dolphin", "manual"), (PlayChoice.OfGame(f.Db, 10)[0].Chosen, PlayChoice.OfGame(f.Db, 10)[0].Reason));
        PlayChoice.ClearManual(f.Db, 20);
        Assert.Equal(("port:1", "port"), (PlayChoice.OfGame(f.Db, 10)[0].Chosen, PlayChoice.OfGame(f.Db, 10)[0].Reason));

        // La versión de PC que tienes va primero en todas, y su propia versión aparece.
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO source (kind, store, store_id, name, version_id) VALUES ('store', 'steam', '1', 'TP', 22)");
        var all = PlayChoice.OfGame(f.Db, 10);
        Assert.Equal([20L, 21L, 22L], all.Select(v => v.VersionId));
        Assert.All(all, v => Assert.Equal(("pc:22", "pc"), (v.Chosen, v.Reason)));
    }

    /// <summary>#407: a igual nivel, gana el emulador que el catálogo recomienda para el juego, aunque no sea el base.</summary>
    [Fact]
    public async Task TheCatalogRecommendedEmulatorWinsAtTheSameLevel()
    {
        await using var f = new Fixture();
        using (var c = f.Db.Open())
            c.Execute("""
                INSERT INTO game (id, name_es) VALUES (7692, 'Skyward Sword HD'), (7693, 'Otro');
                INSERT INTO version (id, game_id, platform) VALUES (30, 7692, 'switch'), (31, 7693, 'switch');
                INSERT INTO source (kind, relative_path, console, version_id) VALUES ('file', 'ss.nsp', 'switch', 30), ('file', 'otro.nsp', 'switch', 31);
                """);
        Profiles.AddEmulator(f.Db, "eden", "emulator", "Eden");
        Profiles.AddEmulator(f.Db, "ryujinx-ss4k", "emulator", "Ryujinx SS4K");

        // ryujinx-ss4k trae el juego 7692 en recommendedFor; para otro juego manda el base.
        Assert.Equal("emulator:ryujinx-ss4k", PlayChoice.OfGame(f.Db, 7692).Single().Chosen);
        Assert.Equal("emulator:eden", PlayChoice.OfGame(f.Db, 7693).Single().Chosen);
    }

    /// <summary>#407: al quitar la búsqueda local, lo elegido a mano en emulator_pick pasa a play_choice en cada versión de esa consola.</summary>
    [Fact]
    public void ManualEmulatorPicksSurviveAsPlayChoices()
    {
        using var tmp = new TempDir();
        var folder = tmp["datos"];
        DataFolder.Prepare(folder);
        var db = DataFolder.Open(folder);
        Migrations.Apply(db, until: "0041_play_choice.sql");
        using (var c = db.Open())
            c.Execute("""
                INSERT INTO game (id, name_es) VALUES (10, 'Twilight Princess');
                INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'gc'), (21, 10, 'gc'), (22, 10, 'wii');
                INSERT INTO emulator_pick (game_id, console, recipe_id, origin, searched_at) VALUES
                  (10, 'gc', 'dolphin', 'manual', '2026-11-01'), (10, 'wii', 'dolphin', 'claude', '2026-11-01');
                INSERT INTO emulator_search (game_id, searched_at) VALUES (10, '2026-11-01');
                INSERT INTO play_choice (version_id, option, chosen_at) VALUES (21, 'port:1', '2026-11-02');
                """);

        Migrations.Apply(db);

        using (var c = db.Open())
        {
            // La manual pasa a cada versión de GameCube sin pisar una elección que ya había; lo de Claude no pasa.
            Assert.Equal([(20L, "emulator:dolphin"), (21L, "port:1")], c.Query<(long, string)>("SELECT version_id, option FROM play_choice ORDER BY version_id"));
            Assert.Equal(0, c.ExecuteScalar<long>("SELECT count(*) FROM sqlite_master WHERE name IN ('emulator_pick', 'emulator_search')"));
        }
    }
}
