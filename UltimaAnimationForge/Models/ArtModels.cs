using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace UltimaAnimationForge.Models;

public partial class ArtEntry : ObservableObject
{
    [ObservableProperty]
    private bool isPendingArtChange;

    [ObservableProperty]
    private bool isPendingTileDataChange;

    public bool IsPendingChange => IsPendingArtChange || IsPendingTileDataChange;

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

    partial void OnIsPendingArtChangeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPendingChange));
    }

    partial void OnIsPendingTileDataChangeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPendingChange));
    }
}

public sealed class ArtVisibleBoundsInfo
{
    public bool HasVisiblePixels { get; set; }

    public int FullWidth { get; set; }
    public int FullHeight { get; set; }

    public int XMin { get; set; }
    public int YMin { get; set; }
    public int XMax { get; set; }
    public int YMax { get; set; }

    public int VisibleWidth => HasVisiblePixels ? XMax - XMin + 1 : 0;
    public int VisibleHeight => HasVisiblePixels ? YMax - YMin + 1 : 0;

    public int PaddingLeft => HasVisiblePixels ? XMin : 0;
    public int PaddingTop => HasVisiblePixels ? YMin : 0;
    public int PaddingRight => HasVisiblePixels ? FullWidth - XMax - 1 : 0;
    public int PaddingBottom => HasVisiblePixels ? FullHeight - YMax - 1 : 0;

    public string FullSizeText => FullWidth + " x " + FullHeight;

    public string BoundsText =>
        HasVisiblePixels
            ? XMin + ", " + YMin + " - " + XMax + ", " + YMax
            : "No visible pixels";

    public string VisibleSizeText =>
        HasVisiblePixels
            ? VisibleWidth + " x " + VisibleHeight
            : "-";

    public string PaddingText =>
        HasVisiblePixels
            ? "L " + PaddingLeft + ", T " + PaddingTop + ", R " + PaddingRight + ", B " + PaddingBottom
            : "-";
}

public sealed class ArtCacheData
{
    public int CacheVersion { get; set; } = 1;
    public string UoFolderPath { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<CachedSourceFileStamp> SourceFiles { get; set; } = new();
    public List<CachedArtEntry> ArtEntries { get; set; } = new();
}

public sealed class CachedArtEntry
{
    public bool IsFreeSlot { get; set; }
    public int ArtId { get; set; }
    public int FileIndex { get; set; }
    public string Type { get; set; } = string.Empty;
    public string SecondaryText { get; set; } = string.Empty;
}

public sealed class ArtEraFilterConfig
{
    public List<ArtEraFilter> Eras { get; set; } = new();
    public List<ArtEraFilter> Categories { get; set; } = new();
}

public sealed class ArtEraFilter
{
    public string Name { get; set; } = string.Empty;
    public string FilterType { get; set; } = "Era";
    public List<ArtEraRange> LandRanges { get; set; } = new();
    public List<ArtEraRange> StaticRanges { get; set; } = new();

    public string DisplayName => FilterType + ": " + Name;

    public override string ToString()
    {
        return DisplayName;
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