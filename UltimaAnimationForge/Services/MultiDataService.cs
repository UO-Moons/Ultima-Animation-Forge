using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class MultiDataService
{
    public const int MaximumMultiIndex = 0x2200;

    private readonly Dictionary<int, List<MultiComponentEntry>> loadedMultis = new();

    public IReadOnlyDictionary<int, List<MultiComponentEntry>> LoadedMultis => loadedMultis;

    public bool Load(string folderPath, bool useUohsaFormat, out string message)
    {
        loadedMultis.Clear();

        string idxPath = Path.Combine(folderPath, "multi.idx");
        string mulPath = Path.Combine(folderPath, "multi.mul");

        if (!File.Exists(idxPath) || !File.Exists(mulPath))
        {
            message = "multi.idx or multi.mul was not found.";
            return false;
        }

        int recordSize = useUohsaFormat ? 16 : 12;

        using FileStream idxStream = File.OpenRead(idxPath);
        using FileStream mulStream = File.OpenRead(mulPath);
        using BinaryReader idxReader = new(idxStream);
        using BinaryReader mulReader = new(mulStream);

        for (int id = 0; id < MaximumMultiIndex; id++)
        {
            if (idxStream.Position + 12 > idxStream.Length)
            {
                break;
            }

            int lookup = idxReader.ReadInt32();
            int length = idxReader.ReadInt32();
            _ = idxReader.ReadInt32();

            if (lookup < 0 || length <= 0 || lookup >= mulStream.Length)
            {
                continue;
            }

            int count = length / recordSize;
            List<MultiComponentEntry> components = new(count);

            mulStream.Seek(lookup, SeekOrigin.Begin);

            for (int i = 0; i < count; i++)
            {
                if (mulStream.Position + recordSize > mulStream.Length)
                {
                    break;
                }

                components.Add(new MultiComponentEntry
                {
                    ItemId = mulReader.ReadUInt16(),
                    X = mulReader.ReadInt16(),
                    Y = mulReader.ReadInt16(),
                    Z = mulReader.ReadInt16(),
                    Flags = mulReader.ReadInt32(),
                    Unknown = useUohsaFormat ? mulReader.ReadInt32() : 0
                });
            }

            if (components.Count > 0)
            {
                loadedMultis[id] = components;
            }
        }

        message = $"Loaded {loadedMultis.Count} multis.";
        return true;
    }

    public List<MultiComponentEntry> GetComponents(int id)
    {
        return loadedMultis.TryGetValue(id, out List<MultiComponentEntry>? components)
            ? components
            : new List<MultiComponentEntry>();
    }

    public WriteableBitmap? BuildSimplePreview(List<MultiComponentEntry> components, int heightCut)
    {
        if (components.Count == 0)
        {
            return null;
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (MultiComponentEntry part in components)
        {
            if (part.Z > heightCut)
            {
                continue;
            }

            minX = Math.Min(minX, part.X);
            minY = Math.Min(minY, part.Y);
            maxX = Math.Max(maxX, part.X);
            maxY = Math.Max(maxY, part.Y);
        }

        if (minX == int.MaxValue)
        {
            return null;
        }

        int tileSize = 8;
        int width = Math.Max(64, ((maxX - minX) + 3) * tileSize);
        int height = Math.Max(64, ((maxY - minY) + 3) * tileSize);

        byte[] pixels = new byte[width * height * 4];

        foreach (MultiComponentEntry part in components)
        {
            if (part.Z > heightCut)
            {
                continue;
            }

            int px = ((part.X - minX) + 1) * tileSize;
            int py = ((part.Y - minY) + 1) * tileSize;

            byte shade = (byte)(80 + Math.Abs(part.Z * 4 % 120));

            FillRect(pixels, width, height, px, py, tileSize - 1, tileSize - 1, shade);
        }

        WriteableBitmap bitmap = new(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);

        return bitmap;
    }

    private static void FillRect(byte[] pixels, int width, int height, int x, int y, int w, int h, byte shade)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            if (yy < 0 || yy >= height)
            {
                continue;
            }

            for (int xx = x; xx < x + w; xx++)
            {
                if (xx < 0 || xx >= width)
                {
                    continue;
                }

                int offset = ((yy * width) + xx) * 4;

                pixels[offset + 0] = shade;
                pixels[offset + 1] = (byte)(shade + 25);
                pixels[offset + 2] = (byte)(shade + 45);
                pixels[offset + 3] = 255;
            }
        }
    }

    public WriteableBitmap? BuildRenderedPreview(
        List<MultiComponentEntry> components,
        int heightCut,
        Func<int, WriteableBitmap?> getArtBitmap,
Func<int, int> getItemHeight,
Func<int, bool> getItemBackground)
    {
        if (components.Count == 0)
        {
            return null;
        }

        int minOffsetX = components.Min(x => x.X);
        int minOffsetY = components.Min(x => x.Y);
        int maxOffsetX = components.Max(x => x.X);
        int maxOffsetY = components.Max(x => x.Y);

        int centerX = -minOffsetX;
        int centerY = -minOffsetY;

        int widthTiles = (maxOffsetX - minOffsetX) + 1;
        int heightTiles = (maxOffsetY - minOffsetY) + 1;

        List<NormalizedMultiTile>[,] tiles = new List<NormalizedMultiTile>[widthTiles, heightTiles];

        for (int x = 0; x < widthTiles; x++)
        {
            for (int y = 0; y < heightTiles; y++)
            {
                tiles[x, y] = new List<NormalizedMultiTile>();
            }
        }

        int solver = 0;

        foreach (MultiComponentEntry part in components)
        {
            int x = part.X + centerX;
            int y = part.Y + centerY;

            if (x < 0 || y < 0 || x >= widthTiles || y >= heightTiles)
            {
                continue;
            }

            WriteableBitmap? bitmap = getArtBitmap(part.ItemId);

            if (bitmap == null)
            {
                continue;
            }

            tiles[x, y].Add(new NormalizedMultiTile
            {
                X = x,
                Y = y,
                Part = part,
                Bitmap = bitmap,
                Solver = solver++
            });
        }

        for (int x = 0; x < widthTiles; x++)
        {
            for (int y = 0; y < heightTiles; y++)
            {
                tiles[x, y].Sort((left, right) =>
     CompareNormalizedMultiTiles(left, right, getItemHeight, getItemBackground));
            }
        }

        int xMin = 1000;
        int yMin = 1000;
        int xMax = -1000;
        int yMax = -1000;

        for (int x = 0; x < widthTiles; x++)
        {
            for (int y = 0; y < heightTiles; y++)
            {
                foreach (NormalizedMultiTile tile in tiles[x, y])
                {
                    int px = (x - y) * 22;
                    int py = (x + y) * 22;

                    px -= tile.Bitmap.PixelSize.Width / 2;
                    py -= tile.Part.Z << 2;
                    py -= tile.Bitmap.PixelSize.Height;

                    xMin = Math.Min(xMin, px);
                    yMin = Math.Min(yMin, py);

                    xMax = Math.Max(xMax, px + tile.Bitmap.PixelSize.Width);
                    yMax = Math.Max(yMax, py + tile.Bitmap.PixelSize.Height);
                }
            }
        }

        int canvasWidth = Math.Clamp(xMax - xMin, 64, 8192);
        int canvasHeight = Math.Clamp(yMax - yMin, 64, 8192);

        byte[] targetPixels = new byte[canvasWidth * canvasHeight * 4];

        for (int x = 0; x < widthTiles; x++)
        {
            for (int y = 0; y < heightTiles; y++)
            {
                foreach (NormalizedMultiTile tile in tiles[x, y])
                {
                    if (tile.Part.Z > heightCut)
                    {
                        continue;
                    }

                    int px = (x - y) * 22;
                    int py = (x + y) * 22;

                    px -= tile.Bitmap.PixelSize.Width / 2;
                    py -= tile.Part.Z << 2;
                    py -= tile.Bitmap.PixelSize.Height;

                    px -= xMin;
                    py -= yMin;

                    BlitBitmap(targetPixels, canvasWidth, canvasHeight, tile.Bitmap, px, py);
                }
            }
        }

        WriteableBitmap result = new(
            new PixelSize(canvasWidth, canvasHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = result.Lock();
        Marshal.Copy(targetPixels, 0, framebuffer.Address, targetPixels.Length);

        return result;
    }

    private static void BlitBitmap(
    byte[] targetPixels,
    int targetWidth,
    int targetHeight,
    WriteableBitmap source,
    int drawX,
    int drawY)
    {
        using ILockedFramebuffer sourceBuffer = source.Lock();

        int sourceWidth = source.PixelSize.Width;
        int sourceHeight = source.PixelSize.Height;
        int sourceRowBytes = sourceBuffer.RowBytes;

        byte[] sourcePixels = new byte[sourceRowBytes * sourceHeight];
        Marshal.Copy(sourceBuffer.Address, sourcePixels, 0, sourcePixels.Length);

        for (int y = 0; y < sourceHeight; y++)
        {
            int targetY = drawY + y;

            if (targetY < 0 || targetY >= targetHeight)
            {
                continue;
            }

            for (int x = 0; x < sourceWidth; x++)
            {
                int targetX = drawX + x;

                if (targetX < 0 || targetX >= targetWidth)
                {
                    continue;
                }

                int sourceOffset = (y * sourceRowBytes) + (x * 4);
                byte alpha = sourcePixels[sourceOffset + 3];

                if (alpha == 0)
                {
                    continue;
                }

                int targetOffset = ((targetY * targetWidth) + targetX) * 4;

                targetPixels[targetOffset + 0] = sourcePixels[sourceOffset + 0];
                targetPixels[targetOffset + 1] = sourcePixels[sourceOffset + 1];
                targetPixels[targetOffset + 2] = sourcePixels[sourceOffset + 2];
                targetPixels[targetOffset + 3] = alpha;
            }
        }
    }

    private sealed class NormalizedMultiTile
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Solver { get; set; }
        public required MultiComponentEntry Part { get; set; }
        public required WriteableBitmap Bitmap { get; set; }
    }

    private static int CompareNormalizedMultiTiles(
        NormalizedMultiTile left,
        NormalizedMultiTile right,
        Func<int, int> getItemHeight,
        Func<int, bool> getItemBackground)
    {
        int leftThreshold = GetMultiTileThreshold(left.Part.ItemId, getItemHeight, getItemBackground);
        int rightThreshold = GetMultiTileThreshold(right.Part.ItemId, getItemHeight, getItemBackground);

        int leftZ = left.Part.Z + leftThreshold;
        int rightZ = right.Part.Z + rightThreshold;

        int result = leftZ - rightZ;

        if (result == 0)
        {
            result = leftThreshold - rightThreshold;
        }

        if (result == 0)
        {
            result = left.Solver - right.Solver;
        }

        return result;
    }

    private static int GetMultiTileThreshold(
        ushort itemId,
        Func<int, int> getItemHeight,
        Func<int, bool> getItemBackground)
    {
        int threshold = 0;

        if (getItemHeight(itemId) > 0)
        {
            threshold++;
        }

        if (!getItemBackground(itemId))
        {
            threshold++;
        }

        return threshold;
    }

    public bool LoadUop(string folderPath, out string message)
    {
        loadedMultis.Clear();

        string uopPath = Path.Combine(folderPath, "multicollection.uop");

        if (!File.Exists(uopPath))
        {
            message = "multicollection.uop was not found.";
            return false;
        }

        try
        {
            using FileStream fileStream = new(uopPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader reader = new(fileStream);

            uint magic = reader.ReadUInt32();

            if (magic != 0x0050594D)
            {
                message = "Invalid multicollection.uop file.";
                return false;
            }

            uint version = reader.ReadUInt32();

            if (version > 5)
            {
                message = "Unsupported multicollection.uop version.";
                return false;
            }

            reader.ReadUInt32(); // signature
            ulong nextTableOffset = reader.ReadUInt64();
            reader.ReadUInt32(); // block capacity
            reader.ReadUInt32(); // file count
            reader.ReadUInt32(); // reserved
            reader.ReadUInt32(); // reserved
            reader.ReadUInt32(); // reserved

            List<(long DataOffset, uint CompressedSize, uint DecompressedSize)> entries = new();

            ulong next = nextTableOffset;

            while (next != 0)
            {
                fileStream.Seek((long)next, SeekOrigin.Begin);

                int count = reader.ReadInt32();
                next = reader.ReadUInt64();

                for (int i = 0; i < count; i++)
                {
                    ulong dataOffset = reader.ReadUInt64();
                    uint headerSize = reader.ReadUInt32();
                    uint compressedSize = reader.ReadUInt32();
                    uint decompressedSize = reader.ReadUInt32();

                    reader.ReadUInt64(); // hash
                    reader.ReadUInt32(); // unknown

                    ushort flag = reader.ReadUInt16();

                    if (dataOffset == 0 || decompressedSize == 0)
                    {
                        continue;
                    }

                    if (flag == 0)
                    {
                        compressedSize = 0;
                    }

                    entries.Add(((long)(dataOffset + headerSize), compressedSize, decompressedSize));
                }
            }

            foreach ((long dataOffset, uint compressedSize, uint decompressedSize) in entries)
            {
                fileStream.Seek(dataOffset, SeekOrigin.Begin);

                byte[] raw;

                if (compressedSize > 0)
                {
                    byte[] compressed = reader.ReadBytes((int)compressedSize);

                    // If your project already has UopUtils.Decompress, use it here.
                    (bool ok, byte[] decompressed) = UopUtils.Decompress(compressed);

                    if (!ok)
                    {
                        continue;
                    }

                    raw = decompressed;
                }
                else
                {
                    raw = reader.ReadBytes((int)decompressedSize);
                }

                using MemoryStream memoryStream = new(raw);
                using BinaryReader binaryReader = new(memoryStream);

                if (memoryStream.Length < 8)
                {
                    continue;
                }

                uint multiId = binaryReader.ReadUInt32();
                int componentCount = binaryReader.ReadInt32();

                if (multiId >= MaximumMultiIndex || componentCount <= 0)
                {
                    continue;
                }

                List<MultiComponentEntry> components = new(componentCount);

                for (int i = 0; i < componentCount; i++)
                {
                    if (memoryStream.Position + 14 > memoryStream.Length)
                    {
                        break;
                    }

                    ushort graphic = binaryReader.ReadUInt16();
                    ushort x = binaryReader.ReadUInt16();
                    ushort y = binaryReader.ReadUInt16();
                    ushort z = binaryReader.ReadUInt16();
                    ushort flags = binaryReader.ReadUInt16();
                    int clilocCount = binaryReader.ReadInt32();

                    if (clilocCount > 0)
                    {
                        long skip = clilocCount * 4L;

                        if (memoryStream.Position + skip > memoryStream.Length)
                        {
                            break;
                        }

                        memoryStream.Seek(skip, SeekOrigin.Current);
                    }

                    components.Add(new MultiComponentEntry
                    {
                        ItemId = graphic,
                        X = unchecked((short)x),
                        Y = unchecked((short)y),
                        Z = unchecked((short)z),
                        Flags = flags != 0 ? 0 : 1,
                        Unknown = 0,
                        Solver = i
                    });
                }

                if (components.Count > 0)
                {
                    loadedMultis[(int)multiId] = components;
                }
            }

            message = $"Loaded {loadedMultis.Count} UOP multis.";
            return true;
        }
        catch (Exception exception)
        {
            message = "Failed loading multicollection.uop: " + exception.Message;
            return false;
        }
    }

    public void ExportUox3(string fileName, int multiId, List<MultiComponentEntry> components)
    {
        using StreamWriter writer = new(
            new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None),
            System.Text.Encoding.GetEncoding(1252));

        writer.WriteLine("// UOX3 Multi Export");
        writer.WriteLine("// Multi ID: 0x" + multiId.ToString("X4") + " (" + multiId + ")");
        writer.WriteLine("// Components: " + components.Count);
        writer.WriteLine();

        for (int i = 0; i < components.Count; i++)
        {
            MultiComponentEntry part = components[i];

            writer.WriteLine("[HOUSE ITEM " + i + "]");
            writer.WriteLine("{");
            writer.WriteLine("ITEM=0x" + part.ItemId.ToString("X4"));
            writer.WriteLine("X=" + part.X);
            writer.WriteLine("Y=" + part.Y);
            writer.WriteLine("Z=" + part.Z);
            writer.WriteLine("}");
            writer.WriteLine();
        }
    }

    public void ExportText(string fileName, List<MultiComponentEntry> components)
    {
        using StreamWriter writer = new(
            new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None),
            System.Text.Encoding.GetEncoding(1252));

        foreach (MultiComponentEntry part in components)
        {
            writer.WriteLine(
                $"0x{part.ItemId:X4} {part.X} {part.Y} {part.Z} {part.Flags}");
        }
    }

    public void ExportCsv(string fileName, List<MultiComponentEntry> components)
    {
        using StreamWriter writer = new(
            new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None),
            System.Text.Encoding.GetEncoding(1252));

        writer.WriteLine("TileID,OffsetX,OffsetY,OffsetZ,Flag,Cliloc");

        foreach (MultiComponentEntry part in components)
        {
            writer.WriteLine(
                $"0x{part.ItemId:X4},{part.X},{part.Y},{part.Z},0x{part.Flags:X},");
        }
    }

    public bool ImportPartsFile(string fileName, out List<MultiComponentEntry> components, out string message)
    {
        components = new List<MultiComponentEntry>();

        if (!File.Exists(fileName))
        {
            message = "Import file was not found.";
            return false;
        }

        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension == ".csv")
        {
            return ImportCsv(fileName, components, out message);
        }

        if (extension == ".dfn")
        {
            return ImportUox3(fileName, components, out message);
        }

        return ImportText(fileName, components, out message);
    }

    private static bool ImportText(string fileName, List<MultiComponentEntry> components, out string message)
    {
        foreach (string rawLine in File.ReadLines(fileName))
        {
            string line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
            {
                continue;
            }

            string[] split = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (split.Length < 5)
            {
                continue;
            }

            components.Add(new MultiComponentEntry
            {
                ItemId = ParseUShort(split[0]),
                X = short.Parse(split[1]),
                Y = short.Parse(split[2]),
                Z = short.Parse(split[3]),
                Flags = ParseInt(split[4]),
                Unknown = 0
            });
        }

        message = "Imported TXT components: " + components.Count;
        return components.Count > 0;
    }

    private static bool ImportCsv(string fileName, List<MultiComponentEntry> components, out string message)
    {
        foreach (string rawLine in File.ReadLines(fileName))
        {
            string line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("TileID", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] split = line.Split(',');

            if (split.Length < 5)
            {
                continue;
            }

            components.Add(new MultiComponentEntry
            {
                ItemId = ParseUShort(split[0]),
                X = short.Parse(split[1]),
                Y = short.Parse(split[2]),
                Z = short.Parse(split[3]),
                Flags = ParseInt(split[4]),
                Unknown = 0
            });
        }

        message = "Imported CSV components: " + components.Count;
        return components.Count > 0;
    }

    private static bool ImportUox3(string fileName, List<MultiComponentEntry> components, out string message)
    {
        ushort item = 0;
        short x = 0;
        short y = 0;
        short z = 0;
        bool hasItem = false;

        foreach (string rawLine in File.ReadLines(fileName))
        {
            string line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
            {
                continue;
            }

            if (line.StartsWith("[HOUSE ITEM", StringComparison.OrdinalIgnoreCase))
            {
                if (hasItem)
                {
                    components.Add(new MultiComponentEntry
                    {
                        ItemId = item,
                        X = x,
                        Y = y,
                        Z = z,
                        Flags = 1,
                        Unknown = 0
                    });
                }

                item = 0;
                x = 0;
                y = 0;
                z = 0;
                hasItem = false;
                continue;
            }

            int equalsIndex = line.IndexOf('=');

            if (equalsIndex < 0)
            {
                continue;
            }

            string key = line[..equalsIndex].Trim();
            string value = line[(equalsIndex + 1)..].Trim();

            if (key.Equals("ITEM", StringComparison.OrdinalIgnoreCase))
            {
                item = ParseUShort(value);
                hasItem = true;
            }
            else if (key.Equals("X", StringComparison.OrdinalIgnoreCase))
            {
                x = short.Parse(value);
            }
            else if (key.Equals("Y", StringComparison.OrdinalIgnoreCase))
            {
                y = short.Parse(value);
            }
            else if (key.Equals("Z", StringComparison.OrdinalIgnoreCase))
            {
                z = short.Parse(value);
            }
        }

        if (hasItem)
        {
            components.Add(new MultiComponentEntry
            {
                ItemId = item,
                X = x,
                Y = y,
                Z = z,
                Flags = 1,
                Unknown = 0
            });
        }

        message = "Imported UOX3 components: " + components.Count;
        return components.Count > 0;
    }

    public void ReplaceMulti(int id, List<MultiComponentEntry> components)
    {
        loadedMultis[id] = components;
    }

    private static ushort ParseUShort(string value)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToUInt16(value[2..], 16);
        }

        return Convert.ToUInt16(value);
    }

    private static int ParseInt(string value)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(value[2..], 16);
        }

        return Convert.ToInt32(value);
    }

    public bool SaveMul(string folderPath, bool useUohsaFormat, out string message)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            message = "UO folder path is invalid.";
            return false;
        }

        string idxPath = Path.Combine(folderPath, "multi.idx");
        string mulPath = Path.Combine(folderPath, "multi.mul");

        try
        {
            string idxBackup = idxPath + ".bak";
            string mulBackup = mulPath + ".bak";

            if (File.Exists(idxPath) && !File.Exists(idxBackup))
            {
                File.Copy(idxPath, idxBackup, false);
            }

            if (File.Exists(mulPath) && !File.Exists(mulBackup))
            {
                File.Copy(mulPath, mulBackup, false);
            }

            using FileStream idxStream = new(idxPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using FileStream mulStream = new(mulPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using BinaryWriter idxWriter = new(idxStream);
            using BinaryWriter mulWriter = new(mulStream);

            for (int id = 0; id < MaximumMultiIndex; id++)
            {
                if (!loadedMultis.TryGetValue(id, out List<MultiComponentEntry>? components) ||
                    components == null ||
                    components.Count == 0)
                {
                    idxWriter.Write(-1);
                    idxWriter.Write(-1);
                    idxWriter.Write(-1);
                    continue;
                }

                List<MultiComponentEntry> cleanComponents = RebuildTilesForSave(components);

                idxWriter.Write((int)mulStream.Position);
                idxWriter.Write(cleanComponents.Count * (useUohsaFormat ? 16 : 12));
                idxWriter.Write(-1);

                foreach (MultiComponentEntry part in cleanComponents)
                {
                    mulWriter.Write(part.ItemId);
                    mulWriter.Write(part.X);
                    mulWriter.Write(part.Y);
                    mulWriter.Write(part.Z);
                    mulWriter.Write(part.Flags);

                    if (useUohsaFormat)
                    {
                        mulWriter.Write(part.Unknown);
                    }
                }
            }

            message = "Saved multi.idx and multi.mul.";
            return true;
        }
        catch (Exception exception)
        {
            message = "Failed saving multis: " + exception.Message;
            return false;
        }
    }

    private static List<MultiComponentEntry> RebuildTilesForSave(List<MultiComponentEntry> components)
    {
        List<MultiComponentEntry> newTiles = components
            .Select(CloneComponent)
            .ToList();

        if (newTiles.Count == 0)
        {
            return newTiles;
        }

        if (newTiles[0].X == 0 && newTiles[0].Y == 0 && newTiles[0].Z == 0)
        {
            if (newTiles[0].ItemId != 0x0001)
            {
                newTiles.RemoveAll(x => x.ItemId == 0x0001);
                return newTiles;
            }

            for (int i = 1; i < newTiles.Count; i++)
            {
                if (newTiles[i].X == 0 &&
                    newTiles[i].Y == 0 &&
                    newTiles[i].Z == 0 &&
                    newTiles[i].ItemId != 0x0001)
                {
                    MultiComponentEntry centerItem = newTiles[i];
                    newTiles.RemoveAt(i);
                    newTiles.RemoveAll(x => x.ItemId == 0x0001);
                    newTiles.Insert(0, centerItem);
                    return newTiles;
                }
            }

            for (int i = newTiles.Count - 1; i >= 1; i--)
            {
                if (newTiles[i].ItemId == 0x0001)
                {
                    newTiles.RemoveAt(i);
                }
            }

            return newTiles;
        }

        for (int i = 0; i < newTiles.Count; i++)
        {
            if (newTiles[i].X == 0 &&
                newTiles[i].Y == 0 &&
                newTiles[i].Z == 0 &&
                newTiles[i].ItemId != 0x0001)
            {
                MultiComponentEntry centerItem = newTiles[i];
                newTiles.RemoveAt(i);
                newTiles.RemoveAll(x => x.ItemId == 0x0001);
                newTiles.Insert(0, centerItem);
                return newTiles;
            }
        }

        newTiles.RemoveAll(x => x.ItemId == 0x0001);

        newTiles.Insert(0, new MultiComponentEntry
        {
            ItemId = 0x0001,
            X = 0,
            Y = 0,
            Z = 0,
            Flags = 0,
            Unknown = 0
        });

        return newTiles;
    }

    private static MultiComponentEntry CloneComponent(MultiComponentEntry source)
    {
        return new MultiComponentEntry
        {
            ItemId = source.ItemId,
            X = source.X,
            Y = source.Y,
            Z = source.Z,
            Flags = source.Flags,
            Unknown = source.Unknown,
            Solver = source.Solver
        };
    }
}
