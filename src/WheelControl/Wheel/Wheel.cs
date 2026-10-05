using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace WheelControl;

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
public sealed partial class Wheel : IDisposable
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
            _fallbackHwnd = CreateWindowExW(0, "STATIC", "WheelControl", 0,
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

    public Wheel(Config cfg, bool simulate, string deviceId = "SIMULATED")
    {
        _cfg = cfg;
        _simulated = simulate;
        _deviceId = deviceId;
        _userFullLock = Math.Clamp(cfg.UserFullLockDeg, 10f, 720f);
        _smoothing = new Smoothing(cfg, 0f);
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
