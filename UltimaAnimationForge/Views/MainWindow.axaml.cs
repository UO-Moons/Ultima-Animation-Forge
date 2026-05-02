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

namespace UltimaAnimationForge.Views;

public partial class MainWindow : Window
{
    private Border? previewDropBorder;

    public MainWindow()
    {
        InitializeComponent();

        previewDropBorder = this.FindControl<Border>("PreviewDropBorder");

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
}