using Dapper;
using Otteretro.Api;
using Otteretro.App;
using Otteretro.Libraries;

namespace Otteretro.Tests;

public class DiskTests
{
    [Fact]
    public async Task CreatesAndReadsTheMarker()
    {
        await using var f = new Fixture();
        var (id, error) = Disk.Create(f.Db, f.Tmp["juegos"], "Mis juegos");

        Assert.Null(error);
        var marker = Disk.ReadMarker(f.Tmp["juegos"])!;
        Assert.Equal(id, marker.Id);
        Assert.Equal(("Mis juegos", "local", Disk.MarkerFormat), (marker.Name, marker.Kind, marker.Format));
        Assert.Contains("\"nombre\"", File.ReadAllText(Path.Combine(f.Tmp["juegos"], Disk.MarkerFile)));
        var info = Assert.Single(Disk.List(f.Db));
        Assert.True(info.Connected);
        Assert.Equal("disk", info.Type); // crear siempre arma un disco, con sus carpetas fijas (#219)
        Assert.All(Disk.DiskFolders.Values, folder => Assert.True(Directory.Exists(Path.Combine(f.Tmp["juegos"], folder))));
    }

    [Fact]
    public void NetworkPathsAreNetworkLibraries()
    {
        Assert.True(Disk.IsNetwork(@"\\nas\roms"));
        Assert.True(Disk.IsNetwork("//nas/roms"));
        Assert.False(Disk.IsNetwork(@"E:\Consolas"));
    }

    [Fact]
    public async Task FindsEachConsoleFolderByNameOrId()
    {
        await using var f = new Fixture();
        var (a, _) = Disk.Create(f.Db, f.Tmp["a"], null);
        var (b, _) = Disk.Create(f.Db, f.Tmp["b"], null);
        Directory.CreateDirectory(f.Tmp[@"a\Juegos\Switch"]);
        Directory.CreateDirectory(f.Tmp[@"a\Juegos\GC"]);
        Directory.CreateDirectory(f.Tmp[@"a\Juegos\_Entrada"]);
        Directory.CreateDirectory(f.Tmp[@"b\Juegos\switch"]);

        var found = Disk.ConsoleFolders(f.Db).Select(x => (x.Console, x.LibraryId, x.Relative)).OrderBy(x => x.Relative, StringComparer.Ordinal).ToList();

        Assert.Equal([("gc", a!, @"Juegos\GC"), ("switch", a!, @"Juegos\Switch"), ("switch", b!, @"Juegos\switch")], found);
    }

    [Fact]
    public async Task RejectsOverlappingLibraries()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["juegos"], null);

        Assert.Equal("library.overlaps", Disk.Create(f.Db, f.Tmp[@"juegos\Switch"], null).Error?.Code);
        Assert.Equal("library.overlaps", Disk.Create(f.Db, f.Tmp.Path, null).Error?.Code);
    }

    [Fact]
    public async Task RemovingNeverDeletesFiles()
    {
        await using var f = new Fixture();
        var (id, _) = Disk.Create(f.Db, f.Tmp["juegos"], null);
        File.WriteAllText(f.Tmp[@"juegos\juego.iso"], "x");

        Assert.True(Disk.Remove(f.Db, id!));

        Assert.Empty(Disk.List(f.Db));
        Assert.True(File.Exists(f.Tmp[@"juegos\juego.iso"]));
        Assert.NotNull(Disk.ReadMarker(f.Tmp["juegos"]));
    }

    [Fact]
    public async Task RenameUpdatesTheMarker()
    {
        await using var f = new Fixture();
        var (id, _) = Disk.Create(f.Db, f.Tmp["juegos"], "A");

        Assert.Null(Disk.Rename(f.Db, id!, " B "));

        Assert.Equal("B", Disk.ReadMarker(f.Tmp["juegos"])!.Name);
        Assert.Equal("B", Disk.List(f.Db).Single().Name);
    }

    [Fact]
    public async Task ReconnectsALibraryThatChangedPlace()
    {
        await using var f = new Fixture();
        var (id, _) = Disk.Create(f.Db, f.Tmp["juegos"], null);
        Directory.CreateDirectory(f.Tmp["disco"]);
        Directory.Move(f.Tmp["juegos"], f.Tmp[@"disco\otra"]);
        Assert.False(Disk.List(f.Db).Single().Connected);

        Assert.Equal(1, Disk.Reconnect(f.Db, [f.Tmp.Path]));

        var info = Disk.List(f.Db).Single();
        Assert.True(info.Connected);
        Assert.Equal(f.Tmp[@"disco\otra"], info.Path);
    }

    [Fact]
    public async Task AdoptingAKnownMarkerReconnects()
    {
        await using var f = new Fixture();
        var (id, _) = Disk.Create(f.Db, f.Tmp["juegos"], null);
        Directory.Move(f.Tmp["juegos"], f.Tmp["movida"]);

        var (again, error) = Disk.Adopt(f.Db, f.Tmp["movida"]);

        Assert.Null(error);
        Assert.Equal(id, again);
        Assert.True(Disk.List(f.Db).Single().Connected);
    }

    [Fact]
    public void LoadsEveryRecipe()
    {
        Assert.Equal(["dolphin", "dusklight", "dusklight-texturas", "ryujinx-ss4k", "ryujinx-ss4k-pack"], Recipes.All.Select(r => r.Id).Order());
        foreach (var complement in Recipes.All.Where(r => r.Kind == "complement"))
            Assert.Equal(complement.Folder, Recipes.Find(complement.Parent!)!.Name);
    }

    [Fact]
    public void RewritesIniPathsAndKeepsACopy()
    {
        using var tmp = new TempDir();
        var ini = tmp[@"User\Config\Dolphin.ini"];
        Directory.CreateDirectory(Path.GetDirectoryName(ini)!);
        File.WriteAllText(ini, "[General]\r\nISOPath0 = E:\\Consolas\\GameCube\\Juegos\r\nISOPath1 = E:\\Consolas\\GameCube\\Juegos2\r\nISOPaths = 2\r\n");

        var changed = Recipes.RewriteGamePaths(Recipes.Find("dolphin")!, tmp.Path, @"E:\Consolas\GameCube\Juegos", @"E:\Consolas\GameCube");

        Assert.Equal(1, changed);
        Assert.Equal("[General]\r\nISOPath0 = E:\\Consolas\\GameCube\r\nISOPath1 = E:\\Consolas\\GameCube\\Juegos2\r\nISOPaths = 2\r\n", File.ReadAllText(ini));
        Assert.Contains("Juegos", File.ReadAllText(ini + ".antes-de-otteretro"));
    }

    [Fact]
    public void RewritesJsonPathsAndArrays()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp["Datos"]);
        File.WriteAllText(tmp[@"Datos\config.json"], """{ "backend.isoPath": "E:\\Consolas\\GameCube\\Juegos\\TP\\tp.iso", "game.x": true }""");
        Directory.CreateDirectory(tmp["portable"]);
        File.WriteAllText(tmp[@"portable\Config.json"], """{ "game_dirs": ["E:\\Consolas\\Switch\\Juegos", "D:\\Otro"] }""");

        Assert.Equal(1, Recipes.RewriteGamePaths(Recipes.Find("dusklight")!, tmp.Path, @"E:\Consolas\GameCube\Juegos", @"E:\Consolas\GameCube"));
        Assert.Equal(1, Recipes.RewriteGamePaths(Recipes.Find("ryujinx-ss4k")!, tmp.Path, @"E:\Consolas\Switch\Juegos", @"F:\Switch"));

        Assert.Contains(@"E:\\Consolas\\GameCube\\TP\\tp.iso", File.ReadAllText(tmp[@"Datos\config.json"]));
        Assert.Contains("\"game.x\": true", File.ReadAllText(tmp[@"Datos\config.json"]));
        var ryujinx = File.ReadAllText(tmp[@"portable\Config.json"]);
        Assert.Contains(@"F:\\Switch", ryujinx);
        Assert.Contains(@"D:\\Otro", ryujinx);
    }

    [Fact]
    public void MakePortableWritesTheSettingInItsSection()
    {
        using var tmp = new TempDir();
        var ini = tmp[@"User\Config\Dolphin.ini"];
        Directory.CreateDirectory(Path.GetDirectoryName(ini)!);
        File.WriteAllText(ini, "[General]\nISOPath0 = X\n[NetPlay]\nA = 1\n");

        Recipes.MakePortable(Recipes.Find("dolphin")!, tmp.Path);

        var lines = File.ReadAllLines(ini);
        Assert.Equal(["[General]", "ISOPath0 = X", "RecursiveISOPaths = True", "[NetPlay]", "A = 1"], lines);
        Assert.True(File.Exists(tmp["portable.txt"]));
    }

    static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    static void Write(string path, int size = 10_000)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Enumerable.Range(0, size).Select(i => (byte)(i * 7 + path.Length)).ToArray());
    }

    [Fact]
    public void MoveFolderCopiesVerifiesAndDeletesTheSource()
    {
        using var tmp = new TempDir();
        Write(tmp[@"origen\a.bin"]);
        Write(tmp[@"origen\sub\b.bin"], 50_000);
        var bytes = File.ReadAllBytes(tmp[@"origen\sub\b.bin"]);

        DiskTasks.MoveFolder(tmp["origen"], tmp[@"destino\x"], (_, _) => { }, CancellationToken.None, forceCopy: true);

        Assert.False(Directory.Exists(tmp["origen"]));
        Assert.Equal(bytes, File.ReadAllBytes(tmp[@"destino\x\sub\b.bin"]));
    }

    [Fact]
    public void MoveFolderResumesAfterACut()
    {
        using var tmp = new TempDir();
        for (var i = 0; i < 5; i++) Write(tmp[$@"origen\{i}.bin"]);
        // Un corte a la mitad: dos copiados enteros y uno a medias.
        Directory.CreateDirectory(tmp["destino"]);
        File.Copy(tmp[@"origen\0.bin"], tmp[@"destino\0.bin"]);
        File.Copy(tmp[@"origen\1.bin"], tmp[@"destino\1.bin"]);
        File.WriteAllBytes(tmp[@"destino\2.bin"], new byte[100]);
        using var cut = new CancellationTokenSource();
        var copied = 0;
        Assert.ThrowsAny<OperationCanceledException>(() => DiskTasks.MoveFolder(tmp["origen"], tmp["destino"], (_, _) => { if (++copied == 4) cut.Cancel(); }, cut.Token, forceCopy: true));
        Assert.True(Directory.Exists(tmp["origen"])); // el origen sigue entero

        DiskTasks.MoveFolder(tmp["origen"], tmp["destino"], (_, _) => { }, CancellationToken.None, forceCopy: true);

        Assert.False(Directory.Exists(tmp["origen"]));
        for (var i = 0; i < 5; i++) Assert.Equal(10_000, new FileInfo(tmp[$@"destino\{i}.bin"]).Length);
        // Repetirla no falla: ya está movida.
        DiskTasks.MoveFolder(tmp["origen"], tmp["destino"], (_, _) => { }, CancellationToken.None, forceCopy: true);
    }

    [Fact]
    public async Task MovingAConsoleUpdatesItsFilesAndTheEmulatorPaths()
    {
        await using var f = new Fixture();
        var (a, _) = Disk.Create(f.Db, f.Tmp["a"], "A");
        var (b, _) = Disk.Create(f.Db, f.Tmp["b"], "B");
        Write(f.Tmp[@"a\Switch\Zelda\zelda.nsp"]);
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO source (kind, library_id, relative_path) VALUES ('file', @a, 'Switch\\Zelda\\zelda.nsp'), ('file', @a, 'Switch2\\otro.nsp')", new { a });
        var ryujinx = Path.Combine(f.Session.Config.DataFolder!, @"Herramientas\Emuladores\Switch\Ryujinx SS4K\1.0");
        Directory.CreateDirectory(Path.Combine(ryujinx, "portable"));
        File.WriteAllText(Path.Combine(ryujinx, "Ryujinx.exe"), "");
        File.WriteAllText(Path.Combine(ryujinx, @"portable\Config.json"), $$"""{ "game_dirs": [{{System.Text.Json.JsonSerializer.Serialize(f.Tmp[@"a\Switch"])}}] }""");

        var (task, error) = DiskTasks.EnqueueMove(f.Db, new(f.Tmp[@"a\Switch"], f.Tmp[@"b\Switch"], FromLibrary: a, FromRelative: "Switch", ToLibrary: b, ToRelative: "Switch"));
        Assert.Null(error);
        var result = await f.Run(task!.Value);

        Assert.True(result.Status == "done", System.Text.Json.JsonSerializer.Serialize(result.Detail));
        Assert.True(File.Exists(f.Tmp[@"b\Switch\Zelda\zelda.nsp"]));
        Assert.False(Directory.Exists(f.Tmp[@"a\Switch"]));
        using (var c = f.Db.Open())
        {
            Assert.Equal((b, @"Switch\Zelda\zelda.nsp"), c.QuerySingle<(string, string)>("SELECT library_id, relative_path FROM source WHERE relative_path LIKE '%zelda%'"));
            Assert.Equal(a, c.ExecuteScalar<string>("SELECT library_id FROM source WHERE relative_path = 'Switch2\\otro.nsp'"));
        }
        Assert.Contains(System.Text.Json.JsonSerializer.Serialize(f.Tmp[@"b\Switch"])[1..^1], File.ReadAllText(Path.Combine(ryujinx, @"portable\Config.json")));
    }

    /// <summary>#202, #219: `E:\Consolas` con la estructura antigua pasa entero a `E:\Otteretro`: juegos a Juegos, herramientas a sus carpetas.</summary>
    [Fact]
    public async Task AdoptsTheOldStructure()
    {
        await using var f = new Fixture();
        var (diskId, _) = Disk.Adopt(f.Db, Directory.CreateDirectory(f.Tmp["Otteretro"]).FullName, "E:", "disk");
        var disk = f.Tmp["Otteretro"];
        var lib = f.Tmp["Consolas"];
        var appData = f.Tmp["appdata"];
        Write(Path.Combine(lib, @"GameCube\Juegos\TP\tp.iso"));
        Write(Path.Combine(lib, @"Switch\Juegos\Zelda\zelda [BASE].nsp"));
        Write(Path.Combine(lib, @"GameCube\Emuladores\Dolphin\Dolphin.exe"));
        File.WriteAllText(Path.Combine(lib, @"GameCube\Emuladores\Dolphin\portable.txt"), "");
        Directory.CreateDirectory(Path.Combine(lib, @"GameCube\Emuladores\Dolphin\User\Config"));
        File.WriteAllText(Path.Combine(lib, @"GameCube\Emuladores\Dolphin\User\Config\Dolphin.ini"), $"[General]\nISOPath0 = {lib}\\GameCube\\Juegos\nISOPaths = 1\n");
        Write(Path.Combine(lib, @"GameCube\Ports\Dusklight\dusklight.exe"));
        Write(Path.Combine(lib, @"GameCube\Ports\Dusklight\Datos\texture_replacements\link.png"));
        File.WriteAllText(Path.Combine(lib, @"GameCube\Ports\Dusklight\Datos\config.json"),
            $$"""{ "backend.isoPath": {{System.Text.Json.JsonSerializer.Serialize(lib + @"\GameCube\Juegos\TP\tp.iso")}} }""");
        Write(Path.Combine(lib, @"Switch\Emuladores\Ryujinx SS4K\Ryujinx.exe"));
        Write(Path.Combine(lib, @"Switch\Emuladores\Ryujinx SS4K\portable\mods\contents\pack.bin"));
        Write(Path.Combine(lib, @"_Herramientas\Catalogo\x.js"));
        // El enlace de Dusklight en AppData apuntaba a la carpeta vieja.
        Links.Point(Path.Combine(appData, @"TwilitRealm\Dusklight"), Path.Combine(lib, @"GameCube\Ports\Dusklight\Datos"));

        var before = Environment.GetEnvironmentVariable("APPDATA");
        Environment.SetEnvironmentVariable("APPDATA", appData);
        try
        {
            var (id, error) = Disk.Adopt(f.Db, lib);
            Assert.Null(error);
            Assert.True(Disk.IsLegacy(lib));
            var result = await f.Run(DiskTasks.EnqueueMigrateIfPending(f.Db)!.Value);
            Assert.True(result.Status == "done", System.Text.Json.JsonSerializer.Serialize(result.Detail));
        }
        finally { Environment.SetEnvironmentVariable("APPDATA", before); }

        var installed = Recipes.Installed(Disk.ToolRoots(f.Db));
        Assert.Equal(["dolphin", "dusklight", "dusklight-texturas", "ryujinx-ss4k", "ryujinx-ss4k-pack"], installed.Select(t => t.RecipeId).Order());
        var dolphin = installed.Single(t => t.RecipeId == "dolphin").Path;
        var dusklight = installed.Single(t => t.RecipeId == "dusklight").Path;

        // Juegos en Juegos\<Consola>\<Juego> del disco, nada de la estructura antigua; lo que empieza con _ viaja sin tocarse.
        var games = Path.Combine(disk, "Juegos");
        Assert.True(File.Exists(Path.Combine(games, @"GameCube\TP\tp.iso")));
        Assert.True(File.Exists(Path.Combine(games, @"Switch\Zelda\zelda [BASE].nsp")));
        Assert.All(new[] { "GameCube", "Switch" }, c => Assert.All(Disk.LegacyFolders, l => Assert.False(Directory.Exists(Path.Combine(games, c, l)))));
        Assert.True(File.Exists(Path.Combine(games, @"_Herramientas\Catalogo\x.js")));
        Assert.False(Directory.Exists(lib));
        Assert.Equal(diskId, Assert.Single(Disk.List(f.Db)).Id);
        Assert.All(installed, t => Assert.StartsWith(disk, t.Path));

        // Rutas internas reescritas y portable.
        var ini = File.ReadAllText(Path.Combine(dolphin, @"User\Config\Dolphin.ini")).Replace("\r\n", "\n");
        Assert.Contains($"ISOPath0 = {games}\\GameCube\n", ini);
        Assert.Contains("RecursiveISOPaths = True", ini);
        Assert.Contains(System.Text.Json.JsonSerializer.Serialize(games + @"\GameCube\TP\tp.iso")[1..^1], File.ReadAllText(Path.Combine(dusklight, @"Datos\config.json")));

        // Complementos aparte y enlazados donde los busca su emulador o port.
        Assert.True(Links.IsLink(Path.Combine(dusklight, @"Datos\texture_replacements")));
        Assert.True(File.Exists(Path.Combine(dusklight, @"Datos\texture_replacements\link.png")));
        Assert.True(File.Exists(Path.Combine(disk, @"Complementos\Dusklight\Texturas\original\link.png")));
        Assert.True(File.Exists(Path.Combine(disk, @"Complementos\Ryujinx SS4K\Pack SS4K\original\contents\pack.bin")));

        // El enlace de AppData apunta a la instalación nueva.
        Assert.True(File.Exists(Path.Combine(appData, @"TwilitRealm\Dusklight\config.json")));
        Assert.True(Disk.Same(new DirectoryInfo(Path.Combine(appData, @"TwilitRealm\Dusklight")).LinkTarget!.Replace(@"\??\", ""), Path.Combine(dusklight, "Datos")));

        // Ya no queda nada de antes: al abrir la app otra vez no se encola nada, y repetirla a mano no rompe nada.
        Assert.Null(DiskTasks.EnqueueMigrateIfPending(f.Db));
        await Done(f, DiskTasks.EnqueueMigrate(f.Db));
    }

    /// <summary>#205: Dusklight y sus texturas se mudan a las carpetas de otro disco y vuelven, sin perder los enlaces.</summary>
    [Fact]
    public async Task ToolsMoveToALocationOfTheirTypeAndBack()
    {
        await using var f = new Fixture();
        var tools = Path.Combine(f.Session.Config.DataFolder!, "Herramientas");
        var install = Path.Combine(tools, @"Ports\GameCube\Dusklight\1.0");
        var textures = Path.Combine(tools, @"Complementos\Dusklight\Texturas\original");
        Write(Path.Combine(install, "dusklight.exe"));
        Directory.CreateDirectory(Path.Combine(install, "Datos"));
        File.WriteAllText(Path.Combine(install, @"Datos\config.json"), "{}");
        Write(Path.Combine(textures, "link.png"));
        Links.Point(Path.Combine(install, @"Datos\texture_replacements"), textures);
        var appData = f.Tmp["appdata"];
        var (ports, _) = Disk.Create(f.Db, f.Tmp["otro"], null);
        var complements = ports;
        Assert.Equal("library.badType", Disk.Adopt(f.Db, f.Tmp.Path, null, "port").Error?.Code); // ya no hay ubicaciones de un solo tipo (#219)

        var before = Environment.GetEnvironmentVariable("APPDATA");
        Environment.SetEnvironmentVariable("APPDATA", appData);
        try
        {
            var moved = f.Tmp[@"otro\Ports\GameCube\Dusklight\1.0"];
            await Done(f, DiskTasks.EnqueueMove(f.Db, new(install, moved)).Task!.Value);
            var movedTextures = f.Tmp[@"otro\Complementos\Dusklight\Texturas\original"];
            await Done(f, DiskTasks.EnqueueMove(f.Db, new(textures, movedTextures)).Task!.Value);

            var installed = Recipes.Installed(Disk.ToolRoots(f.Db));
            Assert.Equal(ports, installed.Single(t => t.RecipeId == "dusklight").LocationId);
            Assert.Equal("The Legend of Zelda: Twilight Princess", installed.Single(t => t.RecipeId == "dusklight").Game);
            Assert.Equal(complements, installed.Single(t => t.RecipeId == "dusklight-texturas").LocationId);
            // Las texturas se ven desde el port movido, y AppData apunta a la instalación nueva.
            Assert.True(File.Exists(Path.Combine(moved, @"Datos\texture_replacements\link.png")));
            Assert.True(Disk.Same(new DirectoryInfo(Path.Combine(appData, @"TwilitRealm\Dusklight")).LinkTarget!.Replace(@"\??\", ""), Path.Combine(moved, "Datos")));

            // Y de vuelta a Herramientas.
            await Done(f, DiskTasks.EnqueueMove(f.Db, new(moved, install)).Task!.Value);
            Assert.True(File.Exists(Path.Combine(install, @"Datos\texture_replacements\link.png")));
            Assert.Null(Recipes.Installed(Disk.ToolRoots(f.Db)).Single(t => t.RecipeId == "dusklight").LocationId);
        }
        finally { Environment.SetEnvironmentVariable("APPDATA", before); }
    }

    static async Task Done(Fixture f, long task)
    {
        var result = await f.Run(task);
        Assert.True(result.Status == "done", System.Text.Json.JsonSerializer.Serialize(result.Detail));
    }

    /// <summary>#192, #195: el detalle de GameCube junta la ISO, Dolphin, Dusklight con su juego, sus texturas y la BIOS.</summary>
    [Fact]
    public async Task ConsoleDetailBringsEverythingTogether()
    {
        await using var f = new Fixture();
        var tools = Path.Combine(f.Session.Config.DataFolder!, "Herramientas");
        var (games, _) = Disk.Create(f.Db, f.Tmp["disco"], "Disco");
        Write(f.Tmp[@"disco\Juegos\GameCube\TP\tp.iso"]);
        Write(f.Tmp[@"disco\Sistema\GameCube\IPL.bin"]);
        Write(Path.Combine(tools, @"Emuladores\GameCube\Dolphin\2506\Dolphin.exe"));
        Write(Path.Combine(tools, @"Ports\GameCube\Dusklight\1.0\dusklight.exe"));
        Write(Path.Combine(tools, @"Complementos\Dusklight\Texturas\original\link.png"));
        Write(Path.Combine(tools, @"Emuladores\Switch\Ryujinx SS4K\1.0\Ryujinx.exe"));

        var d = DiskEndpoints.Detail(f.Db, "gc")!;
        Assert.Equal([(games!, @"Juegos\GameCube")], d.Games.Select(x => (x.LibraryId, x.Relative)));
        Assert.Single(d.System);
        Assert.Equal(["Dolphin"], d.Emulators.Select(x => x.Name));
        Assert.Equal("The Legend of Zelda: Twilight Princess", Assert.Single(d.Ports).Game);
        Assert.Equal(["Texturas"], d.Complements.Select(x => x.Name));
        Assert.Null(d.Destination);
        Assert.Empty(DiskEndpoints.Detail(f.Db, "n64")!.Emulators);
    }

    /// <summary>#158: un disco trae sus cinco carpetas; el primero es el predeterminado y quitarlo pasa la ★ a otro.</summary>
    [Fact]
    public async Task DisksHaveTheirFoldersAndOneDefault()
    {
        await using var f = new Fixture();
        var (a, error) = Disk.Adopt(f.Db, Directory.CreateDirectory(f.Tmp[@"e\Otteretro"]).FullName, "E:", "disk");
        Assert.Null(error);
        var (b, _) = Disk.Adopt(f.Db, Directory.CreateDirectory(f.Tmp[@"f\Otteretro"]).FullName, "F:", "disk");
        var (games, _) = Disk.Adopt(f.Db, Directory.CreateDirectory(f.Tmp["juegos"]).FullName); // una carpeta de antes

        Assert.All(Disk.DiskFolders.Values, folder => Assert.True(Directory.Exists(f.Tmp[$@"e\Otteretro\{folder}"])));
        Assert.Equal(a, Disk.DefaultDisk(f.Db)!.Id);
        Assert.Null(Disk.SetDefault(f.Db, b!));
        Assert.Equal(b, Assert.Single(Disk.List(f.Db), l => l.Default).Id);
        Assert.Equal("library.notDisk", Disk.SetDefault(f.Db, games!)?.Code);
        Disk.Remove(f.Db, b!);
        Assert.Equal(a, Disk.DefaultDisk(f.Db)!.Id);

        // Las cosas de cada tipo se buscan en su carpeta fija.
        Write(f.Tmp[@"e\Otteretro\Juegos\Switch\Zelda\z.nsp"]);
        Write(f.Tmp[@"e\Otteretro\Emuladores\Switch\Ryujinx SS4K\1.0\Ryujinx.exe"]);
        Assert.Equal([(a!, @"Juegos\Switch")], Disk.ConsoleFolders(f.Db).Where(x => x.LibraryId == a).Select(x => (x.LibraryId, x.Relative)));
        Assert.Equal(a, Recipes.Installed(Disk.ToolRoots(f.Db)).Single(t => t.RecipeId == "ryujinx-ss4k").LocationId);
    }

    /// <summary>#159, #202, #204: una carpeta de antes (como E:\Consolas) pasa a Juegos de su disco y Herramientas al predeterminado.</summary>
    [Fact]
    public async Task MigratesAnOldLocationAndHerramientasToTheDisk()
    {
        await using var f = new Fixture();
        var (disk, _) = Disk.Adopt(f.Db, Directory.CreateDirectory(f.Tmp["Otteretro"]).FullName, "E:", "disk");
        var (old, _) = Disk.Adopt(f.Db, Directory.CreateDirectory(f.Tmp["Consolas"]).FullName, "Consolas");
        Write(f.Tmp[@"Consolas\Switch\Zelda\zelda.nsp"]);
        using (var c = f.Db.Open())
        {
            c.Execute("INSERT INTO source (kind, library_id, relative_path) VALUES ('file', @old, 'Switch\\Zelda\\zelda.nsp')", new { old });
            c.Execute("INSERT INTO console_destination (console_id, library_id) VALUES ('switch', @old)", new { old });
        }
        var tools = Path.Combine(f.Session.Config.DataFolder!, "Herramientas");
        var ryujinx = Path.Combine(tools, @"Emuladores\Switch\Ryujinx SS4K\1.0");
        Write(Path.Combine(ryujinx, "Ryujinx.exe"));
        Directory.CreateDirectory(Path.Combine(ryujinx, "portable"));
        File.WriteAllText(Path.Combine(ryujinx, @"portable\Config.json"), $$"""{ "game_dirs": [{{System.Text.Json.JsonSerializer.Serialize(f.Tmp[@"Consolas\Switch"])}}] }""");
        Assert.Equal((1, true), (DiskTasks.Pending(f.Db).Locations.Count, DiskTasks.Pending(f.Db).Tools));

        // Al abrir la app se encola sola, una sola vez (#219).
        var task = DiskTasks.EnqueueMigrateIfPending(f.Db)!.Value;
        Assert.Equal(task, DiskTasks.EnqueueMigrateIfPending(f.Db));
        await Done(f, task);

        Assert.True(File.Exists(f.Tmp[@"Otteretro\Juegos\Switch\Zelda\zelda.nsp"]));
        Assert.False(Directory.Exists(f.Tmp["Consolas"]));
        Assert.Equal(disk, Assert.Single(Disk.List(f.Db)).Id);
        using (var c = f.Db.Open())
        {
            Assert.Equal((disk, @"Juegos\Switch\Zelda\zelda.nsp"), c.QuerySingle<(string, string)>("SELECT library_id, relative_path FROM source"));
            Assert.Equal(disk, c.ExecuteScalar<string>("SELECT library_id FROM console_destination"));
        }
        var moved = f.Tmp[@"Otteretro\Emuladores\Switch\Ryujinx SS4K\1.0"];
        Assert.False(Directory.Exists(ryujinx));
        Assert.Contains(System.Text.Json.JsonSerializer.Serialize(f.Tmp[@"Otteretro\Juegos\Switch"])[1..^1], File.ReadAllText(Path.Combine(moved, @"portable\Config.json")));
        Assert.Equal((0, false), (DiskTasks.Pending(f.Db).Locations.Count, DiskTasks.Pending(f.Db).Tools));
        Assert.Null(DiskTasks.EnqueueMigrateIfPending(f.Db));

        // Repetirla no hace nada.
        await Done(f, DiskTasks.EnqueueMigrate(f.Db));
    }

    /// <summary>#193: cada juego de una carpeta va a su consola y su juego; lo que no se puede ubicar o chocaría se queda donde está.</summary>
    [Fact]
    public async Task ImportsAFolderAndLeavesWhatItCannotPlace()
    {
        await using var f = new Fixture();
        var (disk, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        var (other, _) = Disk.Create(f.Db, f.Tmp["Otro"], "F:");
        var games = f.Tmp[@"Otteretro\Juegos"];
        var iso = new byte[0x100];
        "GZ2P01"u8.CopyTo(iso);
        new byte[] { 0xC2, 0x33, 0x9F, 0x3D }.CopyTo(iso, 0x1C);
        var from = f.Tmp["Descargas"];
        Directory.CreateDirectory(from);
        File.WriteAllBytes(Path.Combine(from, "The Legend of Zelda Twilight Princess (Europe).iso"), iso);
        Write(Path.Combine(from, "Zelda [01006BB00C6F0000][v0].nsp"));
        Write(Path.Combine(from, @"sueltos\Zelda parche [01006BB00C6F0800][v1].nsp"));
        Write(Path.Combine(from, "Metroid Fusion.gba"));
        Write(Path.Combine(from, "misterio.iso"));
        Write(Path.Combine(from, "pack.zip"));
        Write(Path.Combine(from, @"PlayStation\Crash\Crash.cue"), "FILE \"Crash.bin\" BINARY\n  TRACK 01 MODE2/2352\n");
        Write(Path.Combine(from, @"PlayStation\Crash\Crash.bin"));
        Write(Path.Combine(from, "Choque.gba"), "nuevo");
        Write(f.Tmp[@"Otro\Juegos\Game Boy Advance\Choque\Choque.gba"], "el que ya tenías");
        using (var c = f.Db.Open())
        {
            c.Execute("""
                INSERT INTO game (id, name_es) VALUES (8, 'The Legend of Zelda: Twilight Princess'), (9, 'Zelda'), (10, 'Metroid Fusion');
                INSERT INTO version (id, game_id, platform) VALUES (18, 8, 'gc'), (19, 9, 'switch'), (20, 10, 'gba');
                INSERT INTO console_destination (console_id, library_id) VALUES ('gba', @other);
                INSERT INTO dat_entry (console, source, name, sha1) VALUES ('gc', 'redump', 'Legend of Zelda, The - Twilight Princess (Europe)', @sha1);
                """, new { other, sha1 = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(iso)) });
        }
        // Los DAT de las demás consolas no se pueden bajar acá: se importa igual, sin verificar.
        Dats.Handler = new FailingHandler();
        try
        {
            Assert.Equal("import.overlapsDisk", Import.Enqueue(f.Db, games).Error?.Code);
            Assert.Equal("import.overlapsDisk", Import.Enqueue(f.Db, f.Tmp.Path).Error?.Code);
            Assert.Equal("path.notFound", Import.Enqueue(f.Db, f.Tmp["no-existe"]).Error?.Code);
            await Done(f, Import.Enqueue(f.Db, from).Task!.Value);
        }
        finally { Dats.Handler = null; }

        // Cada uno a su lugar; el UPD, a la carpeta de su BASE; el .cue con su .bin.
        Assert.True(File.Exists(Path.Combine(games, @"GameCube\The Legend of Zelda - Twilight Princess\The Legend of Zelda Twilight Princess (Europe).iso")));
        Assert.True(File.Exists(Path.Combine(games, @"Switch\Zelda\Zelda [01006BB00C6F0000][v0].nsp")));
        Assert.True(File.Exists(Path.Combine(games, @"Switch\Zelda\Zelda parche [01006BB00C6F0800][v1].nsp")));
        Assert.True(File.Exists(Path.Combine(games, @"PlayStation\Crash\Crash.bin")));
        // GBA eligió otro disco.
        Assert.True(File.Exists(f.Tmp[@"Otro\Juegos\Game Boy Advance\Metroid Fusion\Metroid Fusion.gba"]));

        // Lo que no se pudo ubicar sigue en el origen, y lo que ya no tiene nada se quitó.
        Assert.Equal(["Choque.gba", "misterio.iso", "pack.zip"], Directory.EnumerateFileSystemEntries(from).Select(Path.GetFileName).Order());

        using (var c = f.Db.Open())
        {
            var tp = c.QuerySingle<(string Disk, long? Version, bool Verified)>("SELECT library_id, version_id, verified FROM source WHERE console = 'gc'");
            Assert.Equal((disk, (long?)18, true), tp);
            Assert.Equal(19, c.ExecuteScalar<long>("SELECT version_id FROM source WHERE component = 'UPD'"));
            Assert.Equal(other, c.ExecuteScalar<string>("SELECT library_id FROM source WHERE version_id = 20"));
        }
    }

    sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new HttpRequestException("sin red");
    }

    /// <summary>#221, #222: lo de `Entrada\` va a su mismo disco si se reconoce; lo demás espera ahí y se ve en la base; lo que se está copiando no se toca.</summary>
    [Fact]
    public async Task TheEntryFolderImportsOnlyWhatItRecognizes()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        var (other, _) = Disk.Create(f.Db, f.Tmp["Otro"], "F:");
        var entry = f.Tmp[@"Otro\Entrada"];
        Assert.True(Directory.Exists(entry));
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO game (id, name_es) VALUES (10, 'Metroid Fusion'); INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'gba');");
        Write(Path.Combine(entry, "Metroid Fusion.gba"));
        Write(Path.Combine(entry, "Raro.gba"));
        Write(Path.Combine(entry, "copiando.gba"));
        var info = Disk.Get(f.Db, other!)!;
        Dats.Handler = new FailingHandler();
        try
        {
            using (new FileStream(Path.Combine(entry, "copiando.gba"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                await Done(f, Import.EnqueueEntry(f.Db, info));

            // Va a su mismo disco, no al ★.
            Assert.True(File.Exists(f.Tmp[@"Otro\Juegos\Game Boy Advance\Metroid Fusion\Metroid Fusion.gba"]));
            Assert.True(File.Exists(Path.Combine(entry, "Raro.gba")));
            Assert.True(File.Exists(Path.Combine(entry, "copiando.gba")));
            using (var c = f.Db.Open())
                Assert.Equal((long?)null, c.QuerySingle<long?>("SELECT version_id FROM source WHERE relative_path LIKE '%Raro%'"));

            // Cuando el catálogo ya lo reconoce, se mueve y deja de esperar.
            using (var c = f.Db.Open())
                c.Execute("INSERT INTO game (id, name_es) VALUES (11, 'Raro'); INSERT INTO version (id, game_id, platform) VALUES (21, 11, 'gba');");
            await Done(f, Import.EnqueueEntry(f.Db, info));
        }
        finally { Dats.Handler = null; }
        Assert.True(File.Exists(f.Tmp[@"Otro\Juegos\Game Boy Advance\Raro\Raro.gba"]));
        using (var c = f.Db.Open())
        {
            Assert.Equal(0, c.ExecuteScalar<long>("SELECT count(*) FROM source WHERE relative_path LIKE 'Entrada%' AND relative_path LIKE '%Raro%'"));
            Assert.Equal(21, c.ExecuteScalar<long>("SELECT version_id FROM source WHERE relative_path LIKE '%Raro%'"));
        }
    }

    /// <summary>#221: dejar algo en `Entrada\` con la app abierta encola su importación, sin pulsar nada.</summary>
    [Fact]
    public async Task TheWatcherImportsWhatArrivesInTheEntryFolder()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        var quiet = GamesWatcher.Quiet;
        GamesWatcher.Quiet = TimeSpan.FromMilliseconds(200);
        try
        {
            using var watcher = new GamesWatcher();
            Assert.Null(watcher.Tick(f.Db));
            Write(f.Tmp[@"Otteretro\Entrada\juego.gba"]);
            long? task = null;
            for (var i = 0; i < 50 && task is null; i++)
            {
                await Task.Delay(100);
                task = watcher.Tick(f.Db);
            }
            using var c = f.Db.Open();
            Assert.Equal(Import.Kind, c.ExecuteScalar<string>("SELECT kind FROM task WHERE id = @task", new { task }));
        }
        finally { GamesWatcher.Quiet = quiet; }
    }

    /// <summary>#193: mover entre discos copia a un temporal y verifica; si ya está igual (se cortó antes de borrar) termina, y uno distinto nunca se pisa.</summary>
    [Fact]
    public async Task MoveInKeepsTheSourceUntilTheCopyIsVerified()
    {
        await using var f = new Fixture();
        Write(f.Tmp[@"a\juego.iso"], "datos");
        var to = f.Tmp[@"b\juego.iso"];
        Import.MoveIn(f.Tmp[@"a\juego.iso"], to, forceCopy: true);
        Assert.Equal("datos", File.ReadAllText(to));
        Assert.False(File.Exists(f.Tmp[@"a\juego.iso"]));
        Assert.False(File.Exists(to + ".parte"));

        Write(f.Tmp[@"a\juego.iso"], "datos"); // se cortó antes de borrar el origen
        Import.MoveIn(f.Tmp[@"a\juego.iso"], to, forceCopy: true);
        Assert.False(File.Exists(f.Tmp[@"a\juego.iso"]));

        Write(f.Tmp[@"a\juego.iso"], "otro contenido");
        Assert.Throws<TaskException>(() => Import.MoveIn(f.Tmp[@"a\juego.iso"], to));
        Assert.Equal("otro contenido", File.ReadAllText(f.Tmp[@"a\juego.iso"]));
        Assert.Equal("datos", File.ReadAllText(to));
    }

    /// <summary>#160: lo que tiene un disco, por tipo y con su tamaño, y mover lo marcado (carpetas y archivos sueltos) a otro.</summary>
    [Fact]
    public async Task MovesMarkedItemsToAnotherDisk()
    {
        await using var f = new Fixture();
        var (a, _) = Disk.Adopt(f.Db, Directory.CreateDirectory(f.Tmp[@"e\Otteretro"]).FullName, "E:", "disk");
        var (b, _) = Disk.Adopt(f.Db, Directory.CreateDirectory(f.Tmp[@"f\Otteretro"]).FullName, "F:", "disk");
        Write(f.Tmp[@"e\Otteretro\Juegos\Switch\Zelda\zelda.nsp"]);
        Write(f.Tmp[@"e\Otteretro\Juegos\GBA\metroid.gba"]);
        Write(f.Tmp[@"e\Otteretro\Emuladores\Switch\Ryujinx SS4K\1.0\Ryujinx.exe"]);
        Write(f.Tmp[@"e\Otteretro\Sistema\Switch\prod.keys"]);
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO source (kind, library_id, relative_path) VALUES ('file', @a, 'Juegos\\GBA\\metroid.gba')", new { a });

        var items = DiskEndpoints.Contents(f.Db, Disk.Get(f.Db, a!)!);
        Assert.Equal(["emulator", "game", "game", "system"], items.Select(i => i.Type).Order());
        Assert.All(items, i => Assert.True(i.Size > 0));

        var (tasks, error) = DiskTasks.EnqueueMoveItems(f.Db, Disk.Get(f.Db, a!)!, Disk.Get(f.Db, b!)!,
            [@"Juegos\GBA\metroid.gba", @"Juegos\Switch\Zelda", @"Emuladores\Switch\Ryujinx SS4K\1.0"]);
        Assert.Null(error);
        foreach (var task in tasks!) await Done(f, task);

        Assert.True(File.Exists(f.Tmp[@"f\Otteretro\Juegos\GBA\metroid.gba"]));
        Assert.True(File.Exists(f.Tmp[@"f\Otteretro\Juegos\Switch\Zelda\zelda.nsp"]));
        Assert.Equal(b, Recipes.Installed(Disk.ToolRoots(f.Db)).Single(t => t.RecipeId == "ryujinx-ss4k").LocationId);
        using (var c = f.Db.Open())
            Assert.Equal(b, c.ExecuteScalar<string>("SELECT library_id FROM source"));
        // Lo que ya está en el otro disco no se pisa, y nada sale del disco.
        Write(f.Tmp[@"e\Otteretro\Juegos\GBA\metroid.gba"]);
        var (from, to) = (Disk.Get(f.Db, a!)!, Disk.Get(f.Db, b!)!);
        Assert.Equal("library.conflict", DiskTasks.EnqueueMoveItems(f.Db, from, to, [@"Juegos\GBA\metroid.gba"]).Error?.Code);
        Assert.Equal("library.outside", DiskTasks.EnqueueMoveItems(f.Db, from, to, [@"..\x"]).Error?.Code);

        // #171: toda una consola en una sola tarea, junto a lo que el otro disco ya tiene de ella; si algo choca, nada.
        Assert.Equal(f.Tmp[@"f\Otteretro\Juegos\GBA\metroid.gba"], DiskTasks.EnqueueMoveItems(f.Db, from, to, [@"Juegos\GBA"]).Error?.Params?["path"]);
        File.Delete(f.Tmp[@"e\Otteretro\Juegos\GBA\metroid.gba"]);
        Write(f.Tmp[@"e\Otteretro\Juegos\GBA\zelda.gba"]);
        Write(f.Tmp[@"e\Otteretro\Juegos\GBA\Pokemon\pokemon.gba"]);
        (tasks, error) = DiskTasks.EnqueueMoveItems(f.Db, from, to, [@"Juegos\GBA"]);
        Assert.Null(error);
        await Done(f, Assert.Single(tasks!));
        Assert.True(File.Exists(f.Tmp[@"f\Otteretro\Juegos\GBA\metroid.gba"]));
        Assert.True(File.Exists(f.Tmp[@"f\Otteretro\Juegos\GBA\zelda.gba"]));
        Assert.True(File.Exists(f.Tmp[@"f\Otteretro\Juegos\GBA\Pokemon\pokemon.gba"]));
        Assert.False(Directory.Exists(f.Tmp[@"e\Otteretro\Juegos\GBA"]));
    }

    /// <summary>#208: la carpeta de datos se registra sola como disco y es siempre el ★.</summary>
    [Fact]
    public async Task TheDataFolderIsTheDefaultDisk()
    {
        await using var f = new Fixture();
        var data = f.Session.Config.DataFolder!;
        var (other, _) = Disk.Adopt(f.Db, Directory.CreateDirectory(f.Tmp[@"f\Otteretro"]).FullName, "F:", "disk");
        Assert.Equal(other, Disk.DefaultDisk(f.Db)!.Id);

        Assert.Null(Disk.EnsureDataDisk(f.Db, data));
        Assert.Null(Disk.EnsureDataDisk(f.Db, data)); // no la registra dos veces

        var disk = Disk.DefaultDisk(f.Db)!;
        Assert.True(Disk.Same(data, disk.Path));
        Assert.Equal(2, Disk.List(f.Db).Count);
        Assert.True(Directory.Exists(Path.Combine(data, "Juegos")));
    }
}
