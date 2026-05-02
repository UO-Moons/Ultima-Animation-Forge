using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class MulEditedFrameSaveService
{
    private const int PaletteCapacity = 256;
    private const int EndOfFrameMarker = 0x7FFF7FFF;
    private const ushort TransparentBlack = 0x8000;
    private const ushort SafeBlack = 0x8001;
    private const uint DoubleXor = ((uint)0x200 << 22) | ((uint)0x200 << 12);

    public sealed class SaveResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public byte[] BuildEditedDirectionBlock(List<VdFrameData> frames)
    {
        if (frames == null || frames.Count == 0)
        {
            return Array.Empty<byte>();
        }

        return BuildMulBlock(frames);
    }

    public SaveResult SaveEditedDirectionToMul(
        string mulPath,
        string idxPath,
        int slotIndex,
        int extra,
        List<VdFrameData> frames)
    {
        if (string.IsNullOrWhiteSpace(mulPath) || !File.Exists(mulPath))
        {
            return Fail("Target MUL file was not found.");
        }

        if (string.IsNullOrWhiteSpace(idxPath) || !File.Exists(idxPath))
        {
            return Fail("Target IDX file was not found.");
        }

        if (slotIndex < 0)
        {
            return Fail("Invalid slot index.");
        }

        if (frames == null || frames.Count == 0)
        {
            return Fail("There are no edited frames to save.");
        }

        try
        {
            byte[] blockData = BuildMulBlock(frames);
            List<AnimationIdxEntry> idxEntries = ReadIdxEntries(idxPath);

            while (idxEntries.Count <= slotIndex)
            {
                idxEntries.Add(new AnimationIdxEntry
                {
                    Offset = -1,
                    Length = -1,
                    Extra = -1,
                    Index = idxEntries.Count
                });
            }

            long nextMulOffset;

            using (FileStream mulStream = new FileStream(mulPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                mulStream.Seek(0, SeekOrigin.End);
                nextMulOffset = mulStream.Position;
                mulStream.Write(blockData, 0, blockData.Length);
            }

            idxEntries[slotIndex].Offset = checked((int)nextMulOffset);
            idxEntries[slotIndex].Length = blockData.Length;
            idxEntries[slotIndex].Extra = extra;

            WriteIdxEntries(idxPath, idxEntries);

            return new SaveResult
            {
                Success = true,
                Message =
                    "Saved edited frames to " +
                    Path.GetFileName(mulPath) +
                    " at slot " + slotIndex + "."
            };
        }
        catch (UnauthorizedAccessException)
        {
            return Fail("Access denied while saving edited MUL frames.");
        }
        catch (IOException exception)
        {
            return Fail("I/O error while saving edited MUL frames: " + exception.Message);
        }
        catch (Exception exception)
        {
            return Fail("Failed saving edited MUL frames: " + exception.Message);
        }
    }

    private static byte[] BuildMulBlock(List<VdFrameData> frames)
    {
        using MemoryStream output = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(output);

        ushort[] palette = BuildExportPalette(frames);

        for (int i = 0; i < PaletteCapacity; i++)
        {
            writer.Write((ushort)(palette[i] ^ 0x8000));
        }

        short animInitX = 0;
        short animInitY = 0;
        bool foundInitCoords = false;

        for (int i = 0; i < frames.Count; i++)
        {
            VdFrameData frame = frames[i];

            if (!foundInitCoords)
            {
                animInitX = frame.InitCoordsX;
                animInitY = frame.InitCoordsY;
                foundInitCoords = true;
            }
            else
            {
                if (frame.InitCoordsX < animInitX)
                {
                    animInitX = frame.InitCoordsX;
                }

                if (frame.InitCoordsY < animInitY)
                {
                    animInitY = frame.InitCoordsY;
                }
            }
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
            SaveFrame(writer, frames[i], palette, animInitX, animInitY);
            curr = writer.BaseStream.Position;
        }

        writer.Flush();
        return output.ToArray();
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
        ushort[] palette,
        short animInitX,
        short animInitY)
    {
        FramePixels pixels = ReadPixels(frame.Bitmap);

        int width = pixels.Width;
        int height = pixels.Height;
        int centerX = frame.CenterX;
        int centerY = frame.CenterY;

        int topX = Math.Abs(animInitX - frame.InitCoordsX);
        int topY = Math.Abs(animInitY - frame.InitCoordsY);

        writer.Write((short)centerX);
        writer.Write((short)centerY);
        writer.Write((ushort)width);
        writer.Write((ushort)height);

        Dictionary<ushort, byte> paletteLookup = BuildPaletteLookup(palette);

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
                if (runLength <= 0)
                {
                    continue;
                }

                int offsetX = runStart - centerX - topX;
                int offsetY = y - centerY - topY - height + 1;

                uint header =
                    (uint)(runLength & 0x0FFF) |
                    (uint)(((offsetY + 0x200) & 0x03FF) << 12) |
                    (uint)(((offsetX + 0x200) & 0x03FF) << 22);

                header ^= DoubleXor;

                writer.Write(header);

                for (int i = 0; i < runLength; i++)
                {
                    uint argb = GetColor(pixels, runStart + i, y);
                    ushort color1555 = ConvertArgbTo1555(argb);

                    if (color1555 == TransparentBlack)
                    {
                        color1555 = SafeBlack;
                    }

                    if (!paletteLookup.TryGetValue(color1555, out byte paletteIndex))
                    {
                        paletteIndex = FindClosestPaletteIndex(color1555, palette);
                    }

                    writer.Write(paletteIndex);
                }
            }
        }

        writer.Write(EndOfFrameMarker);
    }

    private static byte FindClosestPaletteIndex(ushort color1555, ushort[] palette)
    {
        int targetRed = (color1555 >> 10) & 0x1F;
        int targetGreen = (color1555 >> 5) & 0x1F;
        int targetBlue = color1555 & 0x1F;

        int bestDistance = int.MaxValue;
        byte bestIndex = 1;

        for (int i = 1; i < palette.Length && i < 256; i++)
        {
            ushort paletteColor = palette[i];

            if (paletteColor == 0 || paletteColor == TransparentBlack)
            {
                continue;
            }

            int red = (paletteColor >> 10) & 0x1F;
            int green = (paletteColor >> 5) & 0x1F;
            int blue = paletteColor & 0x1F;

            int redDiff = targetRed - red;
            int greenDiff = targetGreen - green;
            int blueDiff = targetBlue - blue;

            int distance =
                (redDiff * redDiff) +
                (greenDiff * greenDiff) +
                (blueDiff * blueDiff);

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

    private static Dictionary<ushort, byte> BuildPaletteLookup(ushort[] palette)
    {
        Dictionary<ushort, byte> result = new Dictionary<ushort, byte>();

        for (int i = 0; i < palette.Length && i < 256; i++)
        {
            ushort value = palette[i];

            if (!result.ContainsKey(value))
            {
                result[value] = (byte)i;
            }
        }

        return result;
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

    private static FramePixels ReadPixels(WriteableBitmap bitmap)
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

    private static uint GetColor(FramePixels pixels, int x, int y)
    {
        int offset = ((y * pixels.Width) + x) * 4;

        byte blue = pixels.Pixels[offset + 0];
        byte green = pixels.Pixels[offset + 1];
        byte red = pixels.Pixels[offset + 2];
        byte alpha = pixels.Pixels[offset + 3];

        return
            ((uint)alpha << 24) |
            ((uint)red << 16) |
            ((uint)green << 8) |
            blue;
    }

    private static bool IsTransparent(FramePixels pixels, int x, int y)
    {
        int offset = ((y * pixels.Width) + x) * 4;
        return pixels.Pixels[offset + 3] == 0;
    }

    private static ushort ConvertArgbTo1555(uint argb)
    {
        byte alpha = (byte)((argb >> 24) & 0xFF);
        if (alpha == 0)
        {
            return TransparentBlack;
        }

        byte red = (byte)((argb >> 16) & 0xFF);
        byte green = (byte)((argb >> 8) & 0xFF);
        byte blue = (byte)(argb & 0xFF);

        return (ushort)(
            0x8000 |
            ((red >> 3) << 10) |
            ((green >> 3) << 5) |
            (blue >> 3));
    }

    private static List<AnimationIdxEntry> ReadIdxEntries(string idxPath)
    {
        List<AnimationIdxEntry> result = new List<AnimationIdxEntry>();

        using FileStream stream = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader reader = new BinaryReader(stream);

        int index = 0;

        while (reader.BaseStream.Position + 12 <= reader.BaseStream.Length)
        {
            result.Add(new AnimationIdxEntry
            {
                Offset = reader.ReadInt32(),
                Length = reader.ReadInt32(),
                Extra = reader.ReadInt32(),
                Index = index
            });

            index++;
        }

        return result;
    }

    private static void WriteIdxEntries(string idxPath, List<AnimationIdxEntry> entries)
    {
        using BinaryWriter writer = new BinaryWriter(File.Open(idxPath, FileMode.Create, FileAccess.Write, FileShare.None));

        for (int i = 0; i < entries.Count; i++)
        {
            writer.Write(entries[i].Offset);
            writer.Write(entries[i].Length);
            writer.Write(entries[i].Extra);
        }
    }

    private static SaveResult Fail(string message)
    {
        return new SaveResult
        {
            Success = false,
            Message = message
        };
    }
}