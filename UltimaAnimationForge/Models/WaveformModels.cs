namespace UltimaAnimationForge.Models;

public sealed class WaveformBar
{
    public double X { get; set; }
    public double TopY { get; set; }
    public double BottomY { get; set; }
    public double Height { get; set; }
}

public sealed class WaveformPeak
{
    public int Index { get; set; }
    public double Positive { get; set; }
    public double Negative { get; set; }
}