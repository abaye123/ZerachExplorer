using LiteExplorer.Infrastructure.Commands;
using LiteExplorer.MVVM.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace LiteExplorer.MVVM.ViewModels;

internal class MainWindowViewModel : ViewModel
{
    private readonly Func<TabContentViewModel> createTab;
    private string mainWindowTitle = "זרח אקספלורר";
    private TabContentViewModel? currentTab;

    public MainWindowViewModel(Func<TabContentViewModel> createTab)
    {
        this.createTab = createTab;
        CloseAppCmd = new ActionCommand(_ => CloseMainWindow());
        AddTabCmd = new ActionCommand(_ => AddTab());
        CloseTabCmd = new ActionCommand(p => CloseTab(p as TabContentViewModel ?? CurrentTab));
        AddTab();
    }

    public string MainWindowTitle
    {
        get => mainWindowTitle;
        set => SetValue(ref mainWindowTitle, value);
    }

    public TabContentViewModel? CurrentTab
    {
        get => currentTab;
        set => SetValue(ref currentTab, value);
    }

    public ObservableCollection<TabContentViewModel> Tabs { get; } = new();

    public ICommand CloseAppCmd { get; }
    public ICommand AddTabCmd { get; }
    public ICommand CloseTabCmd { get; }

    /// <summary>Opens a folder without leaving one the user is already working in.</summary>
    public void OpenInAvailableTab(string path)
    {
        var tab = CurrentTab is { TabPath: null } home ? home : AddTab();
        tab.Open(path);
    }

    // Application.Shutdown ignores a cancelled Closing event and would bypass exit protection.
    private static void CloseMainWindow() => Application.Current.MainWindow?.Close();

    private TabContentViewModel AddTab()
    {
        var tab = createTab();
        Tabs.Add(tab);
        CurrentTab = tab;
        tab.Start();
        return tab;
    }

    private void CloseTab(TabContentViewModel? tab)
    {
        if (tab == null || !Tabs.Contains(tab))
            return;

        if (Tabs.Count == 1)
        {
            CloseMainWindow();
            return;
        }

        tab.Dispose();
        Tabs.Remove(tab);
        CurrentTab = Tabs.LastOrDefault();
    }
}
