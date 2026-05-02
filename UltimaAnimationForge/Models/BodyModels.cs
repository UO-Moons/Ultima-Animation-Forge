namespace UltimaAnimationForge.Models;

public class BodyConvEntry
{
    public int OriginalBodyId { get; set; }
    public int FileIndex { get; set; }
    public int NewBodyId { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
}

public class MobTypeEntry
{
    public int BodyId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string FlagsText { get; set; } = string.Empty;
}
