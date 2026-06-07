using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using UltimaAnimationForge.ViewModels;

namespace UltimaAnimationForge.Views.Tabs;

public partial class LightsTab : UserControl
{
    private bool isPaintingLight;

    public LightsTab()
    {
        InitializeComponent();
    }


    private void LightPreviewPaintSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.BeginLightPaintStroke();
        }

        isPaintingLight = true;
        PaintLightFromPointer(e);
        e.Pointer.Capture(LightPreviewPaintSurface);
        e.Handled = true;
    }

    private void LightPreviewPaintSurface_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isPaintingLight)
        {
            return;
        }

        PaintLightFromPointer(e);
        e.Handled = true;
    }

    private void LightPreviewPaintSurface_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        isPaintingLight = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void PaintLightFromPointer(PointerEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (vm.SelectedLightEntry == null || vm.SelectedLightEntry.Preview == null)
        {
            return;
        }

        Point imagePoint = e.GetPosition(LightPreviewImage);

        double imageWidth = LightPreviewImage.Bounds.Width;
        double imageHeight = LightPreviewImage.Bounds.Height;

        if (imagePoint.X < 0 || imagePoint.Y < 0 ||
            imagePoint.X > imageWidth || imagePoint.Y > imageHeight)
        {
            return;
        }

        vm.PaintSelectedLightAtPreviewPoint(
            imagePoint.X,
            imagePoint.Y,
            imageWidth,
            imageHeight);
    }
}