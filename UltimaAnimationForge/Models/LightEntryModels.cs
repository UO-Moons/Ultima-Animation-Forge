using Avalonia.Media.Imaging;

namespace UltimaAnimationForge.Models;

public sealed class LightEntry
{
    public int Index { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsRemoved { get; set; }
    public byte[] RawData { get; set; } = [];
    public WriteableBitmap? Preview { get; set; }

    public string DisplayText => $"Light {Index} (0x{Index:X})";
    public string SizeText => IsRemoved ? "Removed" : $"{Width}x{Height}";
}