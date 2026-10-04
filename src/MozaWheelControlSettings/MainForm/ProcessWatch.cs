using System.Diagnostics;

namespace MozaWheelControlSettings;

// Step 3: reports whether MozaWheelControl is currently running (by process
// name) — the Save button uses it to pick the status message.
internal static class ProcessWatch
{
    public static bool IsConsoleRunning()
    {
        // GetProcessesByName expects the name without the .exe extension.
        Process[] procs = Process.GetProcessesByName("MozaWheelControl");
        try
        {
            return procs.Length > 0;
        }
        finally
        {
            foreach (Process p in procs) p.Dispose();
        }
    }
}
