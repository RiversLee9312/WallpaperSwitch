using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using WallpaperSwitch.Models;
using WallpaperSwitch.Localization;

namespace WallpaperSwitch.Services;

internal sealed class AppearanceController
{
    public static AppearanceController Instance { get; } = new();

    private DispatcherQueue? _dispatcherQueue;
    private DispatcherQueueTimer? _timer;
    private bool _started;
    private readonly SemaphoreSlim _applyGate = new(1, 1);
    public bool IsApplying { get; private set; }
    private string? _lastLockScreenPath;

    public event Action? StateChanged;

    public ScheduleSnapshot Snapshot { get; private set; }
        = new(DayPeriod.Day, true, DateTime.Now, string.Empty, null, default, default, default, default);

    public DayPeriod AppliedPeriod { get; private set; } = DayPeriod.Day;

    public bool AppliedLight { get; private set; } = true;

    public string? LastError { get; private set; }

    public async Task StartAsync()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException(Localizer.GetString("Error.AppearanceUiThread"));

        _timer = _dispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMinutes(1);
        _timer.Tick += async (_, _) => await ApplyIfNeededAsync();
        _timer.Start();

        SystemEvents.TimeChanged += OnSystemTimeChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        await ApplyIfNeededAsync();
    }

    public Task ApplyIfNeededAsync() => ApplyAsync(null, force: false);
    public Task ApplyNowAsync() => ApplyAsync(null, force: true);
    public Task ApplyPeriodAsync(DayPeriod period, bool force) => ApplyAsync(period, force);

    private async Task ApplyAsync(DayPeriod? requestedPeriod, bool force)
    {
        // Serialize desktop changes, including automatic requests arriving during a preview.
        // Capture the latest UI-owned settings only after obtaining the gate.
        await _applyGate.WaitAsync();
        IsApplying = true;
        StateChanged?.Invoke();
        try
        {
            LastError = null;
            var settings = SettingsStore.Current.Clone();
            Snapshot = ScheduleService.Evaluate(settings, DateTime.Now);
            if (!settings.IsEnabled && !force) return;
            var period = requestedPeriod ?? Snapshot.Period;
            LastError = await Task.Run(() => ApplyCoreAsync(settings, period, force));
            AppliedPeriod = period;
            AppliedLight = settings.GetPeriod(period).UseLightTheme;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsApplying = false;
            _applyGate.Release();
            Snapshot = ScheduleService.Evaluate(SettingsStore.Current, DateTime.Now);
            StateChanged?.Invoke();
        }
    }

    private async Task<string?> ApplyCoreAsync(AppSettings settings, DayPeriod period, bool force)
    {
        var periodSettings = settings.GetPeriod(period);
        var light = periodSettings.UseLightTheme;

        if (settings.SwitchAppTheme || settings.SwitchSystemTheme)
        {
            var appsLight = settings.SwitchAppTheme ? light : ThemeService.AppsUseLightTheme();
            var systemLight = settings.SwitchSystemTheme ? light : ThemeService.SystemUsesLightTheme();
            var themeMatches = ThemeService.AppsUseLightTheme() == appsLight
                && ThemeService.SystemUsesLightTheme() == systemLight;

            if (force || !themeMatches)
            {
                ThemeService.Apply(appsLight, systemLight);
            }
        }

        if (!settings.SwitchWallpaper)
        {
            return null;
        }

        var wallpaper = periodSettings.WallpaperPath;
        if (string.IsNullOrWhiteSpace(wallpaper))
        {
            return null;
        }

        var desktopMatches = WallpaperService.PathsEqual(WallpaperService.GetCurrentWallpaper(), wallpaper);
        if (force || !desktopMatches)
        {
            WallpaperService.Apply(wallpaper, settings.WallpaperFit);
        }

        if (!settings.SyncLockScreen)
        {
            return null;
        }

        try
        {
            if (force || !WallpaperService.PathsEqual(_lastLockScreenPath, wallpaper))
            {
                await LockScreenService.ApplyAsync(wallpaper);
                _lastLockScreenPath = wallpaper;
            }
        }
        catch (Exception ex)
        {
            return Localizer.Format("Error.AppearanceLockScreen", ex.Message);
        }
        return null;
    }

    private void OnSystemTimeChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue?.TryEnqueue(async () => await ApplyIfNeededAsync());
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _dispatcherQueue?.TryEnqueue(async () =>
            {
                await Task.Delay(2000);
                await ApplyIfNeededAsync();
            });
        }
    }
}
