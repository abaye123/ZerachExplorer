using LiteExplorer.MVVM.Models;
using LiteExplorer.MVVM.ViewModels;
using LiteExplorer.MVVM.Views.Windows;
using LiteExplorer.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace LiteExplorer.UnitTests;

/// <summary>Loads the real window, because XAML and binding mistakes only appear at run time.</summary>
public class MainWindowTests
{
    private sealed class BindingErrors : TraceListener
    {
        public List<string> Messages { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message) => Messages.Add(message ?? string.Empty);
    }

    [Fact]
    public void WindowWorksWithoutBindingErrors() => Ui.Run(async () =>
    {
        using var temp = new TempFolder();
        for (int i = 0; i < 200; i++)
            temp.Write($"file{i:D3}.txt", "שלום");
        temp.Write(@"sub\x.txt", "x");
        temp.Write("שיעור מספר 12 בנושא חשוב מאוד עם שם ארוך.docx", "x");
        temp.Write("SanDisk Software installation guide long name.pdf", "x");
        temp.Write("table.xlsx", "x");
        temp.Write("setup.exe", "x");
        Directory.CreateDirectory(temp.At("תיקייה בעברית"));
        Directory.CreateDirectory(temp.At("New folder (2)"));

        // Not App itself: it would start the real application with the real settings.
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var source in new[]
                 {
                     "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.Light.xaml",
                     "pack://application:,,,/ZerachExplorer;component/Themes/Theme.xaml"
                 })
        {
            application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source) });
        }

        var errors = new BindingErrors();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(errors);

        var policy = StubPolicy.Create();
        var audit = new RecordingAudit();
        var operations = new OperationsViewModel(TimeSpan.Zero);
        var dialogs = new FakeDialogs();
        var services = new ExplorerServices(policy, new FakeDialogs(), new FakeClipboard(), new DirectoryLoader(policy),
            new FileTransferService(policy, audit), new FileDeletionService(policy, audit), new FileRenameService(policy, audit), new FolderCreationService(policy, audit),
            new DriveEjectService(policy), audit, new DeviceNotificationService(), new SessionService(audit, () => { }, temp.At("none")), operations);
        var configuration = new ConfigurationService(temp.At(@"sub\config.json"));
        var main = new MainWindowViewModel(() => new TabContentViewModel(services));
        var window = new MainWindow(configuration, policy, operations, dialogs) { DataContext = main, Left = -5000, ShowInTaskbar = false, ShowActivated = false };
        window.Show();

        var tab = main.CurrentTab!;
        await Ui.Until(() => !tab.IsLoading);
        await Task.Delay(200);
        Save(window, "home");
        tab.Open(temp.Root);
        await Ui.Until(() => !tab.IsLoading);
        await Task.Delay(300);

        Save(window, "list");
        var list = Find<ListView>(window).Single();
        list.SelectedItems.Add(list.Items[150]);
        list.SelectedItems.Add(list.Items[152]);
        list.ScrollIntoView(list.Items[150]);
        await Task.Delay(200);
        Assert.Equal(2, tab.SelectedFileSystemObjects.Count);
        var scroll = Find<ScrollViewer>(list).First();
        double offset = scroll.VerticalOffset;
        Assert.True(offset > 0);

        temp.Write("file199.txt", "changed content");
        tab.Reload();
        await Ui.Until(() => !tab.IsLoading);
        await Task.Delay(200);
        Assert.Equal(2, tab.SelectedFileSystemObjects.Count);
        Assert.Equal(offset, scroll.VerticalOffset);

        list.UnselectAll();
        tab.IsPreviewPaneVisible = true;
        await Task.Delay(200);
        Save(window, "folderinfo");
        Assert.Equal("207 פריטים", tab.PreviewInfo.Summary);
        list.SelectedItem = list.Items[150];
        await Ui.Until(() => tab.PreviewInfo.TextContent == "שלום");
        Assert.Null(tab.PreviewInfo.Summary);
        list.SelectedItems.Add(list.Items[152]);
        Assert.Contains("2 פריטים", tab.PreviewInfo.FileName);
        list.SelectedItems.Remove(list.Items[152]);
        await Ui.Until(() => tab.PreviewInfo.TextContent == "שלום");
        tab.ToggleAddressBarEditCmd.Execute(null);
        await Task.Delay(200);
        Save(window, "details");

        tab.ToggleViewModeCmd.Execute(null);
        await Task.Delay(200);
        Save(window, "tiles");
        var tiles = Find<ListBox>(window).Single(l => l.IsVisible);
        tiles.ScrollIntoView(tiles.Items[0]);
        await Task.Delay(150);
        Save(window, "tilestop");
        // Every line of a name fits inside its tile.
        Assert.All(Find<ListBoxItem>(tiles), item => Assert.All(Find<TextBlock>(item), text =>
            Assert.True(text.TranslatePoint(new Point(0, text.ActualHeight), item).Y <= item.ActualHeight)));

        main.OpenInAvailableTab(temp.At("sub"));
        await Ui.Until(() => !main.CurrentTab!.IsLoading);
        Assert.Equal(2, main.Tabs.Count);
        Assert.Equal(temp.Root, tab.TabPath);

        await Task.Delay(200);
        Save(window, "tabs");
        Assert.IsNotType<TextBox>(System.Windows.Input.Keyboard.FocusedElement);
        main.CloseTabCmd.Execute(main.CurrentTab);
        Assert.Same(tab, main.CurrentTab);

        var admin = new AdminSettingsWindow(configuration, policy);
        await ShowAsync(admin, "admin0", keepOpen: true);
        for (int page = 1; page < 7; page++)
        {
            Find<TabControl>(admin).Single().SelectedIndex = page;
            await Task.Delay(60);
            Save(admin, $"admin{page}");
        }
        admin.Close();

        await ShowAsync(new MessageDialog("סוג הקובץ '.exe' אינו מאושר לפתיחה במערכת.", "גישה נדחתה", MessageKind.Warning), "warning");
        await ShowAsync(new MessageDialog("למחוק את הפריט?\nD:\\תיקייה\\file name.txt\n\nשים לב: אין בכונן זה סל מיחזור. המחיקה סופית!", "מחיקה לצמיתות", MessageKind.Question, "מחיקה"), "question");
        await ShowAsync(new MessageDialog("אירעה שגיאה", "שגיאה", MessageKind.Error), "error");
        await ShowAsync(new AboutWindow(), "about");
        await ShowAsync(new QuickUsbDialog("הדסה (\u200ED:\u200E)"), "usb");
        await ShowAsync(new NewFolderDialog(), "folder");
        await ShowAsync(new AdminPasswordDialog(configuration), "password");
        await ShowAsync(new SetPasswordDialog(configuration), "setpassword");
        await ShowAsync(new ConflictResolutionDialog("setup.exe", temp.At("setup.exe"), temp.At("table.xlsx")), "conflict");

        await ShowProgressAsync(operations, window, temp);

        foreach (var button in Find<Button>(window).Where(b => b.ContextMenu != null).Take(2))
        {
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await Task.Delay(200);
            Assert.All(button.ContextMenu!.Items.OfType<MenuItem>(), menuItem => Assert.NotNull(menuItem.Command));
            button.ContextMenu.IsOpen = false;
        }

        // The item menu is outside the visual tree, where a wrong binding silently disables every command.
        tab.CurrentViewMode = ViewMode.Details;
        await Task.Delay(200);
        var row = Find<ListViewItem>(window).First();
        row.IsSelected = true;
        row.ContextMenu!.PlacementTarget = row;
        row.ContextMenu.IsOpen = true;
        await Task.Delay(200);
        Assert.All(row.ContextMenu.Items.OfType<MenuItem>(), menuItem => Assert.NotNull(menuItem.Command));
        Save((FrameworkElement)VisualTreeHelper.GetParent(row.ContextMenu) ?? row.ContextMenu, "menu");
        row.ContextMenu.IsOpen = false;

        // Closing during an operation asks first, then cancels it and closes.
        var lastOperation = operations.RunAsync("אחרונה", Array.Empty<string>(), true, WorkUntilCancelled);
        window.Close();
        Assert.True(window.IsVisible);
        dialogs.ConfirmAnswer = true;
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        window.Close();
        await Ui.Until(() => closed);
        Assert.True(lastOperation.IsCompletedSuccessfully);

        foreach (var item in main.Tabs)
            item.Dispose();

        Assert.Empty(errors.Messages);
    });

    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;
            foreach (var nested in Find<T>(child))
                yield return nested;
        }
    }

    private static bool WorkUntilCancelled(OperationProgress progress)
    {
        while (!progress.IsCancelled)
            Thread.Sleep(10);
        return true;
    }

    private static async Task ShowProgressAsync(OperationsViewModel operations, Window owner, TempFolder temp)
    {
        var random = new Random(1);
        var copy = operations.RunAsync("העתקת 35 פריטים אל \u200EBackup\u200E", new[] { temp.Root }, true, progress =>
        {
            progress.AddTotal(35, 3_500_000_000);
            for (int i = 0; i < 18; i++)
            {
                progress.Begin($"SanDisk Software part {i}.pdf");
                for (int part = 1; part <= 10; part++)
                {
                    progress.ReportFileBytes(part * 10_000_000);
                    Thread.Sleep(random.Next(5, 40));
                }
                progress.Finish(100_000_000);
            }
            return WorkUntilCancelled(progress);
        });
        var folder = operations.RunAsync("יצירת התיקייה חדשה", new[] { temp.Root }, false, _ =>
        {
            Thread.Sleep(5500);
            return true;
        });

        await Task.Delay(4800);
        Assert.Equal(2, operations.Visible.Count);
        Assert.True(operations.IsUsing(Path.GetPathRoot(temp.Root)!));
        var window = owner.OwnedWindows.OfType<ProgressWindow>().Single();
        Assert.True(window.IsVisible);
        Save(window, "progress");
        Save(owner, "status");

        // Closing it hides it while the work goes on.
        window.Close();
        Assert.False(window.IsVisible);
        operations.ShowCmd.Execute(null);
        Assert.True(window.IsVisible);

        operations.Visible[0].CancelCmd.Execute(null);
        await copy;
        await folder;
        Assert.False(window.IsVisible);
        Assert.False(operations.HasActive);
    }

    private static async Task ShowAsync(Window dialog, string name, bool keepOpen = false)
    {
        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.Left = -5000;
        dialog.ShowActivated = false;
        dialog.Show();
        await Task.Delay(120);
        Save(dialog, name);
        if (!keepOpen)
            dialog.Close();
    }

    private static void Save(Window window, string name) => Save((FrameworkElement)VisualTreeHelper.GetChild(window, 0), name);

    /// <summary>Set ZERACH_SCREENSHOTS to a folder to look at the result.</summary>
    private static void Save(FrameworkElement content, string name)
    {
        if (Environment.GetEnvironmentVariable("ZERACH_SCREENSHOTS") is not { Length: > 0 } folder)
            return;

        var bitmap = new RenderTargetBitmap((int)(content.ActualWidth * 1.5), (int)(content.ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }
}
