using System.Collections.Generic;

namespace UltimaAnimationForge.Models;

public sealed class MultiEditorTileGroupConfig
{
    public List<MultiEditorTileGroup> Groups { get; set; } = new();
}

public sealed class MultiEditorTileGroup
{
    public string Name { get; set; } = string.Empty;
    public List<MultiEditorTileSubgroup> Subgroups { get; set; } = new();

    public override string ToString() => Name;
}

public sealed class MultiEditorTileSubgroup
{
    public string Name { get; set; } = string.Empty;
    public List<int> Items { get; set; } = new();

    public override string ToString() => Name;
}