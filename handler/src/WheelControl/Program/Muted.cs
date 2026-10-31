namespace WheelControl;

// Managed Console output swap for the call-site wrappers (demo hint, startup
// park, hands filter). Unrelated to the native SDK output mute in Init.cs:
// this only redirects Console.Out and restores it immediately after the call,
// exactly like the hand-written try/finally blocks those sites used before.
internal static partial class Program
{
    // Run the action with Console output replaced by the given writer,
    // restoring the previous writer afterwards even if the action throws.
    private static void WithOut(TextWriter writer, Action action)
    {
        TextWriter saved = Console.Out;
        Console.SetOut(writer);
        try { action(); }
        finally { Console.SetOut(saved); }
    }

    // Pure silence variant: stable's own prints (demo hint, SIM park lines)
    // are dropped for the duration of the call; our Log*/PrintOrange lines go
    // to the writer captured at startup and are unaffected.
    private static void Muted(Action action) => WithOut(TextWriter.Null, action);
}
