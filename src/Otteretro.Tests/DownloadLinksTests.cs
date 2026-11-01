using Dapper;
using Otteretro.App;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#210: el capturador guarda lo que pegas o copias, y los enlaces llegan a la ficha del juego.</summary>
[Collection("Dats.Handler")]
public class DownloadLinksTests
{
    static List<CapturedLink> All(Fixture f) => DownloadLinks.Captured(f.Db).SelectMany(p => p.Links).ToList();

    static void AddGame(Fixture f)
    {
        using var c = f.Db.Open();
        c.Execute("INSERT INTO catalog.game (id, name_es) VALUES (10, 'Metroid Fusion'); INSERT INTO catalog.version (id, game_id, platform) VALUES (20, 10, 'gba');");
    }

    [Fact]
    public async Task WhatYouAddStaysSavedWithoutAskingTheSiteAndIsNotDuplicated()
    {
        await using var f = new Fixture();
        var text = "mira https://1fichier.com/?abc123&af=9 y https://1fichier.com/?abc123 y https://1fichier.com/?def456";
        Assert.Equal((3, 2), DownloadLinks.Add(f.Db, text));
        Assert.Equal((3, 0), DownloadLinks.Add(f.Db, text));

        // Otra conexión a la misma base: es lo que ve la app al volver a abrir. Sin clave ni red de por medio: no se consultó nada.
        var links = All(f);
        Assert.Equal(["https://1fichier.com/?abc123", "https://1fichier.com/?def456"], links.Select(l => l.Url).Order());
        Assert.All(links, l => Assert.Equal("unchecked", l.State));
        Assert.Equal(2, DownloadLinks.Captured(f.Db).Count);
    }

    [Fact]
    public async Task CheckingSavesWhatTheSiteAnswered()
    {
        await using var f = new Fixture();
        DownloadLinks.Add(f.Db, "https://1fichier.com/?abc123");
        await DownloadLinks.Check(f.Db, f.Credentials, default);
        var link = Assert.Single(All(f));
        Assert.Equal("blocked", link.State);
        Assert.Equal("download.needsAccount", link.Detail?.Code);
    }

    [Fact]
    public async Task PartsGoInOnePackageAndGoingToTheQueueKeepsThemRegistered()
    {
        await using var f = new Fixture();
        AddGame(f);
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        DownloadLinks.Add(f.Db, "https://sitio.test/juego.part1.rar https://sitio.test/juego.part2.rar", 10);
        var package = Assert.Single(DownloadLinks.Captured(f.Db));
        Assert.Equal(2, package.Links.Count);

        Assert.Null(DownloadLinks.Queue(f.Db, [package.Name], disk, 2));
        Assert.Empty(DownloadLinks.Captured(f.Db));
        Assert.Equal(2, DownloadLinks.OfGame(f.Db, 10).Count);
        Assert.All(DownloadLinks.OfGame(f.Db, 10), l => Assert.Equal("queued", l.Status));

        // Pegarlo de nuevo lo devuelve al capturador, sin duplicarlo.
        DownloadLinks.Add(f.Db, "https://sitio.test/juego.part1.rar");
        Assert.Single(All(f));
        Assert.Equal(2, DownloadLinks.OfGame(f.Db, 10).Count);
    }

    /// <summary>Cualquier camino que identifique el archivo (importación, Revisar, Claude) lleva los enlaces a la ficha.</summary>
    [Fact]
    public async Task WhenTheFileOfADownloadIsIdentifiedItsLinksReachTheGame()
    {
        await using var f = new Fixture();
        AddGame(f);
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        DownloadLinks.Add(f.Db, "https://sitio.test/fusion.zip");
        Assert.Null(DownloadLinks.Queue(f.Db, ["fusion"], disk, 2));
        Assert.Empty(DownloadLinks.OfGame(f.Db, 10));

        using (var c = f.Db.Open())
        {
            var task = c.ExecuteScalar<long>("SELECT task_id FROM download_link");
            c.Execute("""
                INSERT INTO source (kind, library_id, relative_path, console, download_task) VALUES ('file', @disk, 'Entrada\fusion.gba', 'gba', @task);
                UPDATE source SET version_id = 20 WHERE relative_path = 'Entrada\fusion.gba';
                """, new { disk, task });
        }
        Assert.Single(DownloadLinks.OfGame(f.Db, 10));

        // Quitar el archivo del disco no borra los enlaces.
        using (var c = f.Db.Open()) c.Execute("DELETE FROM source");
        Assert.Single(DownloadLinks.OfGame(f.Db, 10));
    }

    /// <summary>Decisión 245: lo privado no vive en `catalogo.db`, la base que se puede compartir.</summary>
    [Fact]
    public async Task LinksAreNotInTheCatalogDatabase()
    {
        await using var f = new Fixture();
        using var c = f.Db.Open();
        Assert.Equal(1, c.ExecuteScalar<long>("SELECT count(*) FROM main.sqlite_master WHERE name = 'download_link'"));
        Assert.Equal(0, c.ExecuteScalar<long>("SELECT count(*) FROM catalog.sqlite_master WHERE name = 'download_link'"));
    }

    [Fact]
    public async Task ClipboardOnlyTakesLinksOfSitesWithAConnector()
    {
        await using var f = new Fixture();
        uint sequence = 1;
        var text = "https://1fichier.com/?viejo1";
        var watcher = new ClipboardWatcher(() => sequence, () => (true, text));

        // Lo que ya estaba copiado al abrir no cuenta.
        watcher.Tick(f.Db, true);
        watcher.Tick(f.Db, true);
        Assert.Empty(All(f));

        (sequence, text) = (2, "hola, mi clave es secreta y mira https://ejemplo.test/noticia.html");
        watcher.Tick(f.Db, true);
        Assert.Empty(All(f));

        (sequence, text) = (3, "https://1fichier.com/?nuevo1&af=1");
        watcher.Tick(f.Db, true);
        Assert.Equal("https://1fichier.com/?nuevo1", Assert.Single(All(f)).Url);

        // Apagado no entra, y lo copiado mientras tanto no aparece al volver a encender.
        (sequence, text) = (4, "https://1fichier.com/?apagado");
        watcher.Tick(f.Db, false);
        watcher.Tick(f.Db, true);
        Assert.Single(All(f));
    }

    /// <summary>Copiar avisa (#210): lo nuevo, lo que ya estaba, y nada si no era un enlace o está apagado.</summary>
    [Fact]
    public async Task CopyingALinkNotifies()
    {
        await using var f = new Fixture();
        uint sequence = 1;
        var text = "";
        var notices = new List<(int Found, int Added)>();
        var watcher = new ClipboardWatcher(() => sequence, () => (true, text), (found, added) => notices.Add((found, added)));
        watcher.Tick(f.Db, true);

        void Copy(string copied, bool enabled = true)
        {
            sequence++;
            text = copied;
            watcher.Tick(f.Db, enabled);
        }

        Copy("texto sin enlaces https://ejemplo.test/noticia.html");
        Assert.Empty(notices);
        Copy("https://1fichier.com/?nuevo1");
        Copy("https://1fichier.com/?nuevo1");
        Copy("https://1fichier.com/?nuevo2 https://1fichier.com/?nuevo3");
        Copy("https://1fichier.com/?apagado", enabled: false);
        Assert.Equal([(1, 1), (1, 0), (2, 2)], notices);
    }

    /// <summary>Otro programa tiene el portapapeles abierto: se reintenta en la próxima vuelta y no se pierde lo copiado.</summary>
    [Fact]
    public async Task ABusyClipboardIsRetried()
    {
        await using var f = new Fixture();
        uint sequence = 1;
        var busy = true;
        var watcher = new ClipboardWatcher(() => sequence, () => busy ? (false, null) : (true, "https://1fichier.com/?nuevo1"));
        watcher.Tick(f.Db, true);
        sequence = 2;
        watcher.Tick(f.Db, true);
        Assert.Empty(All(f));
        busy = false;
        watcher.Tick(f.Db, true);
        Assert.Single(All(f));
    }

    /// <summary>Decisión 244: el canal Local toma la carpeta más reciente de tus discos conectados.</summary>
    [Fact]
    public async Task TheLocalChannelFindsTheNewestPackagesInYourDisks()
    {
        await using var f = new Fixture();
        Assert.Null(Disk.NewestPackages(f.Db));
        var (a, _) = Disk.Create(f.Db, f.Tmp["a"], "A");
        var (b, _) = Disk.Create(f.Db, f.Tmp["b"], "B");
        Assert.Null(Disk.NewestPackages(f.Db));

        var older = Path.Combine(Disk.Get(f.Db, a!)!.Path, Disk.PackagesFolder);
        var newer = Path.Combine(Disk.Get(f.Db, b!)!.Path, Disk.PackagesFolder);
        foreach (var folder in new[] { older, newer }) File.WriteAllText(Path.Combine(Directory.CreateDirectory(folder).FullName, "releases.win.json"), "{}");
        File.SetLastWriteTimeUtc(Path.Combine(older, "releases.win.json"), DateTime.UtcNow.AddDays(-1));
        Assert.Equal(newer, Disk.NewestPackages(f.Db));

        // Sin ese disco, sigue con el otro y no falla.
        Directory.Delete(Disk.Get(f.Db, b!)!.Path, recursive: true);
        Assert.Equal(older, Disk.NewestPackages(f.Db));
    }
}
