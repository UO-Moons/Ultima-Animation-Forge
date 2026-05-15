using System;
using System.Collections.Generic;

namespace UltimaAnimationForge.Models;

public class AppSettings
{
    public string LastActiveProfileId { get; set; } = string.Empty;
    public List<AnimationViewerProfile> Profiles { get; set; } = new();
}

public class AnimationViewerProfile
{
    public string OutputFolderPath { get; set; } = string.Empty;
    public string ProfileId { get; set; } = Guid.NewGuid().ToString("N");
    public string ProfileName { get; set; } = "Default";
    public string UoFolderPath { get; set; } = string.Empty;

    public string SelectedAnimationFile { get; set; } = "All Files";
    public string SelectedBodyType { get; set; } = "All";
    public string SearchText { get; set; } = string.Empty;

    public string SelectedDirection { get; set; } = string.Empty;
    public double PreviewZoomLevel { get; set; } = 1.0;

    public bool ShowCheckerBackground { get; set; } = true;
    public bool LoopPlayback { get; set; } = true;

    public bool LoadUopFiles { get; set; } = true;

    public override string ToString()
    {
        return ProfileName ?? string.Empty;
    }
}

public class AnimationCacheData
{
    public int CacheVersion { get; set; } = 2;

    public string ProfileId { get; set; } = string.Empty;
    public string UoFolderPath { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<CachedSourceFileStamp> SourceFiles { get; set; } = new();
    public List<AnimationEntry> AnimationEntries { get; set; } = new();
    public List<MulSlotEntry> MulSlotEntries { get; set; } = new();
}

public class CachedSourceFileStamp
{
    public string FilePath { get; set; } = string.Empty;
    public long Length { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
}
