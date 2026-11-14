using System.IO.Compression;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#195: comprimidos reales (generados con 7-Zip; los rar, de las pruebas de SharpCompress, MIT).</summary>
[Collection("Dats.Handler")] // comparten ese estático
public class ArchivesTests
{
    const string Encrypted = "UEsDBBQAAQAAALeBYV0tQ3PPIAAAABQAAAASAAAATWV0cm9pZCBGdXNpb24uZ2JhS8B8mpQpR1NUuVe8IlfQuh8BjFfUFtcqHkJOZtL7N+1QSwECPwAUAAEAAAC3gWFdLUNzzyAAAAAUAAAAEgAkAAAAAAAAACAAAAAAAAAATWV0cm9pZCBGdXNpb24uZ2JhCgAgAAAAAAABABgATjlWHTVq3QEAAAAAAAAAAAAAAAAAAAAAUEsFBgAAAAABAAEAZAAAAFAAAAAAAA==";

    static readonly string[] SevenZipParts =
    [
        "N3q8ryccAAS1NYQp+gAAAAAAAABaAAAAAAAAALT1MBoABw4VHCMqMTg/Rk1UW2JpcHd+hYyTmqGor7a9xMvS2eDn7vUBCA8WHSQrMjlAR05VXGNqcXh/ho2Um6KpsLe+xczT2uHo7/YCCRAXHiUsMzpBSE9WXWRr",
        "cnmAh46VnKOqsbi/xs3U2+Lp8PcDChEYHyYtNDtCSVBXXmVsc3qBiI+WnaSrsrnAx87V3OPq8fgECxIZICcuNTxDSlFYX2ZtdHuCiZCXnqWss7rByM/W3eTr8vkFDBMaISgvNj1ES1JZYGdudXyDipGYn6attLvC",
        "ydDX3uXs8/oGDRQbIikwNz5FTFNaYWhvdn2Ei5KZoKeutbzDytHY3+btAQQGAAEJgPoABwsBAAEBAAyA+gAICgEWm+BGAAAFARkMAAAAAAAAAAAAAAAAEREAcgBvAG0ALgBnAGIAYQAAABkCAAAUCgEAdD4PJTVq",
        "3QEVBgEAIAAAAAAA",
    ];

    static void Zip(string path, params (string Name, string Text)[] files)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, text) in files)
        {
            using var w = new StreamWriter(zip.CreateEntry(name).Open());
            w.Write(text);
        }
    }

    [Fact]
    public async Task ExtractsZipKeepingFolders()
    {
        await using var f = new Fixture();
        Zip(f.Tmp["a.zip"], (@"Juego/juego.gba", "datos"));
        Assert.True(Archives.IsMain(f.Tmp["a.zip"]));
        Assert.Null(Archives.Extract(f.Tmp["a.zip"], f.Tmp["sale"], [], default));
    }

    [Fact]
    public async Task ExtractsATarGz()
    {
        await using var f = new Fixture();
        var src = Directory.CreateDirectory(f.Tmp["src"]).FullName;
        File.WriteAllText(Path.Combine(src, "tmc_pc.exe"), "exe");
        Directory.CreateDirectory(Path.Combine(src, "assets"));
        File.WriteAllText(Path.Combine(src, "assets", "a.logic"), "datos");
        using (var gz = new GZipStream(File.Create(f.Tmp["port.tar.gz"]), CompressionLevel.Fastest))
            System.Formats.Tar.TarFile.CreateFromDirectory(src, gz, includeBaseDirectory: false);
        Assert.True(Archives.IsMain(f.Tmp["port.tar.gz"]));
        Assert.Null(Archives.Extract(f.Tmp["port.tar.gz"], f.Tmp["sale"], [], default));
        Assert.Equal("exe", File.ReadAllText(Path.Combine(f.Tmp["sale"], "tmc_pc.exe")));
        Assert.Equal("datos", File.ReadAllText(Path.Combine(f.Tmp["sale"], "assets", "a.logic")));
    }

    [Fact]
    public async Task TriesEachPasswordOfTheList()
    {
        await using var f = new Fixture();
        File.WriteAllBytes(f.Tmp["cifrado.zip"], Convert.FromBase64String(Encrypted));

        Assert.Equal("import.log.password", Archives.Extract(f.Tmp["cifrado.zip"], f.Tmp["sale"], [], default)?.Code);
        Assert.False(Directory.Exists(f.Tmp["sale"]));
        Assert.Equal("import.log.password", Archives.Extract(f.Tmp["cifrado.zip"], f.Tmp["sale"], ["otra"], default)?.Code);

        Assert.Null(Archives.Extract(f.Tmp["cifrado.zip"], f.Tmp["sale"], ["otra", "clave"], default));
    }

    [Fact]
    public async Task ExtractsA7zInParts()
    {
        await using var f = new Fixture();
        for (var i = 0; i < SevenZipParts.Length; i++) File.WriteAllBytes(f.Tmp[$"partes.7z.00{i + 1}"], Convert.FromBase64String(SevenZipParts[i]));
        Assert.True(Archives.IsMain(f.Tmp["partes.7z.001"]));
        Assert.False(Archives.IsMain(f.Tmp["partes.7z.002"]));
        Assert.Equal(4, Archives.Parts(f.Tmp["partes.7z.001"]).Count);

        Assert.Null(Archives.Extract(f.Tmp["partes.7z.001"], f.Tmp["sale"], [], default));
    }

    [Fact]
    public async Task ExtractsARarInParts()
    {
        await using var f = new Fixture();
        var fixtures = Path.Combine(AppContext.BaseDirectory, "fixtures", "rar");
        Directory.CreateDirectory(f.Tmp["rar"]);
        foreach (var part in Directory.GetFiles(fixtures)) File.Copy(part, Path.Combine(f.Tmp["rar"], Path.GetFileName(part)));
        var main = f.Tmp[@"rar\Rar.multi.part01.rar"];
        Assert.True(Archives.IsMain(main));
        Assert.False(Archives.IsMain(f.Tmp[@"rar\Rar.multi.part02.rar"]));
        Assert.True(Archives.Parts(main).Count > 1);

        Assert.Null(Archives.Extract(main, f.Tmp["sale"], [], default));
        Assert.NotEmpty(Directory.GetFiles(f.Tmp["sale"], "*", SearchOption.AllDirectories));
    }

    /// <summary>Dañado o con una ruta que se sale de la carpeta: falla, no deja nada y no escribe afuera.</summary>
    [Fact]
    public async Task RejectsDamagedArchivesAndPathsThatEscape()
    {
        await using var f = new Fixture();
        Zip(f.Tmp["malo.zip"], ("juego.gba", new string('x', 5000)));
        var bytes = File.ReadAllBytes(f.Tmp["malo.zip"]);
        bytes[40] ^= 0xFF; // dentro de los datos comprimidos
        File.WriteAllBytes(f.Tmp["malo.zip"], bytes);
        Assert.Equal("import.log.archiveFailed", Archives.Extract(f.Tmp["malo.zip"], f.Tmp["sale"], [], default)?.Code);
        Assert.False(Directory.Exists(f.Tmp["sale"]));

        Zip(f.Tmp["fuga.zip"], ("../afuera.gba", "x"));
        Assert.Equal("import.log.archiveFailed", Archives.Extract(f.Tmp["fuga.zip"], f.Tmp["sale"], [], default)?.Code);
        Assert.False(File.Exists(f.Tmp["afuera.gba"]));
    }

    /// <summary>Importar: lo de adentro va a su lugar y el comprimido se borra; si algo no se pudo ubicar, el comprimido se queda.</summary>
    [Fact]
    public async Task ImportsWhatIsInsideAndDeletesTheArchiveOnlyIfAllWasPlaced()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO game (id, name_es) VALUES (10, 'Metroid Fusion'); INSERT INTO version (id, game_id, platform) VALUES (20, 10, 'gba');");
        var from = f.Tmp["Descargas"];
        Zip(Path.Combine(from, "Metroid Fusion (Europe).zip"), ("Metroid Fusion (Europe).gba", "rom"), ("leeme.txt", "basura"));
        Zip(Path.Combine(from, "mezcla.zip"), ("uno.gba", "rom"), ("misterio.iso", "rom"));
        Zip(Path.Combine(from, "sin juegos.zip"), ("leeme.txt", "basura"));
        Dats.Handler = new NoNetwork();
        try { Assert.Equal("done", (await f.Run(Import.Enqueue(f.Db, from).Task!.Value)).Status); }
        finally { Dats.Handler = null; }

        Assert.True(File.Exists(f.Tmp[@"Otteretro\Juegos\Game Boy Advance\Metroid Fusion\Metroid Fusion (Europe).gba"]));
        Assert.True(File.Exists(f.Tmp[@"Otteretro\Juegos\Game Boy Advance\mezcla\uno.gba"]));
        Assert.Equal(["mezcla.zip", "sin juegos.zip"], Directory.EnumerateFileSystemEntries(from).Select(Path.GetFileName).Order());
        using var db = f.Db.Open();
        Assert.Equal(20, db.ExecuteScalar<long>("SELECT version_id FROM source WHERE relative_path LIKE '%Metroid%'"));
    }

    /// <summary>En `Entrada\`, un comprimido que no se reconoce espera entero, visible, y extraer no deja nada a la vista del vigilante.</summary>
    [Fact]
    public async Task AnUnknownArchiveWaitsInTheEntryFolder()
    {
        await using var f = new Fixture();
        var (id, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        Zip(f.Tmp[@"Otteretro\Entrada\raro.zip"], ("raro.gba", "rom"));
        Dats.Handler = new NoNetwork();
        try { Assert.Equal("done", (await f.Run(Import.EnqueueEntry(f.Db, Disk.Get(f.Db, id!)!))).Status); }
        finally { Dats.Handler = null; }

        Assert.Equal(["raro.zip"], Directory.GetFileSystemEntries(f.Tmp[@"Otteretro\Entrada"]).Select(Path.GetFileName));
        Assert.False(Directory.Exists(f.Tmp[@"Otteretro\.extrayendo"]));
        using var db = f.Db.Open();
        Assert.Equal(@"Entrada\raro.zip", db.ExecuteScalar<string>("SELECT relative_path FROM source"));
    }

    sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new HttpRequestException("sin red");
    }
}
