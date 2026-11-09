using System.Text.Json.Nodes;
using Otteretro.Libraries;

namespace Otteretro.Tests;

/// <summary>Una receta local y la publicada con el mismo id (#357, decisión 351).</summary>
public class RecipeUnifyTests
{
    static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;

    [Fact]
    public void KeepsWhatEachSideAddsAndThePublishedWinsAClash()
    {
        var published = Obj("""{"id":"emu","name":"Emu","kind":"emulator","folder":"Switch","detect":"emu.exe","updates":{"source":"github","repo":"a/emu","dev":true}}""");

        // Sin choque: lo que solo trae la local se suma, también dentro de un objeto.
        var (merged, conflict) = Recipes.Unify(published, Obj("""{"id":"emu","saveAs":"emu-v{version}.exe","updates":{"source":"github","devPattern":"-rc"}}"""));
        Assert.False(conflict);
        Assert.Equal("emu-v{version}.exe", (string?)merged["saveAs"]);
        Assert.Equal(("a/emu", "-rc"), ((string?)merged["updates"]!["repo"], (string?)merged["updates"]!["devPattern"]));

        // Choque en un campo: gana la publicada, y la local queda respaldada tal cual; el archivo pasa a ser la unión.
        using var tmp = new TempDir();
        var file = tmp["emu.json"];
        const string local = """{"id":"emu","detect":"otro.exe","link":"x"}""";
        File.WriteAllText(file, local);
        (merged, conflict) = Recipes.Unify(published, Obj(local));
        Assert.True(conflict);
        Assert.Equal(("emu.exe", "x"), ((string?)merged["detect"], (string?)merged["link"]));
        var backup = Recipes.Backup(file, merged);
        Assert.Equal(Path.Combine(tmp.Path, "_respaldos"), Path.GetDirectoryName(backup));
        Assert.EndsWith("-emu.json", backup);
        Assert.Equal(local, File.ReadAllText(backup));
        Assert.False(Recipes.Unify(published, Obj(File.ReadAllText(file))).Conflict); // la próxima vez ya no choca
    }
}
