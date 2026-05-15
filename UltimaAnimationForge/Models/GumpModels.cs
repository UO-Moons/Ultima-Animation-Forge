using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UltimaAnimationForge.Models;

public sealed partial class GumpEntry : ObservableObject
{
    [ObservableProperty]
    private bool isSelectedForExport;
    public int GumpId { get; set; }
    public int Lookup { get; set; }
    public int Length { get; set; }
    public int Extra { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsValid { get; set; }
    public string SourceFile { get; set; } = string.Empty;

    public string DisplayText =>
        IsValid
            ? GumpId + " [0x" + GumpId.ToString("X") + "]"
            : GumpId + " [0x" + GumpId.ToString("X") + "] Free";

    public string SecondaryText => string.Empty;

    public string HexId => "0x" + GumpId.ToString("X");

    public string SizeText =>
        Width > 0 && Height > 0
            ? Width + " x " + Height
            : "-";

    public string LengthText =>
        Length > 0
            ? Length + " bytes"
            : "-";
}

    public sealed class GumpLoadResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WriteableBitmap? Bitmap { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class GumpSaveResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public enum GumpFreeSlotMode
{
    All = 0,
    MaleWearables = 1,
    FemaleWearables = 2
}

public enum GumpOverlayBlendMode
{
    NormalFade = 0,
    Multiply = 1,
    Overlay = 2,
    SoftLight = 3,
    Screen = 4
}