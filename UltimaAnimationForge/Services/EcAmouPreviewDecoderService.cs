using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class EcAmouPreviewDecoderService
{
    private const uint AmouMagic = 1431260481U; // AMOU
    private const int HeaderSize = 40;
    private const int FrameIndexEntrySize = 16;

    public sealed class AmouMetadata
    {
        public int AnimId { get; set; }

        public int ActionId { get; set; }   // <-- ADD THIS

        public int FrameCount { get; set; }

        public int ColorCount { get; set; }
    }

    private sealed class AmouHeader
    {
        public uint Magic { get; set; }
        public uint Version { get; set; }
        public uint TotalSize { get; set; }
        public uint AnimId { get; set; }
        public short Left { get; set; }
        public short Top { get; set; }
        public short Right { get; set; }
        public short Bottom { get; set; }
        public uint ColorCount { get; set; }
        public uint Unknown2 { get; set; }
        public uint FrameCount { get; set; }
        public uint FrameIndexOffset { get; set; }
    }

    private sealed class FrameIndex
    {
        public ushort Id { get; set; }
        public ushort FrameNumber { get; set; }

        public short InitX { get; set; }
        public short InitY { get; set; }
        public short EndX { get; set; }
        public short EndY { get; set; }

        public uint RelativePixelOffset { get; set; }
        public long EntryOffset { get; set; }
        public long AbsolutePixelOffset { get; set; }

        public int Width => Math.Abs(EndX - InitX);
        public int Height => Math.Abs(EndY - InitY);
    }

    public List<WriteableBitmap> DecodePreviewFrames(byte[] data, int preferredDirection = 0, int maxFrames = 64)
    {
        if (data == null || data.Length < HeaderSize)
        {
            return new List<WriteableBitmap>();
        }

        AmouHeader header = ReadHeader(data);
        if (header.Magic != AmouMagic || header.FrameCount == 0)
        {
            return new List<WriteableBitmap>();
        }

        List<ColorEntry> palette = ReadPalette(data, HeaderSize, header.ColorCount);
        if (palette.Count == 0)
        {
            return new List<WriteableBitmap>();
        }

        List<FrameIndex> frames = ReadFrameIndexes(data, header);
        if (frames.Count == 0)
        {
            return new List<WriteableBitmap>();
        }

        List<WriteableBitmap> bestFrames = new();
        int bestVisiblePixels = 0;

        foreach (IGrouping<ushort, FrameIndex> group in frames.GroupBy(x => x.Id).OrderBy(x => x.Key))
        {
            List<WriteableBitmap> groupFrames = new();
            int groupVisiblePixels = 0;

            foreach (FrameIndex frame in group.OrderBy(x => x.FrameNumber))
            {
                if (groupFrames.Count >= maxFrames)
                {
                    break;
                }

                if (frame.Width <= 0 || frame.Height <= 0 || frame.Width > 1024 || frame.Height > 1024)
                {
                    continue;
                }

                if (frame.AbsolutePixelOffset < 0 || frame.AbsolutePixelOffset >= data.Length)
                {
                    continue;
                }

                WriteableBitmap? rawBitmap = DecodeEcFrame(data, frame, palette);
                if (rawBitmap == null)
                {
                    continue;
                }

                WriteableBitmap bitmap = ComposeFrameCell(rawBitmap, header, frame);
                int visiblePixels = CountVisiblePixels(bitmap);

                if (visiblePixels <= 0)
                {
                    continue;
                }

                groupVisiblePixels += visiblePixels;
                groupFrames.Add(bitmap);
            }

            if (groupVisiblePixels > bestVisiblePixels)
            {
                bestVisiblePixels = groupVisiblePixels;
                bestFrames = groupFrames;
            }
        }

        return bestFrames;
    }

    private static int CountVisiblePixels(WriteableBitmap bitmap)
    {
        byte[] pixels = ReadBitmapPixels(bitmap);
        int count = 0;

        for (int i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0)
            {
                count++;
            }
        }

        return count;
    }

    public string BuildInfoText(byte[] data)
    {
        if (data == null || data.Length < HeaderSize)
        {
            return "AMOU header too small.";
        }

        try
        {
            AmouHeader header = ReadHeader(data);
            List<FrameIndex> frames = ReadFrameIndexes(data, header);

            string firstFrameText = "First Frame: none";
            if (frames.Count > 0)
            {
                FrameIndex first = frames[0];
                firstFrameText =
                    "First Frame:" + Environment.NewLine +
                    "  ID: " + first.Id + Environment.NewLine +
                    "  Frame: " + first.FrameNumber + Environment.NewLine +
                    "  Init: " + first.InitX + "," + first.InitY + Environment.NewLine +
                    "  End: " + first.EndX + "," + first.EndY + Environment.NewLine +
                    "  Size: " + first.Width + " x " + first.Height + Environment.NewLine +
                    "  Pixel Offset: " + first.AbsolutePixelOffset;
            }

            return
                "AMOU Parsed:" + Environment.NewLine +
                "Magic: AMOU" + Environment.NewLine +
                "Version: " + header.Version + Environment.NewLine +
                "Total Size: " + header.TotalSize + Environment.NewLine +
                "Anim ID: " + header.AnimId + Environment.NewLine +
                "Bounds: " + header.Left + "," + header.Top + " to " + header.Right + "," + header.Bottom + Environment.NewLine +
                "Color Count: " + header.ColorCount + Environment.NewLine +
                "Unknown2: " + header.Unknown2 + Environment.NewLine +
                "Frame Entries: " + header.FrameCount + Environment.NewLine +
                "Frame Index Offset: " + header.FrameIndexOffset + Environment.NewLine +
                firstFrameText;
        }
        catch
        {
            return "Failed to parse AMOU header.";
        }
    }

    private static AmouHeader ReadHeader(byte[] data)
    {
        using MemoryStream stream = new(data, false);
        using BinaryReader reader = new(stream);

        return new AmouHeader
        {
            Magic = reader.ReadUInt32(),
            Version = reader.ReadUInt32(),
            TotalSize = reader.ReadUInt32(),
            AnimId = reader.ReadUInt32(),
            Left = reader.ReadInt16(),
            Top = reader.ReadInt16(),
            Right = reader.ReadInt16(),
            Bottom = reader.ReadInt16(),
            ColorCount = reader.ReadUInt32(),
            Unknown2 = reader.ReadUInt32(),
            FrameCount = reader.ReadUInt32(),
            FrameIndexOffset = reader.ReadUInt32()
        };
    }

    private static List<FrameIndex> ReadFrameIndexes(byte[] data, AmouHeader header)
    {
        List<FrameIndex> result = new();

        long start = header.FrameIndexOffset;
        if (start < 0 || start >= data.Length)
        {
            return result;
        }

        long maxCountByLength = (data.Length - start) / FrameIndexEntrySize;
        uint count = Math.Min(header.FrameCount, (uint)Math.Min(maxCountByLength, 100000));

        using MemoryStream stream = new(data, false);
        using BinaryReader reader = new(stream);

        stream.Seek(start, SeekOrigin.Begin);

        for (uint i = 0; i < count; i++)
        {
            long entryStart = stream.Position;

            FrameIndex frame = new()
            {
                EntryOffset = entryStart,
                Id = reader.ReadUInt16(),
                FrameNumber = reader.ReadUInt16(),
                InitX = reader.ReadInt16(),
                InitY = reader.ReadInt16(),
                EndX = reader.ReadInt16(),
                EndY = reader.ReadInt16(),
                RelativePixelOffset = reader.ReadUInt32()
            };

            frame.AbsolutePixelOffset = frame.EntryOffset + frame.RelativePixelOffset;

            if (frame.AbsolutePixelOffset >= 0 && frame.AbsolutePixelOffset < data.Length)
            {
                result.Add(frame);
            }
        }

        return result;
    }

    private static WriteableBitmap? DecodeEcFrame(
        byte[] data,
        FrameIndex frame,
        List<ColorEntry> palette)
    {
        int width = frame.Width;
        int height = frame.Height;

        if (width <= 0 || height <= 0 || width > 1024 || height > 1024)
        {
            return null;
        }

        byte[] pixels = new byte[width * height * 4];

        int curX = 0;
        int curY = 0;
        int currByte = (int)frame.AbsolutePixelOffset;

        while (curY < height && currByte < data.Length)
        {
            byte current = data[currByte++];

            if (current < 128)
            {
                for (int i = 0; i < current && curY < height; i++)
                {
                    MoveNextPixel(ref curX, ref curY, width);
                }

                continue;
            }

            if (currByte >= data.Length)
            {
                break;
            }

            byte blendByte = data[currByte++];

            int factor1 = blendByte / 16;
            int factor2 = blendByte % 16;

            if (factor1 > 0 && curY < height)
            {
                if (currByte >= data.Length)
                {
                    break;
                }

                byte paletteIndex = data[currByte++];
                BlendSetPixel(pixels, width, curX, curY, palette, paletteIndex, factor1);
                MoveNextPixel(ref curX, ref curY, width);
            }

            int runCount = current - 128;

            for (int i = 0; i < runCount && curY < height; i++)
            {
                if (currByte >= data.Length)
                {
                    break;
                }

                byte paletteIndex = data[currByte++];
                SetPixel(pixels, width, curX, curY, palette, paletteIndex);
                MoveNextPixel(ref curX, ref curY, width);
            }

            if (factor2 > 0 && curY < height)
            {
                if (currByte >= data.Length)
                {
                    break;
                }

                byte paletteIndex = data[currByte++];
                BlendSetPixel(pixels, width, curX, curY, palette, paletteIndex, factor2);
                MoveNextPixel(ref curX, ref curY, width);
            }
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

    private static void MoveNextPixel(ref int x, ref int y, int width)
    {
        x++;

        if (x >= width)
        {
            x = 0;
            y++;
        }
    }

    private readonly record struct ColorEntry(byte R, byte G, byte B);

    private static List<ColorEntry> ReadPalette(byte[] data, int colorOffset, uint colorCount)
    {
        List<ColorEntry> palette = new();

        int offset = colorOffset;
        int safeColorCount = (int)Math.Min(colorCount, 4096);

        for (int i = 0; i < safeColorCount; i++)
        {
            if (offset + 4 > data.Length)
            {
                break;
            }

            byte r = data[offset++];
            byte g = data[offset++];
            byte b = data[offset++];
            offset++; // unused alpha / flags byte

            palette.Add(new ColorEntry(r, g, b));
        }

        return palette;
    }

    private static void SetPixel(
        byte[] pixels,
        int width,
        int x,
        int y,
        List<ColorEntry> palette,
        byte paletteIndex)
    {
        if (x < 0 || y < 0 || paletteIndex >= palette.Count)
        {
            return;
        }

        // Super Viewer style: palette index 0 is transparent.
        // Do NOT use the palette alpha byte.
        if (paletteIndex == 0)
        {
            return;
        }

        ColorEntry color = palette[paletteIndex];

        int offset = ((y * width) + x) * 4;

        pixels[offset + 0] = color.B;
        pixels[offset + 1] = color.G;
        pixels[offset + 2] = color.R;
        pixels[offset + 3] = 255;
    }

    private static void BlendSetPixel(
        byte[] pixels,
        int width,
        int x,
        int y,
        List<ColorEntry> palette,
        byte paletteIndex,
        int factor)
    {
        if (x < 0 || y < 0 || paletteIndex >= palette.Count)
        {
            return;
        }

        if (paletteIndex == 0)
        {
            return;
        }

        ColorEntry source = palette[paletteIndex];

        int offset = ((y * width) + x) * 4;

        byte destB = pixels[offset + 0];
        byte destG = pixels[offset + 1];
        byte destR = pixels[offset + 2];

        pixels[offset + 0] = (byte)(((source.B * factor) + (destB * (16 - factor))) >> 4);
        pixels[offset + 1] = (byte)(((source.G * factor) + (destG * (16 - factor))) >> 4);
        pixels[offset + 2] = (byte)(((source.R * factor) + (destR * (16 - factor))) >> 4);
        pixels[offset + 3] = 255;
    }

    private static WriteableBitmap ComposeFrameCell(
    WriteableBitmap rawFrame,
    AmouHeader header,
    FrameIndex frame)
    {
        int cellWidth = Math.Abs(header.Right - header.Left);
        int cellHeight = Math.Abs(header.Bottom - header.Top);

        cellWidth = Math.Max(cellWidth, 160);
        cellHeight = Math.Max(cellHeight, 160);

        if (cellWidth <= 0)
        {
            cellWidth = frame.Width;
        }

        if (cellHeight <= 0)
        {
            cellHeight = frame.Height;
        }

        cellWidth = Math.Max(cellWidth, frame.Width);
        cellHeight = Math.Max(cellHeight, frame.Height);

        byte[] cellPixels = new byte[cellWidth * cellHeight * 4];

        byte[] rawPixels = ReadBitmapPixels(rawFrame);

        int rawWidth = rawFrame.PixelSize.Width;
        int rawHeight = rawFrame.PixelSize.Height;

        int drawX = (cellWidth / 2) + frame.InitX;
        int drawY = (cellHeight / 2) + frame.InitY;

        for (int y = 0; y < rawHeight; y++)
        {
            int targetY = drawY + y;
            if (targetY < 0 || targetY >= cellHeight)
            {
                continue;
            }

            for (int x = 0; x < rawWidth; x++)
            {
                int targetX = drawX + x;
                if (targetX < 0 || targetX >= cellWidth)
                {
                    continue;
                }

                int src = ((y * rawWidth) + x) * 4;
                int dst = ((targetY * cellWidth) + targetX) * 4;

                byte alpha = rawPixels[src + 3];
                if (alpha == 0)
                {
                    continue;
                }

                cellPixels[dst + 0] = rawPixels[src + 0];
                cellPixels[dst + 1] = rawPixels[src + 1];
                cellPixels[dst + 2] = rawPixels[src + 2];
                cellPixels[dst + 3] = alpha;
            }
        }

        WriteableBitmap bitmap = new(
            new PixelSize(cellWidth, cellHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        Marshal.Copy(cellPixels, 0, framebuffer.Address, cellPixels.Length);

        return bitmap;
    }

    private static byte[] ReadBitmapPixels(WriteableBitmap bitmap)
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

        return packed;
    }

    public Dictionary<int, List<VdFrameData>> DecodeVdFramesByDirection(byte[] data)
    {
        Dictionary<int, List<VdFrameData>> result = new();

        if (data == null || data.Length < HeaderSize)
        {
            return result;
        }

        AmouHeader header = ReadHeader(data);
        if (header.Magic != AmouMagic || header.FrameCount == 0)
        {
            return result;
        }

        List<ColorEntry> palette = ReadPalette(data, HeaderSize, header.ColorCount);
        List<FrameIndex> frames = ReadFrameIndexes(data, header);

        if (palette.Count == 0 || frames.Count == 0)
        {
            return result;
        }

        int framesPerDirection = frames.Count / 5;
        if (framesPerDirection <= 0)
        {
            return result;
        }

        List<ushort> palette565 = BuildPalette565(palette);

        short globalInitX = header.Left;
        short globalInitY = header.Top;

        int cellWidth = Math.Abs(header.Right - header.Left);
        int cellHeight = Math.Abs(header.Bottom - header.Top);

        if (cellWidth <= 0)
        {
            cellWidth = frames.Max(x => x.Width);
        }

        if (cellHeight <= 0)
        {
            cellHeight = frames.Max(x => x.Height);
        }

        cellWidth = Math.Max(cellWidth, 1);
        cellHeight = Math.Max(cellHeight, 1);

        short centerX = (short)(cellWidth - header.Right);
        short centerY = (short)(-header.Bottom);

        for (int direction = 0; direction < 5; direction++)
        {
            List<VdFrameData> directionFrames = new();

            int startIndex = direction * framesPerDirection;
            int endIndex = Math.Min(startIndex + framesPerDirection, frames.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                FrameIndex frame = frames[i];

                if (frame.Width <= 0 || frame.Height <= 0)
                {
                    continue;
                }

                WriteableBitmap? rawBitmap = DecodeEcFrame(data, frame, palette);
                if (rawBitmap == null)
                {
                    continue;
                }

                WriteableBitmap cellBitmap = ComposeVdCell(rawBitmap, cellWidth, cellHeight, globalInitX, globalInitY, frame);

                directionFrames.Add(new VdFrameData
                {
                    Bitmap = cellBitmap,
                    Palette565 = new List<ushort>(palette565),
                    CenterX = centerX,
                    CenterY = centerY,
                    Width = (ushort)cellBitmap.PixelSize.Width,
                    Height = (ushort)cellBitmap.PixelSize.Height,
                    InitCoordsX = globalInitX,
                    InitCoordsY = globalInitY,
                    EndCoordsX = header.Right,
                    EndCoordsY = header.Bottom,
                    FrameId = frame.Id,
                    FrameNumber = frame.FrameNumber,
                    DataOffset = frame.RelativePixelOffset
                });
            }

            if (directionFrames.Count > 0)
            {
                result[direction] = directionFrames;
            }
        }

        return result;
    }

    private static WriteableBitmap ComposeVdCell(
    WriteableBitmap rawFrame,
    int cellWidth,
    int cellHeight,
    short globalInitX,
    short globalInitY,
    FrameIndex frame)
    {
        byte[] cellPixels = new byte[cellWidth * cellHeight * 4];
        byte[] rawPixels = ReadBitmapPixels(rawFrame);

        int rawWidth = rawFrame.PixelSize.Width;
        int rawHeight = rawFrame.PixelSize.Height;

        int drawX = Math.Abs(globalInitX - frame.InitX);
        int drawY = Math.Abs(globalInitY - frame.InitY);

        for (int y = 0; y < rawHeight; y++)
        {
            int targetY = drawY + y;
            if (targetY < 0 || targetY >= cellHeight)
            {
                continue;
            }

            for (int x = 0; x < rawWidth; x++)
            {
                int targetX = drawX + x;
                if (targetX < 0 || targetX >= cellWidth)
                {
                    continue;
                }

                int src = ((y * rawWidth) + x) * 4;
                int dst = ((targetY * cellWidth) + targetX) * 4;

                if (rawPixels[src + 3] == 0)
                {
                    continue;
                }

                cellPixels[dst + 0] = rawPixels[src + 0];
                cellPixels[dst + 1] = rawPixels[src + 1];
                cellPixels[dst + 2] = rawPixels[src + 2];
                cellPixels[dst + 3] = rawPixels[src + 3];
            }
        }

        WriteableBitmap bitmap = new(
            new PixelSize(cellWidth, cellHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        Marshal.Copy(cellPixels, 0, framebuffer.Address, cellPixels.Length);

        return bitmap;
    }

    private static List<ushort> BuildPalette565(List<ColorEntry> palette)
    {
        List<ushort> result = new();

        for (int i = 0; i < 256; i++)
        {
            if (i >= palette.Count)
            {
                result.Add(0x8000);
                continue;
            }

            ColorEntry color = palette[i];

            if (i == 0)
            {
                result.Add(0x8000);
                continue;
            }

            ushort value = (ushort)(
                0x8000 |
                ((color.R >> 3) << 10) |
                ((color.G >> 3) << 5) |
                (color.B >> 3));

            result.Add(value);
        }

        return result;
    }

    public bool TryReadMetadata(byte[] data, out AmouMetadata metadata)
    {
        metadata = new AmouMetadata();

        if (data == null || data.Length < HeaderSize)
        {
            return false;
        }

        try
        {
            AmouHeader header = ReadHeader(data);

            if (header.Magic != AmouMagic)
            {
                return false;
            }

            List<FrameIndex> frames = ReadFrameIndexes(data, header);
            int actionId = frames.Count > 0 ? frames[0].Id : -1;

            metadata = new AmouMetadata
            {
                AnimId = checked((int)header.AnimId),
                ActionId = actionId,
                FrameCount = checked((int)header.FrameCount),
                ColorCount = checked((int)header.ColorCount)
            };

            return true;
        }
        catch
        {
            return false;
        }
    }
}