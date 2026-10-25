namespace Otteretro.App;

/// <summary>
/// Discos según el sistema: en Windows, la unidad (C:\); en Linux, el punto de montaje
/// donde cae la ruta (/, /home, /mnt/juegos…). Path.GetPathRoot en Linux siempre da "/".
/// </summary>
public static class Mounts
{
    /// <summary>El disco que contiene la ruta (exista o no todavía).</summary>
    public static string Of(string path) => OperatingSystem.IsWindows()
        ? Path.GetPathRoot(Path.GetFullPath(path))!
        : Of(Path.GetFullPath(path), DriveInfo.GetDrives().Select(d => d.Name));

    /// <summary>El montaje más largo que es prefijo de la ruta (absoluta, con /).</summary>
    public static string Of(string full, IEnumerable<string> mounts) =>
        mounts.Where(m => full == m || full.StartsWith(m.TrimEnd('/') + "/", StringComparison.Ordinal))
            .MaxBy(m => m.Length) ?? "/";

    public static bool Same(string a, string b) =>
        string.Equals(Of(a), Of(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// Los discos que se ofrecen para guardar datos. En Linux, solo la raíz y donde se montan
    /// discos de la persona: fuera quedan /boot, /nix/store y los montajes del sistema.
    /// </summary>
    // ponytail: lista fija de prefijos; si aparece otro lugar habitual de montaje, se suma acá.
    public static bool IsUserMount(string mount) => mount == "/" ||
        new[] { "/home/", "/mnt/", "/media/", "/run/media/" }.Any(p => mount == p.TrimEnd('/') || mount.StartsWith(p, StringComparison.Ordinal));
}
