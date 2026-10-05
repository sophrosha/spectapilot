using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WheelControl;

// Startup plumbing: single instance, native output mute, socket wiring, safe shutdown.
internal static partial class Program
{
    private static int _shutdownDone;

    // ======================== Native SDK output mute ========================
    // The MOZA SDK prints from native code (printf -> process stdout/stderr),
    // and stable's Connect() prints via Console.WriteLine. During
    // installMozaSDK() + connect() we swap STD_OUTPUT_HANDLE/STD_ERROR_HANDLE
    // to NUL and point Console at TextWriter.Null. Our own lines bypass both:
    // they go to the writer/handle captured at startup, so connection errors
    // and the orange hint stay visible. Debug mode never mutes.

    private const int StdOutputHandleId = -11;
    private const int StdErrorHandleId = -12;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;
    private const short AttributeDarkYellow = 6; // ConsoleColor.DarkYellow = RED|GREEN

    private static readonly object _muteLock = new();
    private static bool _muted;
    private static IntPtr _savedStdOut;
    private static IntPtr _savedStdErr;
    private static IntPtr _nulHandle;
    private static TextWriter? _savedOutWriter;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern bool SetStdHandle(int nStdHandle, IntPtr hNewHandle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll")]
    private static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput,
        out NativeScreenBufferInfo lpConsoleScreenBufferInfo);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleTextAttribute(IntPtr hConsoleOutput, short wAttributes);

    // Step 8 of the spec: the native DLLs are copied to Libraries\ — the loader
    // must search that folder (called from Main before the first SDK P/Invoke).
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectoryW(string lpPathName);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeCoord
    {
        public short X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSmallRect
    {
        public short Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeScreenBufferInfo
    {
        public NativeCoord Size;
        public NativeCoord CursorPosition;
        public short Attributes;
        public NativeSmallRect Window;
        public NativeCoord MaximumWindowSize;
    }

    // Silence native SDK printf + stable Console lines during SDK init/connect. No-op in debug mode.
    private static void MuteNativeOutput()
    {
        lock (_muteLock)
        {
            if (_debugEnabled || _muted) return; // debug keeps everything visible

            _savedStdOut = GetStdHandle(StdOutputHandleId);
            _savedStdErr = GetStdHandle(StdErrorHandleId);
            _nulHandle = CreateFileW("NUL", GenericWrite, FileShareReadWrite, IntPtr.Zero,
                OpenExisting, 0, IntPtr.Zero);
            if (_nulHandle == IntPtr.Zero || _nulHandle == new IntPtr(-1))
            {
                _nulHandle = IntPtr.Zero;
                return; // could not open NUL -> leave output untouched
            }

            SetStdHandle(StdOutputHandleId, _nulHandle);
            SetStdHandle(StdErrorHandleId, _nulHandle);

            // Console.WriteLine (stable's startup-angle line etc.) goes through
            // Console.Out — drop it too; our Log/PrintOrange bypass it on purpose.
            _savedOutWriter = Console.Out;
            Console.SetOut(TextWriter.Null);
            _muted = true;
        }
    }

    // Restore real stdout after a successful connect. Idempotent; safe to call from any thread.
    private static void RestoreNativeOutput()
    {
        lock (_muteLock)
        {
            if (!_muted) return;
            SetStdHandle(StdOutputHandleId, _savedStdOut);
            SetStdHandle(StdErrorHandleId, _savedStdErr);
            if (_savedOutWriter is not null) Console.SetOut(_savedOutWriter);
            _nulHandle = IntPtr.Zero; // kept open on purpose: see remark below
            _muted = false;
            // Remark: a CRT that already bound NUL keeps writing into it after the
            // restore — native noise stays gone for this session; debug runs never
            // mute at all. Managed Console.Out is fully restored above.
        }
    }

    // Close other running instances (two processes must not fight over one FFB
    // effect). CloseMainWindow is NOT used: console processes may share a window
    // — we would close someone else's window, so Kill right away.
    private static void CloseOtherInstances()
    {
        try
        {
            using var me = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcessesByName(me.ProcessName))
            {
                try
                {
                    if (p.Id == me.Id) continue; // this is us
                    p.Kill();
                    p.WaitForExit(2000);
                    LogInfo(Strings.T(Strings.Key.ClosedPrevious, p.ProcessName, p.Id));
                }
                catch { /* already gone / no rights — do not block startup */ }
                finally { p.Dispose(); }
            }
        }
        catch { /* diagnostics must not block startup */ }
    }

    // Start the UDP server (config socketHost:socketPort) and hand control to it.
    private static async Task StartSocketAsync()
    {
        if (_socket is { IsRunning: true }) return;

        var socket = new SocketServer(_cfg.SocketHost, _cfg.SocketPort, HandleSocketLineAsync);
        string? error = socket.Start();
        if (error is not null)
        {
            LogError(Strings.T(Strings.Key.SocketStartFailed, error));
            await socket.DisposeAsync();
            return;
        }

        _socket = socket;
        _wheel?.AllowSocketControl(); // fresh socket session — release console override
        LogInfo(Strings.T(Strings.Key.SocketListening, _cfg.SocketHost, _cfg.SocketPort));
    }

    // Idempotent: zero the torque and close everything. Called on Ctrl+C / window close / exit.
    private static void Shutdown()
    {
        if (Interlocked.Exchange(ref _shutdownDone, 1) != 0) return;
        try { _wheel?.Dispose(); } catch { /* zeroing torque comes first */ }
        try { _socket?.DisposeAsync().AsTask().Wait(1000); } catch { /* shutdown must never throw */ }
    }
}
