using System.Globalization;
using System.Windows.Input;
using H.NotifyIcon;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    public ICommand RestoreCommand { get; }
    public ICommand ApplyNowCommand { get; }
    public ICommand ExitCommand { get; }

    public MainWindow()
    {
        RestoreCommand = new RelayCommand(RestoreFromTray);
        ApplyNowCommand = new RelayCommand(() => AppearanceController.Instance.ApplyNow());
        ExitCommand = new RelayCommand(ExitFromTray);
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

        var pngPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.png");
        if (File.Exists(pngPath))
        {
            TitleBarIcon.Source = new BitmapImage(new Uri(pngPath));
        }

        AppWindow.Closing += AppWindow_Closing;
        Activated += (_, _) =>
        {
            if (!_isLoading && !_changingStartup) RefreshStartupUi();
        };
        AppearanceController.Instance.StateChanged += OnControllerStateChanged;
        LocationUpdateService.Instance.StateChanged += OnLocationUpdateStateChanged;
        Closed += (_, _) =>
        {
            AppearanceController.Instance.StateChanged -= OnControllerStateChanged;
            LocationUpdateService.Instance.StateChanged -= OnLocationUpdateStateChanged;
            _showCts?.Cancel();
            DisposeTrayIcon();
        };

        LoadSettingsToUi();
        RefreshStatus();
        _isLoading = false;
        StartShowListener();
        CrashLog.Write("MainWindow ctor done");
    }

    public void SetupTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        try
        {
            CrashLog.Write("SetupTrayIcon");
            var flyout = new MenuFlyout();
            flyout.Items.Add(new MenuFlyoutItem { Text = Localizer.GetString("Tray.OpenWindow"), Command = RestoreCommand });
            flyout.Items.Add(new MenuFlyoutItem { Text = Localizer.GetString("Tray.ApplyPlan"), Command = ApplyNowCommand });
            flyout.Items.Add(new MenuFlyoutSeparator());
            flyout.Items.Add(new MenuFlyoutItem { Text = Localizer.GetString("Tray.Exit"), Command = ExitCommand });

            var period = AppearanceController.Instance.AppliedPeriod;
            var trayIconPath = GetTrayIconPath(period);
            if (!File.Exists(trayIconPath))
            {
                throw new FileNotFoundException(Localizer.GetString("App.TrayIconMissing"), trayIconPath);
            }

            _trayDrawingIcon = new System.Drawing.Icon(trayIconPath);
            _trayIconPeriod = period;

            _trayIcon = new TaskbarIcon
            {
                ToolTipText = Localizer.GetString("App.TrayTooltip"),
                ContextMenuMode = ContextMenuMode.PopupMenu,
                NoLeftClickDelay = true,
                LeftClickCommand = RestoreCommand,
                ContextFlyout = flyout,
                Icon = _trayDrawingIcon
            };

            _trayIcon.ForceCreate(enablesEfficiencyMode: false);
            CrashLog.Write("SetupTrayIcon done");
        }
        catch (Exception ex)
        {
            CrashLog.Write("SetupTrayIcon failed: " + ex);
            DisposeTrayIcon();
        }
    }

    public void HideToTray()
    {
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Hide();
    }

    public void RestoreFromTray()
    {
        AppWindow.IsShownInSwitchers = true;
        AppWindow.Show();
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
        RefreshStartupUi();
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
        SetPreview(DawnPreview, DawnPlaceholder, settings.Dawn.WallpaperPath);
        SetPreview(DayPreview, DayPlaceholder, settings.Day.WallpaperPath);
        SetPreview(DuskPreview, DuskPlaceholder, settings.Dusk.WallpaperPath);
        SetPreview(NightPreview, NightPlaceholder, settings.Night.WallpaperPath);
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
        SettingsStore.Save();
        var displayName = language == LanguageCatalog.SystemLanguage
            ? Localizer.GetString("Language.System")
            : Localizer.LanguageOptions.First(option => option.Tag == language).DisplayName;
        ShowInfo(Localizer.Format("Language.RestartRequired", displayName));
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

        SettingsStore.Save();
    }

    private void SettingToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        SaveUiToSettings();

        if (ReferenceEquals(sender, LockScreenToggle) && SettingsStore.Current.SyncLockScreen)
        {
            AppearanceController.Instance.ApplyNow();
        }
        else
        {
            AppearanceController.Instance.ApplyIfNeeded();
        }
    }

    private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _changingStartup) return;
        _changingStartup = true;
        StartupToggle.IsEnabled = false;
        HighPriorityStartupToggle.IsEnabled = false;
        var mode = !StartupToggle.IsOn ? StartupMode.Disabled
            : HighPriorityStartupToggle.IsOn ? StartupMode.HighPriority : StartupMode.Standard;
        HighPriorityStartupCard.Description = Localizer.GetString("Startup.Updating");
        try
        {
            await _startupService.SetModeAsync(mode);
            ShowInfo(mode switch
            {
                StartupMode.HighPriority => Localizer.GetString("Startup.EnabledHigh"),
                StartupMode.Standard => Localizer.GetString("Startup.EnabledStandard"),
                _ => Localizer.GetString("Startup.Disabled")
            });
        }
        catch (OperationCanceledException) { ShowWarning(Localizer.GetString("Startup.ElevationCancelled")); }
        catch (Exception ex)
        {
            var details = ex is AggregateException aggregate
                ? string.Join(Environment.NewLine, aggregate.InnerExceptions.Select(inner => inner.Message))
                : ex.Message;
            ShowError(Localizer.Format("Startup.SetFailed", details));
        }
        finally
        {
            _changingStartup = false;
            RefreshStartupUi();
            SettingsStore.Save();
        }
    }

    private void RefreshStartupUi()
    {
        var wasLoading = _isLoading;
        _isLoading = true;
        try
        {
            var mode = _startupService.Mode;
            StartupToggle.IsOn = mode != StartupMode.Disabled;
            HighPriorityStartupToggle.IsOn = mode == StartupMode.HighPriority;
            StartupToggle.IsEnabled = !_changingStartup;
            HighPriorityStartupToggle.IsEnabled = StartupToggle.IsOn && !_changingStartup;
            SettingsStore.Current.LaunchAtStartup = StartupToggle.IsOn;
            SettingsStore.Current.HighPriorityStartup = HighPriorityStartupToggle.IsOn;
            HighPriorityStartupCard.Description = mode == StartupMode.HighPriority
                ? (_startupService.ServiceRunning
                    ? Localizer.GetString("Startup.HighServiceRunning")
                    : Localizer.GetString("Startup.HighServiceRepair"))
                : (StartupToggle.IsOn
                    ? Localizer.GetString("Startup.HighRequiresStandard")
                    : Localizer.GetString("Startup.EnableStandardFirst"));
        }
        catch (Exception ex) { ShowError(Localizer.Format("Startup.ReadFailed", ex.Message)); }
        finally { _isLoading = wasLoading; }
    }

    private void ScheduleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSchedulePanels();
        SaveUiToSettings();
        if (!_isLoading)
        {
            AppearanceController.Instance.ApplyIfNeeded();
        }
    }

    private void ScheduleTimeChanged(TimePicker sender, TimePickerSelectedValueChangedEventArgs args)
    {
        SaveUiToSettings();
        if (!_isLoading)
        {
            AppearanceController.Instance.ApplyIfNeeded();
        }
    }

    private void FitCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SaveUiToSettings();
        if (!_isLoading)
        {
            AppearanceController.Instance.ApplyIfNeeded();
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
        if (_isLoading)
        {
            return;
        }

        AutoLocationToggle.IsEnabled = false;
        try
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
                    SettingsStore.Save();
                    ShowError(Localizer.Format("Location.EnableFailed", result.Error));
                    return;
                }

                SettingsStore.Current.AutoUpdateLocation = true;
                SettingsStore.Current.LocationRefreshFrequency = LocationFrequencyCombo.SelectedIndex >= 0
                    ? (LocationRefreshFrequency)LocationFrequencyCombo.SelectedIndex
                    : LocationRefreshFrequency.Daily;
                SettingsStore.Save();
                LocationUpdateService.Instance.SettingsChanged();
                ShowInfo(Localizer.GetString("Location.Enabled"));
            }
            else
            {
                SettingsStore.Current.AutoUpdateLocation = false;
                SettingsStore.Save();
                LocationUpdateService.Instance.SettingsChanged();
            }
        }
        finally
        {
            AutoLocationToggle.IsEnabled = true;
            RefreshLocationText();
            RefreshLocationUpdateUi();
        }
    }

    private void LocationFrequencyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        SaveUiToSettings();
        LocationUpdateService.Instance.SettingsChanged();
        RefreshLocationUpdateUi();
    }

    private void PeriodThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        SaveUiToSettings();
        if (!_isLoading)
        {
            AppearanceController.Instance.ApplyIfNeeded();
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

    private async Task PickPeriodWallpaperAsync(PeriodSettings period, Image preview, TextBlock placeholder)
    {
        var path = await PickWallpaperAsync();
        if (path is null)
        {
            return;
        }

        period.WallpaperPath = path;
        SettingsStore.Save();
        SetPreview(preview, placeholder, path);
        AppearanceController.Instance.ApplyIfNeeded();
    }

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

    private async void UseLocation_Click(object sender, RoutedEventArgs e)
    {
        var result = await LocationUpdateService.Instance.RefreshNowAsync(requestAccess: true);
        if (!result.Success)
        {
            ShowError(Localizer.Format("Location.RefreshFailed", result.Error));
            return;
        }

        RefreshLocationText();
        RefreshLocationUpdateUi();
        ShowInfo(Localizer.GetString("Location.Updated"));
    }

    private void ApplyNow_Click(object sender, RoutedEventArgs e)
    {
        AppearanceController.Instance.ApplyNow();
    }

    private void PreviewOtherMode_Click(object sender, RoutedEventArgs e)
    {
        var next = AppSettings.NextPeriod(AppearanceController.Instance.AppliedPeriod);
        AppearanceController.Instance.ApplyPeriod(next, force: true);
    }

    private void Restore_Click(object sender, RoutedEventArgs e) => RestoreFromTray();

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitFromTray();

    private void ExitFromTray()
    {
        _allowClose = true;
        DisposeTrayIcon();
        Close();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || !SettingsStore.Current.MinimizeToTray)
        {
            DisposeTrayIcon();
            return;
        }

        args.Cancel = true;
        HideToTray();
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
        DispatcherQueue.TryEnqueue(RefreshStatus);
    }

    private void OnLocationUpdateStateChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            RefreshLocationText();
            RefreshLocationUpdateUi();
        });
    }

    private void RefreshStatus()
    {
        var snapshot = AppearanceController.Instance.Snapshot;
        var period = AppearanceController.Instance.AppliedPeriod;
        var light = AppearanceController.Instance.AppliedLight;
        var periodName = Localizer.GetString($"Period.{period}");
        UpdateTrayIcon(period);
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

    private void UpdateTrayIcon(DayPeriod period)
    {
        if (_trayIcon is null || _trayIconPeriod == period)
        {
            return;
        }

        var trayIconPath = GetTrayIconPath(period);
        if (!File.Exists(trayIconPath))
        {
            CrashLog.Write("Tray icon missing: " + trayIconPath);
            return;
        }

        var newIcon = new System.Drawing.Icon(trayIconPath);
        var previousIcon = _trayDrawingIcon;
        _trayDrawingIcon = newIcon;
        _trayIcon.Icon = newIcon;
        _trayIconPeriod = period;
        previousIcon?.Dispose();
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

    private static void SetPreview(Image image, TextBlock placeholder, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            image.Source = null;
            placeholder.Visibility = Visibility.Visible;
            return;
        }

        image.Source = new BitmapImage(new Uri(path));
        placeholder.Visibility = Visibility.Collapsed;
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
