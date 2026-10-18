using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using WallpaperSwitch.Localization;
using WallpaperSwitch.Services;

namespace WallpaperSwitch;

public sealed partial class MainWindow
{
    private bool _legalDialogOpen;

    private async void LegalNotices_Click(object sender, RoutedEventArgs e)
    {
        if (_legalDialogOpen) return;
        _legalDialogOpen = true;
        LegalNoticesButton.IsEnabled = false;
        try
        {
            var store = new LegalDocuments(AppContext.BaseDirectory);
            IReadOnlyList<LegalDocument>? catalog = null;
            await RunUiOperationAsync("legal-catalog", async () => catalog = await store.LoadCatalogAsync(_lifetime.Token));
            if (catalog is null || _closed) return;

            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            var selector = new ComboBox
            {
                Header = Localizer.GetString("Legal.Document"), DisplayMemberPath = nameof(LegalDocument.Title),
                ItemsSource = catalog, HorizontalAlignment = HorizontalAlignment.Stretch,
                MaxDropDownHeight = 320
            };
            AutomationProperties.SetAutomationId(selector, "LegalDocumentSelector");
            var spinner = new ProgressRing { Width = 24, Height = 24, IsActive = false, Visibility = Visibility.Collapsed };
            AutomationProperties.SetAutomationId(spinner, "LegalLoadingRing");
            AutomationProperties.SetName(spinner, Localizer.GetString("ActivityWorking.Text"));
            var text = new TextBox
            {
                IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                Height = Math.Clamp(RootLayout.ActualHeight - 340, 100, 330), MinWidth = 240, FontSize = 12,
                // Original legal documents are intentionally not translated.
                FlowDirection = FlowDirection.LeftToRight
            };
            AutomationProperties.SetAutomationId(text, "LegalDocumentText");
            AutomationProperties.SetName(text, Localizer.GetString("Legal.Document"));
            ScrollViewer.SetVerticalScrollBarVisibility(text, ScrollBarVisibility.Auto);
            var previous = new Button { Content = Localizer.GetString("Legal.Previous") };
            var next = new Button { Content = Localizer.GetString("Legal.Next") };
            AutomationProperties.SetAutomationId(previous, "LegalPreviousPage");
            AutomationProperties.SetAutomationId(next, "LegalNextPage");
            var pageNumber = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetAutomationId(pageNumber, "LegalPageNumber");
            var navigation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            navigation.Children.Add(previous);
            navigation.Children.Add(pageNumber);
            navigation.Children.Add(next);
            var content = new StackPanel { Spacing = 12, Width = 580, MaxWidth = Math.Max(240, RootLayout.ActualWidth - 100) };
            content.Children.Add(selector);
            content.Children.Add(spinner);
            content.Children.Add(text);
            content.Children.Add(navigation);
            var dialog = new ContentDialog
            {
                XamlRoot = RootLayout.XamlRoot, Title = Localizer.GetString("Legal.DialogTitle"),
                Content = content, CloseButtonText = Localizer.GetString("Legal.Close"),
                DefaultButton = ContentDialogButton.Close
            };
            AutomationProperties.SetAutomationId(dialog, "LegalNoticesDialog");
            IReadOnlyList<string> pages = [];
            var page = 0;
            var generation = 0;
            void RenderPage()
            {
                text.Text = pages.Count > 0 ? pages[page] : string.Empty;
                text.SelectionStart = 0;
                pageNumber.Text = pages.Count > 0 ? $"{page + 1} / {pages.Count}" : string.Empty;
                previous.IsEnabled = page > 0;
                next.IsEnabled = page + 1 < pages.Count;
            }
            async Task LoadSelectedAsync()
            {
                if (selector.SelectedItem is not LegalDocument document) return;
                var request = ++generation;
                pages = [];
                page = 0;
                RenderPage();
                SetProgress(spinner, true);
                try
                {
                    var loaded = await store.LoadPagesAsync(document, cancellation.Token);
                    if (request != generation || cancellation.IsCancellationRequested) return;
                    pages = loaded;
                    RenderPage();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    if (request == generation && !cancellation.IsCancellationRequested) { text.Text = ex.Message; CrashLog.Write(ex.ToString()); }
                }
                finally { if (request == generation && !cancellation.IsCancellationRequested) SetProgress(spinner, false); }
            }
            previous.Click += (_, _) => { if (page > 0) { page--; RenderPage(); } };
            next.Click += (_, _) => { if (page + 1 < pages.Count) { page++; RenderPage(); } };
            selector.SelectionChanged += async (_, _) => await LoadSelectedAsync();
            dialog.Opened += (_, _) => selector.SelectedIndex = 0;
            dialog.Closed += (_, _) => cancellation.Cancel();
            SizeChangedEventHandler resize = (_, _) =>
            {
                text.Height = Math.Clamp(RootLayout.ActualHeight - 340, 100, 330);
                content.MaxWidth = Math.Max(240, RootLayout.ActualWidth - 100);
            };
            RootLayout.SizeChanged += resize;
            try { await dialog.ShowAsync(); }
            finally { RootLayout.SizeChanged -= resize; }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { CrashLog.Write(ex.ToString()); if (!_closed) ShowError(ex.Message); }
        finally
        {
            _legalDialogOpen = false;
            if (!_closed) LegalNoticesButton.IsEnabled = true;
        }
    }
}
