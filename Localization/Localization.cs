using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Globalization;

namespace WallpaperSwitch.Localization;

/// <summary>
/// App string lookup backed by the Windows App SDK MRT Core resource index.
/// </summary>
public static class Localizer
{
    private static readonly Lazy<ResourceManager> Manager = new(() => new ResourceManager());
    private static readonly Lazy<ResourceMap> Strings = new(() => Manager.Value.MainResourceMap.GetSubtree("Resources"));

    public static string LanguagePreference { get; private set; } = LanguageCatalog.SystemLanguage;

    public static IReadOnlyList<LanguageOption> LanguageOptions { get; } = LanguageCatalog.Languages;

    public static bool IsRightToLeft => LanguageCatalog.IsRightToLeft(LanguagePreference);

    public static void Initialize(string? language)
    {
        LanguagePreference = LanguageCatalog.NormalizePreference(language);
        ApplicationLanguages.PrimaryLanguageOverride = LanguagePreference == LanguageCatalog.SystemLanguage
            ? string.Empty
            : LanguagePreference;

        if (LanguagePreference != LanguageCatalog.SystemLanguage)
        {
            var culture = LanguageCatalog.GetFormattingCulture(LanguagePreference);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
    }

    public static string GetString(string key)
    {
        try
        {
            var context = Manager.Value.CreateResourceContext();
            if (LanguagePreference != LanguageCatalog.SystemLanguage)
            {
                context.QualifierValues["Language"] = LanguagePreference;
            }

            try
            {
                // Resource identifiers use slash-delimited paths for each dotted
                // segment when queried through MRT Core's ResourceMap API.
                return Strings.Value.GetValue(ToResourcePath(key), context).ValueAsString;
            }
            catch
            {
                var fallback = Manager.Value.CreateResourceContext();
                fallback.QualifierValues["Language"] = LanguageCatalog.DefaultLanguage;
                return Strings.Value.GetValue(ToResourcePath(key), fallback).ValueAsString;
            }
        }
        catch
        {
            return key;
        }
    }

    private static string ToResourcePath(string key) => key.Replace('.', '/');

    public static string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, GetString(key), arguments);
}
