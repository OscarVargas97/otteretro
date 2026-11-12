using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Otteretro.Play;

/// <summary>
/// Lanzar y esperar en Windows (T7): el proceso arranca suspendido dentro de un Job Object, así lo que abra (un lanzador que abre el
/// juego y se cierra) queda en el mismo job. Se espera al lanzado con `WaitForSingleObject` y después a que el job quede vacío: se
/// cuentan sus procesos con cada aviso del job y, como red de seguridad, cada <see cref="Poll"/> (Microsoft no garantiza el aviso
/// `ACTIVE_PROCESS_ZERO`).
/// </summary>
public static class WindowsGame
{
    public static TimeSpan Poll { get; set; } = TimeSpan.FromSeconds(60);

    [SupportedOSPlatform("windows")]
    public static void Run(string exe, IReadOnlyList<string> args, string folder)
    {
        var job = CreateJobObjectW(0, JobName(Environment.ProcessId));
        var port = CreateIoCompletionPort(-1, 0, 0, 1);
        try
        {
            if (job == 0 || port == 0) throw new Win32Exception();
            var link = new AssociateCompletionPort { CompletionPort = port };
            SetInformationJobObject(job, 7 /* JobObjectAssociateCompletionPortInformation */, ref link, Marshal.SizeOf<AssociateCompletionPort>());
            var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>() };
            const int Suspended = 0x4, UnicodeEnvironment = 0x400;
            if (!CreateProcessW(exe, new StringBuilder(CommandLine(exe, args)), 0, 0, false, Suspended | UnicodeEnvironment, 0, folder, ref startup, out var process))
                throw new Win32Exception();
            try
            {
                // Si no se puede (un job padre que no lo deja), solo se espera al lanzado: el job queda vacío.
                AssignProcessToJobObject(job, process.Process);
                ResumeThread(process.Thread);
                WaitForSingleObject(process.Process, uint.MaxValue);
            }
            finally
            {
                CloseHandle(process.Thread);
                CloseHandle(process.Process);
            }
            while (ActiveProcesses(job) > 0)
                GetQueuedCompletionStatus(port, out _, out _, out _, (uint)Poll.TotalMilliseconds);
        }
        finally
        {
            if (port != 0) CloseHandle(port);
            if (job != 0) CloseHandle(job);
        }
    }

    /// <summary>El nombre del job de un `otteretro jugar` (#454): con él, «Detener» lo encuentra desde la app.</summary>
    static string JobName(int waiter) => $@"Local\Otteretro-jugar-{waiter}";

    /// <summary>
    /// «Detener» (#454): termina el job del `otteretro jugar` <paramref name="waiter"/>, el juego y lo que abrió. Ese proceso ve el job
    /// vacío y cierra la sesión como al salir. False si ya no está.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static bool Stop(int waiter)
    {
        var job = OpenJobObjectW(0x8 /* JOB_OBJECT_TERMINATE */, false, JobName(waiter));
        if (job == 0) return false;
        try { return TerminateJobObject(job, 1); }
        finally { CloseHandle(job); }
    }

    /// <summary>Cuánto se espera a que arranque un juego abierto por Steam (T7: solo los primeros segundos).</summary>
    public static TimeSpan SteamStart { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Un juego de Steam (T7, decisión 386): Steam anota en `RunningAppID` del registro el juego que corre, sea cual sea su ejecutable
    /// (lanzadores y anticheat incluidos). Si es <paramref name="app"/> en los primeros segundos, devuelve la espera a que deje de
    /// serlo (cada 5 s); si no, null.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static Action? FindSteam(string app)
    {
        bool Running() => Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "RunningAppID", null)?.ToString() == app;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!Running())
        {
            if (clock.Elapsed > SteamStart) return null;
            Thread.Sleep(1000);
        }
        return () => { while (Running()) Thread.Sleep(5000); };
    }

    /// <summary>La ventana pequeña de un error al jugar: un cuadro de mensaje del sistema.</summary>
    [SupportedOSPlatform("windows")]
    public static void ShowError(string text) => MessageBoxW(0, text, "Otteretro", 0x10 /* MB_ICONERROR */);

    static uint ActiveProcesses(nint job) =>
        QueryInformationJobObject(job, 1 /* JobObjectBasicAccountingInformation */, out var info, Marshal.SizeOf<BasicAccounting>(), 0) ? info.ActiveProcesses : 0;

    internal static string CommandLine(string exe, IEnumerable<string> args) => string.Join(' ', args.Prepend(exe).Select(Quote));

    /// <summary>Un argumento como lo lee `CommandLineToArgvW`: comillas si hace falta y las barras antes de una comilla, dobladas.</summary>
    public static string Quote(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '"']) < 0) return arg;
        var quoted = new StringBuilder("\"");
        var slashes = 0;
        foreach (var ch in arg)
        {
            if (ch == '\\') { slashes++; continue; }
            quoted.Append('\\', ch == '"' ? slashes * 2 + 1 : slashes).Append(ch);
            slashes = 0;
        }
        return quoted.Append('\\', slashes * 2).Append('"').ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    struct StartupInfo
    {
        public int cb;
        public nint Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public short ShowWindow, Reserved2Size;
        public nint Reserved2, StdInput, StdOutput, StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ProcessInformation
    {
        public nint Process, Thread;
        public int ProcessId, ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct AssociateCompletionPort
    {
        public nint CompletionKey, CompletionPort;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BasicAccounting
    {
        public long TotalUserTime, TotalKernelTime, ThisPeriodTotalUserTime, ThisPeriodTotalKernelTime;
        public uint TotalPageFaultCount, TotalProcesses, ActiveProcesses, TotalTerminatedProcesses;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern nint CreateJobObjectW(nint attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern nint OpenJobObjectW(uint access, bool inherit, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool TerminateJobObject(nint job, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern nint CreateIoCompletionPort(nint file, nint existing, nint key, uint threads);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetInformationJobObject(nint job, int infoClass, ref AssociateCompletionPort info, int length);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool QueryInformationJobObject(nint job, int infoClass, out BasicAccounting info, int length, nint returned);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcessW(string application, StringBuilder commandLine, nint processAttributes, nint threadAttributes, bool inheritHandles,
        int flags, nint environment, string directory, ref StartupInfo startup, out ProcessInformation info);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AssignProcessToJobObject(nint job, nint process);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint ResumeThread(nint thread);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint WaitForSingleObject(nint handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetQueuedCompletionStatus(nint port, out uint bytes, out nint key, out nint overlapped, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(nint handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(nint window, string text, string caption, uint type);
}
