namespace WheelControlSettings;

// Ukrainian UI texts: table entries, field tooltips, captions, fallback word.
// Entries are copied verbatim from the original combined file.
internal static partial class Strings
{
    private static class Uk
    {
        internal static readonly Dictionary<Key, string> Table = new()
        {
            [Key.Title] = "SpectAPilot Handler",
            [Key.Language] = "Мова",
            [Key.About] = "Про програму",
            [Key.Save] = "Зберегти",
            [Key.Saved] = "Збережено.",
            [Key.SavedRunning] = "Збережено. WheelControl запущено — перезапустіть його, щоб застосувати зміни.",
            [Key.SaveFailed] = "Помилка збереження:",
            [Key.Version] = "Версія",
            [Key.ConfigMissing] = "config.json не знайдено поруч із WheelControlSettings.exe — програма не може продовжити роботу.",
            [Key.ConsoleMissing] = "WheelControl.exe не знайдено поруч із WheelControlSettings.exe — кнопка Demo буде недоступна.",
            [Key.BadConfig] = "config.json не є коректним JSON — виправте файл вручну.",
            [Key.DemoStarted] = "Demo запущено.",
            [Key.DemoFailed] = "Не вдалося запустити WheelControl:",
            [Key.DemoNoExe] = "WheelControl.exe не знайдено поруч із WheelControlSettings.exe.",
            [Key.DemoNoWindow] = "WheelControl не відкрив вікно. Перевірте, що exe не блокується антивірусом або не аварійно завершується — запустіть вручну.",
            [Key.Subtitle] = "Обробник пакетів кутів керма",
            [Key.ProjectHeader] = "Про проєкт",
            [Key.ProjectText] = "Синхронізація віртуального автопілота BeamNG.drive з фізичним кермом.",
            [Key.LicensesHeader] = "Ліцензії",
            [Key.ModTip] = "Мод у процесі публікації",
        };

        internal static readonly Dictionary<string, string> Tips = new(StringComparer.Ordinal)
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
        };

        internal const string FallbackTipWord = "Налаштування";

        internal static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
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
        };
    }
}
