using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace MozaWheelControl;

public sealed partial class MozaWheel : IDisposable
{
    // ======================== Диагностика раскачки (команда log) ========================

    /// <summary>Начать CSV-лог петли (200 Гц): позиция, скорости, цель, сила → osc_log.csv.</summary>
    public void StartLog(float seconds)
    {
        lock (_gate)
        {
            StopLogInternal();
            float sec = Math.Clamp(seconds <= 0f ? 10f : seconds, 1f, 120f);
            _logPath = Path.Combine(AppContext.BaseDirectory, "osc_log.csv");
            try
            {
                // Без AutoFlush, но с буфером 64 КБ (~700 строк ≈ сброс раз в 3.5 с).
                // Буфер по умолчанию (1 КБ ≈ 11 строк) сбрасывался ~18 раз/с — каждый
                // сброс на диск перехватывается антивирусом и мог подвесить петлю на
                // десятки мс (это и были дыры в логе до 218 мс).
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

    /// <summary>Остановить запись лога досрочно.</summary>
    public void StopLog()
    {
        lock (_gate)
        {
            if (_logWriter is null) { Console.WriteLine("Лог сейчас не пишется."); return; }
            StopLogInternal();
            Console.WriteLine($"Лог остановлен → {_logPath}");
        }
    }

    /// <summary>Закрыть лог (внутри _gate; без печати — используется и из Dispose).</summary>
    private void StopLogInternal()
    {
        if (_logWriter is null) return;
        try { _logWriter.Dispose(); } catch { }
        _logWriter = null;
    }

    /// <summary>Строка лога; вызывается из Tick под _gate.</summary>
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
