using LiteExplorer.Infrastructure.Commands;
using LiteExplorer.MVVM.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace LiteExplorer.MVVM.ViewModels;

internal class MainWindowViewModel : ViewModel
{
    private readonly Func<TabContentViewModel> createTab;
    private TabContentViewModel? currentTab;

    public MainWindowViewModel(Func<TabContentViewModel> createTab)
    {
        this.createTab = createTab;
        CloseAppCmd = new ActionCommand(_ => CloseMainWindow());
        AddTabCmd = new ActionCommand(_ => AddTab());
        CloseOtherTabsCmd = new ActionCommand(p => CloseOtherTabs(p as TabContentViewModel ?? CurrentTab), _ => Tabs.Count > 1);
        CloseTabCmd = new ActionCommand(p => CloseTab(p as TabContentViewModel ?? CurrentTab));
        AddTab();
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
    public ICommand CloseOtherTabsCmd { get; }

    /// <summary>Opens a folder without leaving one the user is already working in.</summary>
    public void OpenInAvailableTab(string path)
    {
        var tab = CurrentTab is { TabPath: null } home ? home : AddTab();
        tab.Open(path);
    }

    // Application.Shutdown ignores a cancelled Closing event and would bypass exit protection.
    private static void CloseMainWindow() => Application.Current.MainWindow?.Close();

    /// <summary>Selects by position; negative steps and overflow wrap around.</summary>
    public void SelectTab(int index) => CurrentTab = Tabs[((index % Tabs.Count) + Tabs.Count) % Tabs.Count];

    public void CycleTab(int step) => SelectTab(Tabs.IndexOf(CurrentTab!) + step);

    private TabContentViewModel AddTab(bool select = true)
    {
        var tab = createTab();
        tab.NewTabRequested += path => AddTab(select: false).Open(path);
        Tabs.Add(tab);
        if (select)
            CurrentTab = tab;
        tab.Start();
        return tab;
    }

    private void CloseOtherTabs(TabContentViewModel? keep)
    {
        if (keep == null) return;

        CurrentTab = keep;
        foreach (var tab in Tabs.Where(t => t != keep).ToList())
        {
            Tabs.Remove(tab);
            tab.Dispose();
        }
    }

    private void CloseTab(TabContentViewModel? tab)
    {
        int index = tab == null ? -1 : Tabs.IndexOf(tab);
        if (index < 0)
            return;

        if (Tabs.Count == 1)
        {
            CloseMainWindow();
            return;
        }

        bool wasCurrent = tab == CurrentTab;
        if (wasCurrent)
            CurrentTab = Tabs[index == Tabs.Count - 1 ? index - 1 : index + 1];

        Tabs.RemoveAt(index);
        tab!.Dispose();
    }
}
