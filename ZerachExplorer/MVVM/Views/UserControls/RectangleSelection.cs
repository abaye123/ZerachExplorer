using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ZerachExplorer.MVVM.Views.UserControls;

/// <summary>
/// Selects the items under a rectangle drawn with the mouse. Positions are kept relative to the whole
/// content, so the selection stays correct while the list scrolls.
/// </summary>
internal sealed class RectangleSelection
{
    private const double ScrollStep = 24;

    private readonly Canvas layer;
    private readonly Rectangle frame;
    private readonly Action<ListBox, bool> setBusy;
    private readonly DispatcherTimer autoScroll = new() { Interval = TimeSpan.FromMilliseconds(40) };

    private ListBox? list;
    private Panel? panel;
    private ScrollViewer? scroll;
    private HashSet<object> initial = new();
    private Point anchor;
    private Point lastMouse;
    private Point pressed;
    private ModifierKeys modifiers;
    private bool isPending;

    /// <param name="setBusy">Told before and after each batch of selection changes.</param>
    public RectangleSelection(Canvas layer, Rectangle frame, Action<ListBox, bool> setBusy)
    {
        this.layer = layer;
        this.frame = frame;
        this.setBusy = setBusy;
        layer.MouseMove += (_, e) => Update(e.GetPosition(list));
        layer.MouseLeftButtonUp += (_, _) => layer.ReleaseMouseCapture();
        layer.LostMouseCapture += (_, _) => End();
        autoScroll.Tick += (_, _) => ScrollTowardsMouse();
    }

    public bool IsActive { get; private set; }

    /// <summary>Remembers where the button went down; the rectangle starts only if the mouse is dragged.</summary>
    public void Press(ListBox target, Point position, ModifierKeys keys)
    {
        list = target;
        pressed = position;
        modifiers = keys;
        initial = target.SelectedItems.Cast<object>().ToHashSet();
        isPending = true;
    }

    public void Release() => isPending = false;

    public bool TryStart(Point position)
    {
        if (!isPending || list == null ||
            (Math.Abs(position.X - pressed.X) < SystemParameters.MinimumHorizontalDragDistance * 2 &&
             Math.Abs(position.Y - pressed.Y) < SystemParameters.MinimumVerticalDragDistance * 2))
            return false;

        isPending = false;
        if (!Begin())
            return false;

        // The list would otherwise keep selecting by itself while it holds the mouse.
        Mouse.Capture(layer);
        Update(position);
        return true;
    }

    internal bool Begin()
    {
        panel = FindChild<Panel>(list!, p => p.IsItemsHost);
        scroll = FindChild<ScrollViewer>(list!, _ => true);
        if (panel == null || scroll == null)
            return false;

        anchor = ToContent(pressed);
        IsActive = true;
        frame.Visibility = Visibility.Visible;
        autoScroll.Start();
        return true;
    }

    internal void Update(Point mouse)
    {
        if (!IsActive || list == null || panel == null) return;

        lastMouse = mouse;
        var area = new Rect(anchor, ToContent(mouse));
        Draw(area);

        var hit = Enumerable.Range(0, list.Items.Count).Where(index => Bounds(index).IntersectsWith(area)).Select(index => list.Items[index]).ToHashSet();
        Apply(Combine(initial, hit, modifiers));
    }

    internal void End()
    {
        autoScroll.Stop();
        IsActive = false;
        frame.Visibility = Visibility.Collapsed;
    }

    internal static HashSet<object> Combine(HashSet<object> initial, HashSet<object> hit, ModifierKeys keys)
    {
        var result = new HashSet<object>(hit);
        if ((keys & ModifierKeys.Control) != 0)
            result.SymmetricExceptWith(initial);
        else if ((keys & ModifierKeys.Shift) != 0)
            result.UnionWith(initial);
        return result;
    }

    private void Apply(HashSet<object> wanted)
    {
        setBusy(list!, true);
        foreach (var item in list!.SelectedItems.Cast<object>().Where(item => !wanted.Contains(item)).ToList())
            list.SelectedItems.Remove(item);
        foreach (var item in wanted.Except(list.SelectedItems.Cast<object>()).ToList())
            list.SelectedItems.Add(item);
        setBusy(list, false);
    }

    // A virtualizing panel only has the rows in view and scrolls by itself; any other panel is moved as a whole.
    private Point ToContent(Point position)
    {
        var point = list!.TranslatePoint(position, panel);
        return panel is VirtualizingPanel ? new Point(point.X, point.Y + scroll!.VerticalOffset) : point;
    }

    private Rect Bounds(int index)
    {
        if (panel is VirtualizingPanel)
        {
            // Rows have equal heights, so rows out of view need no container. The desired size includes margins.
            double height = panel.Children.Count == 0 ? 0 : panel.Children[0].DesiredSize.Height;
            return new Rect(0, index * height, panel.ActualWidth, Math.Max(0, height - 1));
        }

        return list!.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement container
            ? new Rect(container.TranslatePoint(default, panel), new Size(container.ActualWidth, container.ActualHeight))
            : Rect.Empty;
    }

    private void Draw(Rect area)
    {
        double offset = panel is VirtualizingPanel ? scroll!.VerticalOffset : 0;
        var topLeft = panel!.TranslatePoint(new Point(area.Left, area.Top - offset), layer);
        var bottomRight = panel.TranslatePoint(new Point(area.Right, area.Bottom - offset), layer);
        var visible = Rect.Intersect(new Rect(topLeft, bottomRight), new Rect(0, 0, layer.ActualWidth, layer.ActualHeight));
        if (visible.IsEmpty) return;

        Canvas.SetLeft(frame, visible.X);
        Canvas.SetTop(frame, visible.Y);
        frame.Width = visible.Width;
        frame.Height = visible.Height;
    }

    private void ScrollTowardsMouse()
    {
        double step = lastMouse.Y < 0 ? -ScrollStep : lastMouse.Y > list!.ActualHeight ? ScrollStep : 0;
        if (step == 0) return;

        scroll!.ScrollToVerticalOffset(scroll.VerticalOffset + step);
        scroll.UpdateLayout();
        Update(lastMouse);
    }

    private static T? FindChild<T>(DependencyObject root, Func<T, bool> matches) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found && matches(found))
                return found;
            if (FindChild(child, matches) is { } nested)
                return nested;
        }
        return null;
    }
}
