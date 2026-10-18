using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Automation;

namespace WallpaperSwitch.Tests;

public sealed partial class StartupUiTests
{
    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Installed")]
    public void InstalledAppHasOneRunningInstanceAndARegisteredTrayIcon()
    {
        var app = Environment.GetEnvironmentVariable("WALLPAPERSWITCH_INSTALLED_APP");
        if (string.IsNullOrWhiteSpace(app)) Assert.Inconclusive("Set WALLPAPERSWITCH_INSTALLED_APP for the installed-app health check.");
        Assert.AreEqual("1.3.1", FileVersionInfo.GetVersionInfo(app).ProductVersion?.Split('+')[0]);
        int RunningCount()
        {
            var processes = Process.GetProcessesByName("WallpaperSwitch");
            try { return processes.Count(process => string.Equals(process.MainModule?.FileName, app, StringComparison.OrdinalIgnoreCase)); }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        WaitUntil(() => RunningCount() == 1 && TrayIsPresent(app));
        using var duplicate = Process.Start(new ProcessStartInfo(app, "--minimized") { UseShellExecute = false })!;
        Assert.IsTrue(duplicate.WaitForExit(10000));
        Assert.AreEqual(0, duplicate.ExitCode);
        WaitUntil(() => RunningCount() == 1 && TrayIsPresent(app));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public Guid Guid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrayRectangle { public int Left, Top, Right, Bottom; }

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out TrayRectangle rectangle);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindow callback, nint parameter);
    private delegate bool EnumWindow(nint window, nint parameter);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessageTimeout(nint window, uint message, nint wParam, nint lParam,
        uint flags, uint timeout, out nint result);

    private static bool TrayIsPresent(string app)
    {
        // H.NotifyIcon's stable identity includes the executable path and an empty postfix.
        var identifier = new NotifyIconIdentifier
        {
            Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            Guid = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(app + "_")).AsSpan(0, 16))
        };
        return Shell_NotifyIconGetRect(ref identifier, out var rectangle) == 0
            && rectangle.Right > rectangle.Left;
    }

    [STATestMethod]
    [TestCategory("UI")]
    public void TrayRegistersAndRecoversFromTaskbarRecreatedNotification()
    {
        if (!File.Exists(ApplicationPath)) Assert.Inconclusive("Publish the application before running UI tests.");
        var directory = CreateIsolatedSettings(out var settingsFile, "en-US");
        using var process = Process.Start(CreateAppStartInfo(settingsFile, Guid.NewGuid().ToString("N")))!;
        try
        {
            _ = FindAppWindow(process, settingsFile);
            WaitUntil(() => ReadSharedLog(Path.Combine(directory, "startup.log")).Contains("SetupTrayIcon ready", StringComparison.Ordinal)
                && TrayIsPresent(ApplicationPath));
            var notification = RegisterWindowMessage("TaskbarCreated");
            var recipients = new List<nint>();
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out var owner);
                if (owner == process.Id) recipients.Add(window);
                return true;
            }, 0);
            Assert.IsTrue(recipients.Count >= 2, "The application and notification callback windows must both exist.");
            // Deliver only to this test process. Do not restart Explorer or broadcast to other apps.
            foreach (var recipient in recipients)
                Assert.AreNotEqual(0, SendMessageTimeout(recipient, notification, 0, 0, 2, 2000, out _));
            WaitUntil(() => TrayIsPresent(ApplicationPath));
            Assert.IsFalse(process.HasExited);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
            Directory.Delete(directory, recursive: true);
        }
    }

    [STATestMethod]
    [TestCategory("UI")]
    public void WaitingSettingsSaveShowsSpinnerWhilePreviewRemainsResponsive()
    {
        if (!File.Exists(ApplicationPath)) Assert.Inconclusive("Publish the application before running UI tests.");
        var directory = CreateIsolatedSettings(out var settingsFile, "en-US");
        using var process = Process.Start(CreateAppStartInfo(settingsFile, Guid.NewGuid().ToString("N")))!;
        try
        {
            var window = FindAppWindow(process, settingsFile);
            var picker = Find(window, "LanguageCombo");
            var scroll = (ScrollPattern)Find(window, "SettingsScrollViewer").GetCurrentPattern(ScrollPattern.Pattern);
            WaitUntil(() => FindSpinner(window) is null);
            using (var reader = new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                SelectLanguage(window, "Deutsch");
                WaitUntil(() => FindSpinner(window) is not null);
                scroll.SetScrollPercent(ScrollPattern.NoScroll, 0);
                var status = Find(window, "ModeTitle");
                var original = status.Current.Name;
                var preview = Find(window, "PreviewToggleButton");
                WaitUntil(() => preview.Current.IsEnabled && !preview.Current.IsOffscreen);
                var watch = Stopwatch.StartNew();
                ((InvokePattern)preview.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                WaitUntil(() => status.Current.Name != original);
                Assert.IsLessThan(TimeSpan.FromSeconds(5), watch.Elapsed, "The preview must finish while the disk save is still waiting.");
                Assert.AreEqual("en-US", JsonNode.Parse(File.ReadAllText(settingsFile))?["language"]?.GetValue<string>());
                Assert.IsNotNull(FindSpinner(window), "The pending save should keep its loading animation visible.");
                CaptureWindow(window, "busy-save");
            }
            WaitUntil(() => JsonNode.Parse(File.ReadAllText(settingsFile))?["language"]?.GetValue<string>() == "de-DE");
            WaitUntil(() => FindSpinner(window) is null);
            Assert.IsTrue(picker.Current.IsEnabled);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void SelectLanguage(AutomationElement window, string name)
    {
        var picker = Find(window, "LanguageCombo");
        var scroll = (ScrollPattern)Find(window, "SettingsScrollViewer").GetCurrentPattern(ScrollPattern.Pattern);
        scroll.SetScrollPercent(ScrollPattern.NoScroll, 100);
        WaitUntil(() => !picker.Current.IsOffscreen && picker.Current.IsEnabled);
        var expand = (ExpandCollapsePattern)picker.GetCurrentPattern(ExpandCollapsePattern.Pattern);
        var condition = new AndCondition(new PropertyCondition(AutomationElement.NameProperty, name),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
        var item = WaitFor(() =>
        {
            if (expand.Current.ExpandCollapseState == ExpandCollapseState.Collapsed) expand.Expand();
            // WinUI may expose the popup as another top-level window owned by this process.
            foreach (AutomationElement candidate in AutomationElement.RootElement.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ProcessIdProperty, window.Current.ProcessId)))
            {
                var found = candidate.FindFirst(TreeScope.Descendants, condition);
                if (found is not null) return found;
            }
            return null;
        });
        ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
    }

    private static AutomationElement? FindSpinner(AutomationElement window)
    {
        var spinner = window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "ActivityProgressRing"));
        return spinner is not null && !spinner.Current.IsOffscreen ? spinner : null;
    }

    [STATestMethod]
    [TestCategory("UI")]
    public void ForegroundLaunchDuringBackgroundStartupIsNotHiddenAgain()
    {
        if (!File.Exists(ApplicationPath)) Assert.Inconclusive("Publish the application before running UI tests.");
        var directory = CreateIsolatedSettings(out var settingsFile, "en-US");
        var instance = Guid.NewGuid().ToString("N");
        using var process = Process.Start(CreateAppStartInfo(settingsFile, instance, "--minimized"))!;
        try
        {
            WaitUntil(() =>
            {
                try { using var mutex = Mutex.OpenExisting(@"Local\WallpaperSwitch.SingleInstance." + instance); return true; }
                catch (WaitHandleCannotBeOpenedException) { return false; }
            });
            using var foreground = Process.Start(CreateAppStartInfo(settingsFile, instance))!;
            Assert.IsTrue(foreground.WaitForExit(10000));
            Assert.AreEqual(0, foreground.ExitCode);
            var window = FindAppWindow(process, settingsFile);
            var pattern = (WindowPattern)window.GetCurrentPattern(WindowPattern.Pattern);
            WaitUntil(() => pattern.Current.WindowVisualState == WindowVisualState.Normal && !window.Current.IsOffscreen);
            Thread.Sleep(500);
            Assert.AreEqual(WindowVisualState.Normal, pattern.Current.WindowVisualState);
            Assert.IsFalse(window.Current.IsOffscreen);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
            Directory.Delete(directory, recursive: true);
        }
    }

    [STATestMethod]
    [TestCategory("UI")]
    public void ClosingWindowWaitsAsynchronouslyForPendingSettingsSave()
    {
        if (!File.Exists(ApplicationPath)) Assert.Inconclusive("Publish the application before running UI tests.");
        var directory = CreateIsolatedSettings(out var settingsFile, "en-US");
        var settings = JsonNode.Parse(File.ReadAllText(settingsFile))!;
        settings["minimizeToTray"] = false;
        File.WriteAllText(settingsFile, settings.ToJsonString());
        using var process = Process.Start(CreateAppStartInfo(settingsFile, Guid.NewGuid().ToString("N")))!;
        try
        {
            var window = FindAppWindow(process, settingsFile);
            var picker = Find(window, "LanguageCombo");
            WaitUntil(() => FindSpinner(window) is null);
            using (var reader = new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var scroll = (ScrollPattern)Find(window, "SettingsScrollViewer").GetCurrentPattern(ScrollPattern.Pattern);
                SelectLanguage(window, "Deutsch");
                WaitUntil(() => FindSpinner(window) is not null);
                ((WindowPattern)window.GetCurrentPattern(WindowPattern.Pattern)).Close();
                Assert.IsFalse(process.WaitForExit(250), "The window must keep the process alive while its save is pending.");
            }
            Assert.IsTrue(process.WaitForExit(10000));
            Assert.AreEqual(0, process.ExitCode);
            Assert.AreEqual("de-DE", JsonNode.Parse(File.ReadAllText(settingsFile))?["language"]?.GetValue<string>());
        }
        finally
        {
            if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
            Directory.Delete(directory, recursive: true);
        }
    }
}
