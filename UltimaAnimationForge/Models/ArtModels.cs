using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace UltimaAnimationForge.Models;

public partial class ArtEntry : ObservableObject
{
    [ObservableProperty]
    private bool isChecked;
    public bool IsFreeSlot { get; set; }
    public int ArtId { get; set; }
    public int FileIndex { get; set; }
    public string Type { get; set; } = string.Empty;
    public string SecondaryText { get; set; } = string.Empty;
    public WriteableBitmap? Thumbnail { get; set; }

    public string DisplayText => Type + " 0x" + ArtId.ToString("X4") + " (" + ArtId + ")";
    public string ExportFileName => Type.ToLowerInvariant() + "_0x" + ArtId.ToString("X4") + ".png";
}

public sealed class ArtEraFilterConfig
{
    public List<ArtEraFilter> Eras { get; set; } = new();
}

public sealed class ArtEraFilter
{
    public string Name { get; set; } = string.Empty;
    public List<ArtEraRange> LandRanges { get; set; } = new();
    public List<ArtEraRange> StaticRanges { get; set; } = new();

    public override string ToString()
    {
        return Name;
    }
}

public sealed class ArtEraRange
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

public sealed class ArtImportAdjustOptions
{
    public bool AutoTrim { get; set; } = true;
    public bool CenterOnCanvas { get; set; }
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
}

public sealed class ArtCutterSliceEntry
{
    public int SliceIndex { get; set; }
    public int TargetArtId { get; set; }
    public bool IsChecked { get; set; } = true;
    public WriteableBitmap? PreviewBitmap { get; set; }
    public string SourceText { get; set; } = string.Empty;

    public string DisplayText =>
        "0x" + TargetArtId.ToString("X4") + " (" + TargetArtId + ")";
}