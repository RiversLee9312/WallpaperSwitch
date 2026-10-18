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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        CrashLog.Write("OnLaunched");
        await SettingsStore.LoadAsync();
        Localizer.Initialize(SettingsStore.Current.Language);
        MainAppWindow = new MainWindow();
        _window = MainAppWindow;
        CrashLog.Write("MainWindow created");

        var startMinimized = Environment.GetCommandLineArgs()
            .Any(argument => string.Equals(argument, "--minimized", StringComparison.OrdinalIgnoreCase));

        MainAppWindow.Activate();
        if (startMinimized) MainAppWindow.HideToTray(startup: true);
        CrashLog.Write("MainWindow activated");

        MainAppWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                CrashLog.Write("Deferred startup");
                var tray = MainAppWindow.SetupTrayIconAsync();
                var initialize = MainAppWindow.InitializeAsync();
                LocationUpdateService.Instance.Start();
                var appearance = AppearanceController.Instance.StartAsync();
                if (startMinimized)
                {
                    await tray;
                    MainAppWindow.HideToTray(startup: true);
                }
                await Task.WhenAll(tray, initialize, appearance);
            }
            catch (Exception ex)
            {
                CrashLog.Write("Deferred startup failed: " + ex);
            }
        });
    }
}
