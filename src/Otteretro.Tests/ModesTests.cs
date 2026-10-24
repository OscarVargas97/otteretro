using Otteretro.App;

namespace Otteretro.Tests;

public class ModesTests
{
    [Fact]
    public void VersionReturnsZero()
    {
        var output = new StringWriter();
        Assert.Equal(0, CliMode.Run(["version"], output));
        Assert.Equal(Startup.Version, output.ToString().Trim());
    }

    [Fact]
    public void UnknownCommandReturnsOneAndShowsHelp()
    {
        var output = new StringWriter();
        Assert.Equal(1, CliMode.Run(["volar"], output));
        Assert.Contains("Uso: otteretro", output.ToString());
    }

    [Fact]
    public void PlayIsNotImplementedYet() =>
        Assert.Equal(PlayMode.NotImplemented, PlayMode.Run(["123"]));

    [Fact]
    public void OpenApiDescribesTheApi()
    {
        using var tmp = new TempDir();
        var file = tmp["openapi.json"];
        Assert.Equal(0, CliMode.Run(["openapi", file], new StringWriter()));
        var json = File.ReadAllText(file);
        Assert.Contains("/api/state", json);
        Assert.Contains("/api/data-folder", json);
        Assert.DoesNotContain("/api/events", json);
    }
}
