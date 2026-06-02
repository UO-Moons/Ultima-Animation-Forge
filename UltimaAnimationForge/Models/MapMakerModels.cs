using Avalonia.Media;
using System.Collections.Generic;

namespace UltimaAnimationForge.Models;

public enum DragonBrushShape
{
    Circle,
    Square,
    Diamond,
    HorizontalLine,
    VerticalLine,
    Slash,
    Backslash,
    Cross,
    X
}

public enum DragonMapTool
{
    Paint,
    Eyedropper,
    Fill,
    Erase,
    Rectangle,
    Line,
    Ellipse,
    Stamp
}

public sealed class DragonMapStamp
{
    public string Name { get; set; } = string.Empty;
    public int[,] Pattern { get; set; } = new int[0, 0];

    public override string ToString() => Name;
}

public sealed class DragonTerrainColor
{
    public int PaletteIndex { get; set; }
    public string PaletteHex => PaletteIndex.ToString("X2");

    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;

    public int Z { get; set; }
    public List<int> TileIds { get; set; } = new();

    public Color Color { get; set; }
    public IBrush Brush => new SolidColorBrush(Color);

    public string Name => $"{GroupName} {Z}";
    public string TerrainType => GroupName;

    public string DisplayText =>
        $"{PaletteHex} | {GroupName} | Z {Z} | Tiles {TileIds.Count}";
}