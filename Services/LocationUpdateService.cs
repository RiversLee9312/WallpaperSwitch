using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using WallpaperSwitch.Models;
using WallpaperSwitch.Localization;
using Windows.Devices.Geolocation;

namespace WallpaperSwitch.Services;

internal readonly record struct LocationRefreshResult(bool Success, string? Error);

internal sealed class LocationUpdateService
{
    public static LocationUpdateService Instance { get; } = new();

    private DispatcherQueue? _dispatcherQueue;
    private DispatcherQueueTimer? _timer;
    private bool _started;

    public event Action? StateChanged;

    public bool IsRefreshing { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? NextUpdateUtc => CalculateNextUpdateUtc(SettingsStore.Current);

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException(Localizer.GetString("Error.LocationUiThread"));

        _timer = _dispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMinutes(1);
        _timer.Tick += async (_, _) => await RefreshIfDueAsync();
        _timer.Start();

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        CrashLog.Write("LocationUpdateService started");
        _ = RefreshIfDueAsync();
    }

    public void SettingsChanged()
    {
        StateChanged?.Invoke();
        if (_started)
        {
            _ = RefreshIfDueAsync();
        }
    }

    public async Task<LocationRefreshResult> RefreshNowAsync(bool requestAccess)
    {
        if (IsRefreshing)
        {
            return new LocationRefreshResult(false, Localizer.GetString("Error.LocationInProgress"));
        }

        IsRefreshing = true;
        LastError = null;
        StateChanged?.Invoke();
        var attemptUtc = DateTimeOffset.UtcNow;

        try
        {
            if (requestAccess)
            {
                var access = await Geolocator.RequestAccessAsync();
                if (access != GeolocationAccessStatus.Allowed)
                {
                    throw new UnauthorizedAccessException(Localizer.GetString("Error.LocationPermission"));
                }
            }

            var locator = new Geolocator { DesiredAccuracyInMeters = 5000 };
            var position = await locator.GetGeopositionAsync()
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(12));
            var settings = SettingsStore.Current;
            settings.Latitude = position.Coordinate.Point.Position.Latitude;
            settings.Longitude = position.Coordinate.Point.Position.Longitude;
            settings.LastLocationUpdateUtc = DateTimeOffset.UtcNow;
            settings.LastLocationUpdateAttemptUtc = settings.LastLocationUpdateUtc;
            SettingsStore.Save();

            CrashLog.Write(
                $"Location updated: {settings.Latitude:F4}, {settings.Longitude:F4}");
            AppearanceController.Instance.ApplyIfNeeded();
            return new LocationRefreshResult(true, null);
        }
        catch (Exception ex)
        {
            var message = ex is TimeoutException
                ? Localizer.GetString("Error.LocationTimeout")
                : ex.Message;
            SettingsStore.Current.LastLocationUpdateAttemptUtc = attemptUtc;
            SettingsStore.Save();
            LastError = message;
            CrashLog.Write("Location update failed: " + ex);
            return new LocationRefreshResult(false, message);
        }
        finally
        {
            IsRefreshing = false;
            StateChanged?.Invoke();
        }
    }

    internal static DateTimeOffset? CalculateNextUpdateUtc(AppSettings settings)
    {
        var anchor = settings.LastLocationUpdateAttemptUtc ?? settings.LastLocationUpdateUtc;
        if (anchor is null)
        {
            return null;
        }

        return settings.LocationRefreshFrequency switch
        {
            LocationRefreshFrequency.Hourly => anchor.Value.AddHours(1),
            LocationRefreshFrequency.Every12Hours => anchor.Value.AddHours(12),
            LocationRefreshFrequency.Weekly => anchor.Value.AddDays(7),
            LocationRefreshFrequency.Monthly => anchor.Value.AddMonths(1),
            _ => anchor.Value.AddDays(1)
        };
    }

    private async Task RefreshIfDueAsync()
    {
        var settings = SettingsStore.Current;
        if (!settings.AutoUpdateLocation || IsRefreshing)
        {
            return;
        }

        var nextUpdate = CalculateNextUpdateUtc(settings);
        if (nextUpdate is not null && DateTimeOffset.UtcNow < nextUpdate.Value)
        {
            return;
        }

        await RefreshNowAsync(requestAccess: false);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _dispatcherQueue?.TryEnqueue(async () =>
            {
                await Task.Delay(2000);
                await RefreshIfDueAsync();
            });
        }
    }
}
