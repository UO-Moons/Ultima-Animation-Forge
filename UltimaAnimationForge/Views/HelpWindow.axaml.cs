using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UltimaAnimationForge.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
    }

    public HelpWindow(string helpText)
    {
        InitializeComponent();

        DataContext = new HelpWindowViewModel
        {
            HelpText = helpText
        };
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

public sealed class HelpWindowViewModel
{
    public string HelpText { get; set; } = string.Empty;
}