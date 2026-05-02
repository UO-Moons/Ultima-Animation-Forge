using Avalonia.Controls;
using UltimaAnimationForge.ViewModels;

namespace UltimaAnimationForge.Views;

public partial class MythicPackageWindow : Window
{
    public MythicPackageWindow()
    {
        InitializeComponent();
        DataContext = new MythicPackageViewModel();
    }
}