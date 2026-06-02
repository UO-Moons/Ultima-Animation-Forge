using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class UoMapDataService
{
    private readonly Dictionary<string, List<StaticTileEntry>> staticBlockCache =
    new(StringComparer.OrdinalIgnoreCase);

    private sealed class StaticTileEntry
    {
        public ushort Graphic { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }
        public sbyte Z { get; set; }
        public short Hue { get; set; }
    }

    private const int BlockSize = 196;
    private const int TileCountPerBlock = 64;

    private string cachedUopPath = string.Empty;
    private UopFileReader? cachedUopReader;

    private readonly Dictionary<string, byte[]> uopChunkCache =
        new(StringComparer.OrdinalIgnoreCase);

    public UoMapRenderResult RenderMapArea(
        string uoFolderPath,
        UoMapOption map,
        int startX,
        int startY,
        int worldWidth,
        int worldHeight,
        int outputWidth,
        int outputHeight,
        bool useAltitudeShading,
        bool showStatics)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uoFolderPath) || !Directory.Exists(uoFolderPath))
            {
                return Fail("UO folder path is invalid.");
            }

            string mapMulPath = Path.Combine(uoFolderPath, $"map{map.FileIndex}.mul");
            string mapUopPath = Path.Combine(uoFolderPath, $"map{map.FileIndex}LegacyMUL.uop");
            string radarPath = Path.Combine(uoFolderPath, "radarcol.mul");

            bool hasUop = File.Exists(mapUopPath);
            bool hasMul = File.Exists(mapMulPath);

            if (!hasUop && !hasMul)
            {
                return Fail("Missing " + Path.GetFileName(mapUopPath) + " or " + Path.GetFileName(mapMulPath));
            }

            ushort[] radarColors = LoadRadarColors(radarPath);

            int safeWorldWidth = Math.Clamp(worldWidth, 64, map.Width);
            int safeWorldHeight = Math.Clamp(worldHeight, 64, map.Height);

            int safeOutputWidth = Math.Clamp(outputWidth, 64, 2048);
            int safeOutputHeight = Math.Clamp(outputHeight, 64, 2048);

            startX = Math.Clamp(startX, 0, Math.Max(0, map.Width - safeWorldWidth));
            startY = Math.Clamp(startY, 0, Math.Max(0, map.Height - safeWorldHeight));

            byte[] pixels = new byte[safeOutputWidth * safeOutputHeight * 4];

            int blockWidth = map.Width / 8;
            int blockHeight = map.Height / 8;

            int firstBlockX = startX / 8;
            int firstBlockY = startY / 8;
            int lastBlockX = (startX + safeWorldWidth) / 8;
            int lastBlockY = (startY + safeWorldHeight) / 8;

            UopFileReader? uopReader = hasUop
                ? GetCachedUopReader(mapUopPath)
                : null;

            using FileStream? mulStream = uopReader == null && hasMul
                ? File.OpenRead(mapMulPath)
                : null;

            using BinaryReader? mulReader = mulStream != null
                ? new BinaryReader(mulStream)
                : null;

            for (int blockY = firstBlockY; blockY <= lastBlockY; blockY++)
            {
                for (int blockX = firstBlockX; blockX <= lastBlockX; blockX++)
                {
                    if (blockX < 0 || blockY < 0 || blockX >= blockWidth || blockY >= blockHeight)
                    {
                        continue;
                    }

                    byte[]? blockData = null;

                    if (uopReader != null)
                    {
                        int mulBlockIndex = (blockX * blockHeight) + blockY;

                        const int BlocksPerUopChunk = 4096;

                        int chunkIndex = mulBlockIndex / BlocksPerUopChunk;
                        int blockIndexInsideChunk = mulBlockIndex % BlocksPerUopChunk;

                        string virtualPath = $"build/map{map.FileIndex}legacymul/{chunkIndex:D8}.dat";

                        byte[]? chunkData = GetCachedUopChunk(uopReader, virtualPath);

                        if (chunkData != null)
                        {
                            int offsetInChunk = blockIndexInsideChunk * BlockSize;

                            if (offsetInChunk >= 0 && offsetInChunk + BlockSize <= chunkData.Length)
                            {
                                blockData = new byte[BlockSize];
                                Buffer.BlockCopy(chunkData, offsetInChunk, blockData, 0, BlockSize);
                            }
                        }
                    }

                    if ((blockData == null || blockData.Length < BlockSize) &&
                        mulReader != null &&
                        mulStream != null)
                    {
                        long blockOffset = ((blockX * blockHeight) + blockY) * BlockSize;

                        if (blockOffset >= 0 && blockOffset + BlockSize <= mulStream.Length)
                        {
                            mulStream.Seek(blockOffset, SeekOrigin.Begin);
                            blockData = mulReader.ReadBytes(BlockSize);
                        }
                    }

                    if (blockData == null || blockData.Length < BlockSize)
                    {
                        continue;
                    }

                    using MemoryStream blockStream = new(blockData);
                    using BinaryReader blockReader = new(blockStream);

                    blockReader.ReadUInt32(); // header

                    for (int tile = 0; tile < TileCountPerBlock; tile++)
                    {
                        ushort tileId = blockReader.ReadUInt16();
                        sbyte z = blockReader.ReadSByte();

                        int localX = tile % 8;
                        int localY = tile / 8;

                        int worldX = (blockX * 8) + localX;
                        int worldY = (blockY * 8) + localY;

                        int relativeX = worldX - startX;
                        int relativeY = worldY - startY;

                        if (relativeX < 0 || relativeY < 0 || relativeX >= safeWorldWidth || relativeY >= safeWorldHeight)
                        {
                            continue;
                        }

                        int drawX = (relativeX * safeOutputWidth) / safeWorldWidth;
                        int drawY = (relativeY * safeOutputHeight) / safeWorldHeight;

                        if (drawX < 0 || drawY < 0 || drawX >= safeOutputWidth || drawY >= safeOutputHeight)
                        {
                            continue;
                        }

                        ushort color1555 = GetRadarColor(radarColors, tileId);

                        if (useAltitudeShading)
                        {
                            ApplyAltitudeShade(ref color1555, z);
                        }

                        Write1555ToBgra(pixels, safeOutputWidth, drawX, drawY, color1555);
                    }

                    if (showStatics)
                    {
                        List<StaticTileEntry> staticTiles = ReadStaticBlock(
                            uoFolderPath,
                            map,
                            blockX,
                            blockY,
                            blockHeight);

                        foreach (StaticTileEntry staticTile in staticTiles)
                        {
                            int worldX = (blockX * 8) + staticTile.X;
                            int worldY = (blockY * 8) + staticTile.Y;

                            int relativeX = worldX - startX;
                            int relativeY = worldY - startY;

                            if (relativeX < 0 || relativeY < 0 || relativeX >= safeWorldWidth || relativeY >= safeWorldHeight)
                            {
                                continue;
                            }

                            int drawX = (relativeX * safeOutputWidth) / safeWorldWidth;
                            int drawY = (relativeY * safeOutputHeight) / safeWorldHeight;

                            if (drawX < 0 || drawY < 0 || drawX >= safeOutputWidth || drawY >= safeOutputHeight)
                            {
                                continue;
                            }

                            ushort color1555 = GetRadarColor(radarColors, 0x4000 + staticTile.Graphic);

                            if (useAltitudeShading)
                            {
                                ApplyAltitudeShade(ref color1555, staticTile.Z);
                            }

                            Write1555ToBgra(pixels, safeOutputWidth, drawX, drawY, color1555);
                        }
                    }
                }
            }

            WriteableBitmap bitmap = new(
                new PixelSize(safeOutputWidth, safeOutputHeight),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using ILockedFramebuffer framebuffer = bitmap.Lock();
            Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);

            string sourceMode = uopReader != null ? "UOP" : "MUL";

            return new UoMapRenderResult
            {
                Success = true,
                Bitmap = bitmap,
                Message = $"{map.Name}: X {startX}, Y {startY} | Source: {sourceMode}"
            };
        }
        catch (Exception ex)
        {
            return Fail("Failed rendering map: " + ex.Message);
        }
    }

    private static ushort[] LoadRadarColors(string radarPath)
    {
        ushort[] colors = new ushort[0x10000];

        if (!File.Exists(radarPath))
        {
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = 0x4210;
            }

            return colors;
        }

        using FileStream stream = File.OpenRead(radarPath);
        using BinaryReader reader = new(stream);

        int index = 0;

        while (stream.Position + 2 <= stream.Length && index < colors.Length)
        {
            colors[index] = reader.ReadUInt16();
            index++;
        }

        return colors;
    }

    private static ushort GetRadarColor(ushort[] radarColors, int tileId)
    {
        if (tileId >= 0 && tileId < radarColors.Length)
        {
            return radarColors[tileId];
        }

        return 0x4210;
    }

    private static void ApplyAltitudeShade(ref ushort color, sbyte z)
    {
        int shade = Math.Clamp((int)z, -30, 30);
        int adjustment = shade / 8;

        int r = (color >> 10) & 0x1F;
        int g = (color >> 5) & 0x1F;
        int b = color & 0x1F;

        r = Math.Clamp(r + adjustment, 0, 31);
        g = Math.Clamp(g + adjustment, 0, 31);
        b = Math.Clamp(b + adjustment, 0, 31);

        color = (ushort)((r << 10) | (g << 5) | b);
    }

    private static void Write1555ToBgra(byte[] pixels, int width, int x, int y, ushort color)
    {
        int offset = ((y * width) + x) * 4;

        byte r = (byte)(((color >> 10) & 0x1F) << 3);
        byte g = (byte)(((color >> 5) & 0x1F) << 3);
        byte b = (byte)((color & 0x1F) << 3);

        pixels[offset + 0] = b;
        pixels[offset + 1] = g;
        pixels[offset + 2] = r;
        pixels[offset + 3] = 255;
    }

    private static UoMapRenderResult Fail(string message)
    {
        return new UoMapRenderResult
        {
            Success = false,
            Message = message
        };
    }

    private UopFileReader? GetCachedUopReader(string uopPath)
    {
        if (string.IsNullOrWhiteSpace(uopPath) || !File.Exists(uopPath))
        {
            return null;
        }

        if (cachedUopReader != null &&
            string.Equals(cachedUopPath, uopPath, StringComparison.OrdinalIgnoreCase))
        {
            return cachedUopReader;
        }

        cachedUopPath = uopPath;
        cachedUopReader = new UopFileReader(uopPath);
        uopChunkCache.Clear();

        if (!cachedUopReader.Load())
        {
            cachedUopReader = null;
            cachedUopPath = string.Empty;
            return null;
        }

        return cachedUopReader;
    }

    private byte[]? GetCachedUopChunk(
        UopFileReader uopReader,
        string virtualPath)
    {
        if (uopChunkCache.TryGetValue(virtualPath, out byte[]? cachedChunk))
        {
            return cachedChunk;
        }

        ulong hash = UopFileReader.CreateHash(virtualPath);
        UopDataHeader? entry = uopReader.GetEntryByHash(hash);

        if (!entry.HasValue)
        {
            return null;
        }

        byte[]? chunkData = uopReader.ReadData(entry.Value);

        if (chunkData == null || chunkData.Length == 0)
        {
            return null;
        }

        uopChunkCache[virtualPath] = chunkData;
        return chunkData;
    }

    private List<StaticTileEntry> ReadStaticBlock(
    string uoFolderPath,
    UoMapOption map,
    int blockX,
    int blockY,
    int blockHeight)
    {
        string cacheKey = uoFolderPath + "|" + map.FileIndex + "|" + blockX + "|" + blockY;

        if (staticBlockCache.TryGetValue(cacheKey, out List<StaticTileEntry>? cached))
        {
            return cached;
        }

        List<StaticTileEntry> result = new();

        string staidxPath = Path.Combine(uoFolderPath, $"staidx{map.FileIndex}.mul");
        string staticsPath = Path.Combine(uoFolderPath, $"statics{map.FileIndex}.mul");

        if (!File.Exists(staidxPath) || !File.Exists(staticsPath))
        {
            staticBlockCache[cacheKey] = result;
            return result;
        }

        int staticIndex = (blockX * blockHeight) + blockY;
        long indexOffset = staticIndex * 12L;

        using FileStream idxStream = File.OpenRead(staidxPath);
        using BinaryReader idxReader = new(idxStream);

        if (indexOffset < 0 || indexOffset + 12 > idxStream.Length)
        {
            staticBlockCache[cacheKey] = result;
            return result;
        }

        idxStream.Seek(indexOffset, SeekOrigin.Begin);

        int lookup = idxReader.ReadInt32();
        int length = idxReader.ReadInt32();
        _ = idxReader.ReadInt32();

        if (lookup < 0 || length <= 0 || length % 7 != 0)
        {
            staticBlockCache[cacheKey] = result;
            return result;
        }

        using FileStream staticsStream = File.OpenRead(staticsPath);
        using BinaryReader staticsReader = new(staticsStream);

        if (lookup < 0 || lookup + length > staticsStream.Length)
        {
            staticBlockCache[cacheKey] = result;
            return result;
        }

        staticsStream.Seek(lookup, SeekOrigin.Begin);

        int count = length / 7;

        for (int i = 0; i < count; i++)
        {
            result.Add(new StaticTileEntry
            {
                Graphic = staticsReader.ReadUInt16(),
                X = staticsReader.ReadByte(),
                Y = staticsReader.ReadByte(),
                Z = staticsReader.ReadSByte(),
                Hue = staticsReader.ReadInt16()
            });
        }

        result.Sort((a, b) =>
        {
            int zCompare = a.Z.CompareTo(b.Z);
            if (zCompare != 0)
            {
                return zCompare;
            }

            return a.Graphic.CompareTo(b.Graphic);
        });

        staticBlockCache[cacheKey] = result;
        return result;
    }
}