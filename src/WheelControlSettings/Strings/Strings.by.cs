namespace WheelControlSettings;

// Belarusian UI texts: table entries, field tooltips, captions, fallback word.
// Entries are copied verbatim from the original combined file.
internal static partial class Strings
{
    private static class By
    {
        internal static readonly Dictionary<Key, string> Table = new()
        {
            [Key.Title] = "SpectAPilot Handler",
            [Key.Language] = "Мова",
            [Key.About] = "Пра праграму",
            [Key.Save] = "Захаваць",
            [Key.Saved] = "Захавана.",
            [Key.SavedRunning] = "Захавана. WheelControl запушчаны — перазапусціце яго, каб ужыць змены.",
            [Key.SaveFailed] = "Памылка захавання:",
            [Key.Version] = "Версія",
            [Key.ConfigMissing] = "config.json не знойдзены поруч з WheelControlSettings.exe — праграма не можа працягнуць працу.",
            [Key.ConsoleMissing] = "WheelControl.exe не знойдзены поруч з WheelControlSettings.exe — кнопка Demo будзе недаступная.",
            [Key.BadConfig] = "config.json не з'яўляецца карэктным JSON — выпраўце файл уручную.",
            [Key.DemoStarted] = "Demo запушаны.",
            [Key.DemoFailed] = "Не атрымалася запусціць WheelControl:",
            [Key.DemoNoExe] = "WheelControl.exe не знойдзены поруч з WheelControlSettings.exe.",
            [Key.DemoNoWindow] = "WheelControl не адчыніў акно. Праверце, што exe не блокуецца антывірусам і не сканчаецца аварыйна — запусціце ўручную.",
            [Key.Subtitle] = "Апрацоўшчык пакетаў вуглоў руля",
            [Key.ProjectHeader] = "Пра праект",
            [Key.ProjectText] = "Сінхранізацыя віртуальнага аўтапілота BeamNG.drive з фізічным рулем.",
            [Key.LicensesHeader] = "Ліцэнзіі",
            [Key.ModTip] = "Мод у працэсе публікацыі",
        };

        internal static readonly Dictionary<string, string> Tips = new(StringComparer.Ordinal)
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
        };

        internal const string FallbackTipWord = "Наладка";

        internal static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
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
        };
    }
}
