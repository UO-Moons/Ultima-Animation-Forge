namespace UltimaAnimationForge.Models;

public sealed class EquipmentBinderPreview
{
    public int ItemArtId { get; set; }
    public int TileDataAnimationId { get; set; }

    public int DisplayBodyId { get; set; }
    public int AnimationBodyId { get; set; }

    public int FileType { get; set; }
    public int SlotBodyIndex { get; set; }

    public string EquipmentName { get; set; } = string.Empty;
    public string MobType { get; set; } = "EQUIPMENT";

    public string BodyDefLine { get; set; } = string.Empty;
    public string BodyConvLine { get; set; } = string.Empty;
    public string MobTypeLine { get; set; } = string.Empty;

    public bool BodyDefExists { get; set; }
    public bool BodyConvExists { get; set; }
    public bool MobTypeExists { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string TileDataLine { get; set; } = string.Empty;
    public bool UpdateTileDataAnimation { get; set; }
}

public sealed class EquipmentBinderResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}