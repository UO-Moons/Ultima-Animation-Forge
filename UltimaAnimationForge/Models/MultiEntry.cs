using CommunityToolkit.Mvvm.ComponentModel;

namespace UltimaAnimationForge.Models;

public partial class MultiEntry : ObservableObject
{
    public int Id { get; set; }
    public bool IsEmpty { get; set; }
    public int ComponentCount { get; set; }

    public string IdHex => "0x" + Id.ToString("X4");

    public string DisplayText =>
        IsEmpty
            ? $"{Id,5} ({IdHex})  [Free]"
            : $"{Id,5} ({IdHex})  Parts: {ComponentCount}";
}