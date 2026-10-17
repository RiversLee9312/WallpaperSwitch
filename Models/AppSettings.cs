namespace WallpaperSwitch.Models;

public enum ScheduleMode
{
    Custom,
    SunriseSunset
}

public enum WallpaperFit
{
    Fill,
    Fit,
    Stretch,
    Center,
    Span,
    Tile
}

public enum DayPeriod
{
    Dawn,
    Day,
    Dusk,
    Night
}

public enum LocationRefreshFrequency
{
    Hourly,
    Every12Hours,
    Daily,
    Weekly,
    Monthly
}

public sealed class PeriodSettings
{
    public TimeOnly StartTime { get; set; }
    public bool UseLightTheme { get; set; } = true;
    public string? WallpaperPath { get; set; }
}

public sealed class AppSettings
{
    public string Language { get; set; } = "system";
    public bool IsEnabled { get; set; } = true;
    public bool SwitchAppTheme { get; set; } = true;
    public bool SwitchSystemTheme { get; set; } = true;
    public bool SwitchWallpaper { get; set; } = true;
    public bool SyncLockScreen { get; set; } = true;
    public bool LaunchAtStartup { get; set; }
    public bool HighPriorityStartup { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Custom;
    public PeriodSettings Dawn { get; set; } = NewPeriod(6, 0, light: true);
    public PeriodSettings Day { get; set; } = NewPeriod(8, 0, light: true);
    public PeriodSettings Dusk { get; set; } = NewPeriod(17, 30, light: false);
    public PeriodSettings Night { get; set; } = NewPeriod(19, 30, light: false);
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool AutoUpdateLocation { get; set; }
    public LocationRefreshFrequency LocationRefreshFrequency { get; set; } = LocationRefreshFrequency.Daily;
    public DateTimeOffset? LastLocationUpdateUtc { get; set; }
    public DateTimeOffset? LastLocationUpdateAttemptUtc { get; set; }
    public WallpaperFit WallpaperFit { get; set; } = WallpaperFit.Fill;

    // Kept so older settings.json can be migrated.
    public TimeOnly LightStartTime { get; set; } = new(7, 0);
    public TimeOnly DarkStartTime { get; set; } = new(19, 0);
    public string? LightWallpaperPath { get; set; }
    public string? DarkWallpaperPath { get; set; }

    public PeriodSettings GetPeriod(DayPeriod period) => period switch
    {
        DayPeriod.Day => Day,
        DayPeriod.Dusk => Dusk,
        DayPeriod.Night => Night,
        _ => Dawn
    };

    public static DayPeriod NextPeriod(DayPeriod period) => period switch
    {
        DayPeriod.Dawn => DayPeriod.Day,
        DayPeriod.Day => DayPeriod.Dusk,
        DayPeriod.Dusk => DayPeriod.Night,
        _ => DayPeriod.Dawn
    };

    private static PeriodSettings NewPeriod(int hour, int minute, bool light) => new()
    {
        StartTime = new TimeOnly(hour, minute),
        UseLightTheme = light
    };
}
