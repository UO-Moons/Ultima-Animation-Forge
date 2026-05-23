using Avalonia.Controls;
using Avalonia.Interactivity;
using UltimaAnimationForge.ViewModels;

namespace UltimaAnimationForge.Views;

public partial class ArtImportAdjustWindow : Window
{
    public ArtImportAdjustWindow()
    {
        InitializeComponent();
    }

    private void QueueImport_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.QueueAdjustedArtImport();
        }

        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}