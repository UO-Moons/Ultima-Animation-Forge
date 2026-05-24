using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace UltimaAnimationForge.Services;

public static class GumpBuilderRenderService
{
    public static WriteableBitmap BuildBackgroundBitmap(
        IReadOnlyList<WriteableBitmap> parts,
        int targetWidth,
        int targetHeight)
    {
        if (parts == null || parts.Count < 9)
        {
            throw new ArgumentException("Background rendering requires 9 gump parts.", nameof(parts));
        }

        targetWidth = Math.Max(1, targetWidth);
        targetHeight = Math.Max(1, targetHeight);

        WriteableBitmap output = new WriteableBitmap(
            new PixelSize(targetWidth, targetHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer outputBuffer = output.Lock();

        int targetStride = outputBuffer.RowBytes;
        byte[] targetPixels = new byte[targetStride * targetHeight];

        int leftWidth = parts[0].PixelSize.Width;
        int topHeight = parts[0].PixelSize.Height;
        int rightWidth = parts[2].PixelSize.Width;
        int bottomHeight = parts[6].PixelSize.Height;

        DrawPart(targetPixels, targetStride, targetWidth, targetHeight, parts[0], 0, 0);
        DrawPart(targetPixels, targetStride, targetWidth, targetHeight, parts[2], targetWidth - rightWidth, 0);
        DrawPart(targetPixels, targetStride, targetWidth, targetHeight, parts[6], 0, targetHeight - bottomHeight);
        DrawPart(targetPixels, targetStride, targetWidth, targetHeight, parts[8], targetWidth - parts[8].PixelSize.Width, targetHeight - parts[8].PixelSize.Height);

        TilePart(targetPixels, targetStride, targetWidth, targetHeight, parts[1],
            leftWidth, 0,
            targetWidth - leftWidth - rightWidth,
            parts[1].PixelSize.Height);

        TilePart(targetPixels, targetStride, targetWidth, targetHeight, parts[7],
            parts[6].PixelSize.Width, targetHeight - parts[7].PixelSize.Height,
            targetWidth - parts[6].PixelSize.Width - parts[8].PixelSize.Width,
            parts[7].PixelSize.Height);

        TilePart(targetPixels, targetStride, targetWidth, targetHeight, parts[3],
            0, topHeight,
            parts[3].PixelSize.Width,
            targetHeight - topHeight - bottomHeight);

        TilePart(targetPixels, targetStride, targetWidth, targetHeight, parts[5],
            targetWidth - parts[5].PixelSize.Width, parts[2].PixelSize.Height,
            parts[5].PixelSize.Width,
            targetHeight - parts[2].PixelSize.Height - parts[8].PixelSize.Height);

        TilePart(targetPixels, targetStride, targetWidth, targetHeight, parts[4],
            parts[3].PixelSize.Width, parts[1].PixelSize.Height,
            targetWidth - parts[3].PixelSize.Width - parts[5].PixelSize.Width,
            targetHeight - parts[1].PixelSize.Height - parts[7].PixelSize.Height);

        Marshal.Copy(targetPixels, 0, outputBuffer.Address, targetPixels.Length);

        return output;
    }

    private static void TilePart(
        byte[] targetPixels,
        int targetStride,
        int targetWidth,
        int targetHeight,
        WriteableBitmap source,
        int x,
        int y,
        int width,
        int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        int partWidth = source.PixelSize.Width;
        int partHeight = source.PixelSize.Height;

        for (int drawY = y; drawY < y + height; drawY += partHeight)
        {
            for (int drawX = x; drawX < x + width; drawX += partWidth)
            {
                DrawPartClipped(
                    targetPixels,
                    targetStride,
                    targetWidth,
                    targetHeight,
                    source,
                    drawX,
                    drawY,
                    x,
                    y,
                    width,
                    height);
            }
        }
    }

    private static void DrawPart(
        byte[] targetPixels,
        int targetStride,
        int targetWidth,
        int targetHeight,
        WriteableBitmap source,
        int x,
        int y)
    {
        DrawPartClipped(
            targetPixels,
            targetStride,
            targetWidth,
            targetHeight,
            source,
            x,
            y,
            0,
            0,
            targetWidth,
            targetHeight);
    }

    private static void DrawPartClipped(
        byte[] targetPixels,
        int targetStride,
        int targetWidth,
        int targetHeight,
        WriteableBitmap source,
        int drawX,
        int drawY,
        int clipX,
        int clipY,
        int clipWidth,
        int clipHeight)
    {
        byte[] sourcePixels = ReadPixels(source, out int sourceStride);

        int sourceWidth = source.PixelSize.Width;
        int sourceHeight = source.PixelSize.Height;

        int clipRight = clipX + clipWidth;
        int clipBottom = clipY + clipHeight;

        for (int sy = 0; sy < sourceHeight; sy++)
        {
            int dy = drawY + sy;

            if (dy < 0 || dy >= targetHeight || dy < clipY || dy >= clipBottom)
            {
                continue;
            }

            for (int sx = 0; sx < sourceWidth; sx++)
            {
                int dx = drawX + sx;

                if (dx < 0 || dx >= targetWidth || dx < clipX || dx >= clipRight)
                {
                    continue;
                }

                int sourceOffset = (sy * sourceStride) + (sx * 4);
                int targetOffset = (dy * targetStride) + (dx * 4);

                byte alpha = sourcePixels[sourceOffset + 3];

                if (alpha == 0)
                {
                    continue;
                }

                targetPixels[targetOffset + 0] = sourcePixels[sourceOffset + 0];
                targetPixels[targetOffset + 1] = sourcePixels[sourceOffset + 1];
                targetPixels[targetOffset + 2] = sourcePixels[sourceOffset + 2];
                targetPixels[targetOffset + 3] = alpha;
            }
        }
    }

    private static byte[] ReadPixels(WriteableBitmap bitmap, out int stride)
    {
        using ILockedFramebuffer buffer = bitmap.Lock();

        stride = buffer.RowBytes;
        byte[] pixels = new byte[stride * buffer.Size.Height];

        Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);

        return pixels;
    }

    public static WriteableBitmap BuildTiledGumpBitmap(
    WriteableBitmap tile,
    int targetWidth,
    int targetHeight)
    {
        if (tile == null)
        {
            throw new ArgumentNullException(nameof(tile));
        }

        targetWidth = Math.Max(1, targetWidth);
        targetHeight = Math.Max(1, targetHeight);

        WriteableBitmap output = new WriteableBitmap(
            new PixelSize(targetWidth, targetHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer outputBuffer = output.Lock();

        int targetStride = outputBuffer.RowBytes;
        byte[] targetPixels = new byte[targetStride * targetHeight];

        TilePart(
            targetPixels,
            targetStride,
            targetWidth,
            targetHeight,
            tile,
            0,
            0,
            targetWidth,
            targetHeight);

        Marshal.Copy(targetPixels, 0, outputBuffer.Address, targetPixels.Length);

        return output;
    }

    public static WriteableBitmap BuildPicInPicBitmap(
    WriteableBitmap source,
    int spriteX,
    int spriteY,
    int width,
    int height)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        width = Math.Max(1, width);
        height = Math.Max(1, height);

        WriteableBitmap output = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        byte[] sourcePixels = ReadPixels(source, out int sourceStride);

        using ILockedFramebuffer outputBuffer = output.Lock();

        int targetStride = outputBuffer.RowBytes;
        byte[] targetPixels = new byte[targetStride * height];

        int sourceWidth = source.PixelSize.Width;
        int sourceHeight = source.PixelSize.Height;

        for (int y = 0; y < height; y++)
        {
            int sy = spriteY + y;

            if (sy < 0 || sy >= sourceHeight)
            {
                continue;
            }

            for (int x = 0; x < width; x++)
            {
                int sx = spriteX + x;

                if (sx < 0 || sx >= sourceWidth)
                {
                    continue;
                }

                int sourceOffset = (sy * sourceStride) + (sx * 4);
                int targetOffset = (y * targetStride) + (x * 4);

                targetPixels[targetOffset + 0] = sourcePixels[sourceOffset + 0];
                targetPixels[targetOffset + 1] = sourcePixels[sourceOffset + 1];
                targetPixels[targetOffset + 2] = sourcePixels[sourceOffset + 2];
                targetPixels[targetOffset + 3] = sourcePixels[sourceOffset + 3];
            }
        }

        Marshal.Copy(targetPixels, 0, outputBuffer.Address, targetPixels.Length);

        return output;
    }
}