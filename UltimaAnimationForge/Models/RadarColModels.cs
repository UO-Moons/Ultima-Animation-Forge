using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.Models;

public partial class RadarColEntry : ObservableObject
{
    [ObservableProperty]
    private bool isChecked;

    [ObservableProperty]
    private bool isPendingChange;

    public bool IsLand { get; set; }
    public int ArtId { get; set; }

    public ushort CurrentColorValue { get; set; }
    public ushort GeneratedColorValue { get; set; }

    public WriteableBitmap? Thumbnail { get; set; }

    public string TypeText => IsLand ? "Land" : "Static";

    public string IdText => "0x" + ArtId.ToString("X4") + " (" + ArtId + ")";

    public string CurrentColorText =>
        "0x" + CurrentColorValue.ToString("X4");

    public string GeneratedColorText =>
        "0x" + GeneratedColorValue.ToString("X4");

    public IBrush CurrentColorBrush =>
        new SolidColorBrush(RadarColService.ConvertUoColorToAvaloniaColor(CurrentColorValue));

    public IBrush GeneratedColorBrush =>
        new SolidColorBrush(RadarColService.ConvertUoColorToAvaloniaColor(GeneratedColorValue));

    public string MatchText =>
        CurrentColorValue == GeneratedColorValue
            ? "Exact"
            : "Different";
}

public sealed class RadarColCacheData
{
    public int CacheVersion { get; set; } = 1;
    public string UoFolderPath { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<CachedSourceFileStamp> SourceFiles { get; set; } = new();
    public List<CachedRadarColEntry> Entries { get; set; } = new();
}

public sealed class CachedRadarColEntry
{
    public bool IsLand { get; set; }
    public int ArtId { get; set; }
    public int FileIndex { get; set; }
    public string Type { get; set; } = string.Empty;
    public string SecondaryText { get; set; } = string.Empty;
    public bool IsFreeSlot { get; set; }
    public ushort GeneratedColorValue { get; set; }
}
