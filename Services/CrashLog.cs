using System.Threading.Channels;

namespace WallpaperSwitch.Services;

internal static class CrashLog
{
    private static readonly Channel<string> Entries = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    static CrashLog() => _ = Task.Run(WriteEntriesAsync);
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
        => Entries.Writer.TryWrite($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");

    private static async Task WriteEntriesAsync()
    {
        await foreach (var entry in Entries.Reader.ReadAllAsync())
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                await File.AppendAllTextAsync(FilePath, entry);
            }
            catch { /* Diagnostics must never block or crash the app. */ }
        }
    }
}
