namespace UltimaAnimationForge.Models;

public sealed class MusicEntry
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool Loop { get; set; }
    public bool Exists { get; set; }

    public string DisplayText =>
        Id.ToString("D3") + "  " + FileName + (Loop ? "  [loop]" : "");

    public string StatusText =>
        Exists ? "Found" : "Missing";

    public string InfoText { get; set; } = string.Empty;
}