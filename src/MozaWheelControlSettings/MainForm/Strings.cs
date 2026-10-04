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
            [Key.Title] = "MozaWheelControl — настройки",
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
        },
        ["en"] = new()
        {
            [Key.Title] = "MozaWheelControl Settings",
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
        },
        ["uk"] = new()
        {
            [Key.Title] = "MozaWheelControl — налаштування",
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
        },
        ["by"] = new()
        {
            [Key.Title] = "MozaWheelControl — налады",
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
        },
    };

    public static string T(Key key) =>
        Table.TryGetValue(Language, out var texts) && texts.TryGetValue(key, out var text)
            ? text
            : Table["en"][key];

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
