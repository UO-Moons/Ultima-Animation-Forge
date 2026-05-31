using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class UopVdImportService
{
    private const int PaletteCapacity = 256;
    private const int PaletteBytes = 512;
    private const uint EndOfFrameMarker = 0x7FFF7FFF;

    public sealed class ImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    private sealed class DecodedUopFrame
    {
        public required WriteableBitmap Bitmap { get; init; }
        public required List<ushort> Palette { get; init; }
        public short CenterX { get; set; }
        public short CenterY { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
    }

    private sealed class UopFrameIndexEntry
    {
        public ushort Direction { get; set; }
        public ushort FrameNumber { get; set; }
        public short Left { get; set; }
        public short Top { get; set; }
        public short Right { get; set; }
        public short Bottom { get; set; }
        public uint FrameDataOffset { get; set; }
    }

    private sealed class FramePixels
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; }

        public FramePixels(int width, int height, byte[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }
    }

    public ImportResult ImportVdToUop(
        string uoFolderPath,
        string vdPath,
        int bodyId,
        string sourceFileName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uoFolderPath) || !Directory.Exists(uoFolderPath))
            {
                return Fail("UO folder path is invalid.");
            }

            if (string.IsNullOrWhiteSpace(vdPath) || !File.Exists(vdPath))
            {
                return Fail("VD file does not exist.");
            }

            string targetUopPath = Path.Combine(uoFolderPath, sourceFileName ?? string.Empty);
            if (!File.Exists(targetUopPath))
            {
                return Fail("Target UOP file was not found: " + sourceFileName);
            }

            short animType = ReadVdAnimType(vdPath);
            if (animType != 4)
            {
                return Fail("Only creature-style animType 4 VD files are currently supported for UOP import.");
            }

            Dictionary<int, Dictionary<int, List<DecodedUopFrame>>> vdData = ReadCreatureVdFile(vdPath);

            if (vdData.Count == 0)
            {
                return Fail("No animation data was found in the VD file.");
            }

            UopFileReader reader = new UopFileReader(targetUopPath);
            if (!reader.Load())
            {
                return Fail("Failed to load target UOP file.");
            }

            Dictionary<ulong, UopDataHeader> existingEntries = reader.GetAllEntries();
            List<UopFileData> outputEntries = new List<UopFileData>();

            HashSet<ulong> replacementHashes = new HashSet<ulong>();
            for (int action = 0; action < 32; action++)
            {
                replacementHashes.Add(UopFileReader.CreateHash($"build/animationlegacyframe/{bodyId:D6}/{action:D2}.bin"));
            }

            ulong actionZeroHybridHash = 0x0C000000000000UL | (ulong)(ushort)bodyId;
            replacementHashes.Add(actionZeroHybridHash);

            foreach (KeyValuePair<ulong, UopDataHeader> pair in existingEntries)
            {
                if (replacementHashes.Contains(pair.Key))
                {
                    continue;
                }

                byte[]? data = reader.ReadData(pair.Value);
                if (data == null || data.Length == 0)
                {
                    continue;
                }

                outputEntries.Add(new UopFileData
                {
                    Hash = pair.Key,
                    Data = data,
                    DecompressedSize = (uint)data.Length,
                    IsCompressed = true,
                    IsEmpty = false
                });
            }

            foreach (KeyValuePair<int, Dictionary<int, List<DecodedUopFrame>>> actionEntry in vdData)
            {
                int action = actionEntry.Key;
                if (action < 0 || action >= 32)
                {
                    continue;
                }

                byte[] amouBin = EncodeActionToAmouBin(bodyId, action, actionEntry.Value);
                if (amouBin.Length == 0)
                {
                    continue;
                }

                ulong hash = UopFileReader.CreateHash($"build/animationlegacyframe/{bodyId:D6}/{action:D2}.bin");

                outputEntries.Add(new UopFileData
                {
                    Hash = hash,
                    Data = amouBin,
                    DecompressedSize = (uint)amouBin.Length,
                    IsCompressed = true,
                    IsEmpty = false
                });

                if (action == 0)
                {
                    outputEntries.Add(new UopFileData
                    {
                        Hash = actionZeroHybridHash,
                        Data = amouBin,
                        DecompressedSize = (uint)amouBin.Length,
                        IsCompressed = true,
                        IsEmpty = false
                    });
                }
            }

            if (outputEntries.Count == 0)
            {
                return Fail("No output entries were generated.");
            }

            string backupPath = targetUopPath + ".bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(targetUopPath, backupPath, false);
            }

            UopFileWriter.WriteUopFile(targetUopPath, outputEntries.OrderBy(x => x.Hash), 1000);

            return new ImportResult
            {
                Success = true,
                Message = "Imported VD into " + Path.GetFileName(targetUopPath) + " for body " + bodyId + "."
            };
        }
        catch (Exception exception)
        {
            return Fail("VD -> UOP import failed: " + exception.Message);
        }
    }

    private short ReadVdAnimType(string vdPath)
    {
        using FileStream stream = File.OpenRead(vdPath);
        using BinaryReader reader = new BinaryReader(stream);

        _ = reader.ReadInt16();
        return reader.ReadInt16();
    }

    private Dictionary<int, Dictionary<int, List<DecodedUopFrame>>> ReadCreatureVdFile(string vdPath)
    {
        Dictionary<int, Dictionary<int, List<DecodedUopFrame>>> result = new Dictionary<int, Dictionary<int, List<DecodedUopFrame>>>();

        using FileStream stream = File.OpenRead(vdPath);
        using BinaryReader reader = new BinaryReader(stream);

        short version = reader.ReadInt16();
        short animType = reader.ReadInt16();

        _ = version;
        _ = animType;

        (int lookup, int length, int extra)[,] indexTable = new (int, int, int)[32, 5];

        for (int action = 0; action < 32; action++)
        {
            for (int direction = 0; direction < 5; direction++)
            {
                indexTable[action, direction] = (
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32());
            }
        }

        for (int action = 0; action < 32; action++)
        {
            Dictionary<int, List<DecodedUopFrame>> directions = new Dictionary<int, List<DecodedUopFrame>>();

            for (int direction = 0; direction < 5; direction++)
            {
                (int lookup, int length, int extra) entry = indexTable[action, direction];

                if (entry.lookup <= 0)
                {
                    continue;
                }

                stream.Seek(entry.lookup, SeekOrigin.Begin);
                long endOffset = entry.length > 0 ? entry.lookup + entry.length : stream.Length;
                bool hasSharedPalette = entry.extra == 1;

                List<DecodedUopFrame> frames = ReadVdFramesForDirection(reader, endOffset, hasSharedPalette);
                if (frames.Count > 0)
                {
                    directions[direction] = frames;
                }
            }

            if (directions.Count > 0)
            {
                result[action] = directions;
            }
        }

        return result;
    }

    private List<DecodedUopFrame> ReadVdFramesForDirection(BinaryReader reader, long endOffset, bool hasSharedPalette)
    {
        List<DecodedUopFrame> result = new List<DecodedUopFrame>();

        long blockStart = reader.BaseStream.Position;
        List<ushort>? sharedPalette = null;

        if (hasSharedPalette)
        {
            sharedPalette = new List<ushort>(PaletteCapacity);

            for (int i = 0; i < PaletteCapacity; i++)
            {
                ushort value = (ushort)(reader.ReadUInt16() ^ 0x8000);
                sharedPalette.Add(value);
            }
        }

        if (reader.BaseStream.Position + 4 > endOffset)
        {
            return result;
        }

        int frameCount = reader.ReadInt32();
        if (frameCount <= 0)
        {
            return result;
        }

        if (reader.BaseStream.Position + (frameCount * 4L) > endOffset)
        {
            return result;
        }

        int[] offsets = new int[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            offsets[i] = reader.ReadInt32();
        }

        long frameDataBase = hasSharedPalette ? blockStart + PaletteBytes : blockStart;

        for (int i = 0; i < frameCount; i++)
        {
            if (offsets[i] == 0)
            {
                continue;
            }

            reader.BaseStream.Seek(frameDataBase + offsets[i], SeekOrigin.Begin);

            List<ushort> palette;

            if (hasSharedPalette)
            {
                palette = new List<ushort>(sharedPalette!);
            }
            else
            {
                if (reader.BaseStream.Position + PaletteBytes > endOffset)
                {
                    break;
                }

                palette = new List<ushort>(PaletteCapacity);

                for (int p = 0; p < PaletteCapacity; p++)
                {
                    ushort value = (ushort)(reader.ReadUInt16() ^ 0x8000);
                    palette.Add(value);
                }
            }

            short centerX = reader.ReadInt16();
            short centerY = reader.ReadInt16();
            ushort width = reader.ReadUInt16();
            ushort height = reader.ReadUInt16();

            if (width == 0 || height == 0 || width > 4096 || height > 4096)
            {
                continue;
            }

            WriteableBitmap bitmap = new WriteableBitmap(
                new Avalonia.PixelSize(width, height),
                new Avalonia.Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            byte[] pixels = new byte[width * height * 4];

            while (reader.BaseStream.Position < endOffset && reader.BaseStream.Position + 4 <= endOffset)
            {
                uint header = reader.ReadUInt32();

                if (header == EndOfFrameMarker)
                {
                    break;
                }

                int runLength = (int)(header & 0x0FFF);
                int y = (int)((header >> 12) & 0x03FF);
                int x = (int)((header >> 22) & 0x03FF);

                if ((x & 0x0200) != 0)
                {
                    x = -(1024 - x);
                }

                if ((y & 0x0200) != 0)
                {
                    y = -(1024 - y);
                }

                if (runLength == 0)
                {
                    continue;
                }

                if (reader.BaseStream.Position + runLength > endOffset)
                {
                    reader.BaseStream.Seek(endOffset, SeekOrigin.Begin);
                    break;
                }

                byte[] run = reader.ReadBytes(runLength);

                for (int r = 0; r < runLength; r++)
                {
                    int pixelX = centerX + x + r;
                    int pixelY = height - 1 - (-y - centerY);

                    if (pixelX < 0 || pixelX >= width || pixelY < 0 || pixelY >= height)
                    {
                        continue;
                    }

                    ushort color1555 = palette[run[r]];
                    WriteColor1555(pixels, width, pixelX, pixelY, color1555);
                }
            }

            using (ILockedFramebuffer framebuffer = bitmap.Lock())
            {
                Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
            }

            result.Add(new DecodedUopFrame
            {
                Bitmap = bitmap,
                Palette = palette,
                CenterX = centerX,
                CenterY = centerY,
                Width = width,
                Height = height
            });
        }

        return result;
    }

    private byte[] EncodeActionToAmouBin(int animId, int action, Dictionary<int, List<DecodedUopFrame>> allDirectionsFrames)
    {
        if (allDirectionsFrames == null || allDirectionsFrames.Count == 0)
        {
            return [];
        }

        const int directionCount = 5;
        const int paletteCount = 256;
        const uint magic = 1431260481U;
        const uint version = 1U;
        const uint unknown1 = 256U;
        const uint unknown2 = 40U;
        const uint endOfFrameMarker = 0x7FFF7FFF;
        const int headerSize = 40;
        const int frameIndexEntrySize = 16;

        int maxFramesPerDirection = 0;

        foreach (List<DecodedUopFrame> frames in allDirectionsFrames.Values)
        {
            if (frames != null && frames.Count > maxFramesPerDirection)
            {
                maxFramesPerDirection = frames.Count;
            }
        }

        if (maxFramesPerDirection <= 0)
        {
            return [];
        }

        int totalFrameEntries = maxFramesPerDirection * directionCount;
        uint frameIndexOffset = headerSize;

        List<UopFrameIndexEntry> indexEntries = new List<UopFrameIndexEntry>(totalFrameEntries);

        using MemoryStream output = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(output);

        // Header placeholder
        writer.Write(magic);
        writer.Write(version);
        writer.Write(0U); // total size placeholder
        writer.Write((uint)animId);

        // Bounds placeholders
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write((short)0);

        writer.Write(unknown1);
        writer.Write(unknown2);
        writer.Write((uint)totalFrameEntries);
        writer.Write(frameIndexOffset);

        long frameIndexStart = output.Position;

        // Reserve frame index table
        writer.Write(new byte[totalFrameEntries * frameIndexEntrySize]);

        int globalMinLeft = int.MaxValue;
        int globalMinTop = int.MaxValue;
        int globalMaxRight = int.MinValue;
        int globalMaxBottom = int.MinValue;

        for (int direction = 0; direction < directionCount; direction++)
        {
            List<DecodedUopFrame> frames = allDirectionsFrames.TryGetValue(direction, out List<DecodedUopFrame>? foundFrames) && foundFrames != null
                ? foundFrames
                : new List<DecodedUopFrame>();

            for (int frameNumber = 0; frameNumber < maxFramesPerDirection; frameNumber++)
            {
                long indexEntryPosition = frameIndexStart + ((direction * maxFramesPerDirection + frameNumber) * frameIndexEntrySize);

                if (frameNumber >= frames.Count)
                {
                    indexEntries.Add(new UopFrameIndexEntry
                    {
                        Direction = (ushort)direction,
                        FrameNumber = (ushort)frameNumber,
                        Left = 0,
                        Top = 0,
                        Right = 0,
                        Bottom = 0,
                        FrameDataOffset = 0
                    });

                    continue;
                }

                DecodedUopFrame frame = frames[frameNumber];
                if (frame.Bitmap == null)
                {
                    indexEntries.Add(new UopFrameIndexEntry
                    {
                        Direction = (ushort)direction,
                        FrameNumber = (ushort)frameNumber,
                        Left = 0,
                        Top = 0,
                        Right = 0,
                        Bottom = 0,
                        FrameDataOffset = 0
                    });

                    continue;
                }

                (short left, short top, short right, short bottom) = GetVisibleBounds(frame);

                bool hasVisiblePixels = HasVisiblePixels(frame.Bitmap);
                if (!hasVisiblePixels)
                {
                    left = 0;
                    top = 0;
                    right = 0;
                    bottom = 0;
                }

                long frameBlockStart = output.Position;
                uint relativeOffset = frameBlockStart <= indexEntryPosition
                    ? 0U
                    : (uint)(frameBlockStart - indexEntryPosition);

                // Per-frame palette, exactly what UOP decode path expects.
                WriteFramePalette(writer, frame.Palette, paletteCount);

                // Frame header
                writer.Write(frame.CenterX);
                writer.Write(frame.CenterY);
                writer.Write(frame.Width);
                writer.Write(frame.Height);

                // RLE body
                if (hasVisiblePixels)
                {
                    EncodeRlePixels(writer, frame, endOfFrameMarker);
                }
                else
                {
                    writer.Write(endOfFrameMarker);
                }

                indexEntries.Add(new UopFrameIndexEntry
                {
                    Direction = (ushort)direction,
                    FrameNumber = (ushort)frameNumber,
                    Left = left,
                    Top = top,
                    Right = right,
                    Bottom = bottom,
                    FrameDataOffset = relativeOffset
                });

                if (hasVisiblePixels)
                {
                    if (left < globalMinLeft) globalMinLeft = left;
                    if (top < globalMinTop) globalMinTop = top;
                    if (right > globalMaxRight) globalMaxRight = right;
                    if (bottom > globalMaxBottom) globalMaxBottom = bottom;
                }
            }
        }

        if (globalMinLeft == int.MaxValue) globalMinLeft = 0;
        if (globalMinTop == int.MaxValue) globalMinTop = 0;
        if (globalMaxRight == int.MinValue) globalMaxRight = 0;
        if (globalMaxBottom == int.MinValue) globalMaxBottom = 0;

        // Backfill index table
        output.Seek(frameIndexStart, SeekOrigin.Begin);

        foreach (UopFrameIndexEntry entry in indexEntries)
        {
            writer.Write(entry.Direction);
            writer.Write(entry.FrameNumber);
            writer.Write(entry.Left);
            writer.Write(entry.Top);
            writer.Write(entry.Right);
            writer.Write(entry.Bottom);
            writer.Write(entry.FrameDataOffset);
        }

        // Backfill header total size + bounds
        output.Seek(8, SeekOrigin.Begin);
        writer.Write((uint)output.Length);

        output.Seek(16, SeekOrigin.Begin);
        writer.Write((short)globalMinLeft);
        writer.Write((short)globalMinTop);
        writer.Write((short)globalMaxRight);
        writer.Write((short)globalMaxBottom);

        return output.ToArray();
    }

    private void WriteFramePalette(BinaryWriter writer, List<ushort> palette, int paletteCount)
    {
        for (int i = 0; i < paletteCount; i++)
        {
            ushort color1555 = 0x8000;

            if (palette != null && i < palette.Count)
            {
                color1555 = palette[i];

                if (i == 0)
                {
                    color1555 = 0x8000;
                }
                else if ((color1555 & 0x8000) == 0)
                {
                    color1555 = (ushort)(color1555 | 0x8000);
                }
            }

            ushort stored = (ushort)(color1555 ^ 0x8000);
            writer.Write(stored);
        }
    }

    private void EncodeRlePixels(BinaryWriter writer, DecodedUopFrame frame, uint endOfFrameMarker)
    {
        FramePixels pixels = ReadPixels(frame.Bitmap);
        Dictionary<uint, byte> paletteMap = BuildPaletteLookup(frame.Palette);

        for (int y = 0; y < pixels.Height; y++)
        {
            int x = 0;

            while (x < pixels.Width)
            {
                while (x < pixels.Width && IsTransparent(pixels, x, y))
                {
                    x++;
                }

                if (x >= pixels.Width)
                {
                    break;
                }

                int runStart = x;
                List<byte> runBytes = new List<byte>();

                while (x < pixels.Width && !IsTransparent(pixels, x, y))
                {
                    uint argb = GetColor(pixels, x, y);
                    ushort color1555 = ConvertArgbTo1555(argb);

                    if (!paletteMap.TryGetValue(color1555, out byte paletteIndex))
                    {
                        paletteIndex = FindClosestPaletteIndex(color1555, frame.Palette);
                        paletteMap[color1555] = paletteIndex;
                    }

                    runBytes.Add(paletteIndex);
                    x++;
                }

                int consumed = 0;
                while (consumed < runBytes.Count)
                {
                    int currentX = runStart + consumed;
                    int remaining = runBytes.Count - consumed;
                    int length = Math.Min(remaining, 0x0FFF);

                    int offsetX = currentX - frame.CenterX;
                    int offsetY = y - pixels.Height + 1 - frame.CenterY;

                    if (offsetX < -512 || offsetX > 511 || offsetY < -512 || offsetY > 511)
                    {
                        consumed++;
                        continue;
                    }

                    int maxLengthForHeaderX = 512 - offsetX;
                    if (offsetX >= 0)
                    {
                        maxLengthForHeaderX = 1024 - offsetX;
                    }

                    if (maxLengthForHeaderX <= 0)
                    {
                        consumed++;
                        continue;
                    }

                    if (length > maxLengthForHeaderX)
                    {
                        length = maxLengthForHeaderX;
                    }

                    if (length <= 0)
                    {
                        consumed++;
                        continue;
                    }

                    uint header =
                        (uint)(length & 0x0FFF) |
                        (uint)((offsetY & 0x03FF) << 12) |
                        (uint)((offsetX & 0x03FF) << 22);

                    writer.Write(header);

                    for (int i = 0; i < length; i++)
                    {
                        writer.Write(runBytes[consumed + i]);
                    }

                    consumed += length;
                }
            }
        }

        writer.Write(endOfFrameMarker);
    }

    private (short left, short top, short right, short bottom) GetVisibleBounds(DecodedUopFrame frame)
    {
        FramePixels pixels = ReadPixels(frame.Bitmap);

        int minX = pixels.Width;
        int minY = pixels.Height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < pixels.Height; y++)
        {
            for (int x = 0; x < pixels.Width; x++)
            {
                if (IsTransparent(pixels, x, y))
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < 0 || maxY < 0)
        {
            return (0, 0, 0, 0);
        }

        int left = minX - frame.CenterX;
        int top = minY - frame.CenterY - frame.Height + 1;
        int right = maxX - frame.CenterX;
        int bottom = maxY - frame.CenterY - frame.Height + 1;

        return ((short)left, (short)top, (short)right, (short)bottom);
    }

    private bool HasVisiblePixels(WriteableBitmap bitmap)
    {
        FramePixels pixels = ReadPixels(bitmap);

        for (int y = 0; y < pixels.Height; y++)
        {
            for (int x = 0; x < pixels.Width; x++)
            {
                if (!IsTransparent(pixels, x, y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private FramePixels ReadPixels(WriteableBitmap bitmap)
    {
        using ILockedFramebuffer framebuffer = bitmap.Lock();

        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int srcRowBytes = framebuffer.RowBytes;
        int dstRowBytes = width * 4;

        byte[] src = new byte[srcRowBytes * height];
        Marshal.Copy(framebuffer.Address, src, 0, src.Length);

        byte[] packed = new byte[dstRowBytes * height];

        for (int y = 0; y < height; y++)
        {
            Buffer.BlockCopy(src, y * srcRowBytes, packed, y * dstRowBytes, dstRowBytes);
        }

        return new FramePixels(width, height, packed);
    }

    private void WriteColor1555(byte[] pixels, int width, int x, int y, ushort color1555)
    {
        int offset = ((y * width) + x) * 4;

        if ((color1555 & 0x8000) == 0 || color1555 == 0x8000)
        {
            pixels[offset + 0] = 0;
            pixels[offset + 1] = 0;
            pixels[offset + 2] = 0;
            pixels[offset + 3] = 0;
            return;
        }

        byte r = (byte)(((color1555 >> 10) & 0x1F) << 3);
        byte g = (byte)(((color1555 >> 5) & 0x1F) << 3);
        byte b = (byte)((color1555 & 0x1F) << 3);

        pixels[offset + 0] = b;
        pixels[offset + 1] = g;
        pixels[offset + 2] = r;
        pixels[offset + 3] = 255;
    }

    private Dictionary<uint, byte> BuildPaletteLookup(List<ushort> palette)
    {
        Dictionary<uint, byte> lookup = new Dictionary<uint, byte>();

        for (int i = 0; i < palette.Count && i < 256; i++)
        {
            ushort color = palette[i];
            uint key = color;
            if (!lookup.ContainsKey(key))
            {
                lookup[key] = (byte)i;
            }
        }

        return lookup;
    }

    private byte FindClosestPaletteIndex(ushort target1555, List<ushort> palette)
    {
        int targetR = (target1555 >> 10) & 0x1F;
        int targetG = (target1555 >> 5) & 0x1F;
        int targetB = target1555 & 0x1F;

        int bestDistance = int.MaxValue;
        byte bestIndex = 0;

        for (int i = 0; i < palette.Count && i < 256; i++)
        {
            ushort color = palette[i];

            int r = (color >> 10) & 0x1F;
            int g = (color >> 5) & 0x1F;
            int b = color & 0x1F;

            int distance = Math.Abs(targetR - r) + Math.Abs(targetG - g) + Math.Abs(targetB - b);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = (byte)i;
            }
        }

        return bestIndex;
    }

    private ushort ConvertArgbTo1555(uint argb)
    {
        byte alpha = (byte)((argb >> 24) & 0xFF);
        if (alpha == 0)
        {
            return 0x8000;
        }

        byte r = (byte)((argb >> 16) & 0xFF);
        byte g = (byte)((argb >> 8) & 0xFF);
        byte b = (byte)(argb & 0xFF);

        return (ushort)(((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3));
    }

    private bool IsTransparent(FramePixels pixels, int x, int y)
    {
        int offset = ((y * pixels.Width) + x) * 4;
        return pixels.Pixels[offset + 3] == 0;
    }

    private uint GetColor(FramePixels pixels, int x, int y)
    {
        int offset = ((y * pixels.Width) + x) * 4;

        byte b = pixels.Pixels[offset + 0];
        byte g = pixels.Pixels[offset + 1];
        byte r = pixels.Pixels[offset + 2];
        byte a = pixels.Pixels[offset + 3];

        return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private ImportResult Fail(string message)
    {
        return new ImportResult
        {
            Success = false,
            Message = message
        };
    }
}