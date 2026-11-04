using System.Runtime.InteropServices;
using Otteretro.Data;
using Otteretro.Libraries;

namespace Otteretro.App;

/// <summary>
/// Con la app abierta, lo que copias entra al capturador si trae enlaces de un sitio con conector (#210, decisión 246). Lo demás ni se guarda ni se
/// registra, y el interruptor de Descargas lo apaga. Lo que ya estaba copiado al abrir la app no cuenta.
/// </summary>
/// <remarks>ponytail: los enlaces directos no entran, porque casi cualquier dirección copiada lo parecería; se pegan a mano. Linux llega en L.</remarks>
/// <param name="notify">Avisa cuántos enlaces reconoció y cuántos son nuevos.</param>
public sealed class ClipboardWatcher(Func<uint>? sequence = null, Func<(bool Read, string? Text)>? read = null, Action<int, int>? notify = null)
{
    const int MaxLength = 1_000_000;

    readonly Func<uint> sequence = sequence ?? (() => OperatingSystem.IsWindows() ? GetClipboardSequenceNumber() : 0);
    readonly Func<(bool Read, string? Text)> read = read ?? ReadText;
    uint? last;

    public void Tick(Database db, bool enabled)
    {
        var current = sequence();
        if (last is null || !enabled)
        {
            last = current;
            return;
        }
        if (current == last) return;
        var (done, text) = read();
        if (!done) return; // otro programa lo tiene abierto: la próxima vuelta
        last = current;
        if (text is not { Length: > 0 and <= MaxLength }) return;
        var (found, added) = DownloadLinks.Add(db, text, revive: false, only: site => site is not DirectSite);
        if (found > 0) notify?.Invoke(found, added);
    }

    /// <summary>El texto del portapapeles. False si no se pudo abrir; null si no hay texto.</summary>
    public static (bool Read, string? Text) ReadText()
    {
        if (!OperatingSystem.IsWindows()) return (true, null);
        if (!OpenClipboard(0)) return (false, null);
        try
        {
            var handle = GetClipboardData(UnicodeText);
            if (handle == 0) return (true, null);
            var locked = GlobalLock(handle);
            if (locked == 0) return (true, null);
            try { return (true, Marshal.PtrToStringUni(locked)); }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }

    const uint UnicodeText = 13;

    [DllImport("user32.dll")]
    static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool OpenClipboard(nint owner);

    [DllImport("user32.dll")]
    static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    static extern nint GetClipboardData(uint format);

    [DllImport("kernel32.dll")]
    static extern nint GlobalLock(nint handle);

    [DllImport("kernel32.dll")]
    static extern bool GlobalUnlock(nint handle);
}
