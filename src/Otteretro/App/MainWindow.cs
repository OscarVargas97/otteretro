using Photino.NET;

namespace Otteretro.App;

/// <summary>La ventana de la app: Photino (WebView2 en Windows, WebKitGTK en Linux) cargando la interfaz local.</summary>
public static class MainWindow
{
    /// <summary>Muestra la ventana y bloquea hasta que se cierra.</summary>
    public static void Show(string url, SingleInstance instance)
    {
        var window = new PhotinoWindow()
            .SetTitle("Otteretro")
            .SetUseOsDefaultSize(false)
            .SetSize(1440, 900)
            .SetMinSize(1100, 700)
            .Center()
            // GTK no lee el .ico comprimido: en Linux va el PNG de la interfaz.
            .SetIconFile(Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "nutria.ico" : "wwwroot/nutria.png"))
            .Load(new Uri(url));

        instance.Activated += () => window.Invoke(() =>
        {
            window.SetMinimized(false);
            window.SetTopMost(true);
            window.SetTopMost(false);
        });

        window.WaitForClose();
    }
}
