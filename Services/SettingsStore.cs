using System.Text.Json;
using System.Text.Json.Serialization;
using WallpaperSwitch.Models;

namespace WallpaperSwitch.Services;

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppSettings Current { get; private set; } = new();

    public static string FilePath { get; } = GetFilePath();

    private static string GetFilePath()
    {
        var testOverride = Environment.GetEnvironmentVariable("WALLPAPERSWITCH_TEST_SETTINGS_FILE");
        if (!string.IsNullOrWhiteSpace(testOverride) && Path.IsPathFullyQualified(testOverride))
        {
            return Path.GetFullPath(testOverride);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallpaperSwitch",
            "settings.json");
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Current = new AppSettings();
                return;
            }

            var json = File.ReadAllText(FilePath);
            Current = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            MigrateFromLegacy(Current);
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public static void Save()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, Options));
    }

    private static void MigrateFromLegacy(AppSettings settings)
    {
        var hasLegacyWallpaper = !string.IsNullOrWhiteSpace(settings.LightWallpaperPath)
            || !string.IsNullOrWhiteSpace(settings.DarkWallpaperPath);
        var periodsEmpty = string.IsNullOrWhiteSpace(settings.Dawn.WallpaperPath)
            && string.IsNullOrWhiteSpace(settings.Day.WallpaperPath)
            && string.IsNullOrWhiteSpace(settings.Dusk.WallpaperPath)
            && string.IsNullOrWhiteSpace(settings.Night.WallpaperPath);

        if (!hasLegacyWallpaper || !periodsEmpty)
        {
            return;
        }

        settings.Dawn.WallpaperPath = settings.LightWallpaperPath;
        settings.Day.WallpaperPath = settings.LightWallpaperPath;
        settings.Dusk.WallpaperPath = settings.DarkWallpaperPath;
        settings.Night.WallpaperPath = settings.DarkWallpaperPath;
        settings.Dawn.UseLightTheme = true;
        settings.Day.UseLightTheme = true;
        settings.Dusk.UseLightTheme = false;
        settings.Night.UseLightTheme = false;
        settings.Dawn.StartTime = settings.LightStartTime;
        settings.Day.StartTime = settings.LightStartTime.AddHours(2);
        settings.Dusk.StartTime = settings.DarkStartTime.AddHours(-1);
        settings.Night.StartTime = settings.DarkStartTime;
    }
}
