using CommunityToolkit.Mvvm.ComponentModel;

namespace UltimaAnimationForge.Models;

public partial class MultiEditorGridTile : ObservableObject
{
    public int X { get; set; }
    public int Y { get; set; }

    public double ScreenX { get; set; }
    public double ScreenY { get; set; }

    public string Text => $"{X},{Y}";
}