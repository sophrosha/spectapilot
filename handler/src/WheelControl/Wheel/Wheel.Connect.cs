using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace WheelControl;

public sealed partial class Wheel : IDisposable
{
    // ======================== SDK: init and device discovery ========================

    // Load the SDK. Returns false if the native dlls are missing/fail to load.
    // After loading, the SDK needs ~3 seconds to initialize (per the official example).
    public static bool InstallSdk(out string error)
    {
        error = "";
        try
        {
            installMozaSDK();
            Thread.Sleep(3000);
            _sdkInstalled = true;
            return true;
        }
        catch (DllNotFoundException ex)
        {
            error = $"нативная dll не найдена ({ex.Message}). Нужны MOZA_API_C.dll и MOZA_SDK.dll (x64) в подпапке Libraries рядом с exe.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // Check whether the wheel is connected (one poll).
    // Returns "MOZA_WHEELBASE" or null; detail carries the ERRORCODE result for diagnostics.
    //
    // IMPORTANT: the stock getDeviceParent() in SDK 1.0.1.8 CRASHES with AccessViolationException
    // (verified separately, even on .NET Framework) — so we use getMotorLimitAngle:
    // with no wheel it returns NODEVICES, with a wheel — NORMAL.
    public static string? PollDevice(out string detail)
    {
        detail = "?";
        try
        {
            ERRORCODE err = ERRORCODE.NORMAL;
            var limits = getMotorLimitAngle(ref err);
            detail = err.ToString();
            // Sanity check: right after install the SDK may return NORMAL with (0,0),
            // while devices are still being scanned — such a result is not counted as found.
            // A live base has limitAngle >= 90 (per SDK docs, the range is 90..2700).
            if (err == ERRORCODE.NORMAL && limits is not null && limits.Item1 >= 90)
                return "MOZA_WHEELBASE"; // the real SDK name is unavailable (see the TODO in context.md)
            return null;
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return null;
        }
    }

    // ======================== Connection ========================

    // Find the device (if not known yet) and prepare the FFB effect.
    // false + error text if the connection failed.
    public bool Connect(out string error) => Connect(10_000, out error);

    public bool Connect(int waitMs, out string error)
    {
        error = "";
        if (_disposed != 0) { error = "устройство уже закрыто"; return false; }

        if (_simulated)
        {
            _connected = true;
            return true;
        }

        if (!_sdkInstalled)
        {
            if (!InstallSdk(out error)) return false;
            _sdkInstalled = true;
        }

        // Find the base if it is not found yet
        if (string.IsNullOrEmpty(_deviceId) || _deviceId == "SIMULATED")
        {
            var deadline = Environment.TickCount64 + waitMs;
            string? found = null;
            while (Environment.TickCount64 < deadline)
            {
                found = PollDevice(out _);
                if (found is not null) break;
                Thread.Sleep(500);
            }
            if (found is null) { error = "устройство не найдено"; return false; }
            _deviceId = found;
        }

        try
        {
            // ---- Motor limits: used to clamp the input ----
            try
            {
                ERRORCODE lerr = ERRORCODE.NORMAL;
                var limits = getMotorLimitAngle(ref lerr);
                if (lerr == ERRORCODE.NORMAL && limits is not null)
                {
                    _motorLimits = limits;
                    // limitAngle = full turn angle of the base -> working range ±limit/2
                    int total = Math.Max(limits.Item1, 90);
                    _rangeHalf = Math.Clamp(total / 2f, 45f, 1800f);
                }
            }
            catch { /* limits are not critical, stay at ±720 */ }

            // ---- HWND is required for FFB effects ----
            _hwnd = GetOrCreateWindow();
            if (_hwnd == IntPtr.Zero)
            {
                // Without a window DirectInput refuses (FFBERR: "window is null") — report it clearly.
                error = "не удалось получить окно (HWND) для FFB. Запусти программу в обычном окне терминала (cmd/PowerShell).";
                return false;
            }

            // ---- ConstantForce effect (per the official example) ----
            // IMPORTANT (observed on real hardware): the SDK returns Access denied on
            // device::acquire if the wheel is not centered (0°) at start.
            // So we read the angle via getHidData and retry until it is centered.
            ETConstantForce? cf = null;
            string lastErr = "неизвестно";
            bool wasCentered = false;
            for (int attempt = 0; attempt < 6 && cf is null; attempt++)
            {
                float angle = ReadHidAngle();
                bool centered = float.IsNaN(angle) || MathF.Abs(angle) <= 1.5f;
                if (attempt == 0)
                    Console.WriteLine(float.IsNaN(angle)
                        ? "  Угол руля при старте: не читается (NaN)"
                        : $"  Угол руля при старте: {angle:0.#}°");
                if (!centered)
                    Console.WriteLine($"! Руль повёрнут на {angle:0.#}° — SDK стартует только с центра (0°). Поверни руль в центр, повторю сам...");
                else if (attempt > 0 && !wasCentered)
                    Console.WriteLine("  Центр на месте — пробую FFB-эффект...");
                wasCentered = centered;

                if (centered)
                {
                    try
                    {
                        ERRORCODE err = ERRORCODE.NORMAL;
                        var c = createWheelbaseETConstantForce(_hwnd, ref err);
                        if (c is not null && err == ERRORCODE.NORMAL)
                        {
                            c.setDuration(0xFFFFFFFF); // INFINITE (the example had 0xffff — 65 s; we need longer)
                            c.setMagnitude(0);
                            c.start();
                            cf = c;
                            break;
                        }
                        lastErr = $"create -> {err}";
                    }
                    catch (Exception ex)
                    {
                        lastErr = ex.Message;
                    }
                }
                Thread.Sleep(1500);
            }
            if (cf is null)
            {
                error = $"effect.start() -> {lastErr}. Проверь: руль в центре (0°) и запуск от имени администратора.";
                return false;
            }

            _cf = cf;
            _connected = true;

            // Hardware fuse from config (SDK range: 50..100)
            SetTorque(_cfg.MaxTorque);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public string GetDeviceId() => _deviceId;
}
