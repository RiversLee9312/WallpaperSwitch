using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using WallpaperSwitch.Startup;

namespace WallpaperSwitch.StartupHost;

internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(nint window, string text, string caption, uint type);

    public static int Main(string[] args)
    {
        try
        {
            if (args is ["--service"])
            {
                ServiceBase.Run(new LogonStartupService());
                return 0;
            }

            // Called by the per-user installer. Capture the original SID before UAC,
            // including when a different administrator supplies credentials.
            if (args is ["--uninstall-user"] or ["--refresh-user"])
            {
                var sid = StartupConfiguration.CurrentSid;
                var app = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "WallpaperSwitch.exe"));
                if (!StartupConfiguration.IsRegistered(sid, app)) return 0;
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
                start.ArgumentList.Add(args[0] == "--uninstall-user" ? "--disable" : "--enable");
                start.ArgumentList.Add(sid);
                start.ArgumentList.Add(app);
                using var child = Process.Start(start)!;
                child.WaitForExit();
                return child.ExitCode;
            }

            var hasLanguage = args.Length == 5 && string.Equals(args[3], "--language", StringComparison.Ordinal);
            if (args.Length is not (3 or 5) || args[0] is not ("--enable" or "--disable")
                || args.Length == 5 && !hasLanguage)
                throw new ArgumentException(HostText.Get("UsageHint"));
            if (hasLanguage) HostText.SelectCulture(args[4]);
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                throw new UnauthorizedAccessException(HostText.Get("AdminRequired"));
            StartupConfiguration.Validate(args[1], args[2]);
            using var gate = new Mutex(false, @"Global\WallpaperSwitchStartup.Configuration");
            try { if (!gate.WaitOne(TimeSpan.FromSeconds(45))) throw new System.TimeoutException(HostText.Get("OperationInProgress")); }
            catch (AbandonedMutexException) { }
            try
            {
                if (args[0] == "--enable") ServiceRegistration.Enable(args[1], args[2]);
                else ServiceRegistration.Disable(args[1], args[2]);
            }
            finally { gate.ReleaseMutex(); }
            return 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return 1223; }
        catch (Exception ex)
        {
            var message = ex is StartupPathException ? HostText.Get("InvalidStartupPath") : ex.Message;
            if (Environment.UserInteractive) MessageBox(0, message, HostText.Get("DialogTitle"), 0x10);
            return 1;
        }
    }
}
