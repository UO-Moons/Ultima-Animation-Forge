using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UltimaAnimationForge.Models;

public partial class EquipmentArtPickerEntry : ObservableObject
{
    public int ArtId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;

    [ObservableProperty]
    private WriteableBitmap? thumbnail;
}

public partial class EquipmentGumpPickerEntry : ObservableObject
{
    public int GumpId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public bool IsUsed { get; set; }

    [ObservableProperty]
    private WriteableBitmap? thumbnail;
}