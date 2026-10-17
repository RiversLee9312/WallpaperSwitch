namespace WallpaperSwitch.Startup;

public enum StartupMode { Disabled, Standard, HighPriority }

internal interface IStartupBackend
{
    bool StandardEnabled { get; }
    bool HighPriorityEnabled { get; }
    void SetStandard(bool enabled);
    Task SetHighPriorityAsync(bool enabled);
}

internal sealed class StartupModeController(IStartupBackend backend)
{
    public async Task SetModeAsync(StartupMode mode)
    {
        var oldStandard = backend.StandardEnabled;
        var oldHigh = backend.HighPriorityEnabled;
        var highChanged = false;
        try
        {
            if (mode == StartupMode.HighPriority)
            {
                await backend.SetHighPriorityAsync(true);
                highChanged = !oldHigh;
                backend.SetStandard(false);
            }
            else
            {
                backend.SetStandard(mode == StartupMode.Standard);
                if (oldHigh)
                {
                    await backend.SetHighPriorityAsync(false);
                    highChanged = true;
                }
            }
        }
        catch (Exception original)
        {
            var failures = new List<Exception> { original };
            try { backend.SetStandard(oldStandard); }
            catch (Exception rollback) { failures.Add(rollback); }
            if (highChanged)
            {
                try { await backend.SetHighPriorityAsync(oldHigh); }
                catch (Exception rollback) { failures.Add(rollback); }
            }
            if (failures.Count > 1)
                throw new AggregateException(failures);
            throw;
        }
    }
}
