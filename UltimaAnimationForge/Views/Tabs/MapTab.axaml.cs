using Avalonia.Controls;
using Avalonia.Input;
using UltimaAnimationForge.ViewModels;

namespace UltimaAnimationForge.Views.Tabs;

public partial class MapTab : UserControl
{
    public MapTab()
    {
        InitializeComponent();
    }

    private void MapPreview_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (sender is not Control control)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(control);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (vm.IsPlacingMapMarker)
        {
            vm.SelectMarkerSpotFromPreview(
                point.Position.X,
                point.Position.Y,
                control.Bounds.Width,
                control.Bounds.Height);

            e.Handled = true;
            return;
        }

        vm.NavigateMapFromPreview(
            point.Position.X,
            point.Position.Y,
            control.Bounds.Width,
            control.Bounds.Height);
    }

    private void MapPreview_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (sender is not Control control)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(control);

        vm.ZoomMapFromWheel(
            e.Delta.Y,
            point.Position.X,
            point.Position.Y,
            control.Bounds.Width,
            control.Bounds.Height);

        e.Handled = true;
    }

    private void MapPreview_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (sender is not Control control)
        {
            return;
        }

        Avalonia.Point point = e.GetPosition(control);

        vm.InspectMapFromPreview(
            point.X,
            point.Y,
            control.Bounds.Width,
            control.Bounds.Height);
    }
}