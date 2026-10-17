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
    private bool _applying;
    private string? _lastLockScreenPath;

    public event Action? StateChanged;

    public ScheduleSnapshot Snapshot { get; private set; }
        = new(DayPeriod.Day, true, DateTime.Now, string.Empty, null, default, default, default, default);

    public DayPeriod AppliedPeriod { get; private set; } = DayPeriod.Day;

    public bool AppliedLight { get; private set; } = true;

    public string? LastError { get; private set; }

    public void Start()
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
        _timer.Tick += (_, _) => ApplyIfNeeded();
        _timer.Start();

        SystemEvents.TimeChanged += OnSystemTimeChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        ApplyIfNeeded();
    }

    public void ApplyIfNeeded() => _ = ApplyAsync(force: false);

    public void ApplyNow() => _ = ApplyAsync(force: true);

    public void ApplyPeriod(DayPeriod period, bool force) => _ = ApplyPeriodAsync(period, force);

    private async Task ApplyAsync(bool force)
    {
        var settings = SettingsStore.Current;
        Snapshot = ScheduleService.Evaluate(settings, DateTime.Now);
        if (!settings.IsEnabled && !force)
        {
            StateChanged?.Invoke();
            return;
        }

        await ApplyPeriodAsync(Snapshot.Period, force);
    }

    private async Task ApplyPeriodAsync(DayPeriod period, bool force)
    {
        if (_applying)
        {
            return;
        }

        _applying = true;
        try
        {
            LastError = null;
            await ApplyCoreAsync(period, force);
            AppliedPeriod = period;
            AppliedLight = SettingsStore.Current.GetPeriod(period).UseLightTheme;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            _applying = false;
            Snapshot = ScheduleService.Evaluate(SettingsStore.Current, DateTime.Now);
            StateChanged?.Invoke();
        }
    }

    private async Task ApplyCoreAsync(DayPeriod period, bool force)
    {
        var settings = SettingsStore.Current;
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
            return;
        }

        var wallpaper = periodSettings.WallpaperPath;
        if (string.IsNullOrWhiteSpace(wallpaper))
        {
            return;
        }

        var desktopMatches = WallpaperService.PathsEqual(WallpaperService.GetCurrentWallpaper(), wallpaper);
        if (force || !desktopMatches)
        {
            WallpaperService.Apply(wallpaper, settings.WallpaperFit);
        }

        if (!settings.SyncLockScreen)
        {
            return;
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
            LastError = Localizer.Format("Error.AppearanceLockScreen", ex.Message);
        }
    }

    private void OnSystemTimeChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue?.TryEnqueue(ApplyIfNeeded);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _dispatcherQueue?.TryEnqueue(async () =>
            {
                await Task.Delay(2000);
                ApplyIfNeeded();
            });
        }
    }
}
