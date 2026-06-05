using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Linq;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.ViewModels;

namespace UltimaAnimationForge.Views.Tabs;

public partial class MapMakerTab : UserControl
{
    private bool isPaintingDragonMap;

    public MapMakerTab()
    {
        InitializeComponent();
    }

    private void DragonMapPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (sender is not Image image)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(image);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (vm.SelectedDragonMapTool == DragonMapTool.Paint ||
            vm.SelectedDragonMapTool == DragonMapTool.Erase)
        {
            isPaintingDragonMap = true;
        }

        Avalonia.Point position = e.GetPosition(image);

        int mapX = (int)(position.X / vm.DragonMapZoom);
        int mapY = (int)(position.Y / vm.DragonMapZoom);

        if (vm.SelectedDragonMapTool == DragonMapTool.Paint)
        {
            isPaintingDragonMap = true;
        }

        vm.HandleDragonMapClick(mapX, mapY);

        e.Handled = true;
    }

    private void DragonMapPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (sender is not Image image)
        {
            return;
        }

        Avalonia.Point position = e.GetPosition(image);

        int mapX = (int)(position.X / vm.DragonMapZoom);
        int mapY = (int)(position.Y / vm.DragonMapZoom);

        if (!isPaintingDragonMap)
        {
            vm.UpdateDragonMapHoverStatus(mapX, mapY);
        }
        vm.UpdateDragonBrushPreview(mapX, mapY);

        if (!isPaintingDragonMap)
        {
            return;
        }

        if (vm.SelectedDragonMapTool != DragonMapTool.Paint &&
            vm.SelectedDragonMapTool != DragonMapTool.Erase)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(image);

        if (!point.Properties.IsLeftButtonPressed)
        {
            isPaintingDragonMap = false;
            return;
        }

        if (vm.SelectedDragonMapTool == DragonMapTool.Erase)
        {
            vm.EraseDragonMapAt(mapX, mapY);
        }
        else
        {
            vm.PaintDragonMapAt(mapX, mapY);
        }
        image.InvalidateVisual();
        e.Handled = true;
    }

    private async void OpenDragonMapButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open Dragon BMP",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Dragon Map Images")
            {
                Patterns = ["*.bmp", "*.png"]
            }
                ]
            });

        IStorageFile? file = files.FirstOrDefault();

        if (file == null || file.Path == null)
        {
            return;
        }

        vm.OpenDragonMapFromFile(file.Path.LocalPath);
    }

    private async void SaveDragonMapButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save Dragon BMP",
                SuggestedFileName = "dragon_map.bmp",
                FileTypeChoices =
                [
                    new FilePickerFileType("Bitmap Image")
            {
                Patterns = ["*.bmp"]
            }
                ]
            });

        if (file == null || file.Path == null)
        {
            return;
        }

        vm.SaveDragonMapToFile(file.Path.LocalPath);
    }

    private async void ExportDragonMapUopButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }
                TopLevel? topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null)
                {
                    return;
                }

            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export map0LegacyMUL.uop",
                SuggestedFileName = "map0LegacyMUL.uop",
                FileTypeChoices = new[]
                {
                new FilePickerFileType("UOP Map")
                {
                    Patterns = new[] { "*.uop" }
                }
                }
            });

        if (file == null || file.Path == null)
        {
            return;
        }

        vm.ExportDragonMapToUop(file.Path.LocalPath);
    }

    private async void ExportDragonMapMulButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export map0.mul",
                SuggestedFileName = "map0.mul",
                FileTypeChoices = new[]
                {
                new FilePickerFileType("MUL Map")
                {
                    Patterns = new[] { "*.mul" }
                }
                }
            });

        if (file == null || file.Path == null)
        {
            return;
        }

        vm.ExportDragonMapToMul(file.Path.LocalPath);
    }

    private async void ExportDragonMapMulAndUopButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

                TopLevel? topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null)
                {
                    return;
                }

                IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions
            {
                Title = "Choose export folder",
                AllowMultiple = false
            });

        IStorageFolder? folder = folders.FirstOrDefault();

        if (folder == null || folder.Path == null)
        {
            return;
        }

        vm.ExportDragonMapMulAndUop(folder.Path.LocalPath);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (isPaintingDragonMap && DataContext is MainWindowViewModel vm)
        {
            vm.RefreshDragonMapAfterStroke();
        }

        isPaintingDragonMap = false;
    }
}