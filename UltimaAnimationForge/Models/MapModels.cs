using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace UltimaAnimationForge.Models;

public sealed class UoMapOption
{
    public string Name { get; set; } = string.Empty;
    public int MapId { get; set; }
    public int FileIndex { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public string DisplayText => $"{Name} ({Width}x{Height})";
}

public sealed class UoMapRenderResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WriteableBitmap? Bitmap { get; set; }
}

public sealed class UoMapTileDetails
{
    public int X { get; set; }
    public int Y { get; set; }

    public ushort LandTileId { get; set; }
    public sbyte LandZ { get; set; }

    public string LandTileHex => "0x" + LandTileId.ToString("X4");

    public List<UoMapStaticDetails> Statics { get; set; } = new();

    public string DisplayText =>
        $"X: {X}\nY: {Y}\nLand: {LandTileId} [{LandTileHex}]\nZ: {LandZ}\nStatics: {Statics.Count}";
}

public sealed class UoMapStaticDetails
{
    public ushort Graphic { get; set; }
    public byte LocalX { get; set; }
    public byte LocalY { get; set; }
    public sbyte Z { get; set; }
    public short Hue { get; set; }

    public string GraphicHex => "0x" + Graphic.ToString("X4");
    public string HueHex => "0x" + Hue.ToString("X4");

    public string DisplayText =>
        $"{Graphic} [{GraphicHex}] Z:{Z} Hue:{HueHex}";
}

public sealed partial class UoMapMarker : ObservableObject
{
    [ObservableProperty]
    private string label = "Marker";

    [ObservableProperty]
    private int x;

    [ObservableProperty]
    private int y;

    [ObservableProperty]
    private int mapId;

    public string DisplayText => $"{Label} ({X}, {Y})";
}

public enum UoMapAltitudeMode
{
    Normal,
    NormalWithAltitude,
    AltitudeMap
}

public enum UoMapAltitudePreset
{
    Sharp,
    Normal,
    Soft
}