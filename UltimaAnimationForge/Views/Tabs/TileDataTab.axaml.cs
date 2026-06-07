using Avalonia.Controls;
using Avalonia.Interactivity;
using UltimaAnimationForge.ViewModels;

namespace UltimaAnimationForge.Views.Tabs;

public partial class TileDataTab : UserControl
{
    public TileDataTab()
    {
        InitializeComponent();
    }

    private void TileDataEditField_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            vm.SelectedTileDataEntry != null)
        {
            vm.SelectedTileDataEntry.IsEdited = true;
        }
    }
}