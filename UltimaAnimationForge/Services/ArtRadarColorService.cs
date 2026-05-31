using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Runtime.InteropServices;

namespace UltimaAnimationForge.Services;

public sealed class ArtRadarColorService
{
    public Color GetAverageVisibleColor(WriteableBitmap? bitmap)
    {
        if (bitmap == null)
        {
            return Colors.Transparent;
        }

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int rowBytes = framebuffer.RowBytes;

        byte[] pixels = new byte[rowBytes * height];
        Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);

        long totalR = 0;
        long totalG = 0;
        long totalB = 0;
        long count = 0;

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * rowBytes;

            for (int x = 0; x < width; x++)
            {
                int offset = rowOffset + (x * 4);

                byte b = pixels[offset + 0];
                byte g = pixels[offset + 1];
                byte r = pixels[offset + 2];
                byte a = pixels[offset + 3];

                if (a == 0)
                {
                    continue;
                }

                totalR += r;
                totalG += g;
                totalB += b;
                count++;
            }
        }

        if (count == 0)
        {
            return Colors.Transparent;
        }

        return Color.FromRgb(
            (byte)(totalR / count),
            (byte)(totalG / count),
            (byte)(totalB / count));
    }

    public ushort ConvertColorToUoColor(Color color)
    {
        int r = color.R >> 3;
        int g = color.G >> 3;
        int b = color.B >> 3;

        return (ushort)((r << 10) | (g << 5) | b);
    }

    public string ToHexText(Color color)
    {
        if (color.A == 0)
        {
            return "Transparent";
        }

        return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
    }

    public string ToUoColorText(Color color)
    {
        if (color.A == 0)
        {
            return "-";
        }

        return "0x" + ConvertColorToUoColor(color).ToString("X4");
    }
}