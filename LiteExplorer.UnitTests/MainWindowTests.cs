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

        // Not App itself: it would start the real application with the real settings.
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ThemeType.Light });
        application.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());

        var errors = new BindingErrors();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(errors);

        var policy = StubPolicy.Create();
        var audit = new RecordingAudit();
        var services = new ExplorerServices(policy, new FakeDialogs(), new FakeClipboard(), new DirectoryLoader(policy),
            new FileTransferService(policy, audit), new FileDeletionService(policy, audit), new FileRenameService(policy, audit),
            new DriveEjectService(policy), audit, new DeviceNotificationService(), new SessionService(audit, () => { }, temp.At("none")));
        var configuration = new ConfigurationService(temp.At(@"sub\config.json"));
        var main = new MainWindowViewModel(() => new TabContentViewModel(services));
        var window = new MainWindow(configuration, policy) { DataContext = main, Left = -5000, ShowInTaskbar = false, ShowActivated = false };
        window.Show();

        var tab = main.CurrentTab!;
        tab.Open(temp.Root);
        await Ui.Until(() => !tab.IsLoading);
        await Task.Delay(300);

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

        tab.IsPreviewPaneVisible = true;
        await Ui.Until(() => tab.PreviewInfo.TextContent == "שלום");
        tab.ToggleAddressBarEditCmd.Execute(null);
        await Task.Delay(200);
        Save(window, "details");

        tab.ToggleViewModeCmd.Execute(null);
        await Task.Delay(200);
        Save(window, "tiles");

        main.OpenInAvailableTab(temp.At("sub"));
        await Ui.Until(() => !main.CurrentTab!.IsLoading);
        Assert.Equal(2, main.Tabs.Count);
        Assert.Equal(temp.Root, tab.TabPath);

        var admin = new AdminSettingsWindow(configuration, policy) { Left = -5000, ShowInTaskbar = false, ShowActivated = false };
        admin.Show();
        await Task.Delay(100);
        Find<TabControl>(admin).Single().SelectedIndex = 4;
        await Task.Delay(100);
        Save(admin, "admin");
        admin.Close();
        new AboutWindow { Left = -5000 }.Close();
        new QuickUsbDialog(@"E:\", "Stick").Close();

        window.Close();
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

    /// <summary>Set ZERACH_SCREENSHOTS to a folder to look at the result.</summary>
    private static void Save(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("ZERACH_SCREENSHOTS") is not { Length: > 0 } folder)
            return;

        var content = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }
}
