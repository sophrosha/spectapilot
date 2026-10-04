using System.Globalization;

namespace MozaWheelControl;

// CLI options: --demo=enabled, --demo_time=100, --demo_amplitude=100.
// A missing argument falls back to the default value.
internal readonly record struct CliOptions(bool DemoEnabled, float DemoTime, float DemoAmplitude);

internal static partial class Program
{
    private static CliOptions ParseArgs(string[] args)
    {
        const string demo = "--demo=";
        const string time = "--demo_time=";
        const string amp = "--demo_amplitude=";

        bool enabled = false;
        float demoTime = 100f;
        float demoAmp = 100f;

        foreach (string raw in args)
        {
            string a = raw.Trim();
            if (a.StartsWith(demo, StringComparison.OrdinalIgnoreCase))
                enabled = a[demo.Length..].Equals("enabled", StringComparison.OrdinalIgnoreCase);
            else if (a.StartsWith(time, StringComparison.OrdinalIgnoreCase) &&
                     float.TryParse(a[time.Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out float t))
                demoTime = t;
            else if (a.StartsWith(amp, StringComparison.OrdinalIgnoreCase) &&
                     float.TryParse(a[amp.Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out float d))
                demoAmp = d;
        }

        return new CliOptions(enabled, demoTime, demoAmp);
    }
}
