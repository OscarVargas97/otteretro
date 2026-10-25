using Otteretro.App;

namespace Otteretro.Tests;

public class UpdaterTests
{
    [Fact]
    public async Task OnLinuxItSaysItUpdatesWithTheSystem()
    {
        using var events = new Otteretro.Api.Events();
        var updater = new Updater(new AppSession(new AppConfig { UpdateSource = Path.GetTempPath() }, "config.json", "clave"), events);
        await updater.Check();

        if (OperatingSystem.IsWindows())
            Assert.NotEqual("withSystem", updater.State.Message);
        else
            Assert.Equal((UpdatePhase.Unavailable, "withSystem"), (updater.State.Phase, updater.State.Message));
    }
}
