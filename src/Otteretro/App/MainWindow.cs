using Photino.NET;

namespace Otteretro.Inicio;

/// <summary>La ventana de la app: Photino (WebView2 en Windows, WebKitGTK en Linux) cargando la interfaz local.</summary>
public static class Ventana
{
    /// <summary>Muestra la ventana y bloquea hasta que se cierra.</summary>
    public static void Mostrar(string url, UnaInstancia instancia)
    {
        var ventana = new PhotinoWindow()
            .SetTitle("Otteretro")
            .SetUseOsDefaultSize(false)
            .SetSize(1440, 900)
            .SetMinSize(1100, 700)
            .Center()
            .SetIconFile(Path.Combine(AppContext.BaseDirectory, "nutria.ico"))
            .Load(new Uri(url));

        instancia.Activar += () => ventana.Invoke(() =>
        {
            ventana.SetMinimized(false);
            ventana.SetTopMost(true);
            ventana.SetTopMost(false);
        });

        ventana.WaitForClose();
    }
}
