using System.Collections.Generic;

namespace UltimaAnimationForge.Models;

public sealed class MultiEditorRoofProfileConfig
{
    public List<MultiEditorRoofProfile> RoofProfiles { get; set; } = new();
}

public sealed class MultiEditorRoofProfile
{
    public string Name { get; set; } = string.Empty;

    public int EastWestLeftSlope { get; set; }
    public int EastWestRightSlope { get; set; }
    public int EastWestRidge { get; set; }
    public int EastWestNorthCap { get; set; }
    public int EastWestSouthCap { get; set; }

    public int NorthSouthLeftSlope { get; set; }
    public int NorthSouthRightSlope { get; set; }
    public int NorthSouthRidge { get; set; }
    public int NorthSouthNorthCap { get; set; }
    public int NorthSouthSouthCap { get; set; }

    public override string ToString() => Name;
}