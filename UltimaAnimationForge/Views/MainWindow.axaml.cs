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

    public MainWindow()
    {
        InitializeComponent();

        Opened += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        };

        DragDrop.AddDragOverHandler(this, OnWindowDragOver);
        DragDrop.AddDropHandler(this, OnWindowDrop);
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

    private void TileDataEditField_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is UltimaAnimationForge.ViewModels.MainWindowViewModel vm &&
            vm.SelectedTileDataEntry != null)
        {
            vm.SelectedTileDataEntry.IsEdited = true;
        }
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

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
    }
}