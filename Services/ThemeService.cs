using Microsoft.Win32;
using WallpaperSwitch.Native;
using WallpaperSwitch.Localization;

namespace WallpaperSwitch.Services;

internal static class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool AppsUseLightTheme() => ReadDword("AppsUseLightTheme", defaultValue: 1) != 0;

    public static bool SystemUsesLightTheme() => ReadDword("SystemUsesLightTheme", defaultValue: 1) != 0;

    public static void Apply(bool lightApps, bool lightSystem)
    {
        using var key = Registry.CurrentUser.CreateSubKey(PersonalizeKey, writable: true)
            ?? throw new InvalidOperationException(Localizer.GetString("Error.ThemeRegistry"));

        key.SetValue("AppsUseLightTheme", lightApps ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("SystemUsesLightTheme", lightSystem ? 1 : 0, RegistryValueKind.DWord);

        try
        {
            NativeMethods.RefreshImmersiveColorPolicyState();
        }
        catch
        {
            // Undocumented export; ignore on systems that do not provide it.
        }

        try
        {
            NativeMethods.FlushMenuThemes();
        }
        catch
        {
            // Same as above.
        }

        NativeMethods.SendMessageTimeout(
            NativeMethods.HwndBroadcast,
            NativeMethods.WmSettingChange,
            UIntPtr.Zero,
            "ImmersiveColorSet",
            NativeMethods.SmtoAbortIfHung,
            1000,
            out _);
    }

    private static int ReadDword(string name, int defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            var value = key?.GetValue(name, defaultValue);
            return value is int number ? number : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}
