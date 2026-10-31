namespace WheelControlSettings;

// Russian UI texts: table entries, field tooltips, captions, fallback word.
// Entries are copied verbatim from the original combined file.
internal static partial class Strings
{
    private static class Ru
    {
        internal static readonly Dictionary<Key, string> Table = new()
        {
            [Key.Title] = "Настройки обработчика",
            [Key.Language] = "Язык",
            [Key.About] = "О программе",
            [Key.Save] = "Сохранить",
            [Key.Saved] = "Сохранено.",
            [Key.SavedRunning] = "Сохранено. WheelControl запущен — перезапустите его, чтобы применить изменения.",
            [Key.SaveFailed] = "Ошибка сохранения:",
            [Key.Version] = "Версия",
            [Key.ConfigMissing] = "config.json не найден в подпапке Assets рядом с WheelControlSettings.exe — приложение не может продолжить работу.",
            [Key.ConsoleMissing] = "WheelControl.exe не найден рядом с WheelControlSettings.exe — кнопка Демо будет недоступна.",
            [Key.BadConfig] = "config.json не является корректным JSON — исправьте файл вручную.",
            [Key.Demo] = "Демо",
            [Key.DemoStarted] = "Демо запущен.",
            [Key.DemoFailed] = "Не удалось запустить WheelControl:",
            [Key.DemoNoExe] = "WheelControl.exe не найден рядом с WheelControlSettings.exe.",
            [Key.DemoNoWindow] = "WheelControl не открыл окно. Проверьте, что exe не блокируется антивирусом или не крашится — запустите вручную.",
            // Section titles (single-screen rework; keys keep the Tab* prefix).
            [Key.TabGeneral] = "Общие",
            [Key.TabWheel] = "Руль",
            [Key.TabHold] = "Удержание",
            [Key.TabMove] = "Движение",
            [Key.TabSocket] = "Сокет",
            [Key.Subtitle] = "Обработчик пакетов углов руля",
            [Key.ProjectHeader] = "Об проекте",
            [Key.ProjectText] = "Синхронизация виртуального автопилота BeamNG.drive с физическим рулём.",
            [Key.LicensesHeader] = "Лицензии",
            [Key.ModTip] = "Мод в процессе публикации",
        };

        internal static readonly Dictionary<string, string> Tips = new(StringComparer.Ordinal)
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
        };

        internal const string FallbackTipWord = "Настройка";

        internal static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
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
        };
    }
}
