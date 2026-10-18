using System.Globalization;
using System.Windows.Input;
using H.NotifyIcon;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Windowing;
using WallpaperSwitch.Models;
using WallpaperSwitch.Localization;
using WallpaperSwitch.Services;
using WallpaperSwitch.Startup;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WallpaperSwitch;

public sealed partial class MainWindow : Window
{
    private bool _isLoading = true;
    private bool _allowClose;
    private TaskbarIcon? _trayIcon;
    private System.Drawing.Icon? _trayDrawingIcon;
    private DayPeriod? _trayIconPeriod;
    private CancellationTokenSource? _showCts;
    private readonly StartupService _startupService = new();
    private bool _changingStartup;
    private bool _startupReady;
    private bool _closed;
    private bool _explicitShowRequested;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly AsyncOperations _operations = new();
    private readonly Dictionary<Image, int> _previewVersions = new();
    private Task? _trayMonitor;
    private readonly SemaphoreSlim _trayAccess = new(1, 1);
    private readonly AsyncRelayCommand _applyCommand;

    public ICommand RestoreCommand { get; }
    public ICommand ApplyNowCommand { get; }
    public ICommand ExitCommand { get; }

    public MainWindow()
    {
        RestoreCommand = new RelayCommand(RestoreFromTray);
        _applyCommand = new AsyncRelayCommand(ApplyNowAsync);
        ApplyNowCommand = _applyCommand;
        ExitCommand = new AsyncRelayCommand(ExitFromTrayAsync);
        CrashLog.Write("MainWindow ctor start");
        InitializeComponent();
        Title = Localizer.GetString("WindowTitle.Title");
        CrashLog.Write("MainWindow InitializeComponent done");
        RootLayout.FlowDirection = Localizer.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        AppWindow.Resize(new SizeInt32(860, 1040));
        CenterOnScreen();

        TitleBarIcon.Source = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.png"));
        _operations.Changed += RefreshBusyUi;
        AutomationProperties.SetName(ActivityProgressRing, Localizer.GetString("ActivityWorking.Text"));
        AutomationProperties.SetName(StartupProgressRing, Localizer.GetString("Startup.Updating"));
        AutomationProperties.SetName(LocationProgressRing, Localizer.GetString("Location.Fetching"));

        AppWindow.Closing += AppWindow_Closing;
        Activated += async (_, _) =>
        {
            if (!_isLoading && !_changingStartup) await RefreshStartupUiAsync();
        };
        AppearanceController.Instance.StateChanged += OnControllerStateChanged;
        LocationUpdateService.Instance.StateChanged += OnLocationUpdateStateChanged;
        Closed += (_, _) =>
        {
            AppearanceController.Instance.StateChanged -= OnControllerStateChanged;
            LocationUpdateService.Instance.StateChanged -= OnLocationUpdateStateChanged;
            _closed = true;
            _lifetime.Cancel();
            _showCts?.Cancel();
            DisposeTrayIcon();
        };

        LoadSettingsToUi();
        RefreshStatus();
        _isLoading = false;
        StartShowListener();
        CrashLog.Write("MainWindow ctor done");
    }

    public Task InitializeAsync() => Task.WhenAll(
        RefreshStartupUiAsync(),
        RunUiOperationAsync("previews", () => Task.WhenAll(
            SetPreviewAsync(DawnPreview, DawnPlaceholder, SettingsStore.Current.Dawn.WallpaperPath),
            SetPreviewAsync(DayPreview, DayPlaceholder, SettingsStore.Current.Day.WallpaperPath),
            SetPreviewAsync(DuskPreview, DuskPlaceholder, SettingsStore.Current.Dusk.WallpaperPath),
            SetPreviewAsync(NightPreview, NightPlaceholder, SettingsStore.Current.Night.WallpaperPath))));

    public async Task SetupTrayIconAsync()
    {
        await RunUiOperationAsync("tray", async () =>
        {
            if (_trayIcon is null)
            {
                var period = AppearanceController.Instance.AppliedPeriod;
                var icon = await Task.Run(() => new System.Drawing.Icon(GetTrayIconPath(period)));
                if (_closed) { icon.Dispose(); return; }
                _trayDrawingIcon = icon;
                _trayIconPeriod = period;
                var flyout = new MenuFlyout();
                flyout.Items.Add(new MenuFlyoutItem { Text = Localizer.GetString("Tray.OpenWindow"), Command = RestoreCommand });
                flyout.Items.Add(new MenuFlyoutItem { Text = Localizer.GetString("Tray.ApplyPlan"), Command = ApplyNowCommand });
                flyout.Items.Add(new MenuFlyoutSeparator());
                flyout.Items.Add(new MenuFlyoutItem { Text = Localizer.GetString("Tray.Exit"), Command = ExitCommand });
                _trayIcon = new TaskbarIcon
                {
                    ToolTipText = Localizer.GetString("App.TrayTooltip"),
                    ContextMenuMode = ContextMenuMode.PopupMenu,
                    NoLeftClickDelay = true,
                    LeftClickCommand = RestoreCommand,
                    ContextFlyout = flyout,
                    Icon = icon
                };
            }

            var core = _trayIcon.TrayIcon;
            // The callback window belongs to the UI message pump. Shell registration
            // itself can wait for Explorer and runs separately from that pump.
            if (!core.MessageWindow.IsCreated) core.MessageWindow.Create();
            var attempt = 0;
            await AsyncRetry.UntilReadyAsync(async () =>
            {
                await _trayAccess.WaitAsync(_lifetime.Token);
                try
                {
                    // Retain the message window even if Explorer is not ready yet.
                    // H.NotifyIcon also handles TaskbarCreated; the monitor retries failures.
                    await Task.Run(core.Create);
                    CrashLog.Write("SetupTrayIcon ready");
                    return true;
                }
                catch (Exception ex)
                {
                    if (attempt++ % 30 == 0) CrashLog.Write("SetupTrayIcon will retry: " + ex);
                    return false;
                }
                finally { _trayAccess.Release(); }
            }, TimeSpan.FromSeconds(2), _lifetime.Token);
        });
        if (!_closed) _trayMonitor ??= MonitorTrayAsync();
    }

    private async Task MonitorTrayAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), _lifetime.Token);
                if (_trayIcon is { IsCreated: false }) await SetupTrayIconAsync();
            }
        }
        catch (OperationCanceledException) { }
    }

    public void HideToTray(bool startup = false)
    {
        if (_closed || startup && _explicitShowRequested) return;
        if (_trayIcon?.IsCreated != true)
        {
            // Keep a taskbar entry if there is no working tray entry to restore from.
            (AppWindow.Presenter as OverlappedPresenter)?.Minimize();
            return;
        }
        SetShownInSwitchers(false);
        AppWindow.Hide();
    }

    private void SetShownInSwitchers(bool shown)
    {
        try { AppWindow.IsShownInSwitchers = shown; }
        catch (Exception ex) { CrashLog.Write("Switchers visibility unavailable: " + ex.Message); }
    }

    public void RestoreFromTray()
    {
        _explicitShowRequested = true;
        SetShownInSwitchers(true);
        AppWindow.Show();
        (AppWindow.Presenter as OverlappedPresenter)?.Restore();
        Activate();
    }

    private void StartShowListener()
    {
        if (SingleInstance.ShowEvent is null)
        {
            return;
        }

        _showCts = new CancellationTokenSource();
        var token = _showCts.Token;
        var showEvent = SingleInstance.ShowEvent;
        _ = Task.Run(() =>
        {
            WaitHandle[] handles = [showEvent, token.WaitHandle];
            while (!token.IsCancellationRequested)
            {
                var index = WaitHandle.WaitAny(handles);
                if (index != 0)
                {
                    break;
                }

                DispatcherQueue.TryEnqueue(RestoreFromTray);
            }
        }, token);
    }

    private void CenterOnScreen()
    {
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        AppWindow.Move(new PointInt32(
            workArea.X + (workArea.Width - AppWindow.Size.Width) / 2,
            workArea.Y + (workArea.Height - AppWindow.Size.Height) / 2));
    }

    private void LoadSettingsToUi()
    {
        var settings = SettingsStore.Current;
        LanguageCombo.ItemsSource = Localizer.LanguageOptions
            .Select(option => option.Tag == LanguageCatalog.SystemLanguage
                ? option with { DisplayName = Localizer.GetString("Language.System") }
                : option)
            .ToArray();
        LanguageCombo.SelectedValue = LanguageCatalog.NormalizePreference(settings.Language);
        EnabledToggle.IsOn = settings.IsEnabled;
        StartupToggle.IsEnabled = false;
        HighPriorityStartupToggle.IsEnabled = false;
        TrayToggle.IsOn = settings.MinimizeToTray;
        WallpaperToggle.IsOn = settings.SwitchWallpaper;
        LockScreenToggle.IsOn = settings.SyncLockScreen;
        LockScreenToggle.IsEnabled = settings.SwitchWallpaper;
        AppThemeToggle.IsOn = settings.SwitchAppTheme;
        SystemThemeToggle.IsOn = settings.SwitchSystemTheme;
        ScheduleCombo.SelectedIndex = settings.ScheduleMode == ScheduleMode.SunriseSunset ? 1 : 0;
        AutoLocationToggle.IsOn = settings.AutoUpdateLocation;
        LocationFrequencyCombo.SelectedIndex = Math.Clamp((int)settings.LocationRefreshFrequency, 0, 4);
        DawnTimePicker.SelectedTime = settings.Dawn.StartTime.ToTimeSpan();
        DayTimePicker.SelectedTime = settings.Day.StartTime.ToTimeSpan();
        DuskTimePicker.SelectedTime = settings.Dusk.StartTime.ToTimeSpan();
        NightTimePicker.SelectedTime = settings.Night.StartTime.ToTimeSpan();
        DawnThemeCombo.SelectedIndex = settings.Dawn.UseLightTheme ? 0 : 1;
        DayThemeCombo.SelectedIndex = settings.Day.UseLightTheme ? 0 : 1;
        DuskThemeCombo.SelectedIndex = settings.Dusk.UseLightTheme ? 0 : 1;
        NightThemeCombo.SelectedIndex = settings.Night.UseLightTheme ? 0 : 1;
        FitCombo.SelectedIndex = (int)settings.WallpaperFit;
        UpdateSchedulePanels();
        RefreshLocationText();
        RefreshLocationUpdateUi();
    }

    private async void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || LanguageCombo.SelectedValue is not string language)
        {
            return;
        }

        language = LanguageCatalog.NormalizePreference(language);
        if (language == LanguageCatalog.NormalizePreference(SettingsStore.Current.Language))
        {
            return;
        }

        SettingsStore.Current.Language = language;
        await RunUiOperationAsync("language", async () =>
        {
            await SettingsStore.SaveAsync();
            if (SettingsStore.Current.Language != language) return;
            var displayName = language == LanguageCatalog.SystemLanguage
                ? Localizer.GetString("Language.System")
                : Localizer.LanguageOptions.First(option => option.Tag == language).DisplayName;
            ShowInfo(Localizer.Format("Language.RestartRequired", displayName));
        }, allowConcurrent: true);
    }

    private void SaveUiToSettings()
    {
        if (_isLoading)
        {
            return;
        }

        var settings = SettingsStore.Current;
        settings.IsEnabled = EnabledToggle.IsOn;
        settings.MinimizeToTray = TrayToggle.IsOn;
        settings.SwitchWallpaper = WallpaperToggle.IsOn;
        settings.SyncLockScreen = LockScreenToggle.IsOn;
        LockScreenToggle.IsEnabled = WallpaperToggle.IsOn;
        settings.SwitchAppTheme = AppThemeToggle.IsOn;
        settings.SwitchSystemTheme = SystemThemeToggle.IsOn;
        settings.ScheduleMode = ScheduleCombo.SelectedIndex == 1 ? ScheduleMode.SunriseSunset : ScheduleMode.Custom;
        settings.LocationRefreshFrequency = LocationFrequencyCombo.SelectedIndex >= 0
            ? (LocationRefreshFrequency)LocationFrequencyCombo.SelectedIndex
            : LocationRefreshFrequency.Daily;
        settings.WallpaperFit = FitCombo.SelectedIndex >= 0 ? (WallpaperFit)FitCombo.SelectedIndex : WallpaperFit.Fill;
        settings.Dawn.UseLightTheme = DawnThemeCombo.SelectedIndex == 0;
        settings.Day.UseLightTheme = DayThemeCombo.SelectedIndex == 0;
        settings.Dusk.UseLightTheme = DuskThemeCombo.SelectedIndex == 0;
        settings.Night.UseLightTheme = NightThemeCombo.SelectedIndex == 0;

        if (DawnTimePicker.SelectedTime is { } dawn)
        {
            settings.Dawn.StartTime = TimeOnly.FromTimeSpan(dawn);
        }

        if (DayTimePicker.SelectedTime is { } day)
        {
            settings.Day.StartTime = TimeOnly.FromTimeSpan(day);
        }

        if (DuskTimePicker.SelectedTime is { } dusk)
        {
            settings.Dusk.StartTime = TimeOnly.FromTimeSpan(dusk);
        }

        if (NightTimePicker.SelectedTime is { } night)
        {
            settings.Night.StartTime = TimeOnly.FromTimeSpan(night);
        }

    }

    private async void SettingToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        SaveUiToSettings();

        await SaveAndApplyAsync(force: ReferenceEquals(sender, LockScreenToggle) && SettingsStore.Current.SyncLockScreen);
    }

    private Task SaveAndApplyAsync(bool force = false) => RunUiOperationAsync("settings", async () =>
    {
        await SettingsStore.SaveAsync();
        if (force) await AppearanceController.Instance.ApplyNowAsync();
        else await AppearanceController.Instance.ApplyIfNeededAsync();
    }, allowConcurrent: true);

    private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _changingStartup || !_startupReady) return;
        _changingStartup = true;
        var mode = !StartupToggle.IsOn ? StartupMode.Disabled
            : HighPriorityStartupToggle.IsOn ? StartupMode.HighPriority : StartupMode.Standard;
        await RunUiOperationAsync("startup", async () =>
        {
            HighPriorityStartupCard.Description = Localizer.GetString("Startup.Updating");
            try
            {
                await _startupService.SetModeAsync(mode);
                ShowInfo(Localizer.GetString(mode switch
                {
                    StartupMode.HighPriority => "Startup.EnabledHigh",
                    StartupMode.Standard => "Startup.EnabledStandard",
                    _ => "Startup.Disabled"
                }));
            }
            catch (OperationCanceledException) { ShowWarning(Localizer.GetString("Startup.ElevationCancelled")); }
            catch (Exception ex)
            {
                var details = ex is AggregateException aggregate
                    ? string.Join(Environment.NewLine, aggregate.InnerExceptions.Select(inner => inner.Message)) : ex.Message;
                ShowError(Localizer.Format("Startup.SetFailed", details));
            }
            finally
            {
                _changingStartup = false;
                await ReadStartupStateAsync();
                await SettingsStore.SaveAsync();
            }
        });
    }

    private Task RefreshStartupUiAsync() => RunUiOperationAsync("startup-read", ReadStartupStateAsync);

    private async Task ReadStartupStateAsync()
    {
        var state = await _startupService.GetStateAsync();
        if (_closed) return;
        var wasLoading = _isLoading;
        _isLoading = true;
        try
        {
            var mode = state.Mode;
            StartupToggle.IsOn = mode != StartupMode.Disabled;
            HighPriorityStartupToggle.IsOn = mode == StartupMode.HighPriority;
            SettingsStore.Current.LaunchAtStartup = StartupToggle.IsOn;
            SettingsStore.Current.HighPriorityStartup = HighPriorityStartupToggle.IsOn;
            HighPriorityStartupCard.Description = mode == StartupMode.HighPriority
                ? Localizer.GetString(state.ServiceRunning ? "Startup.HighServiceRunning" : "Startup.HighServiceRepair")
                : Localizer.GetString(StartupToggle.IsOn ? "Startup.HighRequiresStandard" : "Startup.EnableStandardFirst");
            _startupReady = true;
        }
        finally { _isLoading = wasLoading; RefreshBusyUi(); }
    }

    private async void ScheduleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSchedulePanels();
        SaveUiToSettings();
        if (!_isLoading)
        {
            await SaveAndApplyAsync();
        }
    }

    private async void ScheduleTimeChanged(TimePicker sender, TimePickerSelectedValueChangedEventArgs args)
    {
        SaveUiToSettings();
        if (!_isLoading)
        {
            await SaveAndApplyAsync();
        }
    }

    private async void FitCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SaveUiToSettings();
        if (!_isLoading)
        {
            await SaveAndApplyAsync();
        }
    }

    private void UpdateSchedulePanels()
    {
        var sunrise = ScheduleCombo.SelectedIndex == 1;
        CustomTimesPanel.Visibility = sunrise ? Visibility.Collapsed : Visibility.Visible;
        SunrisePanel.Visibility = sunrise ? Visibility.Visible : Visibility.Collapsed;
        RefreshLocationUpdateUi();
    }

    private async void AutoLocationToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        await RunUiOperationAsync("location", async () =>
        {
            if (AutoLocationToggle.IsOn)
            {
                var result = await LocationUpdateService.Instance.RefreshNowAsync(requestAccess: true);
                if (!result.Success)
                {
                    _isLoading = true;
                    AutoLocationToggle.IsOn = false;
                    _isLoading = false;
                    SettingsStore.Current.AutoUpdateLocation = false;
                    await SettingsStore.SaveAsync();
                    ShowError(Localizer.Format("Location.EnableFailed", result.Error));
                    return;
                }
                SettingsStore.Current.AutoUpdateLocation = true;
                SettingsStore.Current.LocationRefreshFrequency = LocationFrequencyCombo.SelectedIndex >= 0
                    ? (LocationRefreshFrequency)LocationFrequencyCombo.SelectedIndex : LocationRefreshFrequency.Daily;
                ShowInfo(Localizer.GetString("Location.Enabled"));
            }
            else SettingsStore.Current.AutoUpdateLocation = false;
            await SettingsStore.SaveAsync();
            LocationUpdateService.Instance.SettingsChanged();
            RefreshLocationText();
            RefreshLocationUpdateUi();
        });
    }

    private async void LocationFrequencyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        SaveUiToSettings();
        await SaveAndApplyAsync();
        LocationUpdateService.Instance.SettingsChanged();
        RefreshLocationUpdateUi();
    }

    private async void PeriodThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        SaveUiToSettings();
        if (!_isLoading)
        {
            await SaveAndApplyAsync();
        }
    }

    private async void PickDawnWallpaper_Click(object sender, RoutedEventArgs e) =>
        await PickPeriodWallpaperAsync(SettingsStore.Current.Dawn, DawnPreview, DawnPlaceholder);

    private async void PickDayWallpaper_Click(object sender, RoutedEventArgs e) =>
        await PickPeriodWallpaperAsync(SettingsStore.Current.Day, DayPreview, DayPlaceholder);

    private async void PickDuskWallpaper_Click(object sender, RoutedEventArgs e) =>
        await PickPeriodWallpaperAsync(SettingsStore.Current.Dusk, DuskPreview, DuskPlaceholder);

    private async void PickNightWallpaper_Click(object sender, RoutedEventArgs e) =>
        await PickPeriodWallpaperAsync(SettingsStore.Current.Night, NightPreview, NightPlaceholder);

    private Task PickPeriodWallpaperAsync(PeriodSettings period, Image preview, TextBlock placeholder) =>
        RunUiOperationAsync("wallpaper", async () =>
        {
            var path = await PickWallpaperAsync();
            if (path is null || _closed) return;
            period.WallpaperPath = path;
            await Task.WhenAll(SettingsStore.SaveAsync(), SetPreviewAsync(preview, placeholder, path));
            await AppearanceController.Instance.ApplyIfNeededAsync();
        });

    private async Task<string?> PickWallpaperAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            ViewMode = PickerViewMode.Thumbnail
        };
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".jfif");
        picker.FileTypeFilter.Add(".webp");

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async void UseLocation_Click(object sender, RoutedEventArgs e) =>
        await RunUiOperationAsync("location", async () =>
        {
            var result = await LocationUpdateService.Instance.RefreshNowAsync(requestAccess: true);
            if (!result.Success) { ShowError(Localizer.Format("Location.RefreshFailed", result.Error)); return; }
            RefreshLocationText();
            RefreshLocationUpdateUi();
            ShowInfo(Localizer.GetString("Location.Updated"));
        });

    private Task ApplyNowAsync() => RunUiOperationAsync("appearance", () => AppearanceController.Instance.ApplyNowAsync());

    private async void ApplyNow_Click(object sender, RoutedEventArgs e) => await ApplyNowAsync();

    private async void PreviewOtherMode_Click(object sender, RoutedEventArgs e) =>
        await RunUiOperationAsync("appearance", () => AppearanceController.Instance.ApplyPeriodAsync(
            AppSettings.NextPeriod(AppearanceController.Instance.AppliedPeriod), force: true));

    private void Restore_Click(object sender, RoutedEventArgs e) => RestoreFromTray();

    private async void Exit_Click(object sender, RoutedEventArgs e) => await ExitFromTrayAsync();

    private Task ExitFromTrayAsync() => RunUiOperationAsync("exit", async () =>
    {
        await SettingsStore.FlushAsync();
        _lifetime.Cancel();
        await _trayAccess.WaitAsync();
        try
        {
            var core = _trayIcon?.TrayIcon;
            if (core is not null) await Task.Run(() => core.TryRemove());
            DisposeTrayIcon();
        }
        finally { _trayAccess.Release(); }
        _allowClose = true;
        Close();
    });

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose) return;
        args.Cancel = true;
        if (SettingsStore.Current.MinimizeToTray) HideToTray();
        else await ExitFromTrayAsync();
    }

    private void DisposeTrayIcon()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        _trayDrawingIcon?.Dispose();
        _trayDrawingIcon = null;
        _trayIconPeriod = null;
    }

    private void OnControllerStateChanged()
    {
        DispatcherQueue.TryEnqueue(() => { if (!_closed) { RefreshStatus(); RefreshBusyUi(); } });
    }

    private void OnLocationUpdateStateChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_closed) return;
            RefreshLocationText();
            RefreshLocationUpdateUi();
            RefreshBusyUi();
        });
    }

    private void RefreshStatus()
    {
        var snapshot = AppearanceController.Instance.Snapshot;
        var period = AppearanceController.Instance.AppliedPeriod;
        var light = AppearanceController.Instance.AppliedLight;
        var periodName = Localizer.GetString($"Period.{period}");
        _ = RunUiOperationAsync("tray-image", UpdateTrayIconAsync);
        RootLayout.RequestedTheme = light ? ElementTheme.Light : ElementTheme.Dark;
        var isNight = period == DayPeriod.Night;
        ModeIcon.Visibility = isNight ? Visibility.Collapsed : Visibility.Visible;
        ModeNightIcon.Visibility = isNight ? Visibility.Visible : Visibility.Collapsed;
        if (!isNight)
        {
            ModeIcon.UriSource = GetPeriodUiIconUri(period);
        }
        ModeTitle.Text = Localizer.Format("Status.CurrentPeriod", periodName);
        var themeLabel = Localizer.GetString(light ? "Status.LightMode" : "Status.DarkMode");
        ModeSubtitle.Text = SettingsStore.Current.IsEnabled
            ? (period == snapshot.Period
                ? Localizer.Format("Status.Current", themeLabel, snapshot.NextSwitchLabel)
                : Localizer.Format("Status.Preview", themeLabel, snapshot.NextSwitchLabel))
            : Localizer.Format("Status.DisabledPreview", periodName, themeLabel);
        PreviewToggleButton.Content = Localizer.Format("Status.PreviewNext",
            Localizer.GetString($"Period.{AppSettings.NextPeriod(period)}"));

        DawnExpander.Description = snapshot.SunTimes is not null
            ? Localizer.Format("Status.DawnSolar", FormatTime(snapshot.DawnStart), FormatTime(snapshot.DayStart))
            : Localizer.Format("Status.StartsAround", FormatTime(snapshot.DawnStart));
        DayExpander.Description = Localizer.Format("Status.StartsAround", FormatTime(snapshot.DayStart));
        DuskExpander.Description = Localizer.Format("Status.StartsAround", FormatTime(snapshot.DuskStart));
        NightExpander.Description = Localizer.Format("Status.StartsAround", FormatTime(snapshot.NightStart));

        if (snapshot.SunTimes is { } sun)
        {
            SunTimesText.Visibility = Visibility.Visible;
            SunTimesText.Text = Localizer.Format("Status.SunTimes",
                FormatTime(sun.CivilDawn), FormatTime(sun.MorningAltitude10), FormatTime(sun.Sunrise),
                FormatTime(sun.Sunset), FormatTime(sun.CivilDusk));
        }
        else
        {
            SunTimesText.Visibility = Visibility.Collapsed;
        }

        if (AppearanceController.Instance.LastError is { } error)
        {
            ShowError(error);
        }
        else if (SettingsStore.Current.ScheduleMode == ScheduleMode.SunriseSunset
                 && snapshot.SunTimes is null)
        {
            ShowWarning(Localizer.GetString("Status.SunriseLocationRequired"));
        }
        else if (StatusInfoBar.Severity != InfoBarSeverity.Success)
        {
            StatusInfoBar.IsOpen = false;
        }
    }

    private async Task UpdateTrayIconAsync()
    {
        while (!_closed && _trayIcon is not null && _trayIconPeriod != AppearanceController.Instance.AppliedPeriod)
        {
            var period = AppearanceController.Instance.AppliedPeriod;
            var newIcon = await Task.Run(() => new System.Drawing.Icon(GetTrayIconPath(period)));
            await _trayAccess.WaitAsync();
            try
            {
                if (_closed || _trayIcon is null) { newIcon.Dispose(); return; }
                var core = _trayIcon.TrayIcon;
                var handle = newIcon.Handle;
                await Task.Run(() =>
                {
                    try { core.UpdateIcon(handle); }
                    catch (Exception ex)
                    {
                        // Explorer may disappear during an icon change. Retain the new
                        // live handle for registration retry instead of caching a disposed one.
                        CrashLog.Write("Tray image update will recover: " + ex);
                        core.TryRemove();
                        core.Icon = handle;
                    }
                });
                var previous = _trayDrawingIcon;
                _trayDrawingIcon = newIcon;
                _trayIconPeriod = period;
                previous?.Dispose();
            }
            catch { newIcon.Dispose(); throw; }
            finally { _trayAccess.Release(); }
        }
    }

    private static string GetTrayIconPath(DayPeriod period)
    {
        var fileName = period switch
        {
            DayPeriod.Day => "TrayDay.ico",
            DayPeriod.Dusk => "TrayDusk.ico",
            DayPeriod.Night => "TrayNight.ico",
            _ => "TrayDawn.ico"
        };
        return Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
    }

    private static Uri GetPeriodUiIconUri(DayPeriod period)
    {
        var fileName = period switch
        {
            DayPeriod.Day => "Day.png",
            DayPeriod.Dusk => "Dusk.png",
            _ => "Dawn.png"
        };
        return new Uri($"ms-appx:///Assets/UiIcons/{fileName}");
    }

    private void RefreshLocationText()
    {
        var settings = SettingsStore.Current;
        LocationCard.Description = settings.Latitude is { } latitude && settings.Longitude is { } longitude
            ? Localizer.Format("Location.Coordinates", latitude, longitude)
            : Localizer.GetString("Location.NoLocation");
    }

    private void RefreshLocationUpdateUi()
    {
        var settings = SettingsStore.Current;
        var service = LocationUpdateService.Instance;
        LocationFrequencyCard.IsEnabled = AutoLocationToggle.IsOn && !service.IsRefreshing;

        if (service.IsRefreshing)
        {
            AutoLocationCard.Description = Localizer.GetString("Location.Fetching");
            LocationFrequencyCard.Description = Localizer.GetString("Location.NextAfterComplete");
            return;
        }

        if (!settings.AutoUpdateLocation)
        {
            AutoLocationCard.Description = Localizer.GetString("Location.ManualAvailable");
            LocationFrequencyCard.Description = Localizer.GetString("Location.AutomaticOff");
            return;
        }

        var frequencyName = Localizer.GetString($"Frequency.{settings.LocationRefreshFrequency}");
        if (service.LastError is { Length: > 0 } error)
        {
            AutoLocationCard.Description = Localizer.Format("Location.LastFailed", error);
        }
        else if (settings.LastLocationUpdateUtc is { } lastUpdate)
        {
            AutoLocationCard.Description = Localizer.Format("Location.LastUpdated", lastUpdate.ToLocalTime());
        }
        else
        {
            AutoLocationCard.Description = Localizer.GetString("Location.WaitingFirst");
        }

        LocationFrequencyCard.Description = service.NextUpdateUtc is { } nextUpdate
            ? Localizer.Format("Location.NextUpdate", frequencyName, nextUpdate.ToLocalTime())
            : Localizer.Format("Location.UpdateSoon", frequencyName);
    }

    private static string FormatTime(TimeOnly value) => value.ToString("t", CultureInfo.CurrentCulture);

    private async Task SetPreviewAsync(Image image, TextBlock placeholder, string? path)
    {
        var version = _previewVersions.GetValueOrDefault(image) + 1;
        _previewVersions[image] = version;
        var ring = image == DawnPreview ? DawnPreviewProgress : image == DayPreview ? DayPreviewProgress
            : image == DuskPreview ? DuskPreviewProgress : NightPreviewProgress;
        image.Source = null;
        placeholder.Visibility = Visibility.Visible;
        if (string.IsNullOrWhiteSpace(path)) return;
        SetProgress(ring, true);
        try
        {
            // File opening may block on network/removable drives; decoding remains asynchronous.
            using var stream = await Task.Run(() => File.OpenRead(path));
            using var randomAccess = stream.AsRandomAccessStream();
            if (_closed || _previewVersions[image] != version) return;
            var bitmap = new BitmapImage { DecodePixelWidth = 192 };
            await bitmap.SetSourceAsync(randomAccess);
            if (_closed || _previewVersions[image] != version) return;
            image.Source = bitmap;
            placeholder.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) { CrashLog.Write("Wallpaper preview failed: " + ex); }
        finally { if (!_closed && _previewVersions[image] == version) SetProgress(ring, false); }
    }

    private async Task RunUiOperationAsync(string key, Func<Task> action, bool allowConcurrent = false)
    {
        if (_closed) return;
        try { await _operations.RunAsync(key, action, allowConcurrent); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { CrashLog.Write(ex.ToString()); if (!_closed) ShowError(ex.Message); }
    }

    private static void SetProgress(ProgressRing ring, bool active)
    {
        ring.IsActive = active;
        ring.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshBusyUi()
    {
        if (_closed) return;
        var location = _operations.Contains("location") || LocationUpdateService.Instance.IsRefreshing;
        var appearance = _operations.Contains("appearance") || AppearanceController.Instance.IsApplying;
        var startup = _operations.Contains("startup") || _operations.Contains("startup-read");
        var exiting = _operations.Contains("exit");
        SettingsScrollViewer.IsEnabled = !exiting;
        var busy = _operations.IsBusy || location || appearance;
        ActivityBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        SetProgress(ActivityProgressRing, busy);
        SetProgress(StartupProgressRing, startup);
        SetProgress(LocationProgressRing, location);
        ApplyNowButton.IsEnabled = PreviewToggleButton.IsEnabled = !appearance;
        _applyCommand.IsEnabled = !appearance && !exiting;
        StartupToggle.IsEnabled = _startupReady && !startup;
        HighPriorityStartupToggle.IsEnabled = _startupReady && !startup && StartupToggle.IsOn;
        AutoLocationToggle.IsEnabled = UseLocationButton.IsEnabled = !location;
        LocationFrequencyCard.IsEnabled = AutoLocationToggle.IsOn && !location;
        DawnPickButton.IsEnabled = DayPickButton.IsEnabled = DuskPickButton.IsEnabled = NightPickButton.IsEnabled
            = !_operations.Contains("wallpaper");
    }

    private void ShowError(string message)
    {
        StatusInfoBar.Severity = InfoBarSeverity.Error;
        StatusInfoBar.Title = Localizer.GetString("Status.ErrorTitle");
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
    }

    private void ShowWarning(string message)
    {
        StatusInfoBar.Severity = InfoBarSeverity.Warning;
        StatusInfoBar.Title = Localizer.GetString("Status.WarningTitle");
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
    }

    private void ShowInfo(string message)
    {
        StatusInfoBar.Severity = InfoBarSeverity.Success;
        StatusInfoBar.Title = Localizer.GetString("Status.SuccessTitle");
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
    }
}

internal sealed class RelayCommand(Action action) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => action();
}

internal sealed class AsyncRelayCommand(Func<Task> action) : ICommand
{
    private bool _running;
    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled == value) return; _isEnabled = value; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running && IsEnabled;
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await action(); }
        finally { _running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}
