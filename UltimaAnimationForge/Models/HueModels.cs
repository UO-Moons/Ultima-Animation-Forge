using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace UltimaAnimationForge.Models;

public partial class HueEditorColorSlot : ObservableObject
{
    [ObservableProperty]
    private int index;

    [ObservableProperty]
    private ushort rawValue;

    [ObservableProperty]
    private Color color;

    public string IndexText => Index.ToString("00");
    public string RawHexText => "0x" + RawValue.ToString("X4");
    public IBrush Brush => new SolidColorBrush(Color);

    partial void OnIndexChanged(int value) => OnPropertyChanged(nameof(IndexText));
    partial void OnRawValueChanged(ushort value) => OnPropertyChanged(nameof(RawHexText));
    partial void OnColorChanged(Color value) => OnPropertyChanged(nameof(Brush));
}

public partial class HueEditorEntry : ObservableObject
{
    [ObservableProperty]
    private int hueId;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private ushort tableStart;

    [ObservableProperty]
    private ushort tableEnd;

    public ObservableCollection<HueEditorColorSlot> Colors { get; } = new();

    public string DisplayText => string.IsNullOrWhiteSpace(Name) ? HueId.ToString() : HueId + " - " + Name;
    public string HexText => "0x" + HueId.ToString("X4");
    public string RangeText => TableStart + " - " + TableEnd;

    partial void OnHueIdChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(HexText));
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayText));
    partial void OnTableStartChanged(ushort value) => OnPropertyChanged(nameof(RangeText));
    partial void OnTableEndChanged(ushort value) => OnPropertyChanged(nameof(RangeText));
}