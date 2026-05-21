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

public sealed class LightMulService
{
    public List<LightEntry> Load(string folderPath)
    {
        List<LightEntry> result = new();

        string idxPath = Path.Combine(folderPath, "lightidx.mul");
        string mulPath = Path.Combine(folderPath, "light.mul");

        if (!File.Exists(idxPath) || !File.Exists(mulPath))
        {
            return result;
        }

        using BinaryReader idx = new(File.Open(idxPath, FileMode.Open, FileAccess.Read, FileShare.Read));
        using FileStream mul = new(mulPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        int index = 0;
        while (idx.BaseStream.Position + 12 <= idx.BaseStream.Length)
        {
            int offset = idx.ReadInt32();
            int length = idx.ReadInt32();
            int extra = idx.ReadInt32();

            int width = extra & 0xFFFF;
            int height = (extra >> 16) & 0xFFFF;

            // Fix bad/mismatched IDX size values.
            // Example: Light 50 says 280x280 but length is 90000, which is really 300x300.
            if (width > 0 && height > 0 && length > 0 && width * height != length)
            {
                int square = (int)Math.Sqrt(length);

                if (square * square == length)
                {
                    width = square;
                    height = square;
                }
            }

            LightEntry entry = new()
            {
                Index = index,
                Offset = offset,
                Length = length,
                Width = width,
                Height = height,
                IsRemoved = offset < 0 || length <= 0 || width <= 0 || height <= 0
            };

            if (!entry.IsRemoved && offset + length <= mul.Length)
            {
                mul.Seek(offset, SeekOrigin.Begin);
                entry.RawData = new byte[length];
                mul.Read(entry.RawData, 0, length);
                entry.Preview = BuildLightBitmap(entry.RawData, width, height);
            }

            result.Add(entry);
            index++;
        }

        return result;
    }

    public void Remove(LightEntry entry)
    {
        entry.IsRemoved = true;
        entry.Offset = -1;
        entry.Length = 0;
        entry.Width = 0;
        entry.Height = 0;
        entry.RawData = [];
        entry.Preview = null;
    }

    public void Save(string folderPath, IReadOnlyList<LightEntry> entries)
    {
        string idxPath = Path.Combine(folderPath, "lightidx.mul");
        string mulPath = Path.Combine(folderPath, "light.mul");

        if (File.Exists(idxPath) && !File.Exists(idxPath + ".bak"))
        {
            File.Copy(idxPath, idxPath + ".bak");
        }

        if (File.Exists(mulPath) && !File.Exists(mulPath + ".bak"))
        {
            File.Copy(mulPath, mulPath + ".bak");
        }

        using BinaryWriter idx = new(File.Open(idxPath, FileMode.Create, FileAccess.Write, FileShare.None));
        using BinaryWriter mul = new(File.Open(mulPath, FileMode.Create, FileAccess.Write, FileShare.None));

        foreach (LightEntry entry in entries)
        {
            if (entry.IsRemoved || entry.RawData.Length == 0 || entry.Width <= 0 || entry.Height <= 0)
            {
                idx.Write(-1);
                idx.Write(0);
                idx.Write(0);
                continue;
            }

            idx.Write((int)mul.BaseStream.Position);
            mul.Write(entry.RawData);
            idx.Write(entry.RawData.Length);
            idx.Write((entry.Height << 16) | entry.Width);
        }
    }

    private static WriteableBitmap BuildLightBitmap(byte[] raw, int width, int height)
    {
        WriteableBitmap bitmap = new(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        byte[] pixels = new byte[width * height * 4];
        int count = Math.Min(width * height, raw.Length);

        for (int i = 0; i < count; i++)
        {
            sbyte value = unchecked((sbyte)raw[i]);

            int p = i * 4;

            // 0 is the empty/neutral light background.
            // Do not draw it, or the preview becomes a white square.
            if (value == 0)
            {
                pixels[p + 0] = 0;
                pixels[p + 1] = 0;
                pixels[p + 2] = 0;
                pixels[p + 3] = 0;
                continue;
            }

            int level5 = 0x1F + value;
            if (level5 < 0)
            {
                level5 = 0;
            }
            else if (level5 > 31)
            {
                level5 = 31;
            }

            byte shade = (byte)((level5 * 255) / 31);

            pixels[p + 0] = shade;
            pixels[p + 1] = shade;
            pixels[p + 2] = shade;
            pixels[p + 3] = 255;
        }

        using ILockedFramebuffer fb = bitmap.Lock();
        Marshal.Copy(pixels, 0, fb.Address, pixels.Length);

        return bitmap;
    }

    public void ImportPngIntoLight(LightEntry entry, string pngPath)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        if (string.IsNullOrWhiteSpace(pngPath) || !File.Exists(pngPath))
        {
            throw new FileNotFoundException("PNG file was not found.", pngPath);
        }

        using FileStream stream = File.OpenRead(pngPath);
        Bitmap bitmap = new Bitmap(stream);

        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        byte[] raw = ConvertBitmapToRawLight(bitmap);

        entry.Width = width;
        entry.Height = height;
        entry.Length = raw.Length;
        entry.RawData = raw;
        entry.IsRemoved = false;
        entry.Preview = BuildLightBitmap(raw, width, height);
    }

    public void ExportLightToPng(LightEntry entry, string pngPath)
    {
        if (entry == null || entry.RawData.Length == 0 || entry.Width <= 0 || entry.Height <= 0)
        {
            throw new InvalidOperationException("Selected light has no image data.");
        }

        WriteableBitmap bitmap = BuildLightBitmap(entry.RawData, entry.Width, entry.Height);

        string finalPath = pngPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? pngPath
            : pngPath + ".png";

        bitmap.Save(finalPath);
    }

    private static byte[] ConvertBitmapToRawLight(Bitmap bitmap)
    {
        using WriteableBitmap converted = new(
            bitmap.PixelSize,
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (ILockedFramebuffer target = converted.Lock())
        {
            bitmap.CopyPixels(
                new PixelRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height),
                target.Address,
                target.RowBytes * target.Size.Height,
                target.RowBytes);
        }

        using ILockedFramebuffer framebuffer = converted.Lock();

        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int rowBytes = framebuffer.RowBytes;

        byte[] pixels = new byte[rowBytes * height];
        Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);

        byte[] raw = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            int row = y * rowBytes;

            for (int x = 0; x < width; x++)
            {
                int p = row + (x * 4);

                byte blue = pixels[p + 0];
                byte green = pixels[p + 1];
                byte red = pixels[p + 2];
                byte alpha = pixels[p + 3];

                int rawIndex = (y * width) + x;

                if (alpha == 0)
                {
                    raw[rawIndex] = 0;
                    continue;
                }

                int gray = (red + green + blue) / 3;
                int level5 = (gray * 31) / 255;

                int value = level5 - 31;

                if (value > 0)
                {
                    value--;
                }

                if (value < sbyte.MinValue)
                {
                    value = sbyte.MinValue;
                }
                else if (value > sbyte.MaxValue)
                {
                    value = sbyte.MaxValue;
                }

                raw[rawIndex] = unchecked((byte)(sbyte)value);
            }
        }

        return raw;
    }

    public LightEntry CreateBlankLight(int index, int width, int height)
    {
        width = Math.Clamp(width, 8, 600);
        height = Math.Clamp(height, 8, 600);

        byte[] raw = new byte[width * height];

        return new LightEntry
        {
            Index = index,
            Offset = 0,
            Length = raw.Length,
            Width = width,
            Height = height,
            IsRemoved = false,
            RawData = raw,
            Preview = BuildLightBitmap(raw, width, height)
        };
    }

    public void ClearLight(LightEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        int width = Math.Clamp(entry.Width, 8, 600);
        int height = Math.Clamp(entry.Height, 8, 600);

        entry.RawData = new byte[width * height];
        entry.Length = entry.RawData.Length;
        entry.Width = width;
        entry.Height = height;
        entry.IsRemoved = false;
        entry.Preview = BuildLightBitmap(entry.RawData, width, height);
    }

    public void ApplyRoundPreset(LightEntry entry, int width, int height, int strength)
    {
        width = Math.Clamp(width, 8, 600);
        height = Math.Clamp(height, 8, 600);
        strength = Math.Clamp(strength, 1, 31);

        byte[] raw = new byte[width * height];

        double centerX = (width - 1) / 2.0;
        double centerY = (height - 1) / 2.0;
        double radius = Math.Min(width, height) * 0.42;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double dx = x - centerX;
                double dy = y - centerY;
                double distance = Math.Sqrt((dx * dx) + (dy * dy));

                if (distance > radius)
                {
                    continue;
                }

                double t = 1.0 - (distance / radius);

                // Smoothstep falloff gives a softer UO-style edge.
                t = t * t * (3.0 - (2.0 * t));

                int value = (int)Math.Round(strength * t);

                if (value <= 0)
                {
                    raw[(y * width) + x] = 0;
                }
                else
                {
                    raw[(y * width) + x] = unchecked((byte)(sbyte)value);
                }
            }
        }

        ReplaceEntryData(entry, width, height, raw);

        SoftenLight(entry);
        SoftenLight(entry);
    }

    public void ApplyWindowPreset(LightEntry entry, int width, int height, int strength)
    {
        width = Math.Clamp(width, 8, 600);
        height = Math.Clamp(height, 8, 600);
        strength = Math.Clamp(strength, 1, 31);

        byte[] raw = new byte[width * height];

        double centerX = (width - 1) / 2.0;
        double centerY = (height - 1) / 2.0;

        double halfW = width * 0.34;
        double halfH = height * 0.34;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double nx = Math.Abs(x - centerX) / halfW;
                double ny = Math.Abs(y - centerY) / halfH;

                if (nx > 1.0 || ny > 1.0)
                {
                    continue;
                }

                double edge = Math.Max(nx, ny);
                double falloff = 1.0 - edge;

                bool verticalMullion = Math.Abs(x - centerX) < Math.Max(1, width * 0.025);
                bool horizontalMullion = Math.Abs(y - centerY) < Math.Max(1, height * 0.025);

                if (verticalMullion || horizontalMullion)
                {
                    continue;
                }

                int value = -(int)Math.Round(strength * Math.Max(0.25, falloff));
                raw[(y * width) + x] = unchecked((byte)(sbyte)value);
            }
        }

        ReplaceEntryData(entry, width, height, raw);
    }

    public void ApplyDoorSlitPreset(LightEntry entry, int width, int height, int strength)
    {
        width = Math.Clamp(width, 8, 600);
        height = Math.Clamp(height, 8, 600);
        strength = Math.Clamp(strength, 1, 31);

        byte[] raw = new byte[width * height];

        double centerX = (width - 1) / 2.0;
        double centerY = (height - 1) / 2.0;

        double slitHalfWidth = Math.Max(2.0, width * 0.045);
        double slitHalfHeight = height * 0.42;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double dx = Math.Abs(x - centerX);
                double dy = Math.Abs(y - centerY);

                if (dx > slitHalfWidth || dy > slitHalfHeight)
                {
                    continue;
                }

                double horizontalFalloff = 1.0 - (dx / slitHalfWidth);
                double verticalFalloff = 1.0 - (dy / slitHalfHeight);
                double t = Math.Min(horizontalFalloff, verticalFalloff);

                int value = -(int)Math.Round(strength * Math.Max(0.20, t));
                raw[(y * width) + x] = unchecked((byte)(sbyte)value);
            }
        }

        ReplaceEntryData(entry, width, height, raw);
    }

    private void ReplaceEntryData(LightEntry entry, int width, int height, byte[] raw)
    {
        entry.Width = width;
        entry.Height = height;
        entry.Length = raw.Length;
        entry.RawData = raw;
        entry.IsRemoved = false;
        entry.Preview = BuildLightBitmap(raw, width, height);
    }

    public void PaintLightAt(LightEntry entry, int centerX, int centerY, int brushSize, int strength, bool erase)
    {
        if (entry == null || entry.RawData.Length == 0 || entry.Width <= 0 || entry.Height <= 0)
        {
            return;
        }

        int width = entry.Width;
        int height = entry.Height;

        if (entry.RawData.Length < width * height)
        {
            return;
        }

        brushSize = Math.Clamp(brushSize, 1, 100);
        strength = Math.Clamp(strength, 1, 31);

        int radius = Math.Max(1, brushSize / 2);
        int radiusSquared = radius * radius;

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

                int distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                int rawIndex = (y * width) + x;

                if (erase)
                {
                    entry.RawData[rawIndex] = 0;
                    continue;
                }

                double distance = Math.Sqrt(distanceSquared);
                double falloff = 1.0 - (distance / radius);

                falloff = falloff * falloff * (3.0 - (2.0 * falloff));

                int paintValue = (int)Math.Round(strength * falloff);

                if (paintValue < 1)
                {
                    paintValue = 1;
                }

                sbyte current = unchecked((sbyte)entry.RawData[rawIndex]);
                sbyte next = (sbyte)Math.Max(current, paintValue);

                entry.RawData[rawIndex] = unchecked((byte)next);
            }
        }

        entry.Length = entry.RawData.Length;
        entry.IsRemoved = false;
        entry.Preview = BuildLightBitmap(entry.RawData, width, height);
    }

    public WriteableBitmap? BuildPreview(LightEntry entry)
    {
        if (entry == null || entry.RawData.Length == 0 || entry.Width <= 0 || entry.Height <= 0)
        {
            return null;
        }

        return BuildLightBitmap(entry.RawData, entry.Width, entry.Height);
    }

    public void SoftenLight(LightEntry entry)
    {
        if (entry == null || entry.RawData.Length == 0 || entry.Width <= 0 || entry.Height <= 0)
        {
            return;
        }

        int width = entry.Width;
        int height = entry.Height;

        if (entry.RawData.Length < width * height)
        {
            return;
        }

        byte[] source = entry.RawData.ToArray();
        byte[] output = new byte[source.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int total = 0;
                int count = 0;
                bool hasPaint = false;

                for (int oy = -1; oy <= 1; oy++)
                {
                    int sy = y + oy;
                    if (sy < 0 || sy >= height)
                    {
                        continue;
                    }

                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int sx = x + ox;
                        if (sx < 0 || sx >= width)
                        {
                            continue;
                        }

                        sbyte value = unchecked((sbyte)source[(sy * width) + sx]);

                        if (value != 0)
                        {
                            hasPaint = true;
                        }

                        total += value;
                        count++;
                    }
                }

                int index = (y * width) + x;

                if (!hasPaint || count == 0)
                {
                    output[index] = 0;
                    continue;
                }

                int average = total / count;

                if (average > 31)
                {
                    average = 31;
                }
                else if (average < -31)
                {
                    average = -31;
                }

                // Avoid ultra-faint noise unless surrounded by actual pixels.
                if (average == 0)
                {
                    output[index] = 0;
                }
                else
                {
                    output[index] = unchecked((byte)(sbyte)average);
                }
            }
        }

        entry.RawData = output;
        entry.Length = output.Length;
        entry.IsRemoved = false;
        entry.Preview = BuildLightBitmap(entry.RawData, width, height);
    }

    public void FlipLightHorizontal(LightEntry entry)
    {
        if (entry == null || entry.RawData.Length == 0 || entry.Width <= 0 || entry.Height <= 0)
        {
            return;
        }

        int width = entry.Width;
        int height = entry.Height;

        byte[] source = entry.RawData.ToArray();
        byte[] output = new byte[source.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int src = (y * width) + x;
                int dst = (y * width) + (width - 1 - x);
                output[dst] = source[src];
            }
        }

        entry.RawData = output;
        entry.Length = output.Length;
        entry.IsRemoved = false;
        entry.Preview = BuildLightBitmap(output, width, height);
    }

    public void FlipLightVertical(LightEntry entry)
    {
        if (entry == null || entry.RawData.Length == 0 || entry.Width <= 0 || entry.Height <= 0)
        {
            return;
        }

        int width = entry.Width;
        int height = entry.Height;

        byte[] source = entry.RawData.ToArray();
        byte[] output = new byte[source.Length];

        for (int y = 0; y < height; y++)
        {
            int dstY = height - 1 - y;

            for (int x = 0; x < width; x++)
            {
                int src = (y * width) + x;
                int dst = (dstY * width) + x;
                output[dst] = source[src];
            }
        }

        entry.RawData = output;
        entry.Length = output.Length;
        entry.IsRemoved = false;
        entry.Preview = BuildLightBitmap(output, width, height);
    }

    public void SharpenLight(LightEntry entry)
    {
        if (entry == null || entry.RawData.Length == 0 || entry.Width <= 0 || entry.Height <= 0)
        {
            return;
        }

        int width = entry.Width;
        int height = entry.Height;

        if (entry.RawData.Length < width * height)
        {
            return;
        }

        byte[] source = entry.RawData.ToArray();
        byte[] output = new byte[source.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                sbyte center = unchecked((sbyte)source[index]);

                if (center == 0)
                {
                    output[index] = 0;
                    continue;
                }

                int total = 0;
                int count = 0;

                for (int oy = -1; oy <= 1; oy++)
                {
                    int sy = y + oy;
                    if (sy < 0 || sy >= height)
                    {
                        continue;
                    }

                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int sx = x + ox;
                        if (sx < 0 || sx >= width)
                        {
                            continue;
                        }

                        if (ox == 0 && oy == 0)
                        {
                            continue;
                        }

                        total += unchecked((sbyte)source[(sy * width) + sx]);
                        count++;
                    }
                }

                int average = count > 0 ? total / count : 0;

                int sharpened = center + ((center - average) / 2);

                if (sharpened > 31)
                {
                    sharpened = 31;
                }
                else if (sharpened < -31)
                {
                    sharpened = -31;
                }

                output[index] = unchecked((byte)(sbyte)sharpened);
            }
        }

        entry.RawData = output;
        entry.Length = output.Length;
        entry.IsRemoved = false;
        entry.Preview = BuildLightBitmap(output, width, height);
    }
}