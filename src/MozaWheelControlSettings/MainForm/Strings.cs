namespace MozaWheelControlSettings;

// All user-visible texts of the Settings app in four languages (RU / EN / UKR / BY).
// Only the Settings UI is localized here; the console app's stable strings are untouched.
internal static class Strings
{
    internal enum Key
    {
        Title,
        Language,
        About,
        Save,
        Saved,
        SavedRunning,
        SaveFailed,
        Version,
        ConfigMissing,
        ConsoleMissing,
        BadConfig,
        DemoStarted,
        DemoFailed,
        DemoNoExe,
        DemoNoWindow,
        // About dialog (UI iteration 1).
        Subtitle,
        ProjectHeader,
        ProjectText,
        LicensesHeader,
        ModTip,
    }

    // Menu order as requested: RU / EN / UKR / BY. Names stay native in every language.
    public static readonly (string Code, string Name)[] Languages =
    {
        ("ru", "Русский"),
        ("en", "English"),
        ("uk", "Українська"),
        ("by", "Беларуская"),
    };

    // Current UI language; persisted in config.json under the "uiLanguage" key.
    public static string Language { get; private set; } = "ru";

    private static readonly Dictionary<string, Dictionary<Key, string>> Table = new()
    {
        ["ru"] = new()
        {
            [Key.Title] = "SpectAPilot Handler",
            [Key.Language] = "Язык",
            [Key.About] = "О программе",
            [Key.Save] = "Сохранить",
            [Key.Saved] = "Сохранено.",
            [Key.SavedRunning] = "Сохранено. MozaWheelControl запущен — перезапустите его, чтобы применить изменения.",
            [Key.SaveFailed] = "Ошибка сохранения:",
            [Key.Version] = "Версия",
            [Key.ConfigMissing] = "config.json не найден рядом с MozaWheelControlSettings.exe — приложение не может продолжить работу.",
            [Key.ConsoleMissing] = "MozaWheelControl.exe не найден рядом с MozaWheelControlSettings.exe — кнопка Demo будет недоступна.",
            [Key.BadConfig] = "config.json не является корректным JSON — исправьте файл вручную.",
            [Key.DemoStarted] = "Demo запущен.",
            [Key.DemoFailed] = "Не удалось запустить MozaWheelControl:",
            [Key.DemoNoExe] = "MozaWheelControl.exe не найден рядом с MozaWheelControlSettings.exe.",
            [Key.DemoNoWindow] = "MozaWheelControl не открыл окно. Проверьте, что exe не блокируется антивирусом или не крашится — запустите вручную.",
            [Key.Subtitle] = "Обработчик пакетов углов руля",
            [Key.ProjectHeader] = "Об проекте",
            [Key.ProjectText] = "Синхронизация виртуального автопилота BeamNG.drive с физическим рулём.",
            [Key.LicensesHeader] = "Лицензии",
            [Key.ModTip] = "Мод в процессе публикации",
        },
        ["en"] = new()
        {
            [Key.Title] = "SpectAPilot Handler",
            [Key.Language] = "Language",
            [Key.About] = "About",
            [Key.Save] = "Save",
            [Key.Saved] = "Saved.",
            [Key.SavedRunning] = "Saved. MozaWheelControl is running — restart it to apply the changes.",
            [Key.SaveFailed] = "Save failed:",
            [Key.Version] = "Version",
            [Key.ConfigMissing] = "config.json was not found next to MozaWheelControlSettings.exe — the app cannot continue.",
            [Key.ConsoleMissing] = "MozaWheelControl.exe was not found next to MozaWheelControlSettings.exe — the Demo button will be unavailable.",
            [Key.BadConfig] = "config.json is not valid JSON — fix the file manually.",
            [Key.DemoStarted] = "Demo started.",
            [Key.DemoFailed] = "Failed to start MozaWheelControl:",
            [Key.DemoNoExe] = "MozaWheelControl.exe was not found next to MozaWheelControlSettings.exe.",
            [Key.DemoNoWindow] = "MozaWheelControl did not open a window. Check that the exe is not blocked by the antivirus or crashing — start it manually.",
            [Key.Subtitle] = "Steering angle packet handler",
            [Key.ProjectHeader] = "About the project",
            [Key.ProjectText] = "Synchronization of the BeamNG.drive virtual autopilot with a physical steering wheel.",
            [Key.LicensesHeader] = "Licenses",
            [Key.ModTip] = "The mod is being published",
        },
        ["uk"] = new()
        {
            [Key.Title] = "SpectAPilot Handler",
            [Key.Language] = "Мова",
            [Key.About] = "Про програму",
            [Key.Save] = "Зберегти",
            [Key.Saved] = "Збережено.",
            [Key.SavedRunning] = "Збережено. MozaWheelControl запущено — перезапустіть його, щоб застосувати зміни.",
            [Key.SaveFailed] = "Помилка збереження:",
            [Key.Version] = "Версія",
            [Key.ConfigMissing] = "config.json не знайдено поруч із MozaWheelControlSettings.exe — програма не може продовжити роботу.",
            [Key.ConsoleMissing] = "MozaWheelControl.exe не знайдено поруч із MozaWheelControlSettings.exe — кнопка Demo буде недоступна.",
            [Key.BadConfig] = "config.json не є коректним JSON — виправте файл вручну.",
            [Key.DemoStarted] = "Demo запущено.",
            [Key.DemoFailed] = "Не вдалося запустити MozaWheelControl:",
            [Key.DemoNoExe] = "MozaWheelControl.exe не знайдено поруч із MozaWheelControlSettings.exe.",
            [Key.DemoNoWindow] = "MozaWheelControl не відкрив вікно. Перевірте, що exe не блокується антивірусом або не аварійно завершується — запустіть вручну.",
            [Key.Subtitle] = "Обробник пакетів кутів керма",
            [Key.ProjectHeader] = "Про проєкт",
            [Key.ProjectText] = "Синхронізація віртуального автопілота BeamNG.drive з фізичним кермом.",
            [Key.LicensesHeader] = "Ліцензії",
            [Key.ModTip] = "Мод у процесі публікації",
        },
        ["by"] = new()
        {
            [Key.Title] = "SpectAPilot Handler",
            [Key.Language] = "Мова",
            [Key.About] = "Пра праграму",
            [Key.Save] = "Захаваць",
            [Key.Saved] = "Захавана.",
            [Key.SavedRunning] = "Захавана. MozaWheelControl запушчаны — перазапусціце яго, каб ужыць змены.",
            [Key.SaveFailed] = "Памылка захавання:",
            [Key.Version] = "Версія",
            [Key.ConfigMissing] = "config.json не знойдзены поруч з MozaWheelControlSettings.exe — праграма не можа працягнуць працу.",
            [Key.ConsoleMissing] = "MozaWheelControl.exe не знойдзены поруч з MozaWheelControlSettings.exe — кнопка Demo будзе недаступная.",
            [Key.BadConfig] = "config.json не з'яўляецца карэктным JSON — выпраўце файл уручную.",
            [Key.DemoStarted] = "Demo запушаны.",
            [Key.DemoFailed] = "Не атрымалася запусціць MozaWheelControl:",
            [Key.DemoNoExe] = "MozaWheelControl.exe не знойдзены поруч з MozaWheelControlSettings.exe.",
            [Key.DemoNoWindow] = "MozaWheelControl не адчыніў акно. Праверце, што exe не блокуецца антывірусам і не сканчаецца аварыйна — запусціце ўручную.",
            [Key.Subtitle] = "Апрацоўшчык пакетаў вуглоў руля",
            [Key.ProjectHeader] = "Пра праект",
            [Key.ProjectText] = "Сінхранізацыя віртуальнага аўтапілота BeamNG.drive з фізічным рулем.",
            [Key.LicensesHeader] = "Ліцэнзіі",
            [Key.ModTip] = "Мод у працэсе публікацыі",
        },
    };

    public static string T(Key key) =>
        Table.TryGetValue(Language, out var texts) && texts.TryGetValue(key, out var text)
            ? text
            : Table["en"][key];

    // Per-key field descriptions shown as ToolTips, all four languages —
    // they follow the UI language exactly like the captions (spec: tooltips
    // are localized together with the interface) and are re-applied by
    // MainForm on every language change.
    private static readonly Dictionary<string, Dictionary<string, string>> Tips = new()
    {
        ["ru"] = new(StringComparer.Ordinal)
        {
            ["debug"] = "Показывать отладочные сообщения в консоли.",
            ["smooth"] = "true — плавное движение к цели; false — максимально быстро (скорость и момент всё равно ограничены).",
            ["smoothTimeMs"] = "Чем больше — тем плавнее, но с задержкой. Рекомендуется 300–800 мс.",
            ["maxTorque"] = "Максимальный момент, % от максимума (аппаратный лимит setMotorPeakTorque, допустимо 50..100).",
            ["holdForce"] = "Сила, с которой руль держит позицию. 100% = максимальная.",
            ["holdPosition"] = "Позиция по умолчанию для удержания, градусы (используется, если текущий угол неизвестен).",
            ["socketPort"] = "UDP-порт для приёма углов от BeamNG. По умолчанию 7777.",
            ["socketHost"] = "Адрес UDP-сервера (только localhost — безопасность).",
            ["dataIntervalMs"] = "Ожидаемый интервал входных данных, мс (~30 Гц от мода). Только справочно/для отладки.",
            ["socketSmoothTimeMs"] = "Время сглаживания для команд из сокета, мс (маленькое = почти без задержки от мода).",
            ["userFullLockDeg"] = "Полный ход руля в пользовательских градусах: 90 = 1:1 с физикой базы, 180 = шкала вдвое шире.",
            ["invertSocket"] = "Инвертировать знак угла из игры, если руль едет не в ту сторону.",
            ["socketHandsMode"] = "Включает дополнительные механизмы в руль, можно безопасно держаться за него.",
            // TODO (hands mode smoothing) — proposal only, NOT implemented:
            // regular hold moves the wheel to the target with full kp/kd; in
            // hands mode, on sharp target jumps (>N degrees in <M ms) the torque
            // ramp should be limited / kp dampened for a short window so the
            // wheel doesn't yank the user's hands. Separate config flag
            // (e.g. handsMaxJerkDegPerSec or handsSoftZoneDeg), N/M to be tuned
            // on hardware. Full text — context.md, TODO #41.
            ["socketAutoStart"] = "true — UDP-сервер поднимается сам при старте (не нужно писать socket в консоли).",
            ["progressiveHold"] = "Прогрессивное усиление hold: при усилии руки сила плавно поднимается к 100%.",
            ["lockNudge"] = "У упора — мягкие ритмичные толчки 6..12% силы ~1.2 Гц вместо жёсткого втыкания в стоп.",
            ["simulate"] = "true — не трогать SDK, работать без руля (эквивалент --simulate).",
            ["maxDegPerSec"] = "Страховка от рывков: максимальная скорость изменения угла, °/с.",
            ["holdRampMs"] = "Время разгона силы удержания при включении hold, мс (чтобы не дёргало).",
            ["moveForceLimit"] = "Лимит силы для движения к цели, 0..10000 (шкала DirectInput).",
            ["maxMoveSpeedRpm"] = "Ограничение скорости мотора при движении к цели, об/мин.",
            ["forceInvert"] = "Инверсия знака момента: если руль уходит к упору вместо движения к цели — true.",
            ["kpScale"] = "Множитель жёсткости пружины hold (Kp): 1.0 = по умолчанию, меньше — мягче, больше — жёстче.",
            ["kdScale"] = "Множитель демпфера hold (Kd): 1.0 = по умолчанию, больше — демпфер жёстче.",
        },
        ["en"] = new(StringComparer.Ordinal)
        {
            ["debug"] = "Show debug messages in the console.",
            ["smooth"] = "true — smooth motion toward the target; false — as fast as possible (speed and torque are still limited).",
            ["smoothTimeMs"] = "Higher = smoother, but with more delay. Recommended: 300–800 ms.",
            ["maxTorque"] = "Peak torque, % of maximum (hardware setMotorPeakTorque limit, allowed 50..100).",
            ["holdForce"] = "How hard the wheel holds its position. 100% = maximum.",
            ["holdPosition"] = "Default hold position in degrees (used when the current angle is unknown).",
            ["socketPort"] = "UDP port for receiving angles from BeamNG. Default: 7777.",
            ["socketHost"] = "UDP server address (localhost only — security).",
            ["dataIntervalMs"] = "Expected input interval in ms (~30 Hz from the mod). Informational/debug only.",
            ["socketSmoothTimeMs"] = "Smoothing time for socket commands in ms (small = almost no delay from the mod).",
            ["userFullLockDeg"] = "Full wheel travel in user degrees: 90 = 1:1 with base physics, 180 = twice the range.",
            ["invertSocket"] = "Invert the incoming angle sign if the wheel turns the wrong way.",
            ["socketHandsMode"] = "Engages extra hold mechanisms in the wheel so you can safely hold on to it.",
            ["socketAutoStart"] = "true — the UDP server starts automatically at launch (no need to type socket in the console).",
            ["progressiveHold"] = "Progressive hold: force ramps smoothly up to 100% as resistance on the wheel increases.",
            ["lockNudge"] = "At the steering lock — soft rhythmic nudges (6–12% force, ~1.2 Hz) instead of a hard stop.",
            ["simulate"] = "true — don't touch the SDK, run without a wheel (same as --simulate).",
            ["maxDegPerSec"] = "Anti-jerk safety cap: maximum angle change rate, deg/s.",
            ["holdRampMs"] = "Hold force ramp-up time when hold engages, ms (to avoid a jerk).",
            ["moveForceLimit"] = "Force limit for moving to the target, 0–10000 (DirectInput scale).",
            ["maxMoveSpeedRpm"] = "Motor speed limit while moving to the target, RPM.",
            ["forceInvert"] = "Invert torque sign: set true if the wheel runs to the lock instead of moving to the target.",
            ["kpScale"] = "Hold spring stiffness multiplier (Kp): 1.0 = default, lower = softer, higher = stiffer.",
            ["kdScale"] = "Hold damping multiplier (Kd): 1.0 = default, higher = stronger damping.",
        },
        ["uk"] = new(StringComparer.Ordinal)
        {
            ["debug"] = "Показувати відладкові повідомлення в консолі.",
            ["smooth"] = "true — плавний рух до цілі; false — максимально швидко (швидкість і момент усе одно обмежені).",
            ["smoothTimeMs"] = "Чим більше — тим плавніше, але з затримкою. Рекомендовано 300–800 мс.",
            ["maxTorque"] = "Максимальний момент, % від максімуму (апаратний ліміт setMotorPeakTorque, допустимо 50..100).",
            ["holdForce"] = "Сила, з якою кермо тримає позицію. 100% = максимальна.",
            ["holdPosition"] = "Позиція за замовчуванням для утримання, градуси (використовується, якщо поточний кут невідомий).",
            ["socketPort"] = "UDP-порт для прийому кутів від BeamNG. За замовчуванням 7777.",
            ["socketHost"] = "Адреса UDP-сервера (тільки localhost — безпека).",
            ["dataIntervalMs"] = "Очікуваний інтервал вхідних даних, мс (~30 Гц від мода). Тільки для довідки/відладки.",
            ["socketSmoothTimeMs"] = "Час згладжування для команд із сокета, мс (маленьке = майже без затримки від мода).",
            ["userFullLockDeg"] = "Повний хід керма у користувацьких градусах: 90 = 1:1 з фізикою бази, 180 = шкала вдвічі ширша.",
            ["invertSocket"] = "Інвертувати знак кута з гри, якщо кермо їде не в той бік.",
            ["socketHandsMode"] = "Вмикає додаткові механізми в кермі, можна безпечно триматися за нього.",
            ["socketAutoStart"] = "true — UDP-сервер піднімається сам при старті (не потрібно писати socket в консолі).",
            ["progressiveHold"] = "Прогресивне посилення hold: при зусиллі на кермо сила плавно піднімається до 100%.",
            ["lockNudge"] = "У упору — м'які ритмічні штовхання 6..12% сили ~1,2 Гц замість жорсткого впикання в стоп.",
            ["simulate"] = "true — не чіпати SDK, працювати без керма (еквівалент --simulate).",
            ["maxDegPerSec"] = "Захист від ривків: максимальна швидкість зміни кута, °/с.",
            ["holdRampMs"] = "Час розгону сили утримання при вмиканні hold, мс (щоб не дертало).",
            ["moveForceLimit"] = "Ліміт сили для руху до цілі, 0..10000 (шкала DirectInput).",
            ["maxMoveSpeedRpm"] = "Обмеження швидкості мотора при русі до цілі, об/хв.",
            ["forceInvert"] = "Інверсія знака моменту: якщо кермо йде до упору замість руху до цілі — true.",
            ["kpScale"] = "Множник жорсткості пружини hold (Kp): 1.0 = за замовчуванням, менше — м'якше, більше — жорсткіше.",
            ["kdScale"] = "Множник демпфера hold (Kd): 1.0 = за замовчуванням, більше — демпфер жорсткіший.",
        },
        ["by"] = new(StringComparer.Ordinal)
        {
            ["debug"] = "Паказваць адладачныя паведамленні ў кансолі.",
            ["smooth"] = "true — плаўны рух да мэты; false — максімальна хутка (хуткасць і момант усё абмежаваны).",
            ["smoothTimeMs"] = "Чым больш — тым плаўней, але з затрымкам. Рэкамендуецца 300–800 мс.",
            ["maxTorque"] = "Максімальны момант, % ад максімуму (апаратны ліміт setMotorPeakTorque, дазволена 50..100).",
            ["holdForce"] = "Сіла, з якой руль трымае пазіцыю. 100% = максімальная.",
            ["holdPosition"] = "Пазіцыя па змаўчанні для ўтрымання, градусы (выкарыстоўваецца, калі бягучы кут невядомы).",
            ["socketPort"] = "UDP-порт для прыёму кутоў ад BeamNG. Па змаўчанні 7777.",
            ["socketHost"] = "Адрас UDP-сэрвера (толькі localhost — бяспека).",
            ["dataIntervalMs"] = "Чаканы інтэрвал уваходных дадзеных, мс (~30 Гц ад мода). Толькі для даведкі/адладкі.",
            ["socketSmoothTimeMs"] = "Час згладжвання для каманд з сокета, мс (маленькі = амаль без затрымкі ад мода).",
            ["userFullLockDeg"] = "Поўны ход руля ў карыстацкіх градусах: 90 = 1:1 з фізікай базы, 180 = шкала ўдвойчы шырэй.",
            ["invertSocket"] = "Інвертаваць знак кута з гульні, калі руль едзе не ў той бок.",
            ["socketHandsMode"] = "Уключае дадатковыя механізмы ў рулі, можна бяспечна трымацца за яго.",
            ["socketAutoStart"] = "true — UDP-сэрвер падымаецца сам пры старце (не трэба пісаць socket у кансолі).",
            ["progressiveHold"] = "Прагрэсіўнае ўзмацненне hold: пры намаганні на руль сіла плаўна падымаецца да 100%.",
            ["lockNudge"] = "У ўпора — мяккія рытмічныя штуршкі 6..12% сілы ~1,2 Гц замест жорсткага ўціскання ў стоп.",
            ["simulate"] = "true — не чапаць SDK, працаваць без руля (эквівалент --simulate).",
            ["maxDegPerSec"] = "Абарона ад рывкоў: максімальная хуткасць змены кута, °/с.",
            ["holdRampMs"] = "Час разгону сілы ўтрымання пры ўключэнні hold, мс (каб не драла).",
            ["moveForceLimit"] = "Ліміт сілы для руху да мэты, 0..10000 (шкала DirectInput).",
            ["maxMoveSpeedRpm"] = "Абмежаванне хуткасці матора пры руху да мэты, абар/хв.",
            ["forceInvert"] = "Інверсія знака моманта: калі руль ідзе да ўпора замест руху да мэты — true.",
            ["kpScale"] = "Множнік калянасці пружыны hold (Kp): 1.0 = па змаўчанні, менш — мягчэй, больш — жорстчей.",
            ["kdScale"] = "Множнік дэмпфера hold (Kd): 1.0 = па змаўчанні, больш — дэмпфер жорстчей.",
        },
    };

    // Fallback prefix for a config key without a description (per language).
    private static readonly Dictionary<string, string> FallbackTipWord = new(StringComparer.Ordinal)
    {
        ["ru"] = "Настройка",
        ["en"] = "Setting",
        ["uk"] = "Налаштування",
        ["by"] = "Наладка",
    };

    // Localized tooltip for a config key; an unknown key falls back to
    // "<word>: key" in the current language (never empty).
    public static string KeyTip(string key) =>
        Tips.TryGetValue(Language, out var byKey) && byKey.TryGetValue(key, out var tip)
            ? tip
            : $"{(FallbackTipWord.TryGetValue(Language, out var word) ? word : "Setting")}: {key}";

    // Human-readable field captions per language (Step 5, item 4). Keyed by
    // the raw config key; ConfigLabel falls back to the key itself so an
    // unknown/new key never breaks the UI.
    private static readonly Dictionary<string, Dictionary<string, string>> Captions = new()
    {
        ["ru"] = new(StringComparer.Ordinal)
        {
            ["debug"] = "Отладка",
            ["smooth"] = "Плавное движение",
            ["smoothTimeMs"] = "Время сглаживания, мс",
            ["maxTorque"] = "Макс. момент, %",
            ["holdForce"] = "Сила удержания, %",
            ["holdPosition"] = "Позиция удержания, °",
            ["socketPort"] = "Порт сокета",
            ["socketHost"] = "Адрес сокета",
            ["dataIntervalMs"] = "Интервал данных, мс",
            ["socketSmoothTimeMs"] = "Сглаживание сокета, мс",
            ["userFullLockDeg"] = "Полный ход, °",
            ["invertSocket"] = "Инверсия сокета",
            ["socketHandsMode"] = "Режим: держи меня за руль",
            ["socketAutoStart"] = "Автостарт сокета",
            ["progressiveHold"] = "Прогрессивное удержание",
            ["lockNudge"] = "Толчки у упора",
            ["simulate"] = "Симуляция",
            ["maxDegPerSec"] = "Макс. скорость, °/с",
            ["holdRampMs"] = "Разгон удержания, мс",
            ["moveForceLimit"] = "Лимит силы движения",
            ["maxMoveSpeedRpm"] = "Макс. скорость движения, об/мин",
            ["forceInvert"] = "Инверсия силы",
            ["kpScale"] = "Жёсткость пружины (Kp)",
            ["kdScale"] = "Демпфер (Kd)",
        },
        ["en"] = new(StringComparer.Ordinal)
        {
            ["debug"] = "Debug",
            ["smooth"] = "Smooth motion",
            ["smoothTimeMs"] = "Smooth time, ms",
            ["maxTorque"] = "Max torque, %",
            ["holdForce"] = "Hold force, %",
            ["holdPosition"] = "Hold position, °",
            ["socketPort"] = "Socket port",
            ["socketHost"] = "Socket host",
            ["dataIntervalMs"] = "Data interval, ms",
            ["socketSmoothTimeMs"] = "Socket smooth, ms",
            ["userFullLockDeg"] = "Full lock, °",
            ["invertSocket"] = "Invert socket",
            ["socketHandsMode"] = "Mode: hold my wheel",
            ["socketAutoStart"] = "Auto-start socket",
            ["progressiveHold"] = "Progressive hold",
            ["lockNudge"] = "Lock nudge",
            ["simulate"] = "Simulate",
            ["maxDegPerSec"] = "Max speed, °/s",
            ["holdRampMs"] = "Hold ramp, ms",
            ["moveForceLimit"] = "Move force limit",
            ["maxMoveSpeedRpm"] = "Max move speed, rpm",
            ["forceInvert"] = "Invert force",
            ["kpScale"] = "Spring stiffness (Kp)",
            ["kdScale"] = "Damping (Kd)",
        },
        ["uk"] = new(StringComparer.Ordinal)
        {
            ["debug"] = "Налагодження",
            ["smooth"] = "Плавний рух",
            ["smoothTimeMs"] = "Час згладжування, мс",
            ["maxTorque"] = "Макс. крутний момент, %",
            ["holdForce"] = "Сила утримання, %",
            ["holdPosition"] = "Позиція утримання, °",
            ["socketPort"] = "Порт сокета",
            ["socketHost"] = "Адреса сокета",
            ["dataIntervalMs"] = "Інтервал даних, мс",
            ["socketSmoothTimeMs"] = "Згладжування сокета, мс",
            ["userFullLockDeg"] = "Повний хід, °",
            ["invertSocket"] = "Інверсія сокета",
            ["socketHandsMode"] = "Режим: тримай мене за кермо",
            ["socketAutoStart"] = "Автостарт сокета",
            ["progressiveHold"] = "Прогресивне утримання",
            ["lockNudge"] = "Штуршки в упорі",
            ["simulate"] = "Симуляція",
            ["maxDegPerSec"] = "Макс. швидкість, °/с",
            ["holdRampMs"] = "Розгін утримання, мс",
            ["moveForceLimit"] = "Ліміт сили руху",
            ["maxMoveSpeedRpm"] = "Макс. швидкість руху, об/хв",
            ["forceInvert"] = "Інверсія сили",
            ["kpScale"] = "Жорсткість пружини (Kp)",
            ["kdScale"] = "Амортизація (Kd)",
        },
        ["by"] = new(StringComparer.Ordinal)
        {
            ["debug"] = "Адладка",
            ["smooth"] = "Плаўны рух",
            ["smoothTimeMs"] = "Час згладжвання, мс",
            ["maxTorque"] = "Макс. момант, %",
            ["holdForce"] = "Сіла ўтрымання, %",
            ["holdPosition"] = "Пазіцыя ўтрымання, °",
            ["socketPort"] = "Порт сокета",
            ["socketHost"] = "Адрас сокета",
            ["dataIntervalMs"] = "Інтэрвал дадзеных, мс",
            ["socketSmoothTimeMs"] = "Згладжванне сокета, мс",
            ["userFullLockDeg"] = "Поўны ход, °",
            ["invertSocket"] = "Інверсія сокета",
            ["socketHandsMode"] = "Рэжым: трымай мяне за руль",
            ["socketAutoStart"] = "Аўтастарт сокета",
            ["progressiveHold"] = "Прагрэсіўнае ўтрыманне",
            ["lockNudge"] = "Штуршкі ў упоры",
            ["simulate"] = "Сімуляцыя",
            ["maxDegPerSec"] = "Макс. хуткасць, °/с",
            ["holdRampMs"] = "Разгон утрымання, мс",
            ["moveForceLimit"] = "Ліміт сілы руху",
            ["maxMoveSpeedRpm"] = "Макс. хуткасць руху, аб/хв",
            ["forceInvert"] = "Інверсія сілы",
            ["kpScale"] = "Жорсткасць пружыны (Kp)",
            ["kdScale"] = "Амартызацыя (Kd)",
        },
    };

    // Caption for a config field label in the current language; the raw key is
    // the fallback so unknown keys still render.
    public static string ConfigLabel(string key) =>
        Captions.TryGetValue(Language, out var byKey) && byKey.TryGetValue(key, out var label)
            ? label
            : key;

    // Apply the code stored in config.json; unknown or missing values fall back to the default.
    public static void Load(string? code)
    {
        if (code is not null && Table.ContainsKey(code)) Language = code;
    }

    public static void Set(string code)
    {
        if (Table.ContainsKey(code)) Language = code;
    }
}
