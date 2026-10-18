namespace WallpaperSwitch.Services;

internal static class SingleInstance
{
    private const string DefaultMutexName = @"Local\WallpaperSwitch.SingleInstance";
    private const string DefaultShowEventName = @"Local\WallpaperSwitch.ShowWindow";

    private static string MutexName => GetTestScopedName(DefaultMutexName);
    private static string ShowEventName => GetTestScopedName(DefaultShowEventName);

    private static Mutex? _mutex;
    private static bool _ownsMutex;

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
        // Open the signal before competing for ownership, so a foreground launch
        // cannot arrive between mutex acquisition and event creation and get lost.
        ShowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _mutex = new Mutex(false, MutexName);
        try { _ownsMutex = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsMutex = true; }
        if (!_ownsMutex)
        {
            try
            {
                if (showExisting)
                {
                    ShowEvent.Set();
                }
            }
            catch
            {
                // The other process may be shutting down.
            }

            _mutex.Dispose();
            _mutex = null;
            ShowEvent.Dispose();
            ShowEvent = null;
            return false;
        }

        return true;
    }

    public static void Release()
    {
        ShowEvent?.Dispose();
        ShowEvent = null;
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _ownsMutex = false;
        _mutex?.Dispose();
        _mutex = null;
    }
}
