namespace WallpaperSwitch.Services;

internal readonly record struct SunTimes(
    TimeOnly CivilDawn,
    TimeOnly Sunrise,
    TimeOnly MorningAltitude10,
    TimeOnly Sunset,
    TimeOnly CivilDusk);

internal static class SunTimesCalculator
{
    private const double OfficialZenith = 90.83333333333333;
    private const double CivilZenith = 96.0;
    private const double Altitude10Zenith = 80.0;

    public static SunTimes? Calculate(DateOnly date, double latitude, double longitude)
    {
        var sunriseUtc = CalculateUtc(date, latitude, longitude, sunrise: true, OfficialZenith);
        var sunsetUtc = CalculateUtc(date, latitude, longitude, sunrise: false, OfficialZenith);
        if (sunriseUtc is null || sunsetUtc is null)
        {
            return null;
        }

        var civilDawnUtc = CalculateUtc(date, latitude, longitude, sunrise: true, CivilZenith) ?? sunriseUtc.Value.AddMinutes(-30);
        var morningAltitude10Utc = CalculateUtc(date, latitude, longitude, sunrise: true, Altitude10Zenith)
            ?? sunriseUtc.Value.AddMinutes(50);
        var civilDuskUtc = CalculateUtc(date, latitude, longitude, sunrise: false, CivilZenith) ?? sunsetUtc.Value.AddMinutes(30);

        return new SunTimes(
            ToLocal(civilDawnUtc),
            ToLocal(sunriseUtc.Value),
            ToLocal(morningAltitude10Utc),
            ToLocal(sunsetUtc.Value),
            ToLocal(civilDuskUtc));
    }

    private static TimeOnly ToLocal(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            TimeZoneInfo.Local);
        return TimeOnly.FromDateTime(local);
    }

    private static DateTime? CalculateUtc(DateOnly date, double latitude, double longitude, bool sunrise, double zenith)
    {
        var dayOfYear = date.DayOfYear;
        var lngHour = longitude / 15.0;
        var t = sunrise
            ? dayOfYear + ((6 - lngHour) / 24.0)
            : dayOfYear + ((18 - lngHour) / 24.0);

        var meanAnomaly = (0.9856 * t) - 3.289;
        var trueLongitude = Normalize360(
            meanAnomaly
            + (1.916 * Math.Sin(DegToRad(meanAnomaly)))
            + (0.020 * Math.Sin(2 * DegToRad(meanAnomaly)))
            + 282.634);

        var rightAscension = Normalize360(RadToDeg(Math.Atan(0.91764 * Math.Tan(DegToRad(trueLongitude)))));
        var lQuadrant = Math.Floor(trueLongitude / 90.0) * 90.0;
        var raQuadrant = Math.Floor(rightAscension / 90.0) * 90.0;
        rightAscension = (rightAscension + (lQuadrant - raQuadrant)) / 15.0;

        var sinDec = 0.39782 * Math.Sin(DegToRad(trueLongitude));
        var cosDec = Math.Cos(Math.Asin(sinDec));
        var cosHourAngle = (Math.Cos(DegToRad(zenith)) - (sinDec * Math.Sin(DegToRad(latitude))))
                           / (cosDec * Math.Cos(DegToRad(latitude)));

        if (cosHourAngle > 1 || cosHourAngle < -1)
        {
            return null;
        }

        var hourAngle = sunrise
            ? (360.0 - RadToDeg(Math.Acos(cosHourAngle))) / 15.0
            : RadToDeg(Math.Acos(cosHourAngle)) / 15.0;

        var localMeanTime = hourAngle + rightAscension - (0.06571 * t) - 6.622;
        var utcHours = Normalize24(localMeanTime - lngHour);
        return date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(utcHours);
    }

    private static double DegToRad(double degrees) => degrees * Math.PI / 180.0;

    private static double RadToDeg(double radians) => radians * 180.0 / Math.PI;

    private static double Normalize360(double value)
    {
        value %= 360.0;
        return value < 0 ? value + 360.0 : value;
    }

    private static double Normalize24(double value)
    {
        value %= 24.0;
        return value < 0 ? value + 24.0 : value;
    }
}
