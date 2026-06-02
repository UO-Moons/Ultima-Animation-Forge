using Avalonia.Media.Imaging;

namespace UltimaAnimationForge.Models;

public sealed class UoMapOption
{
    public string Name { get; set; } = string.Empty;
    public int MapId { get; set; }
    public int FileIndex { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public string DisplayText => $"{Name} ({Width}x{Height})";
}

public sealed class UoMapRenderResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WriteableBitmap? Bitmap { get; set; }
}