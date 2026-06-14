using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public static class VdExportService
{
    private const short VdVersion = 6;
    private const int DirectionCount = 5;
    private const int PaletteCapacity = 256;
    private const int DoubleXor = (0x200 << 22) | (0x200 << 12);
    private const int EndOfFrameMarker = 0x7FFF7FFF;
    private const ushort TransparentBlack = 0x8000;
    private const ushort SafeBlack = 0x8001;

    public static void ExportBodyAnimation(
        string filePath,
        short animType,
        Dictionary<int, Dictionary<int, List<VdFrameData>>> actionDirectionFrames)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        int actionCount = GetVdLength(animType);
        int totalEntries = actionCount * DirectionCount;

        using FileStream stream = File.Create(filePath);
        using BinaryWriter writer = new BinaryWriter(stream);

        writer.Write(VdVersion);
        writer.Write(animType);

        long indexPos = writer.BaseStream.Position;
        long animPos = writer.BaseStream.Position + (12L * totalEntries);

        for (int entryIndex = 0; entryIndex < totalEntries; entryIndex++)
        {
            int action = entryIndex / DirectionCount;
            int direction = entryIndex % DirectionCount;

            if (!TryGetFrames(actionDirectionFrames, action, direction, out List<VdFrameData>? frames) ||
                frames == null ||
                frames.Count == 0)
            {
                writer.BaseStream.Seek(indexPos, SeekOrigin.Begin);
                writer.Write(-1);
                writer.Write(-1);
                writer.Write(-1);
                indexPos = writer.BaseStream.Position;
                continue;
            }

            ExportDirectionBlock(writer, frames, ref indexPos, ref animPos);
        }

        writer.Flush();
    }

    public static short GetAnimTypeFromActionCount(int actionCount)
    {
        return actionCount switch
        {
            13 => 1,
            22 => 0,
            35 => 2,
            _ => throw new InvalidOperationException(
                 "VD export only supports 13, 22, or 35 actions.")
        };
    }

    public static int GetVdLength(short animType)
    {
        return animType switch
        {
            0 => 22,
            1 => 13,
            2 => 35,
            _ => throw new InvalidOperationException(
                 "VD export only supports anim types 0, 1, or 2.")
        };
    }

    private static bool TryGetFrames(
        Dictionary<int, Dictionary<int, List<VdFrameData>>> actionDirectionFrames,
        int action,
        int direction,
        out List<VdFrameData>? frames)
    {
        frames = null;

        if (!actionDirectionFrames.TryGetValue(action, out Dictionary<int, List<VdFrameData>>? directionMap) ||
            directionMap == null)
        {
            return false;
        }

        if (!directionMap.TryGetValue(direction, out frames) || frames == null || frames.Count == 0)
        {
            return false;
        }

        return true;
    }

    private static void ExportDirectionBlock(
        BinaryWriter writer,
        List<VdFrameData> frames,
        ref long indexPos,
        ref long animPos)
    {
        writer.BaseStream.Seek(indexPos, SeekOrigin.Begin);
        writer.Write((int)animPos);
        indexPos = writer.BaseStream.Position;

        writer.BaseStream.Seek(animPos, SeekOrigin.Begin);

        ushort[] palette = BuildExportPalette(frames);

        for (int i = 0; i < PaletteCapacity; i++)
        {
            writer.Write((ushort)(palette[i] ^ 0x8000));
        }

        long startPosition = writer.BaseStream.Position;

        writer.Write(frames.Count);

        long seek = writer.BaseStream.Position;
        long curr = writer.BaseStream.Position + (4L * frames.Count);

        for (int i = 0; i < frames.Count; i++)
        {
            writer.BaseStream.Seek(seek, SeekOrigin.Begin);
            writer.Write((int)(curr - startPosition));
            seek = writer.BaseStream.Position;

            writer.BaseStream.Seek(curr, SeekOrigin.Begin);
            SaveFrame(writer, frames[i], palette);
            curr = writer.BaseStream.Position;
        }

        long length = writer.BaseStream.Position - animPos;
        animPos = writer.BaseStream.Position;

        writer.BaseStream.Seek(indexPos, SeekOrigin.Begin);
        writer.Write((int)length);
        writer.Write(GetDirectionExtra(frames));

        indexPos = writer.BaseStream.Position;
    }

    private static int GetDirectionExtra(List<VdFrameData> frames)
    {
        if (frames == null || frames.Count == 0)
        {
            return 0;
        }

        foreach (VdFrameData frame in frames)
        {
            if (frame.SourceExtra >= 0)
            {
                return frame.SourceExtra;
            }
        }

        return 0;
    }

    private static ushort[] BuildExportPalette(List<VdFrameData> frames)
    {
        for (int i = 0; i < frames.Count; i++)
        {
            List<ushort>? sourcePalette = frames[i].Palette565;
            if (sourcePalette != null && sourcePalette.Count >= PaletteCapacity)
            {
                ushort[] palette = new ushort[PaletteCapacity];

                for (int p = 0; p < PaletteCapacity; p++)
                {
                    ushort value = sourcePalette[p];

                    if (p == 0 && value == 0)
                    {
                        palette[p] = TransparentBlack;
                    }
                    else if (value == TransparentBlack && p != 0)
                    {
                        palette[p] = SafeBlack;
                    }
                    else
                    {
                        palette[p] = value;
                    }
                }

                if (palette[0] == 0)
                {
                    palette[0] = TransparentBlack;
                }

                return palette;
            }
        }

        ushort[] builtPalette = new ushort[PaletteCapacity];
        builtPalette[0] = TransparentBlack;

        HashSet<ushort> seen = new HashSet<ushort>();
        int paletteIndex = 1;

        for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
        {
            FramePixels pixels = ReadPixels(frames[frameIndex].Bitmap);

            for (int y = 0; y < pixels.Height; y++)
            {
                for (int x = 0; x < pixels.Width; x++)
                {
                    uint argb = GetColor(pixels, x, y);
                    byte alpha = (byte)((argb >> 24) & 0xFF);

                    if (alpha == 0)
                    {
                        continue;
                    }

                    ushort color1555 = ConvertArgbTo1555(argb);

                    if (color1555 == TransparentBlack)
                    {
                        color1555 = SafeBlack;
                    }

                    if (seen.Add(color1555))
                    {
                        if (paletteIndex >= PaletteCapacity)
                        {
                            return builtPalette;
                        }

                        builtPalette[paletteIndex] = color1555;
                        paletteIndex++;
                    }
                }
            }
        }

        return builtPalette;
    }

    private static void SaveFrame(
        BinaryWriter writer,
        VdFrameData frame,
        ushort[] palette)
    {
        FramePixels pixels = ReadPixels(frame.Bitmap);

        int width = pixels.Width;
        int height = pixels.Height;
        int centerX = frame.CenterX;
        int centerY = frame.CenterY;

        writer.Write((short)centerX);
        writer.Write((short)centerY);
        writer.Write((ushort)width);
        writer.Write((ushort)height);

        Dictionary<ushort, byte> paletteIndexByColor = BuildPaletteIndexMap(palette);

        for (int y = 0; y < height; y++)
        {
            int x = 0;

            while (x < width)
            {
                while (x < width && IsTransparent(pixels, x, y))
                {
                    x++;
                }

                if (x >= width)
                {
                    break;
                }

                int runStart = x;

                while (x < width && !IsTransparent(pixels, x, y))
                {
                    x++;
                }

                int runLength = x - runStart;
                int remaining = runLength;
                int currentRunStart = runStart;

                while (remaining > 0)
                {
                    int chunkLength = Math.Min(remaining, 0x0FFF);

                    int offsetX = currentRunStart - centerX + 512;
                    int offsetY = y - centerY - height + 512;

                    int header = chunkLength | (offsetY << 12) | (offsetX << 22);
                    header ^= DoubleXor;

                    writer.Write(header);

                    for (int i = 0; i < chunkLength; i++)
                    {
                        uint argb = GetColor(pixels, currentRunStart + i, y);
                        ushort color1555 = ConvertArgbTo1555(argb);

                        if (color1555 == TransparentBlack)
                        {
                            color1555 = SafeBlack;
                        }

                        if (!paletteIndexByColor.TryGetValue(color1555, out byte paletteIndex))
                        {
                            paletteIndex = FindClosestPaletteIndex(color1555, palette);
                        }

                        writer.Write(paletteIndex);
                    }

                    currentRunStart += chunkLength;
                    remaining -= chunkLength;
                }
            }
        }

        writer.Write(EndOfFrameMarker);
    }

    private static Dictionary<ushort, byte> BuildPaletteIndexMap(ushort[] palette)
    {
        Dictionary<ushort, byte> map = new Dictionary<ushort, byte>();

        for (int i = 0; i < palette.Length; i++)
        {
            ushort color = palette[i];

            if (!map.ContainsKey(color))
            {
                map[color] = (byte)i;
            }
        }

        return map;
    }

    private static byte FindClosestPaletteIndex(ushort color1555, ushort[] palette)
    {
        int targetR = (color1555 >> 10) & 0x1F;
        int targetG = (color1555 >> 5) & 0x1F;
        int targetB = color1555 & 0x1F;

        int bestDistance = int.MaxValue;
        byte bestIndex = 1;

        for (int i = 1; i < palette.Length; i++)
        {
            ushort paletteColor = palette[i];

            if (paletteColor == 0 || paletteColor == TransparentBlack)
            {
                continue;
            }

            int r = (paletteColor >> 10) & 0x1F;
            int g = (paletteColor >> 5) & 0x1F;
            int b = paletteColor & 0x1F;

            int dr = targetR - r;
            int dg = targetG - g;
            int db = targetB - b;
            int distance = (dr * dr) + (dg * dg) + (db * db);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = (byte)i;

                if (distance == 0)
                {
                    break;
                }
            }
        }

        return bestIndex;
    }

    private static ushort ConvertArgbTo1555(uint argb)
    {
        byte alpha = (byte)((argb >> 24) & 0xFF);
        if (alpha == 0)
        {
            return 0;
        }

        byte r = (byte)((argb >> 16) & 0xFF);
        byte g = (byte)((argb >> 8) & 0xFF);
        byte b = (byte)(argb & 0xFF);

        int r5 = r >> 3;
        int g5 = g >> 3;
        int b5 = b >> 3;

        return (ushort)(0x8000 | (r5 << 10) | (g5 << 5) | b5);
    }

    private static bool IsTransparent(FramePixels frame, int x, int y)
    {
        int offset = ((y * frame.Width) + x) * 4;
        return frame.Pixels[offset + 3] == 0;
    }

    private static uint GetColor(FramePixels frame, int x, int y)
    {
        int offset = ((y * frame.Width) + x) * 4;

        byte b = frame.Pixels[offset + 0];
        byte g = frame.Pixels[offset + 1];
        byte r = frame.Pixels[offset + 2];
        byte a = frame.Pixels[offset + 3];

        return
            ((uint)a << 24) |
            ((uint)r << 16) |
            ((uint)g << 8) |
            b;
    }

    private static FramePixels ReadPixels(WriteableBitmap bitmap)
    {
        using ILockedFramebuffer framebuffer = bitmap.Lock();

        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int sourceRowBytes = framebuffer.RowBytes;
        int targetRowBytes = width * 4;

        byte[] sourceBytes = new byte[sourceRowBytes * height];
        Marshal.Copy(framebuffer.Address, sourceBytes, 0, sourceBytes.Length);

        byte[] packedBytes = new byte[targetRowBytes * height];

        for (int y = 0; y < height; y++)
        {
            Buffer.BlockCopy(
                sourceBytes,
                y * sourceRowBytes,
                packedBytes,
                y * targetRowBytes,
                targetRowBytes);
        }

        return new FramePixels(width, height, packedBytes);
    }

    private readonly struct FramePixels
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; }

        public FramePixels(int width, int height, byte[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels ?? Array.Empty<byte>();
        }
    }
}