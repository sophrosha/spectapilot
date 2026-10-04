using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace MozaWheelControl;

/// <summary>
/// Обёртка над Moza SDK + фоновая петля управления.
///
/// Управление рулём (по мотивам официального примера sdk_api_test.cc::moveTo):
///  - создаётся эффект ConstantForce (нужен HWND — берём консольное окно);
///  - цикл 200 Гц (5 мс, как DT в примере) читает угол через getHIDData и
///    выставляет setMagnitude: наружу подаётся сглаженная цель, ошибка позиции
///    прогоняется через петлю «позиция → скорость → момент»;
///  - hold — пружина-демпфер на том же эффекте: force = Kp·(target−pos) − Kd·vel.
///
/// Режим симуляции: SDK не трогается вообще, позиция интегрируется, команды пишутся как SIM: ...
/// </summary>
public sealed class MozaWheel : IDisposable
{
    // ---- Параметры петли движения (взяты из официального примера SDK) ----
    private const float SpdKp = 5f;              // скорость: пропорциональ (пример: 5)
    private const float SpdKi = 40f;             // скорость: интеграл (пример: 40)
    private const float DegPerSecToRpm = 60f / 360f;
    private const float FastMovePosKp = 4.0f;    // позиция: жёсткое слежение при smooth=false

    // ---- Параметры hold (пружина-демпфер) ----
    private const float HoldSpringFullDeg = 8f;       // ошибка (°), при которой раскрывается полная сила: меньше = жёстче (пользователю: «усиление не сильное»)
    private const float HoldDampFullDegPerSec = 180f; // скорость, при которой раскрывается полный демпфер: меньше = демпфер сильнее (гасит поддёргивания на змейке и при резком рывке). 150→180 (лог 18:51): остаточные подёргивания = пик волны ~15 Гц (внешний возмутитель в цепи Pit House, наша эффективность на ней ~56% из-за задержки velSdk 5 мс + EMA 5 мс); шаги силы на пиках ∝ kd — минус 17% на них и на гуле, амплитуда физ. волны +~10% (0.18°→0.21° — неощутимо)

    // ---- Гладкость и упор ----
    // Мёртвая зона с гистерезисом (антидребезг): плоский порог 5°/с щёлкал
    // вкл/выкл демпфера 3.3 раза/с на МАЛЫХ движениях против 1.1/с на больших
    // (лог езды 19:54: «большие повороты плавно, малые — шестерня») — сила на
    // границе туда-сюда дёргала руль как передача. Включаем >7°/с, отпускаем <3°/с:
    // на покое шум прошивки <5°/с порог не достигает (velFilt в тихом окне = 0),
    // на волне ±18°/с демпфирование не меняется.
    private const float VelEngageDegPerSec = 7f;     // включение демпфера, °/с
    private const float VelReleaseDegPerSec = 3f;    // отпускание демпфера, °/с
    private const float HandFlickerPhysDeg = 1.5f;   // подавление миганий целых градусов A↔B, ° физ
    private const float ProgressiveBoostDeg = 30f;    // отклонение (°), за которым сила плавно поднимается к 100% (progressiveHold)
    private const float NudgeBlendDeg = 3f;           // зона у упора для смешивания пружины с мягкими толчками
    private const float NudgeHz = 1.2f;               // частота мягких толчков у упора
    private const float LockApproachDeg = 2f;         // зона подхода к упору в move-петле (плавно, без подпрыгивания)

    // ---- Защита ----
    // Порог «что-то совсем не так»: ошибка > 1.5 полного хода. Только предупреждение —
    // силу НЕ обнуляем (раньше обнуляла при 60°, и дальние ходы вроде +100 не доезжали).
    private const float PosWarnFactor = 1.5f;

    /// <summary>Требуется HWND для создания FFB-эффектов (в консольном приложении — окно консоли).</summary>
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
        IntPtr hInstance, IntPtr lpParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uPeriod);

    private static IntPtr _fallbackHwnd;

    /// <summary>
    /// Окно для DirectInput: окно консоли, а если его нет (скрытый/фоновый запуск) —
    /// создаём собственное невидимое окно. Нативный SDK валидирует hwnd и при null
    /// аккуратно отдаёт FFBERR (проверено), но лучше дать ему рабочее окно.
    /// </summary>
    private static IntPtr GetOrCreateWindow()
    {
        IntPtr h = GetConsoleWindow();
        if (h != IntPtr.Zero) return h;
        h = Process.GetCurrentProcess().MainWindowHandle;
        if (h != IntPtr.Zero) return h;
        if (_fallbackHwnd != IntPtr.Zero) return _fallbackHwnd;

        try
        {
            // Класс STATIC регистрируется системой — окно скрытое, нужно только для привязки эффектов.
            _fallbackHwnd = CreateWindowExW(0, "STATIC", "MozaWheelControl", 0,
                0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);
        }
        catch
        {
            _fallbackHwnd = IntPtr.Zero;
        }
        return _fallbackHwnd;
    }

    private readonly Config _cfg;
    private readonly bool _simulated;
    private readonly Smoothing _smoothing;
    private readonly object _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private string _deviceId = "SIMULATED";
    private IntPtr _hwnd;
    private ETConstantForce? _cf;
    private static bool _sdkInstalled; // install/remove — процессные, флаг общий для всех экземпляров
    private bool _connected;
    private int _disposed;

    // Управление (потокобезопасно под _gate)
    private volatile bool _holdActive;
    private float _holdPos;           // сглаженная «пружина»: EMA к _holdTarget — без рывков на пакетах
    private float _holdTarget;        // заданная цель hold — сюда пишут все команды
    private float _holdForcePct;
    private float _holdRamp;          // 0..1 — разгон силы при включении hold
    private volatile bool _consoleOverride; // консоль взяла управление — сокет не перехватывает цель
    private long _lastSkipPrint;      // троттлинг сообщения «сокет ждёт»
    private StreamWriter? _logWriter; // CSV-лог петли (команда log) — диагностика раскачки
    private string _logPath = "";
    private long _logEndMs;
    private float _logVelRaw;         // скорость ДО фильтра — в CSV-лог (диагностика риппля)
    private float _logVelSdk;         // сырая скорость из SDK — в CSV-лог (проверка единиц)
    private float _sockMaxRaw;        // максимум |числа| из игры за сессию (до клампа) — шкала

    // Лесенка целых градусов из игры (мод шлёт round(angle) → шаг 0.5° физ):
    // по сглаженному наклону входа «опережаем» EMA цели (lead) — ступеньки глушатся,
    // но руль идёт СИНХРОННО с игрой (просьба: «плавно и синхронно, но не сильно»).
    private float _tgtVel;                     // наклон цели из сокета, °/с физ (EMA по пакетам)
    private long _lastPktMs;                   // последний пакет из игры (_clock; 0 = ещё не было)
    private const float LeadMaxDeg = 2.5f;     // максимум «крена»: пакеты пропали — увод ограничен
    private const float FastSlopeDegPerSec = 60f; // |v|≥60 °/с → обычная τ (резкие движения)
    private bool _lockTarget;         // цель стоит на упоре (кламп ±полному ходу)
    private float _lockDir = 1f;      // направление упора (+1 / −1)
    private float _torquePct = 100f;
    private float _rangeHalf = 90f;   // физические ±° (R3: 180° sweep = ±90; сим = та же база)
    private Tuple<int, int>? _motorLimits;

    // Датчики
    private float _lastPos;           // последний прочитанный угол (°)
    private bool _hasPos;
    private long _lastPosMs;          // когда последний раз приходил угол (диагностика «не читает угол»)
    private float _velFilt;           // отфильтрованная скорость (EMA) — шум не должен дёргать демпфер
    private bool _velEngaged;         // гистерезис мёртвой зоны: демпфер «залипает» вкл/выкл (см. VelEngage)
    private float _pendingVal;        // пакет-кандидат (подавление миганий A↔B, см. HandsTo)
    private bool _pendingValid;
    private float _simPos;            // позиция в симуляции
    // Окно скорости: позиция HID приходит квантованными шагами, мгновенная
    // конечная разность даёт пик на КАЖДЫЙ шаг → демпфер пульсирует («шестерня»).
    // Берём разность по скользящему окну ~40 мс — шаги гаснут в 8×.
    private const int VelRingLen = 8;              // 8 × ~5 мс ≈ 40 мс
    private readonly float[] _velRingPos = new float[VelRingLen];
    private readonly long[] _velRingMs = new long[VelRingLen];
    private int _velRingI;
    private bool _hasVelRing;
    private long _lastTickMs;                // такт до текущего — ловим дыру после подвисания
    private float _gapT = 1f;                // 0 после дыры → 1 за GapRampSec (дозахват силы)
    private float _lastApplied;              // сила, реально отданная на колесо (для дозахвата)
    private const float GapRampSec = 0.08f;  // плавный набор силы после дыры ~80 мс
    private const int GapMs = 30;            // такт > 30 мс = дыра (норма тика 5 мс)
    private float _posInteg;          // интеграл скоростной петли

    // Обратная связь пользователю
    private bool _announcedReach;
    private bool _targetSet;          // задавалось ли движение (чтобы не писать «достигнуто» на старте)
    private long _lastQuietPrint;     // троттлинг вывода для потока из сокета (30 Гц)
    private long _lastWarnTick;
    private bool _effectBroken;

    // Цикл управления
    private CancellationTokenSource? _cts;
    private Task? _loop;

    // ---- Две шкалы ----
    // КОНСОЛЬ = физические градусы (требование пользователя: «hold N = ровно N физ.°
    // при ЛЮБОЙ настройке Pit House»). ИГРА/СОКЕТ = своя шкала: ±_userFullLock =
    // полный руль (userFullLockDeg в config.json, 90 = 1:1 с игрой по умолчанию).
    // Пересчёт сокета: физ = игровые × _rangeHalf / _userFullLock.
    private float _userFullLock = 90f;
    private float U2P(float user) => user * (_rangeHalf / _userFullLock);
    private float P2U(float phys) => phys * (_userFullLock / Math.Max(_rangeHalf, 1f));

    // ---- Публичное состояние ----

    public bool IsSimulated => _simulated;
    public bool IsConnected => _connected;
    public bool IsHoldActive => _holdActive;
    /// <summary>Текущая цель в ФИЗИЧЕСКИХ градусах (консольная шкала).</summary>
    public float CurrentTarget => _smoothing.Target;
    /// <summary>Полный ход до упора, физические ±° (шкала консоли: углы/hold/status).</summary>
    public float MoveRangeHalf => _rangeHalf;
    /// <summary>Полный руль в шкале ИГРЫ, ±° (userFullLockDeg) — масштаб чисел из сокета.</summary>
    public float SocketRangeHalf => _userFullLock;
    /// <summary>
    /// Максимум |числа| из игры за сессию (до клампа) — для настройки userFullLockDeg:
    /// если больше SocketRangeHalf — руль зажат у упора и ввод обрезается (см. status).
    /// </summary>
    public float SocketMaxSeen { get { lock (_gate) { return _sockMaxRaw; } } }
    /// <summary>Перевод из игровой шкалы в физические градусы (для «hold» из сокета).</summary>
    public float SocketToPhysical(float user) => U2P(user);
    public Tuple<int, int>? MotorLimits => _motorLimits;
    public string? LastWarning { get; private set; }

    public MozaWheel(Config cfg, bool simulate, string deviceId = "SIMULATED")
    {
        _cfg = cfg;
        _simulated = simulate;
        _deviceId = deviceId;
        _userFullLock = Math.Clamp(cfg.UserFullLockDeg, 10f, 720f);
        _smoothing = new Smoothing(cfg, 0f);
    }

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

    // ======================== Управление (API из ТЗ) ========================

    /// <summary>Целевая позиция: руль пойдёт туда плавно (smooth) или резко, но с ограничениями.</summary>
    /// <param name="degrees">Угол в ПОЛЬЗОВАТЕЛЬСКИХ градусах (0 = центр, ±userFullLockDeg = полный ход).</param>
    /// <param name="quiet">true — приглушить вывод (поток из сокета приходит 30 раз в секунду).</param>
    public void SetPosition(float degrees, bool quiet = false)
    {
        // Консоль (quiet=false) — физические градусы; сокет (quiet=true) — игровая шкала.
        float clamped;               // цель в физических градусах
        float shown;                  // то, что напечатаем — в единицах своего ввода
        bool atLock;
        if (quiet)
        {
            float userClamped = Math.Clamp(degrees, -_userFullLock, _userFullLock);
            clamped = U2P(userClamped);
            shown = userClamped;
            atLock = MathF.Abs(userClamped) >= _userFullLock - 0.5f;
        }
        else
        {
            clamped = Math.Clamp(degrees, -_rangeHalf, _rangeHalf);
            shown = clamped;
            atLock = MathF.Abs(clamped) >= _rangeHalf - 0.5f;
        }
        lock (_gate)
        {
            // Команда из консоли — сокет больше не перехватывает цель (до release / socket).
            if (!quiet) _consoleOverride = true;

            // Диагностика шкалы (см. HandsTo): максимум |числа| из игры за сессию.
            else if (MathF.Abs(degrees) > _sockMaxRaw) _sockMaxRaw = MathF.Abs(degrees);

            // Идея пользователя: повтор одного и того же значения (стрим «90 90 90»)
            // отсекаем — цель уже стоит, руль и так её ведёт/держит; ждём другое число.
            if (quiet && !_holdActive &&
                MathF.Abs(clamped - _smoothing.Target) < 1e-3f)
                return;

            // Возврат без «прыжка в старую сторону»: если сглаженная точка осталась
            // не на той стороне руля, что и новая цель (или цель = руль) — перепрыгиваем
            // на текущий угол. Иначе после «0» руль сначала уезжал бы вверх к старой цели.
            float pos = _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);
            if (!float.IsNaN(pos) &&
                (_smoothing.Current - pos) * (clamped - pos) <= 0f &&
                MathF.Abs(_smoothing.Current - pos) > 0.01f)
                _smoothing.SnapTo(pos);

            _smoothing.SetTarget(clamped);
            // Цель на упоре? У упора будут мягкие толчки-индикатор (lockNudge).
            _lockTarget = atLock;
            _lockDir = clamped >= 0f ? 1f : -1f;
            // Консоль: привычное сглаживание. Сокет: почти мгновенно (задержка от мода ~0).
            _smoothing.SetTauMs(quiet ? _cfg.SocketSmoothTimeMs : (float?)_cfg.SmoothTimeMs);
            if (_holdActive) _holdTarget = clamped; // hold «переезжает» к новой цели: +10/-10 работают и в hold
            _announcedReach = false;
            _targetSet = true;
            _posInteg = 0;

            if (quiet)
            {
                if (!_simulated)
                    return; // железо из сокета: молчим — Console под _gate дорог (~5-20 мс
                            // рендера) и сбивает тик 200 Гц, юзер чувствовал рывком
                // Сим печатает не чаще раза в секунду — по этим строкам идут смоуки.
                long now = Environment.TickCount64;
                if (now - _lastQuietPrint < 1000) return;
                _lastQuietPrint = now;
            }

            string posNow = _hasPos ? _lastPos.ToString("0.#") : "?";
            if (_simulated)
                Console.WriteLine($"SIM: SetPosition({shown:0.#}°) smooth={_cfg.Smooth} " +
                                  $"smoothTimeMs={(quiet ? _cfg.SocketSmoothTimeMs : _cfg.SmoothTimeMs)}");
            else
                Console.WriteLine($"-> цель {clamped:0.#}° (сейчас {posNow}°)");
        }
    }

    /// <summary>
    /// Ограничение момента, % (0 = полное обнуление; 50..100 = аппаратный лимит setMotorPeakTorque).
    /// </summary>
    public void SetTorque(float torque)
    {
        lock (_gate)
        {
            float pct = Math.Clamp(torque, 0f, 100f);
            _torquePct = pct;

            if (_simulated)
            {
                Console.WriteLine($"SIM: SetTorque({pct:0.#}%)");
                return;
            }
            if (!_connected || _cf is null) return;

            if (pct <= 0f)
            {
                try { _cf.setMagnitude(0); } catch { /* эффект мог быть уже остановлен */ }
                return;
            }

            try { setMotorPeakTorque((int)Math.Clamp(pct, 50f, 100f)); }
            catch { /* не критично */ }
        }
    }

    /// <summary>Текущий угол руля в ФИЗИЧЕСКИХ градусах (шкала консоли); NaN, если данных ещё не было.</summary>
    public float GetPosition()
    {
        lock (_gate)
        {
            if (_simulated) return _simPos;
            return _hasPos ? _lastPos : float.NaN;
        }
    }

    /// <summary>Позиция для относительных команд (физ. °): реальный угол, а если данных нет — текущая цель.</summary>
    public float GetReferencePosition()
    {
        float p = GetPosition();
        return float.IsNaN(p) ? _smoothing.Target : p;
    }

    /// <summary>
    /// Угол руля напрямую из HID (для диагностики старта: SDK стартует только из центра).
    /// NaN, если данные недоступны — тогда считаем «угол неизвестен» и пробуем стартовать.
    /// </summary>
    private float ReadHidAngle()
    {
        try
        {
            ERRORCODE err = ERRORCODE.NORMAL;
            var hid = getHIDData(ref err);
            if (err != ERRORCODE.NORMAL) return float.NaN;
            return hid.fSteeringWheelAngle;
        }
        catch
        {
            return float.NaN;
        }
    }

    /// <summary>Включить удержание: руль мягко, но уверенно сопротивляется попыткам повернуть.</summary>
    /// <param name="position">Позиция для удержания, пользовательские градусы (±полный ход = упор).</param>
    /// <param name="force">Сила, % от максимума (0..100).</param>
    public void EnableHold(float position, float force)
    {
        // Позиция — в ФИЗИЧЕСКИХ градусах (консоль; «hold» из сокета конвертируется в Program).
        float clamped = Math.Clamp(position, -_rangeHalf, _rangeHalf);
        float f = Math.Clamp(force, 0f, 100f);
        lock (_gate)
        {
            _consoleOverride = true;           // консоль взяла управление: сокет не перехватывает до release
            _holdForcePct = f;
            _holdRamp = 0f;                    // силу набираем плавно (holdRampMs) — без рывка
            _holdActive = true;
            _lockTarget = MathF.Abs(clamped) >= _rangeHalf - 0.5f;
            _lockDir = clamped >= 0f ? 1f : -1f;

            // Сглаженную точку — на текущий угол, цель — на удержание:
            // после release руль продолжит именно с этой позиции, без ухода в старую сторону.
            float pos = _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);
            if (!float.IsNaN(pos)) _smoothing.SnapTo(pos);
            _smoothing.SetTarget(clamped);

            // Пружина стартует с ТЕКУЩЕГО угла и плавно (EMA) идёт к цели — без рывка при включении.
            _holdTarget = clamped;
            _holdPos = float.IsNaN(pos) ? clamped : pos;

            if (_simulated)
                Console.WriteLine($"SIM: EnableHold(pos={clamped:0.#}°, force={f:0.#}%) — руль держит позицию");
        }
    }

    /// <summary>Выключить удержание (сила плавно сбрасывается в 0).</summary>
    public void DisableHold()
    {
        lock (_gate)
        {
            if (_holdActive && !_simulated && _cf is not null)
            {
                try { _cf.setMagnitude(0); } catch { }
            }
            _holdActive = false;
            _holdRamp = 0f;
            _lockTarget = false;
            _consoleOverride = false; // release = вернуть управление сокету (игре)

            if (_simulated)
                Console.WriteLine("SIM: DisableHold()");
        }
    }

    /// <summary>Вернуть управление целью сокету (команда socket в консоли).</summary>
    public void AllowSocketControl()
    {
        lock (_gate) { _consoleOverride = false; }
    }

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

    /// <summary>
    /// Режим «держи меня за руки» (hands, для сокета): цель удерживается ЖЁСТКОЙ пружиной hold
    /// с заданной силой (holdForce = 100% → оттолкнуть руль от цели сложно), а не мягким move.
    /// Первый вызов включает hold (с плавным разгоном holdRampMs), дальше просто переставляет
    /// позицию — без перезапуска разгона, поэтому поток 30 Гц идёт плавно и без рывков.
    /// Выключается: release / socketHandsMode=false в config.json.
    /// </summary>
    /// <param name="degrees">Цель, пользовательские градусы (клампится к ±полному ходу).</param>
    /// <param name="force">Сила удержания, % (0..100).</param>
    public void HandsTo(float degrees, float force)
    {
        float userClamped = Math.Clamp(degrees, -_userFullLock, _userFullLock);
        float clamped = U2P(userClamped);

        // Все печати — ПОСЛЕ _gate: Console.WriteLine на железе дорог (рендер 5-20 мс),
        // а под замком он задевает тик 200 Гц — юзер чувствовал это как мелкое дерганье.
        // Флаги ставятся под замком, пишем после.
        bool printSkip = false, printEngage = false, printSim = false;

        lock (_gate)
        {
            // Диагностика шкалы: максимум |числа| из игры за сессию (до клампа) —
            // по нему настраивают userFullLockDeg (см. status).
            if (MathF.Abs(degrees) > _sockMaxRaw) _sockMaxRaw = MathF.Abs(degrees);

            // Консоль взяла управление (hold/угол из консоли) — сокет больше не перехватывает
            // цель, пока пользователь сам не сделает release и не перезапустит socket.
            if (_consoleOverride)
            {
                _pendingValid = false; // консоль взяла руль — хвост сокет-миганий не нужен
                long t0 = Environment.TickCount64;
                if (t0 - _lastSkipPrint >= 2000)
                {
                    _lastSkipPrint = t0;
                    printSkip = true;
                }
            }
            // Дедуп (идея пользователя): «90 90 90» → первое принято, остальные отсечены,
            // просто ждём другое число. Hold при этом продолжает держать ту же цель.
            else if (_holdActive && MathF.Abs(clamped - _holdTarget) < 1e-3f)
            {
                _pendingValid = false;   // хвост мигания A↔B гасим: вернулись к цели
                UpdateSlope(clamped); // дюп = нулевой наклон — быстрее тухнет
            }
            else
            {
                // Подавление миганий целых градусов (лог езды 19:54: 22 эпизода A↔B —
                // толчки силы средние 267 / макс 716 = «шестерня на малых поворотах»).
                // Шаг держим один пакет: следующий тем же числом → реальный шаг (коммит);
                // возврат к цели → мигание (цель не двигаем); третье новое значение →
                // реальный ход (коммит сразу). Задержка 1 пакет (33 мс) прячется
                // под EMA τ=100..200 мс — на ощупь не отличить.
                bool wait = false;
                if (_holdActive && MathF.Abs(clamped - _holdTarget) > 1e-3f)
                {
                    if (!_pendingValid)
                    {
                        _pendingVal = clamped;
                        _pendingValid = true;
                        wait = true;            // отложили — ждём подтверждения
                    }
                    else
                    {
                        _pendingValid = false;  // повторилось или ушло дальше — коммит
                    }
                }

                if (!wait)
                {
                    UpdateSlope(clamped); // наклон считаем ДО записи нового _holdTarget

                    if (!_holdActive)
                    {
                        // Первый пакет: включаем hold мягко, сглаженную — на текущий угол.
                        _holdRamp = 0f;
                        _holdActive = true;
                        _pendingValid = false;
                        float pos0 = _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);
                        if (!float.IsNaN(pos0)) _smoothing.SnapTo(pos0);
                        _holdPos = float.IsNaN(pos0) ? clamped : pos0; // старт с текущего — EMA доведёт до цели
                        printEngage = !_simulated; // железо: ровно ОДНА строка при взятии управления
                    }
                    _holdTarget = clamped;
                    _holdForcePct = Math.Clamp(force, 0f, 100f);
                    _lockTarget = MathF.Abs(userClamped) >= _userFullLock - 0.5f;
                    _lockDir = userClamped >= 0f ? 1f : -1f;
                    _smoothing.SetTarget(clamped);
                    _announcedReach = false;
                    _targetSet = true;
                    _posInteg = 0;

                    // Сим печатает «hands →» как раньше (не чаще 1/с) — по этим строкам идут
                    // смоуки; на железе сопровождение потока пакетов убрано (см. printEngage).
                    if (_simulated)
                    {
                        long now = Environment.TickCount64;
                        if (now - _lastQuietPrint >= 1000)
                        {
                            _lastQuietPrint = now;
                            printSim = true;
                        }
                    }
                }
                // wait: UpdateSlope не кормим — на мигании наклон от нуля тухнет сам,
                // на реальном шаге один пропуск не заметен на фоне EMA цели.
            }
        }

        if (printSkip)
            Console.WriteLine($"-> сокет ждёт: руль под консолью (цель {_holdTarget:0.#}° физ) — release, чтобы вернуть игру");
        if (printEngage)
            Console.WriteLine($"hands: руль взял управление → цель {clamped:0.#}° физ (из игры {userClamped:0.#}°, силой {_holdForcePct:0.#}%)");
        if (printSim)
            Console.WriteLine($"SIM: hands → {userClamped:0.#}° (сила {_holdForcePct:0.#}%)");
    }

    /// <summary>
    /// Наклон целей из сокета (°/с физ) — сырьё для lead-сглаживания лесенки целых
    /// градусов. Дюпы тоже идут сюда: равные значения = нулевой наклон, он быстрее тухнет.
    /// Вызывается из HandsTo под _gate.
    /// </summary>
    private void UpdateSlope(float clamped)
    {
        long now = _clock.ElapsedMilliseconds;
        if (_lastPktMs != 0)
        {
            float dtP = (now - _lastPktMs) / 1000f;
            if (dtP > 0.005f && dtP < 0.5f)
            {
                float v = (clamped - _holdTarget) / dtP; // °/с физ (до записи нового _holdTarget)
                _tgtVel += (v - _tgtVel) * 0.4f;          // EMA по пакетам (~30 Гц)
            }
        }
        _lastPktMs = now;
    }

    /// <summary>
    /// Плавное ведение цели hold (общее для железа и сима). Целые градусы из игры дают
    /// лесенку 0.5° физ: на медленном повороте τ удваивается (ступеньки глушатся сильнее),
    /// а наклон входа добавляет «крен» lead = v×τ — отставание EMA на наклоне компенсируется
    /// и руль идёт синхронно с игрой. Резкие движения (|v| ≥ 60 °/с) — обычная τ без крена
    /// выше LeadMaxDeg, чтобы не «уезжать» вперёд при обрыве пакетов.
    /// </summary>
    private void StepHoldTarget(float dt)
    {
        if (!_holdActive || _demoActive || _cfg.SocketSmoothTimeMs <= 0f) return;

        if (_lastPktMs != 0 && _clock.ElapsedMilliseconds - _lastPktMs > 150)
            _tgtVel = 0f; // пакеты перестали приходить — крен не держим

        float vAbs = MathF.Abs(_tgtVel);
        float tau = _cfg.SocketSmoothTimeMs *
                    (2f - MathF.Min(vAbs / FastSlopeDegPerSec, 1f)); // 2×cfg (медленно) → 1×cfg (быстро)
        float lead = Math.Clamp(_tgtVel * (tau / 1000f), -LeadMaxDeg, LeadMaxDeg);
        float k = Math.Clamp(dt * 1000f / tau, 0f, 1f);
        // Кламп к физ. упору: цель+крен не должны вылезать за rangeHalf — иначе у края
        // пружина ветрается в стенку (лог езды: holdPos 91.7 при ходе ±90), а на развороте
        // крен слетает за пару тактов — юзер это чувствует как дёрганье на конце угла.
        float goal = Math.Clamp(_holdTarget + lead, -_rangeHalf, _rangeHalf);
        _holdPos += (goal - _holdPos) * k;
    }

    // ======================== Demo (автономное «вождение») ========================

    private const float DemoDefaultSec = 50f;   // длительность по умолчанию
    private const float DemoPeriodSec = 5f;     // период «змейки»
    private const float DemoFadeSec = 4f;       // плавный набор/сброс амплитуды

    private volatile bool _demoActive;
    private long _demoStartMs;
    private long _demoDurationMs;
    private float _demoAmp;

    /// <summary>Идёт ли сейчас demo-сценарий.</summary>
    public bool IsDemoActive => _demoActive;

    /// <summary>
    /// Запустить имитацию езды: руль плавно «змейкой» крутит от упора до упора
    /// в режиме hold — сопротивление активно всё время (можно взять в руки).
    /// Останавливается сам (по таймеру) или командой move/hold/release.
    /// </summary>
    /// <param name="seconds">Длительность, с (<=0 → 50).</param>
    /// <param name="amplitude">Амплитуда ±° (<=0 → полный ход руля).</param>
    public void StartDemo(float seconds, float amplitude)
    {
        if (float.IsNaN(seconds) || seconds <= 0f) seconds = DemoDefaultSec;
        float amp = float.IsNaN(amplitude) || amplitude <= 0f
            ? _rangeHalf // по умолчанию — полный ход (физические °)
            : MathF.Abs(amplitude);
        amp = Math.Clamp(amp, 1f, _rangeHalf);

        lock (_gate)
        {
            _demoActive = true;
            _demoStartMs = _clock.ElapsedMilliseconds;
            _demoDurationMs = (long)(seconds * 1000f);
            _demoAmp = amp; // физические градусы (консольная шкала)
            _lockTarget = false; // змейка не у упора — толчки не нужны

            if (!_holdActive)
            {
                // Держим через обычный hold: пружина даёт и слежение за «змейкой»,
                // и сопротивление, если пользователь возьмёт руль в руки.
                _holdPos = 0f;
                _holdForcePct = Math.Clamp(_cfg.HoldForce, 0f, 100f);
                _holdRamp = 0f;
                _holdActive = true;
            }
            float pos = _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);
            if (!float.IsNaN(pos)) _smoothing.SnapTo(pos);
            _smoothing.SetTarget(0f);
            _targetSet = false;
        }

        Console.WriteLine($"Demo: {seconds:0.#} с, амплитуда ±{amp:0.#}° (период {DemoPeriodSec:0.#} с, огибающая {DemoFadeSec:0.#} с). " +
                          "Руль сам крутит змейку; можно взять в руки — будет сопротивление. Остановить: любая команда move/hold/release.");
    }

    /// <summary>Остановить demo: цель — в центр, hold выключается (момент сбрасывается, дальше руль едет в 0).</summary>
    /// <param name="reason">Причина (для вывода в консоль).</param>
    public void StopDemo(string reason)
    {
        lock (_gate)
        {
            if (!_demoActive) return;
            _demoActive = false;

            // Сглаженную — на текущий угол, цель — в центр: возврат сразу «вниз», без ухода вверх.
            float pos = _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);
            if (!float.IsNaN(pos)) _smoothing.SnapTo(pos);
            _smoothing.SetTarget(0f);
            _holdPos = 0f;
            _holdTarget = 0f;
            _holdActive = false;        // освобождаем — move-петля довезёт до 0
            _holdRamp = 0f;
            _lockTarget = false;        // цель — центр, никаких толчков у упора
            _consoleOverride = false;   // демо кончилось — сокет снова может управлять
            _targetSet = true;          // напечатаем «Достигнуто: 0°», когда руль доедет
            if (!_simulated && _cf is not null)
            {
                try { _cf.setMagnitude(0); } catch { }
            }
        }

        if (_simulated) Console.WriteLine("SIM: StopDemo()");
        Console.WriteLine(reason == "конец"
            ? "Demo завершён: руль возвращён в центр, сопротивление снято."
            : $"Demo остановлен ({reason}).");
    }

    /// <summary>Шаг demo — вызывается из Tick под _gate: двигает цель «змейки».</summary>
    private void StepDemo()
    {
        if (!_demoActive) return;

        long t = _clock.ElapsedMilliseconds - _demoStartMs;
        if (t >= _demoDurationMs)
        {
            StopDemo("конец");
            return;
        }

        float sec = t / 1000f;
        float totalSec = _demoDurationMs / 1000f;
        // Огибающая: амплитуда растёт DemoFadeSec и так же гаснет к концу —
        // старт и финиш плавные, руль всегда заканчивает в центре.
        float env = MathF.Min(MathF.Min(sec, totalSec - sec) / DemoFadeSec, 1f);
        if (env < 0f) env = 0f;

        float target = _demoAmp * env * MathF.Sin(2f * MathF.PI * sec / DemoPeriodSec); // физические °
        _holdPos = target;
        _smoothing.SetTarget(target);
        _targetSet = false;
    }

    // ======================== Цикл управления (200 Гц) ========================

    /// <summary>Запустить фоновую петлю управления (5 мс / 200 Гц — как DT в примере SDK).</summary>
    public void Start()
    {
        if (_loop != null || _disposed != 0) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(5));
        long lastTick = _clock.ElapsedMilliseconds;

        // Разрешение системного таймера 1 мс. Факт (замер в osc_log.csv): без этого
        // PeriodicTimer упирается в тик Windows 15.6 мс — петля шла на ~64 Гц вместо
        // задуманных 200, и EMA/демпфер/лог жили в другом временном режиме.
        TimeBeginPeriod(1);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                long nowMs = _clock.ElapsedMilliseconds;
                float dt = (nowMs - lastTick) / 1000f;
                lastTick = nowMs;
                if (dt <= 0f || dt > 1f) dt = 0.005f; // защита после «сна» системы

                lock (_gate)
                {
                    try { Tick(dt); }
                    catch (Exception ex) { Warn($"ошибка тика: {ex.Message}"); }
                }
            }
        }
        catch (OperationCanceledException) { /* остановка */ }
        finally { TimeEndPeriod(1); }
    }

    private void Tick(float dt)
    {
        if (!_connected) return;

        if (_simulated)
        {
            TickSim(dt);
            WriteLog(_simPos, 0f, 0f); // в симе скорость/сила не считаем — лог нужен для железа
            return;
        }

        if (_cf is null) return;

        // Дыры в тактах (подвисание процесса/системы; из CSV: 92 за 120 с, до 218 мс):
        // ошибка растёт молча, и на первом же такте после дыры сила прыгает в упор →
        // руль хлестает через цель серией bang-bang — то самое резкое дёрганье в
        // рандомных местах. После дыры даём силе ~80 мс плавно дойти от реально
        // отданной на колесо до свежесчитанной (первый такт — без изменений силы).
        long tickMs = _clock.ElapsedMilliseconds;
        bool gap = _lastTickMs != 0 && tickMs - _lastTickMs > GapMs;
        _lastTickMs = tickMs;
        if (gap) _gapT = 0f;
        else if (_gapT < 1f) _gapT = MathF.Min(1f, _gapT + dt / GapRampSec);

        // ---- 1. Считать текущее состояние руля ----
        float pos = _lastPos;
        float vel = 0f;
        float velSdk = float.NaN;
        try
        {
            ERRORCODE err = ERRORCODE.NORMAL;
            var hid = getHIDData(ref err);
            float angle = hid.fSteeringWheelAngle;
            if (!float.IsNaN(angle))
            {
                pos = angle;
                _lastPos = angle;
                _hasPos = true;
                _lastPosMs = Environment.TickCount64;
            }

            velSdk = hid.fSteeringWheelVelocity;   // основная скорость в контрол: в логах
                                                  // совпадает с производной позиции (±400 °/с, знак тот же)
        }
        catch (Exception ex)
        {
            Warn($"getHIDData: {ex.Message}");
        }

        // Диагностика «не читает угол»: данные встали → петля не знает, где руль,
        // и не сможет вернуть его в ноль. Выводится не чаще раза в 5 с ( Warn троттлит ).
        if (_hasPos)
        {
            long ageMs = Environment.TickCount64 - _lastPosMs;
            if (ageMs > 1000)
                Warn($"угол руля не обновляется {ageMs} мс — руль может не вернуться в ноль. Перезапусти программу (закроется предыдущий экземпляр сам).");
        }

        // ---- Скорость: окно ~40 мс по позиции — запасная + колонка velRaw в лог ----
        // Позиция HID идёт шагами (напр. 0.2°): мгновенная разность давала пик
        // 40 °/с на КАЖДЫЙ шаг → «шестерня». Оконная разность шестерню убрала, но как
        // основная скорость запаздывала на ~58 мс (окно + EMA) и на дрожаниях ~6 Гц
        // демпфер приходил с опозданием ~120° — разбавлял колебания (см. лог demo):
        // поэтому основная скорость теперь velSdk, окно — fallback при выбросах.
        if (_hasPos)
        {
            long nowMs = _clock.ElapsedMilliseconds;
            if (!_hasVelRing)
            {
                for (int i = 0; i < VelRingLen; i++)
                {
                    _velRingPos[i] = pos;
                    _velRingMs[i] = nowMs;
                }
                _velRingI = 0;
                _hasVelRing = true;
            }

            _velRingI = (_velRingI + 1) % VelRingLen;
            _velRingPos[_velRingI] = pos;
            _velRingMs[_velRingI] = nowMs;
            int old = (_velRingI + 1) % VelRingLen;       // самый старый сэмпл окна
            float winSec = (nowMs - _velRingMs[old]) / 1000f;
            vel = winSec > 0.001f ? (pos - _velRingPos[old]) / winSec : 0f;
        }

        _logVelRaw = vel;                                 // оконная скорость ДО фильтра — в CSV-лог
        _logVelSdk = float.IsNaN(velSdk) ? 0f : velSdk;   // сырая SDK-скорость — в CSV-лог

        // Фильтр скорости: в EMA идёт velSdk (свежая), при выбросе 0 от прошивки —
        // запасное окно. Мёртвая зона — на ВХОДЕ: старый код занулял само состояние
        // фильтра, из-за чего velFilt залипал в 0, пока |vel| < 20 °/с (в логе demo:
        // 271 тик демпфера вырублен до 70 мс при живом velRaw) — демпфер выключался
        // и включался рывком, чем и питались дёрганья.
        float vCtrl = _logVelSdk;
        if (vCtrl == 0f && MathF.Abs(vel) > 10f) vCtrl = vel;    // dropout прошивки → окно
        // Гистерезис вместо плоского порога: смотрим, пересекаем ли мы 7/3 °/с,
        // и только потом решаем (шум гасится ДО EMA, состояние не зануляем).
        if (_velEngaged ? MathF.Abs(vCtrl) < VelReleaseDegPerSec
                        : MathF.Abs(vCtrl) > VelEngageDegPerSec)
            _velEngaged = !_velEngaged;
        if (!_velEngaged) vCtrl = 0f;
        // α=0.5 (~5–8 мс). На прежнем 0.25 (~15 мс) на волне ~10 Гц из лога змейки
        // (velFilt качается 70↔110 — сила повторяет эти качки ±1000) фильтр давал
        // демпферу фазу ~60° — тот успевал не гасить, а ПОДДЕРЖИВАТЬ раскачку.
        // Шум velSdk на покое гасит мёртвая зона на входе.
        _velFilt += (vCtrl - _velFilt) * 0.5f;                   // EMA, состояние не зануляем
        vel = _velFilt;

        // Плавное ведение цели hold — см. StepHoldTarget (lead + адаптивная τ).
        StepHoldTarget(dt);

        // ---- 1.5. Demo: подвинуть цель «змейки» (до выбора режима hold/move) ----
        StepDemo();

        // ---- 2. Считать требуемый момент и отдать его на колесо ----
        float magnitude;
        if (_holdActive)
        {
            magnitude = ComputeHoldForce(pos, vel, dt);
        }
        else
        {
            magnitude = ComputeMoveForce(pos, vel, dt);
            AnnounceReach(pos);
        }

        // Дозахват после дыры: сила плавно сходится от последней отданной к свежей.
        if (_gapT < 1f)
            magnitude = _lastApplied + (magnitude - _lastApplied) * _gapT;

        ApplyMagnitude(magnitude);
        WriteLog(pos, vel, magnitude);
    }

    private void TickSim(float dt)
    {
        StepDemo();

        // Как и в реальном режиме: цель hold ведём плавно — общий метод (lead + адаптивная τ).
        StepHoldTarget(dt);

        float target = _smoothing.Target;

        if (_holdActive)
        {
            // Рук в симуляции нет — просто фиксируем позицию.
            _simPos = _holdPos;
            _smoothing.SnapTo(_holdPos);
            return;
        }

        _simPos = _smoothing.Step(dt);

        if (_targetSet && !_announcedReach && MathF.Abs(target - _simPos) < 0.05f)
        {
            _announcedReach = true;
            _targetSet = false;
            Console.WriteLine($"SIM: позиция достигнута ({target:0.#}°)");
        }
    }

    /// <summary>Позиционная петля (порт official примера sdk_api_test.cc::moveTo).</summary>
    private float ComputeMoveForce(float pos, float vel, float dt)
    {
        float smoothed = _smoothing.Step(dt);
        float posError = smoothed - pos;

        // Только предупреждение — силу НЕ обнуляем: цель должна достигаться всегда.
        // (Раньше при >60° сила сбрасывалась навсегда: +100 и «0» не доезжали.)
        if (MathF.Abs(posError) > _rangeHalf * PosWarnFactor)
            Warn($"очень большая ошибка позиции {posError:0.#}° — если руль идёт не туда, выставь forceInvert в config.json.");

        // Позиция -> требуемая скорость (об/мин).
        // pos_kp подобран так, чтобы при тau=SmoothTimeMs получился 1.0 — как в примере SDK.
        // Из сокета tau маленькая (socketSmoothTimeMs) → реакция почти мгновенная.
        float posKp = _cfg.Smooth
            ? 0.5f / Math.Max(_smoothing.EffectiveTauMs / 1000f, 0.01f)
            : FastMovePosKp;
        float speedLimit = _cfg.Smooth ? _cfg.MaxMoveSpeedRpm : Math.Min(_cfg.MaxMoveSpeedRpm * 2f, 300f);

        float spdRef = posKp * posError;
        spdRef = Math.Clamp(spdRef, -speedLimit, speedLimit);

        // Скорость -> момент.
        float currentRpm = vel * DegPerSecToRpm;
        float spdErr = spdRef - currentRpm;

        _posInteg += spdErr * dt * SpdKi;
        // Интегратор может выйти на ВЕСЬ предел момента. (Раньше делили на SpdKi:
        // получалось 1500/40 = 37.5 ≈ 0.4% — руль не преодолевал трение и +10 не доезжал.)
        float integLimit = Math.Max(_cfg.MoveForceLimit, 100f);
        _posInteg = Math.Clamp(_posInteg, -integLimit, integLimit); // без виндапа

        float torqueRef = _posInteg + spdErr * SpdKp;

        // Знак: в официальном примере target_ref = -torque_ref.
        float magnitude = -torqueRef;
        if (_cfg.ForceInvert) magnitude = -magnitude;

        float cap = MathF.Min(_cfg.MoveForceLimit, 10_000f);
        float result = Math.Clamp(magnitude, -cap, cap);

        // ---- Плавный подход к упору (анти-«подпрыгивание как мячик") ----
        // Цель на клампе у механического стопа: интеграл копился весь ход и вдавливал
        // руль в упор → отскок → повторный удар (качается). У упора: стравливаем
        // интеграл и притормаживаем подачу — руль мягко прижимается и стоит.
        float toStop = smoothed >= 0f ? _rangeHalf - pos : _rangeHalf + pos; // до упора в сторону цели
        bool approachingStop = toStop < LockApproachDeg && (posError >= 0f) == (smoothed >= 0f);
        if (approachingStop)
        {
            _posInteg *= 0.85f; // стравить виндап у упора (каждый тик)
            if (toStop > 0f)
                result *= 0.3f + 0.7f * Math.Clamp(toStop / LockApproachDeg, 0f, 1f);
        }

        return result;
    }

    /// <summary>Удержание: пружина-демпфер. Мягко, но уверенно сопротивляется, без рывков.</summary>
    private float ComputeHoldForce(float pos, float vel, float dt)
    {
        // Разгон силы при включении (и после каждого входа в hold) — чтобы не дёргало.
        if (_cfg.HoldRampMs <= 0)
            _holdRamp = 1f;
        else
            _holdRamp = Math.Min(1f, _holdRamp + dt * 1000f / _cfg.HoldRampMs);

        float baseCap = Math.Clamp(_holdForcePct, 0f, 100f) / 100f * 10_000f; // % → шкала DirectInput
        if (baseCap <= 0f) return 0f;

        float err = _holdPos - pos;                     // >0 — руль левее цели

        // Прогрессивное усиление («чувствует мою руку»): если пользователь усилил
        // отклонение от цели, сила ОСТОРОЖНО поднимается к 100% — smoothstep, без ступенек.
        float cap = baseCap;
        if (_cfg.ProgressiveHold && cap < 10_000f)
        {
            float x = Math.Clamp(MathF.Abs(err) / ProgressiveBoostDeg, 0f, 1f);
            x = x * x * (3f - 2f * x);
            cap += (10_000f - cap) * x;
        }

        // KpScale/KdScale из config.json (по умолчанию 1.0 = прежнее поведение):
        // kpScale 0.5 = пружина вдвое мягче (меньше борьбы с рукой и щелчков стик-липа),
        // kdScale — демпфер (осторожно: <1 возвращает волну ~15 Гц, см. README).
        // Губернатор от KpScale не зависит (кламп по cap), равновесная скорость
        // подлёта = cap/kd = 180/KdScale °/с.
        float kp = cap / HoldSpringFullDeg * _cfg.KpScale;             // пружина: полная сила при 8° ошибки
        float kd = cap / HoldDampFullDegPerSec * _cfg.KdScale;         // демпфер: полный при 180 °/с

        // Сила в «+ угол»: тянет к цели и гасит движение (ни туда, ни сюда — не дёргается).
        // Губернатор подлёта: пружина ограничивается cap ДО сложения с демпфером.
        // Иначе при большом ветре (заряжена у упора / скачок цели) сумма kp·err − kd·v
        // даёт равновесную скорость kp·err/kd = 22.5·err — на скачке цели −95→0 руль
        // улетал на 697 °/с с шагом силы 6300 (лог езды 19:54, событие 94.94) — «стол
        // дрожит, руль хочет быстро вернуться». С клампом: v_равн = cap/kd = 180 °/с при
        // ЛЮБОЙ ошибке; на типичной работе |err|<8° пружина ниже cap — формула не меняется,
        // сила удержания и сопротивление рывку (err>8, скорость от руки) те же.
        float spring = Math.Clamp(kp * err, -cap, cap);
        float forcePlus = spring - kd * vel;

        // У упора (цель на клампе, руль у механического стопа): мягкие ритмичные толчки
        // 6..12% силы ~1.2 Гц — «осторожные, не сильные» (в игре может быть 3 витка,
        // а ход у нас один — упор подсказывает предел без жёсткого втыкания).
        if (_cfg.LockNudge && _lockTarget)
        {
            float dist = _rangeHalf - MathF.Abs(pos);   // сколько осталось до упора
            if (dist <= NudgeBlendDeg)
            {
                float prox = 1f - MathF.Max(dist, 0f) / NudgeBlendDeg;      // 0..1 к упору
                float phase = Environment.TickCount64 * 0.001f * 2f * MathF.PI * NudgeHz;
                float pulse = 0.06f + 0.06f * (0.5f + 0.5f * MathF.Sin(phase));
                float nudgePlus = cap * pulse * _lockDir;
                forcePlus += (nudgePlus - forcePlus) * prox;   // плавный переход пружина↔толчок
            }
        }

        float magnitude = _cfg.ForceInvert ? forcePlus : -forcePlus;
        magnitude *= _holdRamp;
        // ВАЖНО: slew-лимитера (ограничение скорости изменения силы) здесь НЕТ — он в петле
        // обратной связи давал фазовый сдвиг и раскачку руля до упора («качался 120..160»).
        // Плавность дают: EMA цели (_holdPos), мёртвая зона скорости и holdRampMs.
        return Math.Clamp(magnitude, -cap, cap);
    }

    /// <summary>Отдать момент на колесо (с восстановлением эффекта после сбоя).</summary>
    private void ApplyMagnitude(float magnitude)
    {
        if (_cf is null) return;
        try
        {
            float c = Math.Clamp(magnitude, -10_000f, 10_000f);
            _lastApplied = c;
            _cf.setMagnitude((long)c);
            if (_effectBroken)
            {
                _effectBroken = false;
                Warn("FFB-эффект восстановлен.");
            }
        }
        catch (Exception ex)
        {
            // Эффект мог «умереть» (например, по таймауту) — пробуем перезапустить один раз.
            if (!_effectBroken)
            {
                _effectBroken = true;
                try { _cf.stop(); } catch { }
                try { _cf.start(); Warn($"FFB-эффект перезапущен после сбоя: {ex.Message}"); }
                catch (Exception ex2) { Warn($"FFB-эффект не запускается: {ex2.Message}"); }
            }
        }
    }

    private void AnnounceReach(float pos)
    {
        if (!_targetSet || _announcedReach) return;
        float target = _smoothing.Target;
        if (MathF.Abs(target - pos) < 1.0f)
        {
            _announcedReach = true;
            _targetSet = false;
            Console.WriteLine($"Достигнуто: {target:0.#}°");
        }
    }

    /// <summary>Предупреждение (не чаще раза в 5 секунд, чтобы не спамить).</summary>
    private void Warn(string message)
    {
        LastWarning = message;
        long now = Environment.TickCount64;
        if (now - _lastWarnTick < 5000) return;
        _lastWarnTick = now;
        Console.WriteLine($"! {message}");
    }

    // ======================== Безопасное выключение ========================

    /// <summary>Идемпотентно: обнулить момент, остановить эффекты, выгрузить SDK.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        // 1) Остановить цикл управления
        try { _cts?.Cancel(); } catch { }
        try { _loop?.Wait(700); } catch { }
        try { _cts?.Dispose(); } catch { }

        lock (_gate)
        {
            StopLogInternal(); // дописать/закрыть CSV-лог, если шёл

            if (_simulated)
            {
                Console.WriteLine("SIM: SetTorque(0) — момент обнулён");
                _connected = false;
                return;
            }

            // 2) Обнуление момента — главное правило безопасности
            try { _cf?.setMagnitude(0); } catch { }
            try { _cf?.stop(); } catch { }
            try { stopForceFeedback(); } catch { }
            try { motorStopMove(); } catch { }
            try { if (_sdkInstalled) removeMozaSDK(); } catch { }

            _cf = null;
            _sdkInstalled = false;
            _connected = false;
        }
    }
}
