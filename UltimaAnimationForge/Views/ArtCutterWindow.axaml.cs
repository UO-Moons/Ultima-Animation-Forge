using Avalonia.Controls;
using Avalonia.Interactivity;
using UltimaAnimationForge.ViewModels;

namespace UltimaAnimationForge.Views;

public partial class ArtCutterWindow : Window
{
    public ArtCutterWindow()
    {
        InitializeComponent();
    }

    private void QueueChecked_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.QueueCheckedArtCutterSlices();
        }

        Close();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}