using Avalonia.Controls;
using Avalonia.Interactivity;
using UltimaAnimationForge.ViewModels;

namespace UltimaAnimationForge.Views;

public partial class AnimDataEditorWindow : Window
{
    public AnimDataEditorWindow()
    {
        InitializeComponent();
    }

    private void AnimDataEditField_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.RefreshArtAnimDataFromEditor();
        }
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.RefreshArtAnimDataFromEditor();
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}