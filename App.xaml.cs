using Microsoft.UI.Xaml;
using WallpaperSwitch.Localization;
using WallpaperSwitch.Services;

namespace WallpaperSwitch;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        CrashLog.Write("App ctor");
        SettingsStore.Load();
        Localizer.Initialize(SettingsStore.Current.Language);
        UnhandledException += (_, args) =>
        {
            CrashLog.Write("UnhandledException: " + args.Exception);
            NativeDialog.Show(Localizer.GetString("App.ErrorTitle"), args.Exception.Message);
            args.Handled = true;
        };

        InitializeComponent();
        CrashLog.Write("App initialized");
    }

    public static MainWindow? MainAppWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        CrashLog.Write("OnLaunched");
        MainAppWindow = new MainWindow();
        _window = MainAppWindow;
        CrashLog.Write("MainWindow created");

        var startMinimized = Environment.GetCommandLineArgs()
            .Any(argument => string.Equals(argument, "--minimized", StringComparison.OrdinalIgnoreCase));

        MainAppWindow.Activate();
        CrashLog.Write("MainWindow activated");

        MainAppWindow.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                CrashLog.Write("Deferred startup");
                AppearanceController.Instance.Start();
                LocationUpdateService.Instance.Start();
                MainAppWindow.SetupTrayIcon();
                if (startMinimized)
                {
                    MainAppWindow.HideToTray();
                }
            }
            catch (Exception ex)
            {
                CrashLog.Write("Deferred startup failed: " + ex);
            }
        });
    }
}
