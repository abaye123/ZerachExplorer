using LiteExplorer.Infrastructure.Commands;
using LiteExplorer.MVVM.ViewModels.Base;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace LiteExplorer.MVVM.ViewModels;

internal class MainWindowViewModel : ViewModel
{
    private string mainWindowTitle = "זרח אקספלורר";
    public string MainWindowTitle
    {
        get => mainWindowTitle;
        set => SetValue(ref mainWindowTitle, value);
    }

    private TabContentViewModel currentTab;
    public TabContentViewModel CurrentTab
    {
        get => currentTab;
        set => SetValue(ref currentTab, value);
    }

    public ObservableCollection<TabContentViewModel> Tabs { get; set; } = new();

    public ICommand CloseAppCmd { get; }
    public ICommand AddTabCmd { get; }
    public ICommand CloseTabCmd { get; }

    public MainWindowViewModel()
    {
        var initialTab = CreateTabViewModel();
        Tabs.Add(initialTab);
        CurrentTab = initialTab;

        CloseAppCmd = new ActionCommand(_ => Application.Current.Shutdown());
        AddTabCmd = new ActionCommand(OnAddTabCmdExecuted);
        CloseTabCmd = new ActionCommand(OnCloseTabCmdExecuted, _ => Tabs.Count > 1);
    }

    private static TabContentViewModel CreateTabViewModel() =>
        App.Container?.GetService<TabContentViewModel>() ?? new TabContentViewModel();

    private void OnAddTabCmdExecuted(object p)
    {
        var newTab = CreateTabViewModel();
        Tabs.Add(newTab);
        CurrentTab = newTab;
    }

    private void OnCloseTabCmdExecuted(object p)
    {
        if (p is TabContentViewModel closeTab)
        {
            closeTab.Dispose();
            Tabs.Remove(closeTab);
            CurrentTab = Tabs.LastOrDefault();
        }
    }
}
