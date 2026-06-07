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

public partial class ArtTab : UserControl
{
    private bool isArtBrowserDragChecking;
    private bool artBrowserDragCheckValue;

    public ArtTab()
    {
        InitializeComponent();

        ListBox? mulSlotListBox = this.FindControl<ListBox>("MulSlotListBox");

        if (mulSlotListBox != null)
        {
            DragDrop.AddDragOverHandler(mulSlotListBox, OnMulSlotDragOver);
            DragDrop.AddDropHandler(mulSlotListBox, OnMulSlotDrop);
        }
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

        TopLevel? topLevel = TopLevel.GetTopLevel(this);

        if (topLevel is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        e.Handled = true;
    }

    private void ArtTileDataEditField_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is UltimaAnimationForge.ViewModels.MainWindowViewModel vm)
        {
            vm.CommitArtTileDataFieldEdits();
        }
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

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        isArtBrowserDragChecking = false;
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
}