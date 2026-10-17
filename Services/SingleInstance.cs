namespace WallpaperSwitch.Services;

internal static class SingleInstance
{
    private const string DefaultMutexName = @"Local\WallpaperSwitch.SingleInstance";
    private const string DefaultShowEventName = @"Local\WallpaperSwitch.ShowWindow";

    private static string MutexName => GetTestScopedName(DefaultMutexName);
    private static string ShowEventName => GetTestScopedName(DefaultShowEventName);

    private static Mutex? _mutex;

    public static EventWaitHandle? ShowEvent { get; private set; }

    private static string GetTestScopedName(string defaultName)
    {
        var instance = Environment.GetEnvironmentVariable("WALLPAPERSWITCH_TEST_INSTANCE");
        if (string.IsNullOrWhiteSpace(instance)) return defaultName;
        var safe = new string(instance.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_').Take(48).ToArray());
        return safe.Length == 0 ? defaultName : $"{defaultName}.{safe}";
    }

    public static bool TryStart(bool showExisting = true)
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            try
            {
                if (showExisting)
                {
                    using var existing = EventWaitHandle.OpenExisting(ShowEventName);
                    existing.Set();
                }
            }
            catch
            {
                // The other process may be shutting down.
            }

            return false;
        }

        ShowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        return true;
    }
}
