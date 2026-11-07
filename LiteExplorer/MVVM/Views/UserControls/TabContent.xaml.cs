using LiteExplorer.MVVM.Models;
using LiteExplorer.MVVM.ViewModels;
using System.Linq;
using System.Windows.Controls;

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
}
