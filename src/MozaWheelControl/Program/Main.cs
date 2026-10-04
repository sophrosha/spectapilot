namespace MozaWheelControl;

// Step 1 skeleton. The console flow lives in sibling partials of Program:
// Args     — CLI parsing (--demo, --demo_time, --demo_amplitude);
// Init     — single instance, SetForegroundWindow(GetConsoleWindow()),
//            installMozaSDK() after the focus gate, socket wiring;
// FocusWait— console focus gate (DarkYellow prompts);
// DeviceWatch — fastboot-style PollDevice loop + 20 s hint;
// Log      — "[ 0.000 ] LEVEL : text" format, DEBUG only when debug: true.
internal static partial class Program
{
    private static void Main()
    {
        Console.WriteLine("MozaWheelControl (skeleton)");
    }
}
