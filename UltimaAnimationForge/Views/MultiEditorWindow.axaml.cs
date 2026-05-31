using Avalonia.Controls;
using Avalonia.Input;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.ViewModels;

namespace UltimaAnimationForge.Views;

public partial class MultiEditorWindow : Window
{
    public MultiEditorWindow()
    {
        InitializeComponent();
    }

    private void EditorTile_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Image image)
        {
            return;
        }

        if (image.DataContext is not MultiEditorTile tile)
        {
            return;
        }

        if (DataContext is not MultiEditorViewModel viewModel)
        {
            return;
        }

        if (tile.IsVirtualFloor)
        {
            viewModel.HandleCanvasClick(tile.ScreenX + 22, tile.ScreenY + 11);
            e.Handled = true;
            return;
        }

        viewModel.HandleTileCanvasClick(tile);
        e.Handled = true;
    }

    private void EditorCanvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Canvas canvas)
        {
            return;
        }

        if (DataContext is not MultiEditorViewModel viewModel)
        {
            return;
        }

        var point = e.GetPosition(canvas);
        viewModel.HandleCanvasClick(point.X, point.Y);
        e.Handled = true;
    }

    private void MultiEditorWindow_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MultiEditorViewModel viewModel)
        {
            return;
        }

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (ctrl && e.Key == Key.Z && !shift)
        {
            viewModel.UndoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if ((ctrl && e.Key == Key.Y) || (ctrl && shift && e.Key == Key.Z))
        {
            viewModel.RedoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.S:
                viewModel.UseSelectToolCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.D:
                viewModel.UseDrawToolCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.R:
                viewModel.UseRemoveToolCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.P:
                viewModel.UsePipetteToolCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Left:
                viewModel.MoveSelectedLeftCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Right:
                viewModel.MoveSelectedRightCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Up:
                viewModel.MoveSelectedUpCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Down:
                viewModel.MoveSelectedDownCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.OemOpenBrackets:
                viewModel.MoveSelectedZDownCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.OemCloseBrackets:
                viewModel.MoveSelectedZUpCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Delete:
                viewModel.RemoveSelectedCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}