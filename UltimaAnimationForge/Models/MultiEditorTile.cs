using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UltimaAnimationForge.Models;

public partial class MultiEditorTile : ObservableObject
{
    public ushort ItemId { get; set; }

    [ObservableProperty]
    private int x;

    [ObservableProperty]
    private int y;

    [ObservableProperty]
    private int z;

    [ObservableProperty]
    private int flags = 1;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isVirtualFloor;

    public WriteableBitmap? Bitmap { get; set; }

    public string DisplayText => $"0x{ItemId:X4} X:{X} Y:{Y} Z:{Z}";

    public double ScreenX { get; set; }

    public double ScreenY { get; set; }

    public bool IsTransparentOverlay { get; set; }

    public void SetScreenPosition(double x, double y)
    {
        ScreenX = x;
        ScreenY = y;
        OnPropertyChanged(nameof(ScreenX));
        OnPropertyChanged(nameof(ScreenY));
    }

    partial void OnXChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnYChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnZChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }
}