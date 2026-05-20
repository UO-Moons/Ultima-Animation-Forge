using Avalonia.Media.Imaging;
using System.Collections.Generic;

namespace UltimaAnimationForge.Models;

public sealed class DetachedPreviewLoadResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public List<WriteableBitmap> Frames { get; set; } = new();

    public List<VdFrameData> FrameData { get; set; } = new();

    public string PreviewInfoText { get; set; } = string.Empty;

    public int FrameCount { get; set; }

    public string FrameSizeText { get; set; } = "-";
}