using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.ViewModels;

namespace UltimaAnimationForge.Views.Tabs;

public partial class AnimationEditorTab : UserControl
{
    private Border? previewDropBorder;

    public AnimationEditorTab()
    {
        InitializeComponent();

        previewDropBorder = this.FindControl<Border>("PreviewDropBorder");

        if (previewDropBorder != null)
        {
            DragDrop.AddDragOverHandler(previewDropBorder, OnPreviewDragOver);
            DragDrop.AddDropHandler(previewDropBorder, OnPreviewDrop);
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
            string? localPath = item.TryGetLocalPath();

            if (!string.IsNullOrWhiteSpace(localPath))
            {
                results.Add(localPath);
            }
        }

        return results;
    }

    private void OnPreviewDragOver(object? sender, DragEventArgs e)
    {
        if (!HasFileDrop(e))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        List<string> paths = GetDroppedLocalPaths(e);

        bool allPng =
            paths.Count > 0 &&
            paths.All(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

        e.DragEffects = allPng ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnPreviewDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        List<string> paths = GetDroppedLocalPaths(e);
        if (paths.Count == 0)
        {
            return;
        }

        await vm.HandlePreviewDroppedFilesAsync(paths);
    }

    private void PreviewDragSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || previewDropBorder == null)
        {
            return;
        }

        Point position = e.GetPosition(previewDropBorder);

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
        if (DataContext is not MainWindowViewModel vm || previewDropBorder == null)
        {
            return;
        }

        Point position = e.GetPosition(previewDropBorder);

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

    private void ReplaceThumbnailMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: AnimationFrameThumbnail thumbnail } &&
            DataContext is MainWindowViewModel vm &&
            vm.ReplaceFrameThumbnailCommand.CanExecute(thumbnail))
        {
            vm.ReplaceFrameThumbnailCommand.Execute(thumbnail);
        }
    }

    private void RemoveThumbnailMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: AnimationFrameThumbnail thumbnail } &&
            DataContext is MainWindowViewModel vm &&
            vm.RemoveFrameThumbnailCommand.CanExecute(thumbnail))
        {
            vm.RemoveFrameThumbnailCommand.Execute(thumbnail);
        }
    }
}