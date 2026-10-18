using System.Diagnostics;
using System.Windows.Automation;

namespace WallpaperSwitch.Tests;

public sealed partial class StartupUiTests
{
    [STATestMethod]
    [TestCategory("UI")]
    public void OfflineLicenseViewerLoadsOriginalTermsAndPagesLongNotices()
    {
        if (!File.Exists(ApplicationPath)) Assert.Inconclusive("Publish the application before running UI tests.");
        var directory = CreateIsolatedSettings(out var settingsFile, "en-US");
        using var process = Process.Start(CreateAppStartInfo(settingsFile, Guid.NewGuid().ToString("N")))!;
        try
        {
            var window = FindAppWindow(process, settingsFile);
            var scroll = (ScrollPattern)Find(window, "SettingsScrollViewer").GetCurrentPattern(ScrollPattern.Pattern);
            scroll.SetScrollPercent(ScrollPattern.NoScroll, 100);
            var button = Find(window, "LegalNoticesButton");
            WaitUntil(() => !button.Current.IsOffscreen && button.Current.IsEnabled);
            ((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            _ = Find(window, "LegalNoticesDialog");
            var text = Find(window, "LegalDocumentText");
            string Text() => ((ValuePattern)text.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
            WaitUntil(() => Text().Contains("Copyright (c) 2026 RiversLee9312", StringComparison.Ordinal));
            var picker = Find(window, "LegalDocumentSelector");
            var expand = (ExpandCollapsePattern)picker.GetCurrentPattern(ExpandCollapsePattern.Pattern);
            expand.Expand();
            const string notice = "Microsoft.NETCore.App.Runtime.win-x64 8.0.30 - THIRD-PARTY-NOTICES.TXT";
            var item = WaitFor(() =>
            {
                foreach (AutomationElement candidate in AutomationElement.RootElement.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id)))
                {
                    var found = candidate.FindFirst(TreeScope.Descendants, new AndCondition(
                        new PropertyCondition(AutomationElement.NameProperty, notice),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)));
                    if (found is not null) return found;
                }
                return null;
            });
            ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            var next = Find(window, "LegalNextPage");
            WaitUntil(() => next.Current.IsEnabled && Text().Length > 0);
            Assert.IsFalse(next.Current.IsOffscreen, "Paging controls must remain visible at the current display scale.");
            var originalPage = Text();
            ((InvokePattern)next.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            WaitUntil(() => Text() != originalPage);
            var pageNumber = Find(window, "LegalPageNumber");
            Assert.IsTrue(pageNumber.Current.Name.StartsWith("2 / ", StringComparison.Ordinal));
            ((InvokePattern)Find(window, "LegalPreviousPage").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            WaitUntil(() => Text() == originalPage);
            Assert.IsFalse(ReadSharedLog(Path.Combine(directory, "startup.log")).Contains("UnhandledException:", StringComparison.Ordinal));
            CaptureWindow(window, "legal-notices");
        }
        finally
        {
            if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
            Directory.Delete(directory, recursive: true);
        }
    }
}
