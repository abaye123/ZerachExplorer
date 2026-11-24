using LiteExplorer.MVVM.Models;
using LiteExplorer.MVVM.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LiteExplorer.MVVM.Views.UserControls;

/// <summary>Mouse and keyboard behavior of the Windows file explorer.</summary>
public partial class TabContent
{
    private readonly ListBox[] lists;
    private bool isSelectingMany;

    public TabContent()
    {
        InitializeComponent();

        lists = new[] { drivesList, items, tilesList };
        foreach (var list in lists)
        {
            list.SelectionChanged += (_, _) => UpdateSelection(list);
            list.IsVisibleChanged += (_, _) => OnListVisibleChanged(list);
        }

        Selection = new RectangleSelection(SelectionLayer, SelectionFrame, (list, busy) =>
        {
            isSelectingMany = busy;
            if (!busy)
                UpdateSelection(list);
        });

        AddressInput.IsVisibleChanged += OnAddressInputVisibleChanged;
        PreviewMouseDown += OnPreviewMouseDown;
        PreviewKeyDown += OnPreviewKeyDown;
        DataContextChanged += OnDataContextChanged;
    }

    internal RectangleSelection Selection { get; }

    private TabContentViewModel? ViewModel => DataContext as TabContentViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TabContentViewModel previous)
            previous.PropertyChanged -= OnViewModelChanged;
        if (e.NewValue is TabContentViewModel current)
            current.PropertyChanged += OnViewModelChanged;
        FocusList(force: true);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TabContentViewModel.TabPath)) return;

        // Keep the end of a long path in view, and the keyboard in the list.
        Dispatcher.InvokeAsync(BreadcrumbScroll.ScrollToRightEnd, DispatcherPriority.Loaded);
        FocusList();
    }

    private void FocusList(bool force = false) => Dispatcher.InvokeAsync(() =>
    {
        if (force || !SearchInput.IsKeyboardFocusWithin && !AddressInput.IsKeyboardFocusWithin)
            lists.FirstOrDefault(list => list.IsVisible)?.Focus();
    }, DispatcherPriority.Input);

    private void OnListVisibleChanged(ListBox list)
    {
        UpdateSelection(list);
        // The list that was focused before is hidden now.
        if (list.IsVisible)
            FocusList(force: true);
        if (list.IsVisible && list.SelectedItem != null)
            list.ScrollIntoView(list.SelectedItem);
    }

    private void UpdateSelection(ListBox list)
    {
        // All lists follow the current item, but only the visible one holds the full selection.
        if (!list.IsVisible || isSelectingMany) return;

        ViewModel?.SetSelectedItems(list.SelectedItems.OfType<FileSystemObject>());
        SelectAllBox.IsChecked = list.SelectedItems.Count == 0 ? false : list.SelectedItems.Count == list.Items.Count ? true : null;
    }

    private void OnAddressInputVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!AddressInput.IsVisible)
        {
            // Hiding a focused box would pass the focus to the search box.
            FocusList(force: true);
            return;
        }

        Dispatcher.InvokeAsync(() =>
        {
            AddressInput.Focus();
            AddressInput.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void AddressInput_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Focus that leaves the window, for example to an error dialog, keeps the text for correction.
        if (e.NewFocus is DependencyObject target && Window.GetWindow(target) == Window.GetWindow(this))
            ViewModel?.CancelAddressCmd.Execute(null);
    }

    private void Address_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.ToggleAddressBarEditCmd.Execute(null);
        e.Handled = true;
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var command = e.ChangedButton switch
        {
            MouseButton.XButton1 => ViewModel?.BackCmd,
            MouseButton.XButton2 => ViewModel?.ForwardCmd,
            _ => null
        };

        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.F or Key.E) || e.Key == Key.F3)
        {
            SearchInput.Focus();
            SearchInput.SelectAll();
            e.Handled = true;
        }
    }

    private void List_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;

        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && vm.CurrentFileSystemObject is { } current)
            vm.Open(current.Path);
        else if (e.Key == Key.Back && vm.BackCmd.CanExecute(null))
            vm.BackCmd.Execute(null);
        else if (e.Key == Key.Escape)
            ((ListBox)sender).UnselectAll();
        else
            return;

        e.Handled = true;
    }

    private void List_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var list = (ListBox)sender;
        var item = FindItem(e.OriginalSource);
        // These handle the mouse themselves.
        if (FindParent<ScrollBar>(e.OriginalSource) != null || FindParent<GridViewColumnHeader>(e.OriginalSource) != null ||
            FindParent<CheckBox>(e.OriginalSource) != null)
            return;

        // A rectangle starts anywhere except on the icon and name of an item.
        if (e.ChangedButton == MouseButton.Left && (item == null || !IsOnName(e.OriginalSource, item)))
            Selection.Press(list, e.GetPosition(list), Keyboard.Modifiers);

        if (item == null)
        {
            if (Keyboard.Modifiers == ModifierKeys.None)
                list.UnselectAll();
            list.Focus();
        }
        else if (e.ChangedButton == MouseButton.Middle && item.DataContext is FileSystemObject { IsDirectory: true } folder)
        {
            ViewModel?.OpenInNewTabCmd.Execute(folder.Path);
            e.Handled = true;
        }
    }

    private void List_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            Selection.Release();
        else if (Selection.TryStart(e.GetPosition((ListBox)sender)))
            e.Handled = true;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (SelectAllBox.IsChecked == true)
            items.SelectAll();
        else
            items.UnselectAll();
        items.Focus();
    }

    /// <summary>In the details view only the name column counts as the item itself.</summary>
    private static bool IsOnName(object source, ListBoxItem item)
    {
        if (item is not ListViewItem)
            return true;

        for (var current = source as DependencyObject; current != null && current != item; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { Name: "NamePart" })
                return true;
        }
        return false;
    }

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<CheckBox>(e.OriginalSource) == null && e.ChangedButton == MouseButton.Left && FindItem(e.OriginalSource)?.DataContext is FileSystemObject target)
            ViewModel?.Open(target.Path);
    }

    private void List_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || ViewModel is not { } vm) return;

        vm.CurrentViewMode = e.Delta > 0 ? ViewMode.Tiles : ViewMode.Details;
        e.Handled = true;
    }

    private void Sidebar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || ((FrameworkElement)sender).DataContext is not FileSystemObject target) return;

        ViewModel?.OpenInNewTabCmd.Execute(target.Path);
        e.Handled = true;
    }

    private void OpenMenu_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var menu = button.ContextMenu!;
        menu.DataContext = DataContext;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader { Tag: string columnTag })
            ViewModel?.SortByColumn(columnTag);
    }

    private static ListBoxItem? FindItem(object source) => FindParent<ListBoxItem>(source);

    private static T? FindParent<T>(object source) where T : DependencyObject
    {
        var current = source as DependencyObject;
        while (current is not null and not T)
            current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
        return (T?)current;
    }
}
