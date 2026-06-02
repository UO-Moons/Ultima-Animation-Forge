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

public sealed class DragonMapService
{
    public sealed class ValidateResult
    {
        public bool Success { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int UnknownColorCount { get; set; }
        public int KnownColorCount { get; set; }
        public int UniqueColorCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public const int DefaultMapWidth = 6144;
    public const int DefaultMapHeight = 4096;

    public WriteableBitmap CreateBlankMap(Color fillColor)
    {
        WriteableBitmap bitmap = new(
            new PixelSize(DefaultMapWidth, DefaultMapHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        Fill(bitmap, fillColor);
        return bitmap;
    }

    public WriteableBitmap LoadBitmap(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return WriteableBitmap.Decode(stream);
    }

    public void SaveBitmap(WriteableBitmap bitmap, string path)
    {
        bitmap.Save(path);
    }

    public List<DragonTerrainColor> BuildDefaultTerrainPalette()
    {
        return new List<DragonTerrainColor>
    {
        new() { PaletteIndex = 0, GroupId = 0x02, GroupName = "Water", Z = -5, Color = Color.FromRgb(0, 0, 90) },
        new() { PaletteIndex = 1, GroupId = 0x00, GroupName = "Grass", Z = 0, Color = Color.FromRgb(36, 82, 32) },
        new() { PaletteIndex = 2, GroupId = 0x00, GroupName = "Grass", Z = 5, Color = Color.FromRgb(48, 105, 42) },
        new() { PaletteIndex = 3, GroupId = 0x00, GroupName = "Grass", Z = 10, Color = Color.FromRgb(64, 128, 54) },
        new() { PaletteIndex = 4, GroupId = 0x01, GroupName = "Forest", Z = 0, Color = Color.FromRgb(0, 75, 38) },
        new() { PaletteIndex = 5, GroupId = 0x05, GroupName = "Desert", Z = 0, Color = Color.FromRgb(180, 158, 94) },
        new() { PaletteIndex = 6, GroupId = 0x06, GroupName = "Mountain", Z = 20, Color = Color.FromRgb(96, 88, 96) },
        new() { PaletteIndex = 7, GroupId = 0x18, GroupName = "Swamp", Z = 0, Color = Color.FromRgb(50, 65, 42) },
        new() { PaletteIndex = 8, GroupId = 0x07, GroupName = "Snow", Z = 0, Color = Color.FromRgb(220, 220, 220) },
        new() { PaletteIndex = 9, GroupId = 0x04, GroupName = "Black", Z = 0, Color = Color.FromRgb(0, 0, 0) }
    };
    }

    public unsafe void PaintBrush(
        WriteableBitmap bitmap,
        int centerX,
        int centerY,
        int brushSize,
        DragonBrushShape shape,
        Color color)
    {
        if (bitmap == null)
        {
            return;
        }

        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        int radius = Math.Max(1, brushSize / 2);
        int radiusSquared = radius * radius;

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        byte b = color.B;
        byte g = color.G;
        byte r = color.R;
        byte a = 255;

        byte* basePtr = (byte*)framebuffer.Address;

        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            if (y < 0 || y >= height)
            {
                continue;
            }

            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                if (x < 0 || x >= width)
                {
                    continue;
                }

                int dx = x - centerX;
                int dy = y - centerY;

                bool shouldPaint = shape switch
                {
                    DragonBrushShape.Circle => (dx * dx) + (dy * dy) <= radiusSquared,
                    DragonBrushShape.Square => true,
                    DragonBrushShape.Diamond => Math.Abs(dx) + Math.Abs(dy) <= radius,

                    DragonBrushShape.HorizontalLine => Math.Abs(dy) <= 1,
                    DragonBrushShape.VerticalLine => Math.Abs(dx) <= 1,

                    DragonBrushShape.Slash => Math.Abs(dx + dy) <= 1,
                    DragonBrushShape.Backslash => Math.Abs(dx - dy) <= 1,

                    DragonBrushShape.Cross => Math.Abs(dx) <= 1 || Math.Abs(dy) <= 1,
                    DragonBrushShape.X => Math.Abs(dx - dy) <= 1 || Math.Abs(dx + dy) <= 1,

                    _ => true
                };

                if (!shouldPaint)
                {
                    continue;
                }

                byte* pixel = basePtr + (y * framebuffer.RowBytes) + (x * 4);
                pixel[0] = b;
                pixel[1] = g;
                pixel[2] = r;
                pixel[3] = a;
            }
        }
    }

    private static void Fill(WriteableBitmap bitmap, Color color)
    {
        using ILockedFramebuffer framebuffer = bitmap.Lock();

        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int rowBytes = framebuffer.RowBytes;

        byte[] pixels = new byte[rowBytes * height];

        for (int y = 0; y < height; y++)
        {
            int row = y * rowBytes;

            for (int x = 0; x < width; x++)
            {
                int offset = row + (x * 4);
                pixels[offset + 0] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = 255;
            }
        }

        Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
    }

    public void FillCanvas(WriteableBitmap bitmap, Color color)
    {
        if (bitmap == null)
        {
            return;
        }

        Fill(bitmap, color);
    }

    public Color? GetPixelColor(WriteableBitmap bitmap, int x, int y)
    {
        if (bitmap == null)
        {
            return null;
        }

        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return null;
        }

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        int offset = (y * framebuffer.RowBytes) + (x * 4);

        byte[] pixel = new byte[4];
        Marshal.Copy(framebuffer.Address + offset, pixel, 0, 4);

        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    public void FloodFill(
    WriteableBitmap bitmap,
    int startX,
    int startY,
    Color fillColor)
    {
        if (bitmap == null)
        {
            return;
        }

        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        if (startX < 0 || startY < 0 || startX >= width || startY >= height)
        {
            return;
        }

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        int rowBytes = framebuffer.RowBytes;
        int totalBytes = rowBytes * height;
        byte[] pixels = new byte[totalBytes];

        Marshal.Copy(framebuffer.Address, pixels, 0, totalBytes);

        int startOffset = (startY * rowBytes) + (startX * 4);

        byte targetB = pixels[startOffset + 0];
        byte targetG = pixels[startOffset + 1];
        byte targetR = pixels[startOffset + 2];
        byte targetA = pixels[startOffset + 3];

        byte fillB = fillColor.B;
        byte fillG = fillColor.G;
        byte fillR = fillColor.R;
        byte fillA = 255;

        if (targetB == fillB &&
            targetG == fillG &&
            targetR == fillR &&
            targetA == fillA)
        {
            return;
        }

        Queue<(int X, int Y)> queue = new();
        queue.Enqueue((startX, startY));

        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();

            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                continue;
            }

            int offset = (y * rowBytes) + (x * 4);

            if (pixels[offset + 0] != targetB ||
                pixels[offset + 1] != targetG ||
                pixels[offset + 2] != targetR ||
                pixels[offset + 3] != targetA)
            {
                continue;
            }

            pixels[offset + 0] = fillB;
            pixels[offset + 1] = fillG;
            pixels[offset + 2] = fillR;
            pixels[offset + 3] = fillA;

            queue.Enqueue((x + 1, y));
            queue.Enqueue((x - 1, y));
            queue.Enqueue((x, y + 1));
            queue.Enqueue((x, y - 1));
        }

        Marshal.Copy(pixels, 0, framebuffer.Address, totalBytes);
    }

    public unsafe void PaintRectangle(
    WriteableBitmap bitmap,
    int x1,
    int y1,
    int x2,
    int y2,
    Color color)
    {
        if (bitmap == null)
        {
            return;
        }

        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        int left = Math.Clamp(Math.Min(x1, x2), 0, width - 1);
        int right = Math.Clamp(Math.Max(x1, x2), 0, width - 1);
        int top = Math.Clamp(Math.Min(y1, y2), 0, height - 1);
        int bottom = Math.Clamp(Math.Max(y1, y2), 0, height - 1);

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        byte b = color.B;
        byte g = color.G;
        byte r = color.R;
        byte a = 255;

        byte* basePtr = (byte*)framebuffer.Address;

        for (int y = top; y <= bottom; y++)
        {
            byte* row = basePtr + (y * framebuffer.RowBytes);

            for (int x = left; x <= right; x++)
            {
                byte* pixel = row + (x * 4);

                pixel[0] = b;
                pixel[1] = g;
                pixel[2] = r;
                pixel[3] = a;
            }
        }
    }

    public void PaintLine(
    WriteableBitmap bitmap,
    int x1,
    int y1,
    int x2,
    int y2,
    int brushSize,
    DragonBrushShape shape,
    Color color)
    {
        if (bitmap == null)
        {
            return;
        }

        int dx = Math.Abs(x2 - x1);
        int dy = Math.Abs(y2 - y1);

        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;

        int error = dx - dy;

        int x = x1;
        int y = y1;

        while (true)
        {
            PaintBrush(bitmap, x, y, brushSize, shape, color);

            if (x == x2 && y == y2)
            {
                break;
            }

            int e2 = error * 2;

            if (e2 > -dy)
            {
                error -= dy;
                x += sx;
            }

            if (e2 < dx)
            {
                error += dx;
                y += sy;
            }
        }
    }

    public unsafe void PaintEllipse(
    WriteableBitmap bitmap,
    int x1,
    int y1,
    int x2,
    int y2,
    Color color)
    {
        if (bitmap == null)
        {
            return;
        }

        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        int left = Math.Clamp(Math.Min(x1, x2), 0, width - 1);
        int right = Math.Clamp(Math.Max(x1, x2), 0, width - 1);
        int top = Math.Clamp(Math.Min(y1, y2), 0, height - 1);
        int bottom = Math.Clamp(Math.Max(y1, y2), 0, height - 1);

        double radiusX = Math.Max(1, (right - left) / 2.0);
        double radiusY = Math.Max(1, (bottom - top) / 2.0);
        double centerX = left + radiusX;
        double centerY = top + radiusY;

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        byte b = color.B;
        byte g = color.G;
        byte r = color.R;
        byte a = 255;

        byte* basePtr = (byte*)framebuffer.Address;

        for (int y = top; y <= bottom; y++)
        {
            double ny = (y - centerY) / radiusY;

            byte* row = basePtr + (y * framebuffer.RowBytes);

            for (int x = left; x <= right; x++)
            {
                double nx = (x - centerX) / radiusX;

                if ((nx * nx) + (ny * ny) > 1.0)
                {
                    continue;
                }

                byte* pixel = row + (x * 4);

                pixel[0] = b;
                pixel[1] = g;
                pixel[2] = r;
                pixel[3] = a;
            }
        }
    }

    public ValidateResult ValidateDragonMap(
    WriteableBitmap bitmap,
    IReadOnlyCollection<DragonTerrainColor> terrainColors)
    {
        ValidateResult result = new();

        if (bitmap == null)
        {
            result.Message = "No map loaded.";
            return result;
        }

        result.Width = bitmap.PixelSize.Width;
        result.Height = bitmap.PixelSize.Height;

        if (result.Width != DefaultMapWidth || result.Height != DefaultMapHeight)
        {
            result.Message =
                $"Invalid map size: {result.Width}x{result.Height}. Expected {DefaultMapWidth}x{DefaultMapHeight}.";
            return result;
        }

        HashSet<uint> knownColors = terrainColors
            .Select(x => PackColor(x.Color))
            .ToHashSet();

        HashSet<uint> uniqueColors = new();
        int knownCount = 0;
        int unknownCount = 0;

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int rowBytes = framebuffer.RowBytes;

        byte[] pixels = new byte[rowBytes * height];
        Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);

        for (int y = 0; y < height; y++)
        {
            int row = y * rowBytes;

            for (int x = 0; x < width; x++)
            {
                int offset = row + (x * 4);

                uint packed = PackColor(
                    pixels[offset + 2],
                    pixels[offset + 1],
                    pixels[offset + 0]);

                uniqueColors.Add(packed);

                if (knownColors.Contains(packed))
                {
                    knownCount++;
                }
                else
                {
                    unknownCount++;
                }
            }
        }

        result.KnownColorCount = knownCount;
        result.UnknownColorCount = unknownCount;
        result.UniqueColorCount = uniqueColors.Count;
        result.Success = unknownCount == 0;

        result.Message = result.Success
            ? $"Map valid. Size {result.Width}x{result.Height}. Unique colors: {result.UniqueColorCount}."
            : $"Map has {unknownCount} pixels with colors not in Dragon terrain palette. Unique colors: {result.UniqueColorCount}.";

        return result;
    }

    private static uint PackColor(Color color)
    {
        return PackColor(color.R, color.G, color.B);
    }

    private static uint PackColor(byte r, byte g, byte b)
    {
        return ((uint)r << 16) | ((uint)g << 8) | b;
    }

    public unsafe void PaintStampPattern(
        WriteableBitmap bitmap,
        int centerX,
        int centerY,
        int[,] pattern,
        IReadOnlyCollection<DragonTerrainColor> terrainColors,
        int cellSize)
    {
        if (bitmap == null || pattern == null)
        {
            return;
        }

        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        int rows = pattern.GetLength(0);
        int cols = pattern.GetLength(1);

        cellSize = Math.Clamp(cellSize, 1, 256);

        int stampWidth = cols * cellSize;
        int stampHeight = rows * cellSize;

        int startX = centerX - (stampWidth / 2);
        int startY = centerY - (stampHeight / 2);

        Dictionary<int, Color> colorByGroup = terrainColors
            .GroupBy(x => x.GroupId)
            .ToDictionary(x => x.Key, x => x.First().Color);

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        byte* basePtr = (byte*)framebuffer.Address;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int groupId = pattern[y, x];

                if (groupId < 0)
                {
                    continue;
                }

                if (!colorByGroup.TryGetValue(groupId, out Color color))
                {
                    continue;
                }

                int mapX = startX + x;
                int mapY = startY + y;

                if (mapX < 0 || mapY < 0 || mapX >= width || mapY >= height)
                {
                    continue;
                }

                byte* pixel = basePtr + (mapY * framebuffer.RowBytes) + (mapX * 4);

                pixel[0] = color.B;
                pixel[1] = color.G;
                pixel[2] = color.R;
                pixel[3] = 255;
            }
        }
    }
}