using System.Globalization;
using WallpaperSwitch.Localization;
using WallpaperSwitch.Models;

namespace WallpaperSwitch.Services;

internal readonly record struct ScheduleSnapshot(
    DayPeriod Period,
    bool WantLight,
    DateTime NextSwitchAt,
    string NextSwitchLabel,
    SunTimes? SunTimes,
    TimeOnly DawnStart,
    TimeOnly DayStart,
    TimeOnly DuskStart,
    TimeOnly NightStart);

internal static class ScheduleService
{
    public static ScheduleSnapshot Evaluate(AppSettings settings, DateTime now)
    {
        var bounds = ResolveBounds(settings, DateOnly.FromDateTime(now));
        var period = ResolvePeriod(TimeOnly.FromDateTime(now), bounds.Dawn, bounds.Day, bounds.Dusk, bounds.Night);
        var wantLight = settings.GetPeriod(period).UseLightTheme;
        var next = NextSwitch(now, bounds.Dawn, bounds.Day, bounds.Dusk, bounds.Night);
        var nextPeriod = ResolvePeriod(TimeOnly.FromDateTime(next), bounds.Dawn, bounds.Day, bounds.Dusk, bounds.Night);
        var label = Localizer.Format(
            "Error.ScheduleNextSwitch",
            next.ToString("t", CultureInfo.CurrentCulture),
            Localizer.GetString($"Period.{nextPeriod}"));
        return new ScheduleSnapshot(
            period,
            wantLight,
            next,
            label,
            bounds.SunTimes,
            bounds.Dawn,
            bounds.Day,
            bounds.Dusk,
            bounds.Night);
    }

    public static (TimeOnly Dawn, TimeOnly Day, TimeOnly Dusk, TimeOnly Night, SunTimes? SunTimes) ResolveBounds(
        AppSettings settings,
        DateOnly date)
    {
        if (settings.ScheduleMode == ScheduleMode.SunriseSunset
            && settings.Latitude is { } latitude
            && settings.Longitude is { } longitude)
        {
            var sun = SunTimesCalculator.Calculate(date, latitude, longitude);
            if (sun is { } times)
            {
                return (times.CivilDawn, times.MorningAltitude10, times.Sunset.AddMinutes(-45), times.CivilDusk, times);
            }
        }

        return (
            settings.Dawn.StartTime,
            settings.Day.StartTime,
            settings.Dusk.StartTime,
            settings.Night.StartTime,
            null);
    }

    public static DayPeriod ResolvePeriod(TimeOnly now, TimeOnly dawn, TimeOnly day, TimeOnly dusk, TimeOnly night)
    {
        (DayPeriod Period, TimeOnly Start)[] slots =
        [
            (DayPeriod.Dawn, dawn),
            (DayPeriod.Day, day),
            (DayPeriod.Dusk, dusk),
            (DayPeriod.Night, night)
        ];

        var started = slots.Where(slot => slot.Start <= now).ToArray();
        if (started.Length > 0)
        {
            return started.MaxBy(slot => slot.Start).Period;
        }

        return slots.MaxBy(slot => slot.Start).Period;
    }

    private static DateTime NextSwitch(DateTime now, params TimeOnly[] starts)
    {
        var today = DateOnly.FromDateTime(now);
        var upcoming = starts
            .Select(start => today.ToDateTime(start))
            .Where(candidate => candidate > now)
            .ToArray();

        if (upcoming.Length > 0)
        {
            return upcoming.Min();
        }

        return today.AddDays(1).ToDateTime(starts.Min());
    }
}
