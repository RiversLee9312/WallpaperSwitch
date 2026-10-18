using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WallpaperSwitch.Localization;
using WallpaperSwitch.Services;

namespace WallpaperSwitch;

internal static class NativeDialog
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    public static void Show(string caption, string text)
    {
        MessageBox(IntPtr.Zero, text, caption, 0x10);
    }
}

public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        CrashLog.Write("Main start");
        var minimized = args.Any(argument => string.Equals(argument, "--minimized", StringComparison.OrdinalIgnoreCase));
        if (!SingleInstance.TryStart(showExisting: !minimized))
        {
            CrashLog.Write("Handed off to the running instance");
            return 0;
        }

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                CrashLog.Write("Creating App");
                _ = new App();
            });
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex.ToString());
            NativeDialog.Show(Localizer.GetString("App.LaunchFailed"), ex.Message);
            return 1;
        }
        finally { SingleInstance.Release(); }

        CrashLog.Write("Main end");
        return 0;
    }
}
