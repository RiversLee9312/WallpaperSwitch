using System.Security.Principal;
using Microsoft.Win32;

namespace WallpaperSwitch.Startup;

internal static class StartupConfiguration
{
    public const string ServiceName = "WallpaperSwitchStartup";
    public const string HostFileName = "WallpaperSwitch.StartupHost.exe";
    public const string UsersKey = @"SOFTWARE\WallpaperSwitch\StartupUsers";
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string RunValueName = "WallpaperSwitch";

    public static string CurrentSid => WindowsIdentity.GetCurrent().User!.Value;
    public static string InstalledHostPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "WallpaperSwitch", "StartupService", HostFileName);

    public static void Validate(string sid, string applicationPath)
    {
        _ = new SecurityIdentifier(sid);
        if (!Path.IsPathFullyQualified(applicationPath) || applicationPath.StartsWith(@"\\")
            || applicationPath.Contains('"') || applicationPath.IndexOf(':', 2) >= 0
            || !string.Equals(Path.GetFileName(applicationPath), "WallpaperSwitch.exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new StartupPathException();
        }
    }

    public static string? GetApplicationPath(string sid)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = root.OpenSubKey($@"{UsersKey}\{sid}");
        return key?.GetValue("ApplicationPath") as string;
    }

    public static bool IsRegistered(string sid, string path) =>
        string.Equals(GetApplicationPath(sid), path, StringComparison.OrdinalIgnoreCase);

    public static string CommandLine(string path) => $"\"{path}\" --minimized";
}

internal sealed class StartupPathException : ArgumentException
{
    public StartupPathException() : base("The startup path must point to a local WallpaperSwitch.exe file.") { }
}
