using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace MozaWheelControl;

public sealed partial class MozaWheel : IDisposable
{
    // ======================== SDK: инициализация и поиск ========================

    /// <summary>
    /// Загрузить SDK. Возвращает false, если нативные dll не найдены/не грузятся.
    /// После загрузки SDK нужно ~3 секунды на инициализацию (по официальному примеру).
    /// </summary>
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
            error = $"нативная dll не найдена ({ex.Message}). Нужны MOZA_API_C.dll и MOZA_SDK.dll (x64) рядом с exe.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Проверить, подключён ли руль (один опрос).
    /// Возвращает "MOZA_WHEELBASE" или null; в detail — результат ERRORCODE для диагностики.
    ///
    /// ВАЖНО: штатный getDeviceParent() в SDK 1.0.1.8 ПАДАЕТ с AccessViolationException
    /// (проверено отдельно, даже на .NET Framework) — поэтому используем getMotorLimitAngle:
    /// без руля он возвращает NODEVICES, с рульм — NORMAL.
    /// </summary>
    public static string? PollDevice(out string detail)
    {
        detail = "?";
        try
        {
            ERRORCODE err = ERRORCODE.NORMAL;
            var limits = getMotorLimitAngle(ref err);
            detail = err.ToString();
            // Санity-проверка: сразу после install SDK может отдать NORMAL с (0,0),
            // пока идёт сканирование устройств — такое не считаем найденным.
            // У живого базы limitAngle >= 90 (по документации SDK, диапазон 90..2700).
            if (err == ERRORCODE.NORMAL && limits is not null && limits.Item1 >= 90)
                return "MOZA_WHEELBASE"; // настоящее имя из SDK недоступно (см. TODO в context.md)
            return null;
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return null;
        }
    }

    // ======================== Подключение ========================

    /// <summary>
    /// Найти устройство (если ещё не известно) и подготовить FFB-эффект.
    /// В false + текст ошибки, если подключиться не удалось.
    /// </summary>
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

        // Найти базу, если ещё не найдена
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
            // ---- Лимиты мотора: используем для клампа ввода ----
            try
            {
                ERRORCODE lerr = ERRORCODE.NORMAL;
                var limits = getMotorLimitAngle(ref lerr);
                if (lerr == ERRORCODE.NORMAL && limits is not null)
                {
                    _motorLimits = limits;
                    // limitAngle = полный угол поворота базы → рабочий диапазон ±limit/2
                    int total = Math.Max(limits.Item1, 90);
                    _rangeHalf = Math.Clamp(total / 2f, 45f, 1800f);
                }
            }
            catch { /* лимиты не критичны, остаёмся на ±720 */ }

            // ---- HWND обязателен для FFB-эффектов ----
            _hwnd = GetOrCreateWindow();
            if (_hwnd == IntPtr.Zero)
            {
                // Без окна DirectInput отказывается (FFBERR: "window is null") — скажем об этом понятно.
                error = "не удалось получить окно (HWND) для FFB. Запусти программу в обычном окне терминала (cmd/PowerShell).";
                return false;
            }

            // ---- Эффект ConstantForce (по official примеру) ----
            // ВАЖНО (наблюдение на реальном железе): SDK отдаёт Access denied на
            // device::acquire, если руль в момент старта не в центре (0°).
            // Поэтому смотрим угол через getHidData и повторяем до центрирования.
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
                            c.setDuration(0xFFFFFFFF); // INFINITE (в примере стояло 0xffff — 65 с; нам нужно дольше)
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

            // Аппаратный предохранитель по конфигу (диапазон SDK: 50..100)
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
