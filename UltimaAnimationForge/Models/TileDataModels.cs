using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UltimaAnimationForge.Models;

public partial class TileDataEntry : ObservableObject
{
    [ObservableProperty]
    private bool isEdited;

    public IBrush DisplayBrush => IsEdited
        ? Brushes.Orange
        : Brushes.White;

    partial void OnIsEditedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayBrush));
    }
    public bool IsLand { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ulong Flags { get; set; }

    public ulong OriginalFlags { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public ushort OriginalTextureId { get; set; }
    public short OriginalAnimation { get; set; }
    public byte OriginalWeight { get; set; }
    public byte OriginalQuality { get; set; }
    public byte OriginalQuantity { get; set; }
    public byte OriginalHue { get; set; }
    public byte OriginalHeight { get; set; }
    public byte OriginalStackingOffset { get; set; }
    public byte OriginalValue { get; set; }
    public ushort OriginalMiscData { get; set; }
    public byte OriginalUnknown2 { get; set; }
    public byte OriginalUnknown3 { get; set; }

    public bool IsDifferentFromOriginal()
    {
        return Flags != OriginalFlags ||
               Name != OriginalName ||
               TextureId != OriginalTextureId ||
               Animation != OriginalAnimation ||
               Weight != OriginalWeight ||
               Quality != OriginalQuality ||
               Quantity != OriginalQuantity ||
               Hue != OriginalHue ||
               Height != OriginalHeight ||
               StackingOffset != OriginalStackingOffset ||
               Value != OriginalValue ||
               MiscData != OriginalMiscData ||
               Unknown2 != OriginalUnknown2 ||
               Unknown3 != OriginalUnknown3;
    }

    public void AcceptChanges()
    {
        OriginalFlags = Flags;
        OriginalName = Name;
        OriginalTextureId = TextureId;
        OriginalAnimation = Animation;
        OriginalWeight = Weight;
        OriginalQuality = Quality;
        OriginalQuantity = Quantity;
        OriginalHue = Hue;
        OriginalHeight = Height;
        OriginalStackingOffset = StackingOffset;
        OriginalValue = Value;
        OriginalMiscData = MiscData;
        OriginalUnknown2 = Unknown2;
        OriginalUnknown3 = Unknown3;
        IsEdited = false;
    }

    public ushort TextureId { get; set; }

    public short Animation { get; set; }
    public byte Weight { get; set; }
    public byte Quality { get; set; }
    public byte Quantity { get; set; }
    public byte Hue { get; set; }
    public byte Height { get; set; }
    public byte StackingOffset { get; set; }
    public byte Value { get; set; }
    public ushort MiscData { get; set; }
    public byte Unknown2 { get; set; }
    public byte Unknown3 { get; set; }

    public string TypeText => IsLand ? "Land" : "Item";
    public string IdText => "0x" + Id.ToString("X4");
    public string DisplayText => IdText + " | " + TypeText + " | " + Name;
}

public partial class TileDataFlagOption : ObservableObject
{
    public int BitIndex { get; set; }
    public ulong Mask => 1UL << BitIndex;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [ObservableProperty]
    private bool isChecked;
}