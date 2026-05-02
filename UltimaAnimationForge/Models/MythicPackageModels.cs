namespace UltimaAnimationForge.Models;

public sealed class MythicPackageEntry
{
    public string FileName { get; set; } = string.Empty;

    public ulong Hash { get; set; }

    public string HashText => "0x" + Hash.ToString("X16");

    public uint CompressedSize { get; set; }

    public uint DecompressedSize { get; set; }

    public ushort CompressionFlag { get; set; }

    public uint HeaderSize { get; set; }

    public ulong Offset { get; set; }

    public string PreviewType { get; set; } = "Unknown";

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(FileName)
            ? FileName + " | " + PreviewType + " | " + DecompressedSize + " bytes"
            : HashText + " | " + PreviewType + " | " + DecompressedSize + " bytes";

    public int BlockIndex { get; set; } = -1;
    public int FileIndex { get; set; } = -1;
}

public sealed class EcAmouBodyOption
{
    public int BodyId { get; set; }
    public string Name { get; set; } = string.Empty;

    public string DisplayText =>
        string.IsNullOrWhiteSpace(Name)
            ? "Body " + BodyId
            : "Body " + BodyId + " - " + Name;
}

public sealed class EcAmouActionOption
{
    public int BodyId { get; set; }
    public int ActionIndex { get; set; }
    public MythicPackageEntry Entry { get; set; } = new MythicPackageEntry();

    public string DisplayText =>
        "Action " + ActionIndex +
        " | Block " + Entry.BlockIndex +
        " File " + Entry.FileIndex;
}

public sealed class EcAnimationCollectionEntry
{
    public int BodyId { get; set; }
    public string BodyName { get; set; } = string.Empty;
    public int BodyType { get; set; }
    public int Layer { get; set; }

    public int ActionId { get; set; }
    public string UopFileName { get; set; } = string.Empty;
    public int BlockIndex { get; set; }
    public int FileIndex { get; set; }
}