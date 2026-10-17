namespace WallpaperSwitch.Services;

internal static class CrashLog
{
    public static string FilePath { get; } = GetFilePath();

    private static string GetFilePath()
    {
        var testSettingsFile = Environment.GetEnvironmentVariable("WALLPAPERSWITCH_TEST_SETTINGS_FILE");
        if (!string.IsNullOrWhiteSpace(testSettingsFile) && Path.IsPathFullyQualified(testSettingsFile))
        {
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(testSettingsFile))!, "startup.log");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallpaperSwitch",
            "startup.log");
    }

    public static void Write(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(FilePath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
