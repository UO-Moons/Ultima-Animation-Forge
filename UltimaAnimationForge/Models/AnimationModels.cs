using Avalonia.Media.Imaging;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UltimaAnimationForge.Models;

public class AnimationEntry
{
    public string DisplayName { get; set; } = string.Empty;
    public string SecondaryText { get; set; } = string.Empty;
    public int BodyId { get; set; }
    public int ActionId { get; set; }
    public int FrameCount { get; set; }
    public string FrameSize { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;

    public string SourceMode { get; set; } = string.Empty; // "MUL" or "UOP"

    public int IndexNumber { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; }
    public int Extra { get; set; }
}

public class AnimationIdxEntry
{
    public int Offset { get; set; }
    public int Length { get; set; }
    public int Extra { get; set; }
    public int Index { get; set; }
}

public class UoAnimationFile
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // MUL or UOP
}

public class MulSlotEntry
{
    public string FileName { get; set; } = string.Empty;   // anim.idx, anim2.idx, etc.
    public int FileType { get; set; }                      // 1=anim, 2=anim2, etc.
    public int BodyIndex { get; set; }
    public int TrueBodyId { get; set; }
    public int AnimLength { get; set; }                    // 13 / 22 / 35

    public bool IsEmpty { get; set; } = true;

    public string TypeLetter =>
        AnimLength switch
        {
            13 => "L",
            22 => "H",
            35 => "P",
            _ => "?"
        };

    public string DisplayText =>
        FileName + " | " + TypeLetter + ": " + BodyIndex + " (" + TrueBodyId + ")";
}

public class ResolvedAnimationBlock
{
    public int BodyId { get; set; }
    public int ResolvedBodyId { get; set; }
    public int ActionIndex { get; set; }
    public int DirectionIndex { get; set; }

    public int SlotIndex { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; }
    public int Extra { get; set; }

    public string MulPath { get; set; } = string.Empty;
    public string IdxPath { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public string DebugText { get; set; } = string.Empty;

    public bool IsUop { get; set; }
    public int UopFileIndex { get; set; } = -1;
    public string UopVirtualPath { get; set; } = string.Empty;

    // New sequence/remap fields
    public int RequestedActionIndex { get; set; } = -1;
    public int ResolvedUopGroupIndex { get; set; } = -1;
    public int SequenceFrameCount { get; set; } = -1;
    public bool UsedSequenceRemap { get; set; }
    public int RemapTargetGroupIndex { get; set; } = -1;
}

public sealed class VdFrameData
{
    public required WriteableBitmap Bitmap { get; init; }

    // Optional palette storage if you want to preserve original palette info later.
    public List<ushort>? Palette565 { get; init; }

    public short CenterX { get; init; }
    public short CenterY { get; init; }

    public ushort Width { get; init; }
    public ushort Height { get; init; }

    public short InitCoordsX { get; init; }
    public short InitCoordsY { get; init; }
    public short EndCoordsX { get; init; }
    public short EndCoordsY { get; init; }

    public ushort FrameId { get; init; }
    public ushort FrameNumber { get; init; }
    public uint DataOffset { get; init; }
    public int SourceExtra { get; init; }
}

public sealed class AnimationFrameThumbnail
{
    public int FrameIndex { get; set; }

    public required WriteableBitmap Bitmap { get; init; }

    public string DisplayText => (FrameIndex + 1).ToString("D3");
}

public sealed class AnimationBrowserTileViewModel : INotifyPropertyChanged
{
    private WriteableBitmap? thumbnail;
    private bool isLoadingThumbnail;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AnimationEntry? SourceEntry { get; set; }

    public WriteableBitmap? Thumbnail
    {
        get => thumbnail;
        set
        {
            if (!ReferenceEquals(thumbnail, value))
            {
                thumbnail = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsLoadingThumbnail
    {
        get => isLoadingThumbnail;
        set
        {
            if (isLoadingThumbnail != value)
            {
                isLoadingThumbnail = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsMissingSlot { get; set; }

    public int BodyId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string SecondaryText { get; set; } = string.Empty;

    public string SourceText { get; set; } = string.Empty;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}