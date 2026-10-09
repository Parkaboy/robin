using System.Globalization;
using System.Resources;
using System.Reflection;

public static class UiText
{
    private static readonly ResourceManager Resources = new("Robin.Resources.Strings", Assembly.GetExecutingAssembly());
    private static readonly IReadOnlyDictionary<UiLanguage, CultureInfo> Cultures = new Dictionary<UiLanguage, CultureInfo>
    {
        [UiLanguage.English] = CultureInfo.GetCultureInfo("en-US"),
        [UiLanguage.Spanish] = CultureInfo.GetCultureInfo("es-ES"),
        [UiLanguage.German] = CultureInfo.GetCultureInfo("de-DE"),
        [UiLanguage.Portuguese] = CultureInfo.GetCultureInfo("pt-PT"),
        [UiLanguage.Italian] = CultureInfo.GetCultureInfo("it-IT")
    };

    public static UiLanguage Language { get; private set; } = UiLanguage.English;

    static UiText()
    {
        ValidateResourceKeys();
    }

    public static void SetLanguage(UiLanguage language)
    {
        if (!Cultures.ContainsKey(language))
            throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported UI language.");

        Language = language;
    }

    public static string Get(string key) =>
        Resources.GetString(key, Cultures[Language])
            ?? throw new KeyNotFoundException($"Translation '{key}' is missing for {Language}.");

    public static string Format(string key, params object?[] arguments) =>
        string.Format(Cultures[Language], Get(key), arguments);

    public static string LanguageName(UiLanguage language) => language switch
    {
        UiLanguage.English => GetForLanguage(language, "LanguageEnglish"),
        UiLanguage.Spanish => GetForLanguage(language, "LanguageSpanish"),
        UiLanguage.German => GetForLanguage(language, "LanguageGerman"),
        UiLanguage.Portuguese => GetForLanguage(language, "LanguagePortuguese"),
        UiLanguage.Italian => GetForLanguage(language, "LanguageItalian"),
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported UI language.")
    };

    private static string GetForLanguage(UiLanguage language, string key) =>
        Resources.GetString(key, Cultures[language])
            ?? throw new KeyNotFoundException($"Translation '{key}' is missing for {language}.");

    private static void ValidateResourceKeys()
    {
        var english = Resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)
            ?? throw new InvalidOperationException("The neutral UI resource could not be found.");
        var englishKeys = english.Cast<System.Collections.DictionaryEntry>()
            .Select(entry => (string)entry.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (var (language, culture) in Cultures.Where(entry => entry.Key != UiLanguage.English))
        {
            var resourceSet = Resources.GetResourceSet(culture, true, false)
                ?? throw new InvalidOperationException($"The UI resource for {language} could not be found.");
            var resourceKeys = resourceSet.Cast<System.Collections.DictionaryEntry>()
                .Select(entry => (string)entry.Key)
                .Order(StringComparer.Ordinal);

            if (!resourceKeys.SequenceEqual(englishKeys, StringComparer.Ordinal))
                throw new InvalidDataException($"The {language} translations do not match the English resource keys.");
        }
    }
}
