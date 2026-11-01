using Dapper;
using Otteretro.App;
using Otteretro.Libraries;

namespace Otteretro.Tests;

public class LibraryTests
{
    [Fact]
    public async Task CreatesAndReadsTheMarker()
    {
        await using var f = new Fixture();
        var (id, error) = Library.Create(f.Db, f.Tmp["juegos"], "Mis juegos");

        Assert.Null(error);
        var marker = Library.ReadMarker(f.Tmp["juegos"])!;
        Assert.Equal(id, marker.Id);
        Assert.Equal(("Mis juegos", "local", Library.MarkerFormat), (marker.Name, marker.Kind, marker.Format));
        Assert.Contains("\"nombre\"", File.ReadAllText(Path.Combine(f.Tmp["juegos"], Library.MarkerFile)));
        var info = Assert.Single(Library.List(f.Db));
        Assert.True(info.Connected);
        Assert.False(info.Legacy);
    }

    [Fact]
    public void NetworkPathsAreNetworkLibraries()
    {
        Assert.True(Library.IsNetwork(@"\\nas\roms"));
        Assert.True(Library.IsNetwork("//nas/roms"));
        Assert.False(Library.IsNetwork(@"E:\Consolas"));
    }

    [Fact]
    public async Task RejectsOverlappingLibraries()
    {
        await using var f = new Fixture();
        Library.Create(f.Db, f.Tmp["juegos"], null);

        Assert.Equal("library.overlaps", Library.Create(f.Db, f.Tmp[@"juegos\Switch"], null).Error?.Code);
        Assert.Equal("library.overlaps", Library.Create(f.Db, f.Tmp.Path, null).Error?.Code);
    }

    [Fact]
    public async Task RemovingNeverDeletesFiles()
    {
        await using var f = new Fixture();
        var (id, _) = Library.Create(f.Db, f.Tmp["juegos"], null);
        File.WriteAllText(f.Tmp[@"juegos\juego.iso"], "x");

        Assert.True(Library.Remove(f.Db, id!));

        Assert.Empty(Library.List(f.Db));
        Assert.True(File.Exists(f.Tmp[@"juegos\juego.iso"]));
        Assert.NotNull(Library.ReadMarker(f.Tmp["juegos"]));
    }

    [Fact]
    public async Task RenameUpdatesTheMarker()
    {
        await using var f = new Fixture();
        var (id, _) = Library.Create(f.Db, f.Tmp["juegos"], "A");

        Assert.Null(Library.Rename(f.Db, id!, " B "));

        Assert.Equal("B", Library.ReadMarker(f.Tmp["juegos"])!.Name);
        Assert.Equal("B", Library.List(f.Db).Single().Name);
    }

    [Fact]
    public async Task ReconnectsALibraryThatChangedPlace()
    {
        await using var f = new Fixture();
        var (id, _) = Library.Create(f.Db, f.Tmp["juegos"], null);
        Directory.CreateDirectory(f.Tmp["disco"]);
        Directory.Move(f.Tmp["juegos"], f.Tmp[@"disco\otra"]);
        Assert.False(Library.List(f.Db).Single().Connected);

        Assert.Equal(1, Library.Reconnect(f.Db, [f.Tmp.Path]));

        var info = Library.List(f.Db).Single();
        Assert.True(info.Connected);
        Assert.Equal(f.Tmp[@"disco\otra"], info.Path);
    }

    [Fact]
    public async Task AdoptingAKnownMarkerReconnects()
    {
        await using var f = new Fixture();
        var (id, _) = Library.Create(f.Db, f.Tmp["juegos"], null);
        Directory.Move(f.Tmp["juegos"], f.Tmp["movida"]);

        var (again, legacy, error) = Library.Adopt(f.Db, f.Tmp["movida"]);

        Assert.Null(error);
        Assert.False(legacy);
        Assert.Equal(id, again);
        Assert.True(Library.List(f.Db).Single().Connected);
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

        LibraryTasks.MoveFolder(tmp["origen"], tmp[@"destino\x"], (_, _) => { }, CancellationToken.None, forceCopy: true);

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
        Assert.ThrowsAny<OperationCanceledException>(() => LibraryTasks.MoveFolder(tmp["origen"], tmp["destino"], (_, _) => { if (++copied == 4) cut.Cancel(); }, cut.Token, forceCopy: true));
        Assert.True(Directory.Exists(tmp["origen"])); // el origen sigue entero

        LibraryTasks.MoveFolder(tmp["origen"], tmp["destino"], (_, _) => { }, CancellationToken.None, forceCopy: true);

        Assert.False(Directory.Exists(tmp["origen"]));
        for (var i = 0; i < 5; i++) Assert.Equal(10_000, new FileInfo(tmp[$@"destino\{i}.bin"]).Length);
        // Repetirla no falla: ya está movida.
        LibraryTasks.MoveFolder(tmp["origen"], tmp["destino"], (_, _) => { }, CancellationToken.None, forceCopy: true);
    }

    [Fact]
    public async Task MovingAConsoleUpdatesItsFilesAndTheEmulatorPaths()
    {
        await using var f = new Fixture();
        var (a, _) = Library.Create(f.Db, f.Tmp["a"], "A");
        var (b, _) = Library.Create(f.Db, f.Tmp["b"], "B");
        Write(f.Tmp[@"a\Switch\Zelda\zelda.nsp"]);
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO source (kind, library_id, relative_path) VALUES ('file', @a, 'Switch\\Zelda\\zelda.nsp'), ('file', @a, 'Switch2\\otro.nsp')", new { a });
        var ryujinx = Path.Combine(f.Session.Config.DataFolder!, @"Herramientas\Emuladores\Switch\Ryujinx SS4K\1.0");
        Directory.CreateDirectory(Path.Combine(ryujinx, "portable"));
        File.WriteAllText(Path.Combine(ryujinx, "Ryujinx.exe"), "");
        File.WriteAllText(Path.Combine(ryujinx, @"portable\Config.json"), $$"""{ "game_dirs": [{{System.Text.Json.JsonSerializer.Serialize(f.Tmp[@"a\Switch"])}}] }""");

        var (task, error) = LibraryTasks.EnqueueMove(f.Db, new(f.Tmp[@"a\Switch"], f.Tmp[@"b\Switch"], FromLibrary: a, FromRelative: "Switch", ToLibrary: b, ToRelative: "Switch"));
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

    [Fact]
    public async Task AdoptsTheOldStructure()
    {
        await using var f = new Fixture();
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
            var (id, legacy, error) = Library.Adopt(f.Db, lib);
            Assert.Null(error);
            Assert.True(legacy);
            var result = await f.Run(LibraryTasks.EnqueueAdopt(f.Db, id!));
            Assert.True(result.Status == "done", System.Text.Json.JsonSerializer.Serialize(result.Detail));
        }
        finally { Environment.SetEnvironmentVariable("APPDATA", before); }

        var tools = Path.Combine(f.Session.Config.DataFolder!, "Herramientas");
        var installed = Recipes.Installed(tools);
        Assert.Equal(["dolphin", "dusklight", "dusklight-texturas", "ryujinx-ss4k", "ryujinx-ss4k-pack"], installed.Select(t => t.RecipeId).Order());
        var dolphin = installed.Single(t => t.RecipeId == "dolphin").Path;
        var dusklight = installed.Single(t => t.RecipeId == "dusklight").Path;

        // Juegos en <Consola>\<Juego>, nada de la estructura antigua; lo que empieza con _ no se toca.
        Assert.True(File.Exists(Path.Combine(lib, @"GameCube\TP\tp.iso")));
        Assert.True(File.Exists(Path.Combine(lib, @"Switch\Zelda\zelda [BASE].nsp")));
        Assert.All(new[] { "GameCube", "Switch" }, c => Assert.All(Library.LegacyFolders, l => Assert.False(Directory.Exists(Path.Combine(lib, c, l)))));
        Assert.True(File.Exists(Path.Combine(lib, @"_Herramientas\Catalogo\x.js")));
        Assert.False(Library.List(f.Db).Single().Legacy);

        // Rutas internas reescritas y portable.
        var ini = File.ReadAllText(Path.Combine(dolphin, @"User\Config\Dolphin.ini")).Replace("\r\n", "\n");
        Assert.Contains($"ISOPath0 = {lib}\\GameCube\n", ini);
        Assert.Contains("RecursiveISOPaths = True", ini);
        Assert.Contains(System.Text.Json.JsonSerializer.Serialize(lib + @"\GameCube\TP\tp.iso")[1..^1], File.ReadAllText(Path.Combine(dusklight, @"Datos\config.json")));

        // Complementos aparte y enlazados donde los busca su emulador o port.
        Assert.True(Links.IsLink(Path.Combine(dusklight, @"Datos\texture_replacements")));
        Assert.True(File.Exists(Path.Combine(dusklight, @"Datos\texture_replacements\link.png")));
        Assert.True(File.Exists(Path.Combine(tools, @"Complementos\Dusklight\Texturas\original\link.png")));
        Assert.True(File.Exists(Path.Combine(tools, @"Complementos\Ryujinx SS4K\Pack SS4K\original\contents\pack.bin")));

        // El enlace de AppData apunta a la instalación nueva.
        Assert.True(File.Exists(Path.Combine(appData, @"TwilitRealm\Dusklight\config.json")));
        Assert.True(Library.Same(new DirectoryInfo(Path.Combine(appData, @"TwilitRealm\Dusklight")).LinkTarget!.Replace(@"\??\", ""), Path.Combine(dusklight, "Datos")));

        // Repetir la migración no rompe nada.
        var again = await f.Run(LibraryTasks.EnqueueAdopt(f.Db, Library.List(f.Db).Single().Id));
        Assert.True(again.Status == "done", System.Text.Json.JsonSerializer.Serialize(again.Detail));
    }
}
