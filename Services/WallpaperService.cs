using WallpaperSwitch.Models;
using WallpaperSwitch.Native;
using WallpaperSwitch.Localization;

namespace WallpaperSwitch.Services;

internal static class WallpaperService
{
    public static void Apply(string path, WallpaperFit fit)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(Localizer.GetString("Error.WallpaperFileMissing"), path);
        }

        var fullPath = Path.GetFullPath(path);
        var position = ToPosition(fit);

        try
        {
            var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
            wallpaper.SetPosition(position);
            wallpaper.SetWallpaper(null, fullPath);
            return;
        }
        catch
        {
            // Fall back to the classic SystemParametersInfo path.
        }

        if (!NativeMethods.SystemParametersInfo(
                NativeMethods.SpiSetDeskWallpaper,
                0,
                fullPath,
                NativeMethods.SpifUpdateIniFile | NativeMethods.SpifSendWinIniChange))
        {
            throw new InvalidOperationException(Localizer.GetString("Error.WallpaperApply"));
        }
    }

    public static string? GetCurrentWallpaper()
    {
        try
        {
            var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
            wallpaper.GetMonitorDevicePathCount(out var count);
            if (count == 0)
            {
                return null;
            }

            wallpaper.GetMonitorDevicePathAt(0, out var monitorId);
            wallpaper.GetWallpaper(monitorId, out var path);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch
        {
            return null;
        }
    }

    public static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static DesktopWallpaperPosition ToPosition(WallpaperFit fit) => fit switch
    {
        WallpaperFit.Fit => DesktopWallpaperPosition.Fit,
        WallpaperFit.Stretch => DesktopWallpaperPosition.Stretch,
        WallpaperFit.Center => DesktopWallpaperPosition.Center,
        WallpaperFit.Span => DesktopWallpaperPosition.Span,
        WallpaperFit.Tile => DesktopWallpaperPosition.Tile,
        _ => DesktopWallpaperPosition.Fill
    };
}
