using System.Text.Json;

public enum UiLanguage
{
    English,
    Spanish,
    German,
    Portuguese,
    Italian
}

public sealed class UserPreferences
{
    public UiLanguage Language { get; set; } = UiLanguage.English;
    public bool IsDarkMode { get; set; }

    private static string PreferencesPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Robin", "settings.json");

    public static UserPreferences Load()
    {
        var path = PreferencesPath;
        if (!File.Exists(path))
            return new UserPreferences();

        var preferences = JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Robin preferences could not be read.");

        if (!Enum.IsDefined(preferences.Language))
            throw new InvalidDataException("Robin preferences contain an unsupported language.");

        return preferences;
    }

    public void Save()
    {
        var path = PreferencesPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
