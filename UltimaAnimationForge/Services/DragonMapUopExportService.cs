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

public sealed class DragonMapUopExportService
{
    private const int BlockSize = 8;
    private const int MapBlockBytes = 196;
    private const int BlocksPerUopChunk = 4096;
    private const int UopChunkBytes = MapBlockBytes * BlocksPerUopChunk;

    public sealed class ExportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public ExportResult ExportMapMul(
        WriteableBitmap bitmap,
        IReadOnlyCollection<DragonTerrainColor> terrainColors,
        string outputPath)
    {
        byte[] mapBytes = BuildLegacyMapMulBytes(bitmap, terrainColors, out string? error);

        if (error != null)
        {
            return Fail(error);
        }

        File.WriteAllBytes(outputPath, mapBytes);

        return new ExportResult
        {
            Success = true,
            Message = $"Exported {Path.GetFileName(outputPath)} with {mapBytes.Length:N0} bytes."
        };
    }

    public ExportResult ExportMapLegacyUop(
        WriteableBitmap bitmap,
        IReadOnlyCollection<DragonTerrainColor> terrainColors,
        string outputPath,
        int mapIndex = 0)
    {
        byte[] mapBytes = BuildLegacyMapMulBytes(bitmap, terrainColors, out string? error);

        if (error != null)
        {
            return Fail(error);
        }

        WriteMapLegacyUopUoFiddlerStyle(outputPath, mapBytes, mapIndex);

        return new ExportResult
        {
            Success = true,
            Message = $"Exported {Path.GetFileName(outputPath)} from generated map bytes."
        };
    }

    private static byte[] BuildLegacyMapMulBytes(
        WriteableBitmap bitmap,
        IReadOnlyCollection<DragonTerrainColor> terrainColors,
        out string? error)
    {
        error = null;

        if (bitmap == null)
        {
            error = "No map loaded.";
            return Array.Empty<byte>();
        }

        if (bitmap.PixelSize.Width != 6144 || bitmap.PixelSize.Height != 4096)
        {
            error = "Map must be 6144 x 4096.";
            return Array.Empty<byte>();
        }

        Dictionary<uint, DragonTerrainColor> terrainByColor = terrainColors
            .GroupBy(x => PackColor(x.Color))
            .ToDictionary(x => x.Key, x => x.First());

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int rowBytes = framebuffer.RowBytes;

        byte[] pixels = new byte[rowBytes * height];
        Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);

        int blockColumns = width / BlockSize;
        int blockRows = height / BlockSize;

        using MemoryStream stream = new(blockColumns * blockRows * MapBlockBytes);
        using BinaryWriter writer = new(stream);

        Random random = new(12345);

        // IMPORTANT: UO map block order is X first, then Y.
        for (int blockX = 0; blockX < blockColumns; blockX++)
        {
            for (int blockY = 0; blockY < blockRows; blockY++)
            {
                WriteMapBlock(
                    writer,
                    pixels,
                    rowBytes,
                    blockX * BlockSize,
                    blockY * BlockSize,
                    terrainByColor,
                    random,
                    out error);

                if (error != null)
                {
                    return Array.Empty<byte>();
                }
            }
        }

        return stream.ToArray();
    }

    private static void WriteMapBlock(
        BinaryWriter writer,
        byte[] pixels,
        int rowBytes,
        int startX,
        int startY,
        Dictionary<uint, DragonTerrainColor> terrainByColor,
        Random random,
        out string? error)
    {
        error = null;

        writer.Write(0); // 4-byte map block header

        for (int y = 0; y < BlockSize; y++)
        {
            for (int x = 0; x < BlockSize; x++)
            {
                int pixelX = startX + x;
                int pixelY = startY + y;
                int offset = (pixelY * rowBytes) + (pixelX * 4);

                uint packed = PackColor(
                    pixels[offset + 2],
                    pixels[offset + 1],
                    pixels[offset + 0]);

                if (!terrainByColor.TryGetValue(packed, out DragonTerrainColor? terrain))
                {
                    error = $"Unknown Dragon color at X:{pixelX} Y:{pixelY}.";
                    return;
                }

                if (terrain.TileIds.Count == 0)
                {
                    error = $"Terrain {terrain.DisplayText} has no tile IDs.";
                    return;
                }

                ushort tileId = (ushort)terrain.TileIds[random.Next(terrain.TileIds.Count)];
                sbyte z = (sbyte)Math.Clamp(terrain.Z, sbyte.MinValue, sbyte.MaxValue);

                writer.Write(tileId);
                writer.Write(z);
            }
        }
    }

    private static List<UopFileData> BuildUopChunks(byte[] mapBytes, int mapIndex)
    {
        List<UopFileData> entries = new();

        int chunkCount = (mapBytes.Length + UopChunkBytes - 1) / UopChunkBytes;

        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            int offset = chunkIndex * UopChunkBytes;
            int length = Math.Min(UopChunkBytes, mapBytes.Length - offset);

            byte[] chunk = new byte[length];
            Buffer.BlockCopy(mapBytes, offset, chunk, 0, length);

            string virtualPath = $"build/map{mapIndex}legacymul/{chunkIndex:D8}.dat";

            entries.Add(new UopFileData
            {
                Hash = UopFileReader.CreateHash(virtualPath),
                Data = chunk,
                DecompressedSize = (uint)chunk.Length,
                IsCompressed = true,
                IsEmpty = false
            });
        }

        return entries;
    }

    private static uint PackColor(Color color)
    {
        return PackColor(color.R, color.G, color.B);
    }

    private static uint PackColor(byte r, byte g, byte b)
    {
        return ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private static ExportResult Fail(string message)
    {
        return new ExportResult
        {
            Success = false,
            Message = message
        };
    }

    public ExportResult ExportMapMulAndUop(
    WriteableBitmap bitmap,
    IReadOnlyCollection<DragonTerrainColor> terrainColors,
    string mulOutputPath,
    string uopOutputPath,
    int mapIndex = 0)
    {
        byte[] mapBytes = BuildLegacyMapMulBytes(bitmap, terrainColors, out string? error);

        if (error != null)
        {
            return Fail(error);
        }

        File.WriteAllBytes(mulOutputPath, mapBytes);

        List<UopFileData> entries = BuildUopChunks(mapBytes, mapIndex);
        UopFileWriter.WriteUopFile(uopOutputPath, entries, 1000);

        return new ExportResult
        {
            Success = true,
            Message =
                $"Exported {Path.GetFileName(mulOutputPath)} and " +
                $"{Path.GetFileName(uopOutputPath)} with {entries.Count} UOP chunks."
        };
    }


    private static void WriteMapLegacyUopUoFiddlerStyle(
    string outputPath,
    byte[] mapBytes,
    int mapIndex)
    {
        const long firstTable = 0x200;
        const int tableSize = 0x64;

        List<byte[]> chunks = new();

        for (int offset = 0; offset < mapBytes.Length; offset += UopChunkBytes)
        {
            int length = Math.Min(UopChunkBytes, mapBytes.Length - offset);
            byte[] chunk = new byte[length];
            Buffer.BlockCopy(mapBytes, offset, chunk, 0, length);
            chunks.Add(chunk);
        }

        using FileStream fs = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using BinaryWriter writer = new(fs);

        writer.Write(0x50594D);      // MYP
        writer.Write(5);             // version
        writer.Write(0xFD23EC43);    // timestamp
        writer.Write(firstTable);     // first table
        writer.Write(tableSize);      // table size
        writer.Write(chunks.Count);   // file count
        writer.Write(0);             // modified count
        writer.Write(0);
        writer.Write(0);

        while (fs.Position < firstTable)
        {
            writer.Write((byte)0);
        }

        int tableCount = (int)Math.Ceiling((double)chunks.Count / tableSize);

        for (int tableIndex = 0; tableIndex < tableCount; tableIndex++)
        {
            long tableStart = fs.Position;

            int start = tableIndex * tableSize;
            int count = Math.Min(tableSize, chunks.Count - start);

            writer.Write(count);
            writer.Write((long)0); // next table patched later

            long tableEntriesStart = fs.Position;
            writer.Write(new byte[34 * tableSize]);

            long dataStart = fs.Position;

            long[] offsets = new long[count];
            int[] sizes = new int[count];
            int[] decompressedSizes = new int[count];
            ulong[] hashes = new ulong[count];
            uint[] adlers = new uint[count];

            for (int i = 0; i < count; i++)
            {
                int chunkIndex = start + i;
                byte[] data = chunks[chunkIndex];

                offsets[i] = fs.Position;
                sizes[i] = data.Length;
                decompressedSizes[i] = data.Length;

                string virtualPath = $"build/map{mapIndex}legacymul/{chunkIndex:D8}.dat";
                hashes[i] = UopFileReader.CreateHash(virtualPath);
                adlers[i] = ComputeAdler32(data);

                writer.Write(data);
            }

            long nextTable = fs.Position;

            if (tableIndex < tableCount - 1)
            {
                fs.Seek(tableStart + 4, SeekOrigin.Begin);
                writer.Write(nextTable);
            }

            fs.Seek(tableEntriesStart, SeekOrigin.Begin);

            for (int i = 0; i < count; i++)
            {
                writer.Write(offsets[i]);
                writer.Write(0);                    // header length
                writer.Write(sizes[i]);             // compressed size
                writer.Write(decompressedSizes[i]); // decompressed size
                writer.Write(hashes[i]);
                writer.Write(adlers[i]);
                writer.Write((short)0);             // compression none
            }

            int emptyCount = tableSize - count;
            for (int i = 0; i < emptyCount; i++)
            {
                writer.Write(new byte[34]);
            }

            fs.Seek(nextTable, SeekOrigin.Begin);
        }
    }

    private static uint ComputeAdler32(byte[] data)
    {
        const uint mod = 65521;

        uint a = 1;
        uint b = 0;

        foreach (byte value in data)
        {
            a = (a + value) % mod;
            b = (b + a) % mod;
        }

        return (b << 16) | a;
    }
}