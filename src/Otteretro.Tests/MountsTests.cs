using Otteretro.App;

namespace Otteretro.Tests;

public class MountsTests
{
    static readonly string[] Linux = ["/", "/boot", "/home", "/mnt/juegos", "/mnt/juegos2", "/nix/store"];

    [Theory]
    [InlineData("/mnt/juegos/Otteretro", "/mnt/juegos")]
    [InlineData("/mnt/juegos2/Otteretro", "/mnt/juegos2")]
    [InlineData("/mnt/juegos", "/mnt/juegos")]
    [InlineData("/home/player/Otteretro", "/home")]
    [InlineData("/opt/Otteretro", "/")]
    public void El_montaje_es_el_prefijo_mas_largo(string path, string mount) =>
        Assert.Equal(mount, Mounts.Of(path, Linux));

    [Theory]
    [InlineData("/", true)]
    [InlineData("/home", true)]
    [InlineData("/mnt/juegos", true)]
    [InlineData("/run/media/player/USB", true)]
    [InlineData("/boot", false)]
    [InlineData("/nix/store", false)]
    [InlineData("/homero", false)]
    public void Solo_se_ofrecen_los_discos_de_la_persona(string mount, bool offered) =>
        Assert.Equal(offered, Mounts.IsUserMount(mount));
}
