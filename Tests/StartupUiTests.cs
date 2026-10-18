using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace WallpaperSwitch.Tests;

[TestClass]
[DoNotParallelize]
public sealed partial class StartupUiTests
{
    private const string TestSettingsEnvironmentVariable = "WALLPAPERSWITCH_TEST_SETTINGS_FILE";
    private const string TestInstanceEnvironmentVariable = "WALLPAPERSWITCH_TEST_INSTANCE";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll")] private static extern nint GetWindowDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("user32.dll")] private static extern bool PrintWindow(nint window, nint dc, uint flags);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);

    internal static string ApplicationPath
    {
        get
        {
            if (Environment.GetEnvironmentVariable("WALLPAPERSWITCH_TEST_APP") is { Length: > 0 } configured) return configured;
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WallpaperSwitch.csproj"))) directory = directory.Parent;
            return Path.Combine(directory?.FullName ?? AppContext.BaseDirectory, "artifacts", "publish-1.3.2", "WallpaperSwitch.exe");
        }
    }

    [STATestMethod]
    [TestCategory("UI")]
    public void StartupControlsReflectRealStateAndBackgroundLaunchStaysMinimized()
    {
        if (!File.Exists(ApplicationPath)) Assert.Inconclusive("Publish the application before running UI tests.");
        using var run = Registry.CurrentUser.OpenSubKey(Startup.StartupConfiguration.RunKey);
        var tempDirectory = CreateIsolatedSettings(out var settingsFile, "en-US");
        var testInstance = Guid.NewGuid().ToString("N");
        using var process = Process.Start(CreateAppStartInfo(settingsFile, testInstance))!;
        try
        {
            var window = FindAppWindow(process, settingsFile);
            var standard = WaitFor(() => window.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "StartupToggle")));
            var high = WaitFor(() => window.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "HighPriorityStartupToggle")));
            var standardToggle = (TogglePattern)standard.GetCurrentPattern(TogglePattern.Pattern);
            var highToggle = (TogglePattern)high.GetCurrentPattern(TogglePattern.Pattern);
            WaitUntil(() => standard.Current.IsEnabled);
            var standardEnabled = string.Equals(
                run?.GetValue(Startup.StartupConfiguration.RunValueName) as string,
                Startup.StartupConfiguration.CommandLine(ApplicationPath),
                StringComparison.OrdinalIgnoreCase);
            var highEnabled = Startup.StartupConfiguration.IsRegistered(
                Startup.StartupConfiguration.CurrentSid, ApplicationPath);
            Assert.AreEqual(standardEnabled || highEnabled,
                standardToggle.Current.ToggleState == ToggleState.On);
            Assert.AreEqual(highEnabled, highToggle.Current.ToggleState == ToggleState.On);
            Assert.AreEqual(standardToggle.Current.ToggleState == ToggleState.On, high.Current.IsEnabled);
            var windowPattern = (WindowPattern)window.GetCurrentPattern(WindowPattern.Pattern);
            windowPattern.SetWindowVisualState(WindowVisualState.Minimized);
            WaitUntil(() => windowPattern.Current.WindowVisualState == WindowVisualState.Minimized);
            using var duplicate = Process.Start(CreateAppStartInfo(settingsFile, testInstance, "--minimized"))!;
            Assert.IsTrue(duplicate.WaitForExit(10000));
            Assert.AreEqual(WindowVisualState.Minimized, windowPattern.Current.WindowVisualState,
                "A background startup must not restore an existing window.");
            var duplicates = Enumerable.Range(0, 6)
                .Select(_ => Process.Start(CreateAppStartInfo(settingsFile, testInstance, "--minimized"))!).ToArray();
            try
            {
                foreach (var candidate in duplicates)
                {
                    Assert.IsTrue(candidate.WaitForExit(10000), "A duplicate must hand off and exit.");
                    Assert.AreEqual(0, candidate.ExitCode);
                }
            }
            finally { foreach (var candidate in duplicates) candidate.Dispose(); }
            using var foreground = Process.Start(CreateAppStartInfo(settingsFile, testInstance))!;
            Assert.IsTrue(foreground.WaitForExit(10000));
            WaitUntil(() => windowPattern.Current.WindowVisualState == WindowVisualState.Normal);
            Assert.IsFalse(process.HasExited);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [STATestMethod]
    [TestCategory("UI")]
    public void AppLanguageResourcesAndLayoutAppearCorrectlyInTheWindow()
    {
        if (!File.Exists(ApplicationPath)) Assert.Inconclusive("Publish the application before running UI tests.");
        var cases = new[]
        {
            new LanguageUiCase("en-US", "Wallpaper Switch", "English (United States)", "Current period:", IsRightToLeft: false),
            new LanguageUiCase("zh-Hans", "日夜切换", "简体中文", "当前时段：", IsRightToLeft: false),
            new LanguageUiCase("de-DE", "Hintergrundbild wechseln", "Deutsch", "Aktueller Zeitraum:", IsRightToLeft: false),
            new LanguageUiCase("ar-SA", "تبديل الخلفية", "العربية", "الفترة الحالية:", IsRightToLeft: true),
            new LanguageUiCase("he-IL", "מחליף טפטים", "עברית", "התקופה הנוכחית:", IsRightToLeft: true)
        };

        foreach (var testCase in cases)
        {
            var tempDirectory = CreateIsolatedSettings(out var settingsFile, testCase.Language);
            var testInstance = Guid.NewGuid().ToString("N");
            using var process = Process.Start(CreateAppStartInfo(settingsFile, testInstance))!;
            try
            {
                var window = FindAppWindow(process, settingsFile);
                WaitUntil(() => window.Current.Name == testCase.ExpectedTitle);
                Assert.AreEqual(testCase.ExpectedTitle, window.Current.Name, testCase.Language);

                var languagePicker = Find(window, "LanguageCombo");
                var selectedLanguage = (SelectionPattern)languagePicker.GetCurrentPattern(SelectionPattern.Pattern);
                WaitUntil(() => selectedLanguage.Current.GetSelection().SingleOrDefault()?.Current.Name == testCase.ExpectedLanguage);

                var currentPeriod = Find(window, "ModeTitle");
                WaitUntil(() => currentPeriod.Current.Name.StartsWith(testCase.ExpectedStatusPrefix, StringComparison.Ordinal));
                Assert.IsFalse(currentPeriod.Current.Name.Contains("Status.CurrentPeriod", StringComparison.Ordinal));
                Assert.IsFalse(currentPeriod.Current.Name.Contains("{0}", StringComparison.Ordinal));

                // A narrow window should still expose the status and its action button;
                // the surrounding ScrollViewer handles the remaining settings content.
                var originalBounds = window.Current.BoundingRectangle;
                var compactWidth = (int)(640 * GetDpiForWindow(window.Current.NativeWindowHandle) / 96.0);
                Assert.IsTrue(SetWindowPos(window.Current.NativeWindowHandle, 0,
                    (int)originalBounds.Left, (int)originalBounds.Top, compactWidth, (int)originalBounds.Height, 0x0004 | 0x0010),
                    "Could not resize the window for a compact-layout check.");
                WaitUntil(() => Math.Abs(window.Current.BoundingRectangle.Width - compactWidth) < 4);
                CaptureWindow(window, testCase.Language);
                var previewButton = Find(window, "PreviewToggleButton");
                WaitUntil(() => previewButton.Current.BoundingRectangle.Width > 0 && !previewButton.Current.IsOffscreen);
                Assert.IsTrue(previewButton.Current.BoundingRectangle.Width > 0, testCase.Language);
                Assert.IsFalse(previewButton.Current.IsOffscreen, testCase.Language);

                var startupToggle = Find(window, "StartupToggle");
                var scroll = (ScrollPattern)Find(window, "SettingsScrollViewer").GetCurrentPattern(ScrollPattern.Pattern);
                for (var attempt = 0; startupToggle.Current.IsOffscreen && attempt < 30; attempt++)
                    scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallIncrement);
                WaitUntil(() => !startupToggle.Current.IsOffscreen && startupToggle.Current.BoundingRectangle.Width > 0);
                var toggleCenter = (startupToggle.Current.BoundingRectangle.Left + startupToggle.Current.BoundingRectangle.Right) / 2;
                var windowCenter = (window.Current.BoundingRectangle.Left + window.Current.BoundingRectangle.Right) / 2;
                Assert.AreEqual(testCase.IsRightToLeft, toggleCenter < windowCenter,
                    $"The startup control should be on the {(testCase.IsRightToLeft ? "left" : "right")} side for {testCase.Language}.");
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit();
                }
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [STATestMethod]
    [TestCategory("UI")]
    [DataRow(false)]
    [DataRow(true)]
    public void SystemLanguageAndLegacySettingsStartSuccessfully(bool legacySettings)
    {
        if (!File.Exists(ApplicationPath)) Assert.Inconclusive("Publish the application before running UI tests.");
        var directory = CreateIsolatedSettings(out var settingsFile, "system");
        if (legacySettings)
        {
            var settings = JsonNode.Parse(File.ReadAllText(settingsFile))!.AsObject();
            settings.Remove("language");
            File.WriteAllText(settingsFile, settings.ToJsonString());
        }
        using var process = Process.Start(CreateAppStartInfo(settingsFile, Guid.NewGuid().ToString("N")))!;
        try
        {
            var window = FindAppWindow(process, settingsFile);
            _ = Find(window, "LanguageCombo");
            _ = Find(window, "PreviewToggleButton");
            WaitUntil(() => ReadSharedLog(Path.Combine(directory, "startup.log")).Contains("SetupTrayIcon ready", StringComparison.Ordinal)
                && TrayIsPresent(ApplicationPath));
            Assert.IsFalse(ReadSharedLog(Path.Combine(directory, "startup.log")).Contains("UnhandledException:", StringComparison.Ordinal));
            Assert.AreNotEqual("App.Title", window.Current.Name);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
            Directory.Delete(directory, recursive: true);
        }
    }

    [STATestMethod]
    [TestCategory("UI")]
    public void LanguageChoicePersistsAndAppliesAfterRestart()
    {
        if (!File.Exists(ApplicationPath)) Assert.Inconclusive("Publish the application before running UI tests.");
        var directory = CreateIsolatedSettings(out var settingsFile, "en-US");
        Process? process = null;
        try
        {
            process = Process.Start(CreateAppStartInfo(settingsFile, Guid.NewGuid().ToString("N")))!;
            var window = FindAppWindow(process, settingsFile);
            WaitUntil(() => FindSpinner(window) is null);
            SelectLanguage(window, "Deutsch");
            WaitUntil(() => JsonNode.Parse(File.ReadAllText(settingsFile))?["language"]?.GetValue<string>() == "de-DE");
            Assert.AreEqual("Wallpaper Switch", window.Current.Name, "The current window keeps its language until restart.");
            process.Kill();
            process.WaitForExit();
            process.Dispose();
            process = Process.Start(CreateAppStartInfo(settingsFile, Guid.NewGuid().ToString("N")))!;
            window = FindAppWindow(process, settingsFile);
            WaitUntil(() => window.Current.Name == "Hintergrundbild wechseln");
        }
        finally
        {
            if (process is not null)
            {
                if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
                process.Dispose();
            }
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CaptureWindow(AutomationElement window, string language)
    {
        var handle = (nint)window.Current.NativeWindowHandle;
        var bounds = window.Current.BoundingRectangle;
        var source = GetWindowDC(handle);
        var destination = CreateCompatibleDC(source);
        var bitmap = CreateCompatibleBitmap(source, (int)bounds.Width, (int)bounds.Height);
        var original = SelectObject(destination, bitmap);
        try
        {
            if (!PrintWindow(handle, destination, 2)) return;
            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "screenshots");
            Directory.CreateDirectory(directory);
            using var output = File.Create(Path.Combine(directory, language + ".png"));
            encoder.Save(output);
        }
        finally
        {
            SelectObject(destination, original);
            DeleteObject(bitmap);
            DeleteDC(destination);
            ReleaseDC(handle, source);
        }
    }

    private static AutomationElement FindAppWindow(Process process, string settingsFile) => WaitFor(() =>
    {
        var logPath = Path.Combine(Path.GetDirectoryName(settingsFile)!, "startup.log");
        var log = ReadSharedLog(logPath);
        Assert.IsFalse(log.Contains("UnhandledException:", StringComparison.Ordinal), log);
        Assert.IsFalse(process.HasExited, $"Application exited before creating its window. {log}");
        return AutomationElement.RootElement.FindFirst(TreeScope.Children,
            new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id),
                new PropertyCondition(AutomationElement.IsWindowPatternAvailableProperty, true)));
    });

    private static string ReadSharedLog(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (IOException) { return string.Empty; }
    }

    private static AutomationElement Find(AutomationElement window, string automationId) => WaitFor(() =>
        window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId)));

    private static string CreateIsolatedSettings(out string settingsFile, string language)
    {
        var directory = Path.Combine(Path.GetTempPath(), "WallpaperSwitch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        settingsFile = Path.Combine(directory, "settings.json");
        var settings = new JsonObject
        {
            ["language"] = language,
            ["isEnabled"] = false,
            ["switchAppTheme"] = false,
            ["switchSystemTheme"] = false,
            ["switchWallpaper"] = false
        };
        File.WriteAllText(settingsFile, settings.ToJsonString());
        return directory;
    }

    private static ProcessStartInfo CreateAppStartInfo(string settingsFile, string testInstance, string? arguments = null)
    {
        var start = new ProcessStartInfo(ApplicationPath) { UseShellExecute = false };
        if (!string.IsNullOrWhiteSpace(arguments)) start.ArgumentList.Add(arguments);
        start.Environment[TestSettingsEnvironmentVariable] = settingsFile;
        start.Environment[TestInstanceEnvironmentVariable] = testInstance;
        return start;
    }

    private sealed record LanguageUiCase(
        string Language,
        string ExpectedTitle,
        string ExpectedLanguage,
        string ExpectedStatusPrefix,
        bool IsRightToLeft);

    internal static T WaitFor<T>(Func<T?> get) where T : class
    {
        T? value = null;
        WaitUntil(() => (value = get()) is not null);
        return value!;
    }

    internal static void WaitUntil(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (condition()) return;
            Thread.Sleep(150);
        }
        Assert.Fail("Timed out waiting for application/UI state.");
    }
}
