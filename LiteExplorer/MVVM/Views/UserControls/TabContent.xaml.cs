using LiteExplorer.MVVM.Models;
using LiteExplorer.MVVM.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LiteExplorer.MVVM.Views.UserControls;

public partial class TabContent
{
    public TabContent()
    {
        InitializeComponent();
        items.SelectionChanged += OnItemsSelectionChanged;
    }

    private void OnItemsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is TabContentViewModel vm)
        {
            vm.SetSelectedItems(items.SelectedItems.OfType<FileSystemObject>());
        }
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header && header.Tag is string columnTag && DataContext is TabContentViewModel vm)
        {
            vm.SortByColumn(columnTag);
        }
    }

    private void OnListViewItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is TabContentViewModel vm && vm.CurrentFileSystemObject != null)
        {
            vm.OpenCmd.Execute(vm.CurrentFileSystemObject.Path);
        }
    }
}
