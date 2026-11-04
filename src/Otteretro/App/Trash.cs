using System.Runtime.InteropServices;
using Otteretro.Api;

namespace Otteretro.App;

/// <summary>
/// La papelera del sistema (#224). Solo en discos locales fijos: en un disco de red o en una memoria extraíble Windows no tiene
/// papelera y «borrar» sería borrar para siempre, así que ahí se niega (P7).
/// </summary>
public static partial class Trash
{
    /// <summary>Para las pruebas: reemplaza a la papelera de verdad. Devuelve si pudo.</summary>
    public static Func<string, bool>? Fake { get; set; }

    public static ApiError? Send(string path)
    {
        if (Fake is not null) return Fake(path) ? null : ApiError.Of("trash.failed", ("path", path));
        // ponytail: en Linux (gio trash) llega con L, como el resto de PlayOS.
        if (!OperatingSystem.IsWindows()) return ApiError.Of("trash.unsupported");
        if (Libraries.Disk.IsNetwork(path) || new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).DriveType != DriveType.Fixed)
            return ApiError.Of("trash.unavailable", ("path", path));
        var op = new FileOperation
        {
            Func = FoDelete,
            From = Path.GetFullPath(path) + "\0\0",
            Flags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi,
        };
        return SHFileOperation(ref op) == 0 && !op.AnyOperationsAborted && !Path.Exists(path) ? null : ApiError.Of("trash.failed", ("path", path));
    }

    const uint FoDelete = 3;
    const ushort FofSilent = 0x4, FofNoConfirmation = 0x10, FofAllowUndo = 0x40, FofNoErrorUi = 0x400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct FileOperation
    {
        public IntPtr Window;
        public uint Func;
        [MarshalAs(UnmanagedType.LPWStr)] public string From;
        [MarshalAs(UnmanagedType.LPWStr)] public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)] public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    static extern int SHFileOperation(ref FileOperation op);
}
