using LiteExplorer.MVVM.Models;
using LiteExplorer.MVVM.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LiteExplorer.MVVM.Views.UserControls;

public partial class TabContent
{
    public TabContent()
    {
        InitializeComponent();

        foreach (var list in new ListBox[] { items, tilesList })
        {
            list.SelectionChanged += (_, _) => UpdateSelection(list);
            list.IsVisibleChanged += (_, _) => UpdateSelection(list);
        }

        AddressInput.IsVisibleChanged += OnAddressInputVisibleChanged;
    }

    private void UpdateSelection(ListBox list)
    {
        // Both lists follow the current item, but only the visible one holds the full selection.
        if (list.IsVisible && DataContext is TabContentViewModel vm)
            vm.SetSelectedItems(list.SelectedItems.OfType<FileSystemObject>());
    }

    private void OnAddressInputVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!AddressInput.IsVisible) return;

        Dispatcher.InvokeAsync(() =>
        {
            AddressInput.Focus();
            AddressInput.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader { Tag: string columnTag } && DataContext is TabContentViewModel vm)
            vm.SortByColumn(columnTag);
    }

    private void OnListViewItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is TabContentViewModel { CurrentFileSystemObject: { } current } vm)
            vm.Open(current.Path);
    }
}
