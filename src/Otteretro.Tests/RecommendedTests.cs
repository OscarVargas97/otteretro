using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>#366: el recomendado de un juego sale de recommendedFor, por gameId o, en recetas viejas, por nombre.</summary>
public class RecommendedTests
{
    static Recipe Fork(string id, RecipeGame game) =>
        new(id, id, "emulator", "Switch", "x.exe") { Consoles = ["switch"], RecommendedFor = [game] };

    static readonly (string Es, string? Original) Names = ("Tears of the Kingdom", "Zelda TotK");

    [Fact]
    public void PorGameIdOPorNombre()
    {
        var installed = new List<ProfileEmulator> { new("ryubing", "ryubing", "emulator", false, 1, "ryubing", ["1.0"]) };
        // Con gameId se cruza solo por él, aunque el nombre coincida.
        Assert.Null(Profiles.Recommended([Fork("citron", new RecipeGame("Tears of the Kingdom", 99))], 10, Names, "switch", installed));
        var byId = Profiles.Recommended([Fork("citron", new RecipeGame("TotK", 10, "Reddit"))], 10, Names, "switch", installed);
        Assert.Equal(("citron", "Reddit", false), (byId!.Id, byId.Source, byId.Installed));
        // Sin gameId (receta vieja): por el nombre, en español o el original.
        var byName = Profiles.Recommended([Fork("ryubing", new RecipeGame("zelda totk"))], 10, Names, "switch", installed);
        Assert.Equal(("ryubing", true), (byName!.Id, byName.Installed));
        // Otra consola no cuenta.
        Assert.Null(Profiles.Recommended([Fork("citron", new RecipeGame("TotK", 10))], 10, Names, "ps2", installed));
    }
}
