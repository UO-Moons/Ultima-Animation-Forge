using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;

namespace UltimaAnimationForge.Models;

public enum PropDrawOrder
{
    Behind = 0,
    InFront = 1
}

public enum PropApplyScope
{
    CurrentFrame = 0,
    CurrentDirection = 1,
    FullAnimation = 2
}

public sealed class AnimationPropOverlay
{
    public string SourceFilePath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public int OffsetX { get; set; }
    public int OffsetY { get; set; }

    public double ScalePercent { get; set; } = 100.0;
    public double RotationDegrees { get; set; } = 0.0;

    public PropDrawOrder DrawOrder { get; set; } = PropDrawOrder.InFront;

    public bool ExpandCanvasToFit { get; set; } = true;
}