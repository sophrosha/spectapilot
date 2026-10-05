using System.Text.Json;
using System.Text.Json.Serialization;

namespace WheelControl;

/// <summary>
/// Конфигурация приложения. Файл config.json лежит рядом с exe.
/// Загружается при старте; если файла нет — создаётся с значениями по умолчанию.
/// </summary>
public sealed class Config
{
    // ---------- Поля из ТЗ ----------

    /// <summary>true — плавное движение к цели; false — максимально быстро (скорость и момент всё равно ограничены).</summary>
    public bool Smooth { get; set; } = true;

    /// <summary>Время достижения цели, мс (~95% пути за это время при smooth=true).</summary>
    public int SmoothTimeMs { get; set; } = 500;

    /// <summary>Максимальный момент, % от максимума (аппаратный лимит setMotorPeakTorque, допустимо 50..100).</summary>
    public float MaxTorque { get; set; } = 100.0f;

    /// <summary>Сила удержания (hold), % от максимума (0..100 → шкала DirectInput 0..10000).</summary>
    public float HoldForce { get; set; } = 50.0f;

    /// <summary>Позиция по умолчанию для удержания, градусы (используется, если текущий угол неизвестен).</summary>
    public float HoldPosition { get; set; } = 90.0f;

    /// <summary>Порт UDP-сервера (socket mode).</summary>
    public int SocketPort { get; set; } = 7777;

    /// <summary>Адрес UDP-сервера (только localhost — безопасность).</summary>
    public string SocketHost { get; set; } = "127.0.0.1";

    /// <summary>Ожидаемый интервал входных данных, мс (~30 Гц от мода). Только справочно/для отладки.</summary>
    public int DataIntervalMs { get; set; } = 33;

    /// <summary>Время сглаживания для команд из СОКЕТА, мс (маленькое = почти без задержки от мода).</summary>
    public int SocketSmoothTimeMs { get; set; } = 50;

    /// <summary>
    /// Полный ход руля в ПОЛЬЗОВАТЕЛЬСКИХ градусах: 90 = 1:1 с физикой базы (по умолчанию),
    /// 180 = шкала вдвое шире (старый вариант, «значения до 180 вместо 90»).
    /// </summary>
    public float UserFullLockDeg { get; set; } = 90.0f;

    /// <summary>
    /// Инвертировать числа, приходящие ИЗ ИГРЫ (сокет): если руль в игре крутится
    /// в обратную сторону относительно реального — поставь true. Консоль не трогает.
    /// </summary>
    public bool InvertSocket { get; set; } = true;

    /// <summary>
    /// Режим «держи меня за руки» для сокета (hands): позиции из игры удерживаются
    /// жёсткой пружиной hold с силой holdForce (100% = оттолкнуть руль сложно),
    /// а не мягким move. Автоматом включается при первом пакете.
    /// </summary>
    public bool SocketHandsMode { get; set; } = true;

    /// <summary>true — UDP-сервер поднимается сам при старте (не нужно писать socket в консоли).</summary>
    public bool SocketAutoStart { get; set; } = false;

    /// <summary>
    /// Прогрессивное усиление hold: если рука усилила отклонение от цели,
    /// сила плавно поднимается к 100% («чувствует усилие — осторожно помогает»).
    /// </summary>
    public bool ProgressiveHold { get; set; } = true;

    /// <summary>
    /// У упора (цель = полный ход): мягкие ритмичные толчки 6..12% силы ~1.2 Гц
    /// вместо жёсткого втыкания в механический стоп.
    /// </summary>
    public bool LockNudge { get; set; } = true;

    /// <summary>
    /// Множитель жёсткости пружины hold (Kp): 1.0 = по умолчанию (полная сила при 8°),
    /// 0.5 = вдвое мягче (ровнее борьба с рукой на микро-отклонениях), 2.0 = жёстче.
    /// Крутится без пересборки; демо и удержание используют один и тот же множитель.
    /// </summary>
    public float KpScale { get; set; } = 1.0f;

    /// <summary>
    /// Множитель демпфера hold (Kd): 1.0 = по умолчанию (полный при 180 °/с).
    /// Меньше 1 — слабее гашение волны ~15 Гц (дрожание вернётся), больше 1 —
    /// демпфер жёстче, подлёт медленнее (макс. скорость = 180/KdScale °/с).
    /// </summary>
    public float KdScale { get; set; } = 1.0f;

    // ---------- Дополнительные поля (есть дефолты, в ТЗ не фигурировали) ----------

    /// <summary>true — не трогать SDK, работать без руля (эквивалент --simulate).</summary>
    public bool Simulate { get; set; } = false;

    /// <summary>Страховка от рывков: максимальная скорость изменения угла, °/с.</summary>
    public float MaxDegPerSec { get; set; } = 720.0f;

    /// <summary>Время разгона силы удержания при включении hold, мс (чтобы не дёргало).</summary>
    public int HoldRampMs { get; set; } = 300;

    /// <summary>Лимит силы для движения к цели, 0..10000 (шкала DirectInput; пример SDK использовал 800).</summary>
    public float MoveForceLimit { get; set; } = 1500.0f;

    /// <summary>Ограничение скорости мотора при движении к цели, об/мин (пример SDK: 160).</summary>
    public float MaxMoveSpeedRpm { get; set; } = 90.0f;

    /// <summary>
    /// Инверсия знака момента. Если при первом тесте руль уходит к упору вместо движения к цели —
    /// выставьте true (знак мотора зависит от модели базы/прошивки).
    /// </summary>
    public bool ForceInvert { get; set; } = false;

    // ---------- Работа с файлом ----------

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Путь к config.json (папка с exe).</summary>
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "config.json");

    /// <summary>Загрузить конфиг; при отсутствии или ошибке — создать/вернуть значения по умолчанию.</summary>
    public static Config Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                var def = new Config();
                def.Save();
                return def;
            }

            var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath), JsonOpts) ?? new Config();
            cfg.Normalize();
            return cfg;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"! Ошибка чтения config.json: {ex.Message}. Используются значения по умолчанию.");
            var def = new Config();
            def.Normalize();
            return def;
        }
    }

    /// <summary>Сохранить конфиг в файл.</summary>
    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"! Не удалось сохранить config.json: {ex.Message}");
        }
    }

    /// <summary>Привести значения к допустимым диапазонам (защита от кривого файла).</summary>
    public void Normalize()
    {
        SmoothTimeMs = Math.Clamp(SmoothTimeMs, 50, 10_000);
        MaxTorque = Math.Clamp(MaxTorque, 50f, 100f);      // диапазон setMotorPeakTorque: 50..100
        HoldForce = Math.Clamp(HoldForce, 0f, 100f);
        DataIntervalMs = Math.Clamp(DataIntervalMs, 5, 1000);
        SocketSmoothTimeMs = Math.Clamp(SocketSmoothTimeMs, 10, 5000);
        UserFullLockDeg = Math.Clamp(UserFullLockDeg, 10f, 720f);
        SocketPort = Math.Clamp(SocketPort, 1, 65535);
        SocketHost = string.IsNullOrWhiteSpace(SocketHost) ? "127.0.0.1" : SocketHost.Trim();
        MaxDegPerSec = Math.Clamp(MaxDegPerSec, 30f, 5000f);
        HoldRampMs = Math.Clamp(HoldRampMs, 0, 5000);
        MoveForceLimit = Math.Clamp(MoveForceLimit, 100f, 10_000f);
        MaxMoveSpeedRpm = Math.Clamp(MaxMoveSpeedRpm, 5f, 300f);
        HoldPosition = Math.Clamp(HoldPosition, -3600f, 3600f);
        KpScale = Math.Clamp(KpScale, 0.1f, 3f);   // защита от «сломал петлю» в ручную
        KdScale = Math.Clamp(KdScale, 0.1f, 3f);
    }
}
