using System.Globalization;
using System.Resources;

namespace WallpaperSwitch.StartupHost;

/// <summary>
/// ResourceManager-based text for the standalone startup helper. It uses only
/// resources shipped beside this protected helper, never the user's profile.
/// </summary>
internal static class HostText
{
    private static readonly ResourceManager Resources = new(
        "WallpaperSwitch.StartupHost.HostText",
        typeof(HostText).Assembly);

    public static void SelectCulture(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)
            || string.Equals(language, "system", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var formattingCultureName = language switch
            {
                "zh-Hans" => "zh-CN",
                "zh-Hant" => "zh-TW",
                _ => language
            };
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(formattingCultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            // Invalid command-line tags retain the operating-system culture.
        }
    }

    public static string Get(string name) => Resources.GetString(name, CultureInfo.CurrentUICulture)
        ?? Resources.GetString(name, CultureInfo.GetCultureInfo("en-US"))
        ?? name;

    public static string Format(string name, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(name), arguments);
}
