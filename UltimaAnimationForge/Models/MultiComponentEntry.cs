namespace UltimaAnimationForge.Models;

public sealed class MultiComponentEntry
{
    public ushort ItemId { get; set; }
    public short X { get; set; }
    public short Y { get; set; }
    public short Z { get; set; }
    public int Flags { get; set; }
    public int Unknown { get; set; }
    public int Solver { get; set; }
}