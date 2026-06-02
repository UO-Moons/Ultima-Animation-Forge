namespace UltimaAnimationForge.Models;

public sealed class SoundEntry
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double LengthSeconds { get; set; }
    public bool IsValid { get; set; }
    public bool IsTranslated { get; set; }

    public string IdHex => "0x" + Id.ToString("X3");

    public string DisplayText =>
        IsValid
            ? IdHex + "  " + Name
            : IdHex + "  <free>";

    public string LengthText =>
        IsValid ? LengthSeconds.ToString("0.00") + "s" : "Empty Slot";
}