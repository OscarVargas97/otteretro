using System.IO.Compression;
using System.Net;
using Dapper;
using Otteretro.App;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>
/// #350: un emulador desde su repo deja una receta local en la carpeta de datos (decisiones 346 y 348). Sin paralelo: las recetas
/// locales son de la carpeta de datos abierta, que es una sola (cada Fixture la cambia).
/// </summary>
[Collection(nameof(RepoInstallTests))]
public class RepoInstallTests
{
    [Fact]
    public void ARepoUrlSaysItsSource()
    {
        Assert.Equal(new RecipeUpdates("github", "Frizti/Fritchii", Dev: true), ToolUpdates.Source("https://github.com/Frizti/Fritchii/releases"));
        Assert.Equal(new RecipeUpdates("github", "Frizti/Fritchii", Dev: true), ToolUpdates.Source("github.com/Frizti/Fritchii.git"));
        Assert.Equal(new RecipeUpdates("forgejo", "eden-emu/eden", "git.eden-emu.dev", Dev: true), ToolUpdates.Source("https://git.eden-emu.dev/eden-emu/eden"));
        Assert.Null(ToolUpdates.Source("http://github.com/a/b"));
        Assert.Null(ToolUpdates.Source("https://github.com/solo-uno"));
        Assert.Null(ToolUpdates.Source("no es una url"));
        Assert.Equal(@"^Fritchii-v[0-9.]+-Windows-x[0-9.]+\.zip$", Recipes.AssetPattern("Fritchii-v4.1-Windows-x64.zip"));
        Assert.Matches(Recipes.AssetPattern("Fritchii-v4.1-Windows-x64.zip"), "Fritchii-v5-Windows-x64.zip");
    }

    [Fact]
    public async Task InstallingFromARepoLeavesALocalRecipe()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["E"], "E:");
        var src = f.Tmp["src"];
        Directory.CreateDirectory(Path.Combine(src, "Fritchii-v4.1"));
        foreach (var exe in new[] { "lichii.exe", "vc_redist.x64.exe" }) File.WriteAllText(Path.Combine(src, "Fritchii-v4.1", exe), exe);
        var zip = f.Tmp["Fritchii-v4.1-Windows-x64.zip"];
        ZipFile.CreateFromDirectory(src, zip);
        Installs.Handler = new Fake(File.ReadAllBytes(zip));
        try
        {
            var job = new InstallJob("mi-fritchii", "fritchii-v4.1", Url: "https://github.com/Frizti/Fritchii/releases/download/fritchii-v4.1/Fritchii-v4.1-Windows-x64.zip",
                Name: "Mi Fritchii", Repo: "https://github.com/Frizti/Fritchii", Console: "switch2");
            Assert.Equal("install.badRepo", Installs.Enqueue(f.Db, job with { Repo = "https://github.com/x" }).Error?.Code);
            var (task, error) = Installs.Enqueue(f.Db, job);
            Assert.Null(error);
            Assert.Equal("done", (await f.Run(task!.Value)).Status);
        }
        finally { Installs.Handler = null; }

        // El instalador de Visual C++ no cuenta: el ejecutable es el otro.
        Assert.True(File.Exists(f.Tmp[@"datos\data\recetas\mi-fritchii.json"]));
        var recipe = Recipes.Find("mi-fritchii");
        Assert.True(recipe is { Name: "Mi Fritchii", Kind: "emulator", Detect: "lichii.exe", Consoles: ["switch2"], Updates: { Source: "github", Repo: "Frizti/Fritchii" } });
        Assert.Matches(recipe!.Asset!, "Fritchii-v4.2-Windows-x64.zip");
        using var c = f.Db.Open();
        Assert.Equal(1, c.ExecuteScalar<int>("SELECT count(*) FROM console_emulator WHERE console_id = 'switch2' AND emulator_id = 'mi-fritchii'"));
        // Se vuelve a leer al abrir la carpeta de datos.
        Recipes.LocalFolder = Recipes.LocalFolder;
        Assert.NotNull(Recipes.Find("mi-fritchii"));
    }

    [Fact]
    public async Task WithSeveralExecutablesItAsksWhichOne()
    {
        await using var f = new Fixture();
        Disk.Create(f.Db, f.Tmp["E"], "E:");
        var src = f.Tmp["src"];
        Directory.CreateDirectory(src);
        foreach (var exe in new[] { "uno.exe", "dos.exe" }) File.WriteAllText(Path.Combine(src, exe), exe);
        var zip = f.Tmp["emu.zip"];
        ZipFile.CreateFromDirectory(src, zip);
        Installs.Handler = new Fake(File.ReadAllBytes(zip));
        try
        {
            var job = new InstallJob("emu", "1.0", Url: "https://git.example.org/a/emu/releases/download/1.0/emu.zip", Name: "Emu", Repo: "git.example.org/a/emu");
            var asked = await f.Run(Installs.Enqueue(f.Db, job).Task!.Value);
            Assert.Equal(("install.pickExe", "dos.exe|uno.exe"), (asked.Detail?.Code, asked.Detail?.Params?["files"]?.ToString()));
            Assert.Null(Recipes.Find("emu"));
            // La respuesta reintenta con el archivo que ya quedó en la librería.
            Installs.Handler = null;
            Assert.Equal("done", (await f.Run(Installs.Enqueue(f.Db, job with { Detect = "uno.exe" }).Task!.Value)).Status);
        }
        finally { Installs.Handler = null; }
        Assert.True(Recipes.Find("emu") is { Detect: "uno.exe", Consoles: [], Updates: { Source: "forgejo", Host: "git.example.org", Repo: "a/emu" } });
    }

    sealed class Fake(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }
}

[CollectionDefinition(nameof(RepoInstallTests), DisableParallelization = true)]
public class RepoInstallCollection;
