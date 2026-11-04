using System.IO.Compression;
using System.Net;
using System.Text;
using Dapper;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#237: juegos de Switch 2 que llegan ya extraídos (`exefs\main`, `romfs`), como los bajan los paquetes de hoy.</summary>
[Collection("Dats.Handler")]
public class SwitchDumpTests
{
    const string Pokopia = "0400A3A01EE4C000", Unknown = "0400FFFFFFFF0000";

    /// <summary>Un `main.npdm` mínimo: cabecera META y una sección ACI0 con el program id.</summary>
    static byte[] Npdm(string titleId)
    {
        var data = new byte[0xA0];
        "META"u8.CopyTo(data);
        BitConverter.GetBytes(0x80).CopyTo(data, 0x70);
        "ACI0"u8.CopyTo(data.AsSpan(0x80));
        BitConverter.GetBytes(Convert.ToUInt64(titleId, 16)).CopyTo(data, 0x90);
        return data;
    }

    /// <summary>Un zip con lo que se le da (texto o bytes), con sus carpetas.</summary>
    static byte[] Zip(params (string Name, byte[] Data)[] files)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            foreach (var (name, data) in files)
                using (var w = zip.CreateEntry(name).Open()) w.Write(data);
        return memory.ToArray();
    }

    static (string, byte[])[] Dump(string folder, string titleId) =>
    [
        ($"{folder}/base/exefs/main", "codigo"u8.ToArray()),
        ($"{folder}/base/exefs/main.npdm", Npdm(titleId)),
        ($"{folder}/base/romfs/datos.bin", "datos"u8.ToArray()),
        ($"{folder}/updates/1.1.0/exefs/main", "parche"u8.ToArray()),
    ];

    sealed class Switchbrew : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            const string Wiki = """
                {| class="wikitable"
                |-
                | 0400A3A01EE4C000 || Pokémon™ Pokopia || CHN EUR JPN KOR USA ||  || Digital ||  ||  || Game
                |-
                | 0100C2500FC20000 || Splatoon™ 3 – Nintendo Switch 2 Edition || USA || || Digital || || || Game
                |}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Wiki, Encoding.UTF8) });
        }
    }

    sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new HttpRequestException("sin red");
    }

    [Fact]
    public void TheTitleTableKeepsTheGameName()
    {
        var table = SwitchTitles.Parse("""
            | 0400A3A01EE4C000 || Pokémon™ Pokopia || CHN || x
            | 0100C2500FC20000 || Splatoon™ 3 – Nintendo Switch 2 Edition || USA || x
            """);
        Assert.Equal("Pokémon Pokopia", table[Pokopia]);
        Assert.Equal("Splatoon 3", table["0100C2500FC20000"]);
    }

    /// <summary>
    /// El paquete de Mario Kart World: un comprimido con otro adentro (el juego extraído) y un emulador. El juego va entero a
    /// `Juegos\Switch 2\&lt;Juego&gt;`, reconocido por su TitleID sin IA; el emulador queda apartado; y el escaneo lo cuenta como «lo tengo».
    /// </summary>
    [Fact]
    public async Task AnExtractedGameInsideAnotherArchiveIsPlacedWholeAndTheEmulatorSetAside()
    {
        await using var f = new Fixture();
        var (id, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        using (var c = f.Db.Open())
            c.Execute("INSERT INTO game (id, name_es) VALUES (50205, 'Pokémon Pokopia'); INSERT INTO version (id, game_id, platform) VALUES (70, 50205, 'switch2');");
        var inner = Zip(Dump("PPP ROM", Pokopia));
        File.WriteAllBytes(f.Tmp[@"Otteretro\Entrada\PPP\PPP ROM.zip"].Also(p => Directory.CreateDirectory(Path.GetDirectoryName(p)!)),
            Zip(("PPP [ROM].zip", inner), ("Emulator.zip", Zip(("eden.exe", "emulador"u8.ToArray())))));
        var switchbrew = new Switchbrew();
        (Dats.Handler, SwitchTitles.Handler) = (new NoNetwork(), switchbrew);
        try { Assert.Equal("done", (await f.Run(Import.EnqueueEntry(f.Db, Disk.Get(f.Db, id!)!))).Status); }
        finally { (Dats.Handler, SwitchTitles.Handler) = (null, null); }

        var game = f.Tmp[@"Otteretro\Juegos\Switch 2\Pokémon Pokopia"];
        Assert.Equal("codigo", File.ReadAllText(Path.Combine(game, @"base\exefs\main")));
        Assert.True(File.Exists(Path.Combine(game, @"updates\1.1.0\exefs\main")));
        Assert.True(File.Exists(f.Tmp[@"Otteretro\Entrada\Apartado\PPP ROM\Emulator\eden.exe"]));
        Assert.False(File.Exists(f.Tmp[@"Otteretro\Entrada\PPP\PPP ROM.zip"]));
        using (var c = f.Db.Open())
            Assert.Equal((70L, "switch2", Pokopia), c.QuerySingle<(long, string, string)>(
                "SELECT version_id, console, title_id FROM source WHERE relative_path = @path", new { path = @"Juegos\Switch 2\Pokémon Pokopia" }));

        // El escaneo cuenta la carpeta como un juego y no la borra; la tabla ya está guardada: no la vuelve a bajar.
        SwitchTitles.Handler = new NoNetwork();
        try { Assert.Equal("done", (await f.Run(Scan.Enqueue(f.Db))).Status); }
        finally { SwitchTitles.Handler = null; }
        using var db = f.Db.Open();
        Assert.Equal(70, db.ExecuteScalar<long>("SELECT version_id FROM source WHERE relative_path = @path", new { path = @"Juegos\Switch 2\Pokémon Pokopia" }));
        Assert.Equal(0, db.ExecuteScalar<long>("SELECT count(*) FROM source WHERE relative_path LIKE '%exefs%'"));
        Assert.Equal(1, switchbrew.Calls);
    }

    /// <summary>
    /// Lo que no se reconoce espera en `Entrada\` (para Revisar) y no se vuelve a descomprimir cada vez que el vigilante mira; un paquete
    /// sin juegos, tampoco.
    /// </summary>
    [Fact]
    public async Task WhatWasTriedIsNotExtractedAgainByItself()
    {
        await using var f = new Fixture();
        var (id, _) = Disk.Create(f.Db, f.Tmp["Otteretro"], "E:");
        Directory.CreateDirectory(f.Tmp[@"Otteretro\Entrada"]);
        File.WriteAllBytes(f.Tmp[@"Otteretro\Entrada\DKB ROM.zip"], Zip(Dump("DKB ROM", Unknown)));
        File.WriteAllBytes(f.Tmp[@"Otteretro\Entrada\leeme.zip"], Zip(("leeme.txt", "nada"u8.ToArray())));
        (Dats.Handler, SwitchTitles.Handler) = (new NoNetwork(), new Switchbrew());
        try
        {
            var disk = Disk.Get(f.Db, id!)!;
            var first = await f.Run(Import.EnqueueEntry(f.Db, disk));
            var second = await f.Run(Import.EnqueueEntry(f.Db, disk));
            using var c = f.Db.Open();
            string[] Logs(long task) => c.Query<string>("SELECT message FROM task_log WHERE task_id = @task", new { task }).ToArray();
            Assert.Contains(Logs(first.Id), m => m.Contains("import.log.waiting"));
            Assert.Contains(Logs(first.Id), m => m.Contains("import.log.noGames"));
            Assert.DoesNotContain(Logs(second.Id), m => m.Contains("import.log.waiting") || m.Contains("import.log.noGames"));
            Assert.Equal(("switch2", Unknown), c.QuerySingle<(string, string)>("SELECT console, title_id FROM source WHERE relative_path = @path", new { path = @"Entrada\DKB ROM.zip" }));
        }
        finally { (Dats.Handler, SwitchTitles.Handler) = (null, null); }
    }
}

static class TestExtensions
{
    public static T Also<T>(this T value, Action<T> action) { action(value); return value; }
}
