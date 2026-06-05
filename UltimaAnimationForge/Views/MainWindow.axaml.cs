using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.ViewModels;

namespace UltimaAnimationForge.Views;

public partial class MainWindow : Window
{
    private Border? previewDropBorder;
    private ListBox? mulSlotListBox;
    private bool isPaintingLight;
    private GumpBuilderElement? draggedGumpBuilderElement;
    private Point gumpBuilderDragStartPointer;
    private int gumpBuilderDragStartX;
    private int gumpBuilderDragStartY;
    private GumpBuilderElement? resizingGumpBuilderElement;
    private string gumpBuilderResizeHandle = string.Empty;
    private Point gumpBuilderResizeStartPointer;
    private int gumpBuilderResizeStartX;
    private int gumpBuilderResizeStartY;
    private int gumpBuilderResizeStartWidth;
    private int gumpBuilderResizeStartHeight;
    private bool isArtBrowserDragChecking;
    private bool artBrowserDragCheckValue;

    private bool isPaintingDragonMap;

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

    public MainWindow()
    {
        InitializeComponent();

        previewDropBorder = this.FindControl<Border>("PreviewDropBorder");
        mulSlotListBox = this.FindControl<ListBox>("MulSlotListBox");

        Opened += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        };

        DragDrop.AddDragOverHandler(this, OnWindowDragOver);
        DragDrop.AddDropHandler(this, OnWindowDrop);

        if (previewDropBorder != null)
        {
            DragDrop.AddDragOverHandler(previewDropBorder, OnPreviewDragOver);
            DragDrop.AddDropHandler(previewDropBorder, OnPreviewDrop);
        }

        if (mulSlotListBox != null)
        {
            DragDrop.AddDragOverHandler(mulSlotListBox, OnMulSlotDragOver);
            DragDrop.AddDropHandler(mulSlotListBox, OnMulSlotDrop);
        }
    }

    private static bool HasFileDrop(DragEventArgs e)
    {
        return e.Data.Contains(DataFormats.Files);
    }

    private static List<string> GetDroppedLocalPaths(DragEventArgs e)
    {
        List<string> results = new();

        if (!e.Data.Contains(DataFormats.Files))
        {
            return results;
        }

        IEnumerable<IStorageItem>? items = e.Data.GetFiles();
        if (items == null)
        {
            return results;
        }

        foreach (IStorageItem item in items)
        {
            try
            {
                string? localPath = item.TryGetLocalPath();

                if (!string.IsNullOrWhiteSpace(localPath))
                {
                    results.Add(localPath);
                }
            }
            catch
            {
                // Ignore non-local items.
            }
        }

        return results;
    }

    private void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        if (!HasFileDrop(e))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        List<string> paths = GetDroppedLocalPaths(e);

        if (paths.Count == 0)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        bool hasFolder = paths.Any(System.IO.Directory.Exists);
        bool hasVd = paths.Any(path => path.EndsWith(".vd", StringComparison.OrdinalIgnoreCase));
        bool allPng =
            paths.Count > 0 &&
            paths.All(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

        if (hasFolder || hasVd || allPng)
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnWindowDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        List<string> paths = GetDroppedLocalPaths(e);
        if (paths.Count == 0)
        {
            return;
        }

        bool allPng =
            paths.Count > 0 &&
            paths.All(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

        if (allPng)
        {
            return;
        }

        await viewModel.HandleDroppedFilesAsync(paths);
    }

    private void OnPreviewDragOver(object? sender, DragEventArgs e)
    {
        if (!HasFileDrop(e))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        List<string> paths = GetDroppedLocalPaths(e);
        if (paths.Count == 0)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        bool allPng =
            paths.Count > 0 &&
            paths.All(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

        e.DragEffects = allPng
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnPreviewDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        List<string> paths = GetDroppedLocalPaths(e);
        if (paths.Count == 0)
        {
            return;
        }

        await viewModel.HandlePreviewDroppedFilesAsync(paths);
    }

    private void ReplaceThumbnailMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        if (menuItem.Tag is not AnimationFrameThumbnail thumbnail)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.ReplaceFrameThumbnailCommand.CanExecute(thumbnail))
        {
            viewModel.ReplaceFrameThumbnailCommand.Execute(thumbnail);
        }
    }

    private void RemoveThumbnailMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        if (menuItem.Tag is not AnimationFrameThumbnail thumbnail)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.RemoveFrameThumbnailCommand.CanExecute(thumbnail))
        {
            viewModel.RemoveFrameThumbnailCommand.Execute(thumbnail);
        }
    }

    private void PreviewDragSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        Point position = e.GetPosition(PreviewDropBorder);

        if (vm.CompareOverlayDragModeEnabled)
        {
            vm.BeginCompareOverlayDrag(position);
            e.Pointer.Capture((IInputElement?)sender);
            e.Handled = true;
            return;
        }

        if (vm.PreviewDragModeEnabled)
        {
            bool affectAllFrames = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            vm.BeginPreviewDrag(position, affectAllFrames);
            e.Pointer.Capture((IInputElement?)sender);
            e.Handled = true;
        }
    }

    private void PreviewDragSurface_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        Point position = e.GetPosition(PreviewDropBorder);

        if (vm.CompareOverlayDragModeEnabled)
        {
            vm.UpdateCompareOverlayDrag(position);
            e.Handled = true;
            return;
        }

        if (vm.PreviewDragModeEnabled)
        {
            vm.UpdatePreviewDrag(position);
            e.Handled = true;
        }
    }

    private void PreviewDragSurface_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        vm.EndCompareOverlayDrag();
        vm.EndPreviewDrag();

        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void PreviewDragSurface_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.EndCompareOverlayDrag();
            vm.EndPreviewDrag();
        }
    }

    private async void CopyAnimationBrowserBodyIdMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Avalonia.Controls.MenuItem menuItem)
        {
            return;
        }

        if (menuItem.Tag is not AnimationBrowserTileViewModel tile || tile.SourceEntry == null)
        {
            return;
        }

        if (Clipboard == null)
        {
            return;
        }

        await Clipboard.SetTextAsync(tile.SourceEntry.BodyId.ToString());
    }

    private async void CopyAnimationBrowserSourceFileMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Avalonia.Controls.MenuItem menuItem)
        {
            return;
        }

        if (menuItem.Tag is not AnimationBrowserTileViewModel tile || tile.SourceEntry == null)
        {
            return;
        }

        if (Clipboard == null)
        {
            return;
        }

        await Clipboard.SetTextAsync(tile.SourceEntry.SourceFile ?? string.Empty);
    }

    private void AnimationBrowserTile_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Avalonia.Controls.Border border)
        {
            return;
        }

        if (border.DataContext is not AnimationBrowserTileViewModel tile)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.StartAnimationBrowserTileHoverPreview(tile);
    }

    private void AnimationBrowserTile_PointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.StopAnimationBrowserTileHoverPreview();
    }

    private async void EditAnimationBrowserNameMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Avalonia.Controls.MenuItem menuItem)
        {
            return;
        }

        if (menuItem.Tag is not AnimationBrowserTileViewModel tile || tile.SourceEntry == null)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string currentName = tile.DisplayName ?? string.Empty;
        string prefix = tile.SourceEntry.BodyId + " - ";

        if (currentName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            currentName = currentName.Substring(prefix.Length);
        }

        string? newName = await ShowEditAnimationNameDialog(tile.SourceEntry.BodyId, currentName);

        if (newName == null)
        {
            return;
        }

        viewModel.SaveAnimationBrowserName(tile, newName);

        await ShowSimpleMessage(
            "Name Saved",
            "Saved to animation_names.json.\n\nMake sure this file is included with your tool build/release.");
    }

    private async Task<string?> ShowEditAnimationNameDialog(int bodyId, string currentName)
    {
        TextBox nameBox = new TextBox
        {
            Text = currentName,
            Width = 320,
            Watermark = "Animation name..."
        };

        Button cancelButton = new Button
        {
            Content = "Cancel"
        };

        Button saveButton = new Button
        {
            Content = "Save"
        };

        Window dialog = new Window
        {
            Title = "Edit Animation Name",
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
            {
                new TextBlock
                {
                    Text = "Body " + bodyId + " name:",
                    FontWeight = FontWeight.Bold
                },
                nameBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        cancelButton,
                        saveButton
                    }
                }
            }
            }
        };

        cancelButton.Click += (_, _) => dialog.Close(null);
        saveButton.Click += (_, _) => dialog.Close(nameBox.Text ?? string.Empty);

        return await dialog.ShowDialog<string?>(this);
    }

    private async Task ShowSimpleMessage(string title, string message)
    {
        Button okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Right
        };

        Window dialog = new Window
        {
            Title = title,
            Width = 460,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 14,
                Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                okButton
            }
            }
        };

        okButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    private void OnMulSlotDragOver(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        if (!viewModel.CanAcceptDroppedVdForSelectedMulSlot())
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        List<string> paths = GetDroppedLocalPaths(e);

        bool hasVd =
            paths.Count > 0 &&
            paths.Any(path => path.EndsWith(".vd", StringComparison.OrdinalIgnoreCase));

        e.DragEffects = hasVd ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnMulSlotDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        List<string> paths = GetDroppedLocalPaths(e);
        if (paths.Count == 0)
        {
            return;
        }

        await viewModel.HandleDroppedFilesAsync(paths);
        e.Handled = true;
    }

    private void TileDataEditField_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is UltimaAnimationForge.ViewModels.MainWindowViewModel vm &&
            vm.SelectedTileDataEntry != null)
        {
            vm.SelectedTileDataEntry.IsEdited = true;
        }
    }

    private void ArtTileDataEditField_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is UltimaAnimationForge.ViewModels.MainWindowViewModel vm)
        {
            vm.CommitArtTileDataFieldEdits();
        }
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

    private void GumpBuilderElement_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        if (control.DataContext is not GumpBuilderElement element)
        {
            return;
        }

        draggedGumpBuilderElement = element;
        gumpBuilderDragStartX = element.X;
        gumpBuilderDragStartY = element.Y;

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectedGumpBuilderElement = element;
        }

        Control? canvas = control.FindAncestorOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        gumpBuilderDragStartPointer = e.GetPosition(canvas);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void GumpBuilderElement_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (draggedGumpBuilderElement == null)
        {
            return;
        }

        if (sender is not Control control)
        {
            return;
        }

        Control? canvas = control.FindAncestorOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Point currentPointer = e.GetPosition(canvas);

        int deltaX = (int)Math.Round(currentPointer.X - gumpBuilderDragStartPointer.X);
        int deltaY = (int)Math.Round(currentPointer.Y - gumpBuilderDragStartPointer.Y);

        if (DataContext is MainWindowViewModel viewModel)
        {
            draggedGumpBuilderElement.X = viewModel.SnapGumpBuilderValue(gumpBuilderDragStartX + deltaX);
            draggedGumpBuilderElement.Y = viewModel.SnapGumpBuilderValue(gumpBuilderDragStartY + deltaY);
        }
        else
        {
            draggedGumpBuilderElement.X = gumpBuilderDragStartX + deltaX;
            draggedGumpBuilderElement.Y = gumpBuilderDragStartY + deltaY;
        }

        e.Handled = true;
    }

    private void GumpBuilderElement_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control control)
        {
            e.Pointer.Capture(null);
        }

        draggedGumpBuilderElement = null;
        e.Handled = true;
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        GumpBuilderElement? element = viewModel.SelectedGumpBuilderElement;
        if (element == null)
        {
            return;
        }

        int step = viewModel.GumpBuilderSnapToGrid ? Math.Max(1, viewModel.GumpBuilderGridSize) : 1;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            step *= 10;
        }

        switch (e.Key)
        {
            case Key.Left:
                element.X = viewModel.SnapGumpBuilderValue(element.X - step);
                e.Handled = true;
                break;

            case Key.Right:
                element.X = viewModel.SnapGumpBuilderValue(element.X + step);
                e.Handled = true;
                break;

            case Key.Up:
                element.Y = viewModel.SnapGumpBuilderValue(element.Y - step);
                e.Handled = true;
                break;

            case Key.Down:
                element.Y = viewModel.SnapGumpBuilderValue(element.Y + step);
                e.Handled = true;
                break;

            case Key.Delete:
                viewModel.DeleteSelectedGumpBuilderElementCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void GumpBuilderResizeHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        if (control.DataContext is not GumpBuilderElement element)
        {
            return;
        }

        resizingGumpBuilderElement = element;
        gumpBuilderResizeHandle = control.Tag?.ToString() ?? string.Empty;

        gumpBuilderResizeStartX = element.X;
        gumpBuilderResizeStartY = element.Y;
        gumpBuilderResizeStartWidth = element.Width;
        gumpBuilderResizeStartHeight = element.Height;

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectedGumpBuilderElement = element;
        }

        Control? canvas = control.FindAncestorOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        gumpBuilderResizeStartPointer = e.GetPosition(canvas);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void GumpBuilderResizeHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (resizingGumpBuilderElement == null)
        {
            return;
        }

        if (sender is not Control control)
        {
            return;
        }

        Control? canvas = control.FindAncestorOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Point currentPointer = e.GetPosition(canvas);

        int deltaX = (int)Math.Round(currentPointer.X - gumpBuilderResizeStartPointer.X);
        int deltaY = (int)Math.Round(currentPointer.Y - gumpBuilderResizeStartPointer.Y);

        int newX = gumpBuilderResizeStartX;
        int newY = gumpBuilderResizeStartY;
        int newWidth = gumpBuilderResizeStartWidth;
        int newHeight = gumpBuilderResizeStartHeight;

        if (gumpBuilderResizeHandle.Contains("W"))
        {
            newX = gumpBuilderResizeStartX + deltaX;
            newWidth = gumpBuilderResizeStartWidth - deltaX;
        }

        if (gumpBuilderResizeHandle.Contains("E"))
        {
            newWidth = gumpBuilderResizeStartWidth + deltaX;
        }

        if (gumpBuilderResizeHandle.Contains("N"))
        {
            newY = gumpBuilderResizeStartY + deltaY;
            newHeight = gumpBuilderResizeStartHeight - deltaY;
        }

        if (gumpBuilderResizeHandle.Contains("S"))
        {
            newHeight = gumpBuilderResizeStartHeight + deltaY;
        }

        const int minSize = 8;

        if (newWidth < minSize)
        {
            if (gumpBuilderResizeHandle.Contains("W"))
            {
                newX = gumpBuilderResizeStartX + gumpBuilderResizeStartWidth - minSize;
            }

            newWidth = minSize;
        }

        if (newHeight < minSize)
        {
            if (gumpBuilderResizeHandle.Contains("N"))
            {
                newY = gumpBuilderResizeStartY + gumpBuilderResizeStartHeight - minSize;
            }

            newHeight = minSize;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            newX = viewModel.SnapGumpBuilderValue(newX);
            newY = viewModel.SnapGumpBuilderValue(newY);
            newWidth = viewModel.SnapGumpBuilderValue(newWidth);
            newHeight = viewModel.SnapGumpBuilderValue(newHeight);
        }

        resizingGumpBuilderElement.X = newX;
        resizingGumpBuilderElement.Y = newY;
        resizingGumpBuilderElement.Width = newWidth;
        resizingGumpBuilderElement.Height = newHeight;

        e.Handled = true;
    }

    private void GumpBuilderResizeHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);

        resizingGumpBuilderElement = null;
        gumpBuilderResizeHandle = string.Empty;

        e.Handled = true;
    }

    private void ArtBrowserTile_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not ArtEntry entry)
        {
            return;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            vm.SelectedArtEntry = entry;
        }

        PointerPoint point = e.GetCurrentPoint(border);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        entry.IsChecked = !entry.IsChecked;
        artBrowserDragCheckValue = entry.IsChecked;
        isArtBrowserDragChecking = true;

        e.Handled = true;
    }

    private void ArtBrowserTile_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (!isArtBrowserDragChecking)
        {
            return;
        }

        if (sender is not Border border || border.DataContext is not ArtEntry entry)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(border);

        if (!point.Properties.IsLeftButtonPressed)
        {
            isArtBrowserDragChecking = false;
            return;
        }

        entry.IsChecked = artBrowserDragCheckValue;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        isArtBrowserDragChecking = false;

        if (isPaintingDragonMap && DataContext is MainWindowViewModel vm)
        {
            vm.RefreshDragonMapAfterStroke();
        }

        isPaintingDragonMap = false;
    }

    private void ArtBrowserTile_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not ArtEntry entry)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        vm.SelectedArtEntry = entry;

        ArtPreviewWindow window = new()
        {
            DataContext = vm
        };

        window.Show(this);

        e.Handled = true;
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

    private async void OpenDragonMapButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open Dragon BMP",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                new FilePickerFileType("Dragon Map Images")
                {
                    Patterns = new[] { "*.bmp", "*.png" }
                }
                }
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

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save Dragon BMP",
                SuggestedFileName = "dragon_map.bmp",
                FileTypeChoices = new[]
                {
                new FilePickerFileType("Bitmap Image")
                {
                    Patterns = new[] { "*.bmp" }
                }
                }
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

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
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

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(
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

        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
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