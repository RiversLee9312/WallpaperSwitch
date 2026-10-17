using System.Globalization;

namespace WallpaperSwitch.Localization;

public sealed record LanguageOption(string Tag, string DisplayName);

/// <summary>
/// Pure language metadata shared with the tests. Resource lookup itself is handled by MRT Core.
/// </summary>
public static class LanguageCatalog
{
    public const string SystemLanguage = "system";
    public const string DefaultLanguage = "en-US";

    public static IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new(SystemLanguage, "System default"),
        new("en-US", "English (United States)"),
        new("zh-Hans", "简体中文"),
        new("zh-Hant", "繁體中文"),
        new("ja-JP", "日本語"),
        new("ko-KR", "한국어"),
        new("de-DE", "Deutsch"),
        new("fr-FR", "Français"),
        new("es-ES", "Español"),
        new("it-IT", "Italiano"),
        new("pt-BR", "Português (Brasil)"),
        new("ru-RU", "Русский"),
        new("uk-UA", "Українська"),
        new("pl-PL", "Polski"),
        new("nl-NL", "Nederlands"),
        new("sv-SE", "Svenska"),
        new("da-DK", "Dansk"),
        new("fi-FI", "Suomi"),
        new("tr-TR", "Türkçe"),
        new("ar-SA", "العربية"),
        new("he-IL", "עברית"),
        new("hi-IN", "हिन्दी"),
        new("id-ID", "Bahasa Indonesia"),
        new("th-TH", "ไทย"),
        new("vi-VN", "Tiếng Việt")
    ];

    private static readonly IReadOnlyDictionary<string, string> FormattingCultures =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh-Hans"] = "zh-CN",
            ["zh-Hant"] = "zh-TW"
        };

    public static string NormalizePreference(string? language) =>
        Languages.Any(option => string.Equals(option.Tag, language, StringComparison.OrdinalIgnoreCase))
            ? Languages.First(option => string.Equals(option.Tag, language, StringComparison.OrdinalIgnoreCase)).Tag
            : SystemLanguage;

    public static CultureInfo GetFormattingCulture(string language, CultureInfo? systemCulture = null)
    {
        var preference = NormalizePreference(language);
        if (preference == SystemLanguage)
        {
            return systemCulture ?? CultureInfo.CurrentCulture;
        }

        var cultureName = FormattingCultures.TryGetValue(preference, out var mapped)
            ? mapped
            : preference;
        return CultureInfo.GetCultureInfo(cultureName);
    }

    public static IReadOnlyList<string> GetFallbackLanguages(string language)
    {
        var preference = NormalizePreference(language);
        if (preference == SystemLanguage)
        {
            return [DefaultLanguage];
        }

        var result = new List<string> { preference };
        var separator = preference.IndexOf('-');
        if (separator > 0)
        {
            result.Add(preference[..separator]);
        }

        if (!result.Contains(DefaultLanguage, StringComparer.OrdinalIgnoreCase))
        {
            result.Add(DefaultLanguage);
        }

        return result;
    }

    public static bool IsRightToLeft(string language, CultureInfo? systemCulture = null) =>
        (NormalizePreference(language) == SystemLanguage
            ? systemCulture ?? CultureInfo.CurrentUICulture
            : GetFormattingCulture(language)).TextInfo.IsRightToLeft;
}
