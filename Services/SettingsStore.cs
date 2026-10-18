using WallpaperSwitch.Models;

namespace WallpaperSwitch.Services;

internal static class SettingsStore
{
    public static AppSettings Current { get; private set; } = new();

    public static string FilePath { get; } = GetFilePath();
    private static readonly SettingsFile FileStore = new(FilePath);
    private static readonly HashSet<Task> PendingWrites = new();

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

    public static async Task LoadAsync()
    {
        try
        {
            Current = await FileStore.LoadAsync();
            MigrateFromLegacy(Current);
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public static Task SaveAsync()
    {
        var write = FileStore.SaveAsync(Current);
        lock (PendingWrites) PendingWrites.Add(write);
        _ = ObserveWriteAsync(write);
        return write;
    }

    private static async Task ObserveWriteAsync(Task write)
    {
        try { await write; }
        catch { /* The initiating operation reports the failure to the user. */ }
        finally { lock (PendingWrites) PendingWrites.Remove(write); }
    }

    public static Task FlushAsync()
    {
        lock (PendingWrites) return Task.WhenAll(PendingWrites.ToArray());
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
