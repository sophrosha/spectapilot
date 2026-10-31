using System.IO;
using System.Text;

namespace WheelControl;

public sealed partial class Wheel : IDisposable
{
    // ======================== Oscillation diagnostics (log command) ========================

    // Start the CSV loop log (200 Hz): position, velocities, target, force -> osc_log.csv.
    public void StartLog(float seconds)
    {
        lock (_gate)
        {
            StopLogInternal();
            float sec = Math.Clamp(seconds <= 0f ? 10f : seconds, 1f, 120f);
            _logPath = Path.Combine(AppContext.BaseDirectory, "osc_log.csv");
            try
            {
                // No AutoFlush, but with a 64 KB buffer (~700 lines ≈ a flush every 3.5 s).
                // The default buffer (1 KB ≈ 11 lines) flushed ~18 times/s — every
                // disk flush is intercepted by the antivirus and could stall the loop for
                // tens of ms (those were the holes in the log of up to 218 ms).
                _logWriter = new StreamWriter(_logPath, false, new UTF8Encoding(false), 64 * 1024);
                _logWriter.WriteLine("ms,pos,velRaw,velSdk,velFilt,holdPos,smTarget,force,hold,lock,override,ramp");
                _logEndMs = _clock.ElapsedMilliseconds + (long)(sec * 1000f);
                Console.WriteLine($"Лог {sec:0.#} с → {_logPath}. Покачай руль руками в проблемном режиме — файл разберу я.");
            }
            catch (Exception ex)
            {
                _logWriter = null;
                Console.WriteLine($"! Лог не открылся: {ex.Message}");
            }
        }
    }

    // Stop log recording early.
    public void StopLog()
    {
        lock (_gate)
        {
            if (_logWriter is null) { Console.WriteLine("Лог сейчас не пишется."); return; }
            StopLogInternal();
            Console.WriteLine($"Лог остановлен → {_logPath}");
        }
    }

    // Close the log (inside _gate; no printing — also used from Dispose).
    private void StopLogInternal()
    {
        if (_logWriter is null) return;
        try { _logWriter.Dispose(); } catch { }
        _logWriter = null;
    }

    // One log line; called from Tick under _gate.
    private void WriteLog(float pos, float velFilt, float force)
    {
        var w = _logWriter;
        if (w is null) return;
        long now = _clock.ElapsedMilliseconds;
        if (now >= _logEndMs)
        {
            StopLogInternal();
            Console.WriteLine($"Лог завершён → {_logPath}");
            return;
        }
        w.WriteLine(FormattableString.Invariant(
            $"{now},{pos:0.###},{_logVelRaw:0.###},{_logVelSdk:0.###},{velFilt:0.###},{_holdPos:0.###},{_smoothing.Target:0.###},{force:0.#},{(_holdActive ? 1 : 0)},{(_lockTarget ? 1 : 0)},{(_consoleOverride ? 1 : 0)},{_holdRamp:0.###}"));
    }
}
