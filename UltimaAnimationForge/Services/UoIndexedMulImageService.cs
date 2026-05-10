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

public sealed class UoIndexedMulImageService
{
    private const int IdxEntrySize = 12;
    private const int StaticArtOffset = 0x4000;

    private sealed class IdxEntry
    {
        public int Lookup { get; set; }
        public int Length { get; set; }
        public int Extra { get; set; }

        public bool IsValid => Lookup >= 0 && Length > 0;
    }

    public List<EquipmentArtPickerEntry> LoadArtEntries(string uoFolderPath, int maxCount = int.MaxValue)
    {
        List<EquipmentArtPickerEntry> results = new();

        AddMulArtEntries(results, uoFolderPath, maxCount);

        if (results.Count < maxCount)
        {
            AddUopArtEntries(results, uoFolderPath, maxCount);
        }

        return results;
    }

    public List<EquipmentGumpPickerEntry> LoadGumpEntries(string uoFolderPath, int maxCount = int.MaxValue)
    {
        List<EquipmentGumpPickerEntry> results = new();

        AddMulGumpEntries(results, uoFolderPath, maxCount);

        if (results.Count < maxCount)
        {
            AddUopGumpEntries(results, uoFolderPath, maxCount);
        }

        return results;
    }

    private void AddMulArtEntries(List<EquipmentArtPickerEntry> results, string uoFolderPath, int maxCount)
    {
        string idxPath = Path.Combine(uoFolderPath, "artidx.mul");
        string mulPath = Path.Combine(uoFolderPath, "art.mul");

        if (!File.Exists(idxPath) || !File.Exists(mulPath))
        {
            return;
        }

        List<IdxEntry> idxEntries = ReadIdxEntries(idxPath);

        for (int artId = 0; artId < 0x10000 && results.Count < maxCount; artId++)
        {
            int idxIndex = artId + StaticArtOffset;

            if (idxIndex < 0 || idxIndex >= idxEntries.Count)
            {
                break;
            }

            IdxEntry entry = idxEntries[idxIndex];
            if (!entry.IsValid)
            {
                continue;
            }

            WriteableBitmap? bitmap = TryReadStaticArtBitmap(mulPath, entry.Lookup, entry.Length);
            if (bitmap == null)
            {
                continue;
            }

            results.Add(new EquipmentArtPickerEntry
            {
                ArtId = artId,
                DisplayText = "0x" + artId.ToString("X4") + "\n" + artId,
                Thumbnail = bitmap
            });
        }
    }

    private void AddMulGumpEntries(List<EquipmentGumpPickerEntry> results, string uoFolderPath, int maxCount)
    {
        string idxPath = Path.Combine(uoFolderPath, "gumpidx.mul");
        string mulPath = Path.Combine(uoFolderPath, "gumpart.mul");
        string bodyDefPath = Path.Combine(uoFolderPath, "body.def");

        if (!File.Exists(idxPath) || !File.Exists(mulPath))
        {
            return;
        }

        BodyDefService bodyDefService = new();
        HashSet<int> usedBodyDefIds = new(bodyDefService.Load(bodyDefPath).Keys);

        List<IdxEntry> idxEntries = ReadIdxEntries(idxPath);

        for (int gumpId = 0; gumpId < idxEntries.Count && results.Count < maxCount; gumpId++)
        {
            IdxEntry entry = idxEntries[gumpId];
            if (!entry.IsValid || entry.Extra <= 0)
            {
                continue;
            }

            int width = (entry.Extra >> 16) & 0xFFFF;
            int height = entry.Extra & 0xFFFF;

            if (width <= 0 || height <= 0 || width > 2048 || height > 2048)
            {
                continue;
            }

            WriteableBitmap? bitmap = TryReadGumpBitmap(mulPath, entry.Lookup, entry.Length, width, height);
            if (bitmap == null)
            {
                continue;
            }

            bool isUsed = usedBodyDefIds.Contains(gumpId);

            results.Add(new EquipmentGumpPickerEntry
            {
                GumpId = gumpId,
                DisplayText =
    "0x" + gumpId.ToString("X4") +
    "\n" + gumpId +
    (isUsed ? "\nUSED" : ""),
                Thumbnail = bitmap,
                IsUsed = isUsed
            });
        }
    }

    private void AddUopArtEntries(List<EquipmentArtPickerEntry> results, string uoFolderPath, int maxCount)
    {
        string uopPath = Path.Combine(uoFolderPath, "artLegacyMUL.uop");
        if (!File.Exists(uopPath))
        {
            return;
        }

        UopFileReader reader = new(uopPath);
        if (!reader.Load())
        {
            return;
        }

        HashSet<int> existingArtIds = new();
        foreach (EquipmentArtPickerEntry entry in results)
        {
            existingArtIds.Add(entry.ArtId);
        }

        // UOFiddler FileIndex count is 0x14000.
        // Static item art starts at 0x4000.
        // So valid item IDs are 0..0xFFFF.
        for (int artId = 0; artId <= 0xFFFF && results.Count < maxCount; artId++)
        {
            if (existingArtIds.Contains(artId))
            {
                continue;
            }

            int fileIndex = artId + StaticArtOffset;

            byte[]? data = ReadUopIndexedTga(reader, "artlegacymul", fileIndex);
            if (data == null || data.Length == 0)
            {
                continue;
            }

            WriteableBitmap? bitmap =
                TryReadStaticArtBitmapFromBytes(data) ??
                TryDecodeTgaBitmap(data);

            if (bitmap == null)
            {
                continue;
            }

            results.Add(new EquipmentArtPickerEntry
            {
                ArtId = artId,
                DisplayText = "0x" + artId.ToString("X4") + "\n" + artId,
                Thumbnail = bitmap
            });
        }
    }
    private void AddUopGumpEntries(List<EquipmentGumpPickerEntry> results, string uoFolderPath, int maxCount)
    {
        string uopPath = Path.Combine(uoFolderPath, "gumpartLegacyMUL.uop");
        string bodyDefPath = Path.Combine(uoFolderPath, "body.def");

        if (!File.Exists(uopPath))
        {
            return;
        }

        UopFileReader reader = new(uopPath);
        if (!reader.Load())
        {
            return;
        }

        BodyDefService bodyDefService = new();
        HashSet<int> usedBodyDefIds = new(bodyDefService.Load(bodyDefPath).Keys);

        HashSet<int> existingGumpIds = new();
        foreach (EquipmentGumpPickerEntry entry in results)
        {
            existingGumpIds.Add(entry.GumpId);
        }

        for (int gumpId = 0; gumpId <= 0xFFFF && results.Count < maxCount; gumpId++)
        {
            if (existingGumpIds.Contains(gumpId))
            {
                continue;
            }

            byte[]? data = ReadUopIndexedTga(reader, "gumpartlegacymul", gumpId);
            if (data == null || data.Length <= 8)
            {
                continue;
            }

            WriteableBitmap? bitmap =
                TryDecodeUopGumpPayload(data) ??
                TryDecodeTgaBitmap(data);

            if (bitmap == null)
            {
                continue;
            }

            bool isUsed = usedBodyDefIds.Contains(gumpId);

            results.Add(new EquipmentGumpPickerEntry
            {
                GumpId = gumpId,
                DisplayText =
    "0x" + gumpId.ToString("X4") +
    "\n" + gumpId +
    (isUsed ? "\nUSED" : ""),
                Thumbnail = bitmap,
                IsUsed = isUsed
            });
        }
    }
    private static byte[]? ReadUopIndexedTga(UopFileReader reader, string pattern, int index)
    {
        string virtualPath = "build/" + pattern.ToLowerInvariant() + "/" + index.ToString("D8") + ".tga";
        ulong hash = UopFileReader.CreateHash(virtualPath);

        UopDataHeader? header = reader.GetEntryByHash(hash);
        if (!header.HasValue)
        {
            return null;
        }

        return reader.ReadData(header.Value);
    }

    private static List<IdxEntry> ReadIdxEntries(string idxPath)
    {
        List<IdxEntry> entries = new();

        using FileStream stream = File.OpenRead(idxPath);
        using BinaryReader reader = new(stream);

        while (stream.Position + IdxEntrySize <= stream.Length)
        {
            entries.Add(new IdxEntry
            {
                Lookup = reader.ReadInt32(),
                Length = reader.ReadInt32(),
                Extra = reader.ReadInt32()
            });
        }

        return entries;
    }

    private static WriteableBitmap? TryReadStaticArtBitmap(string mulPath, int lookup, int length)
    {
        try
        {
            using FileStream stream = File.OpenRead(mulPath);
            using BinaryReader reader = new(stream);

            if (lookup < 0 || lookup + length > stream.Length)
            {
                return null;
            }

            stream.Seek(lookup, SeekOrigin.Begin);
            byte[] data = reader.ReadBytes(length);
            return TryReadStaticArtBitmapFromBytes(data);
        }
        catch
        {
            return null;
        }
    }

    private static WriteableBitmap? TryReadStaticArtBitmapFromBytes(byte[] data)
    {
        try
        {
            using MemoryStream stream = new(data);
            using BinaryReader reader = new(stream);

            if (data.Length < 8)
            {
                return null;
            }

            reader.ReadInt32();
            int width = reader.ReadUInt16();
            int height = reader.ReadUInt16();

            if (width <= 0 || height <= 0 || width > 1024 || height > 1024)
            {
                return null;
            }

            if (stream.Position + (height * 2L) > stream.Length)
            {
                return null;
            }

            ushort[] lookups = new ushort[height];

            for (int y = 0; y < height; y++)
            {
                lookups[y] = reader.ReadUInt16();
            }

            long pixelDataStart = stream.Position;
            byte[] pixels = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                stream.Seek(pixelDataStart + (lookups[y] * 2L), SeekOrigin.Begin);

                while (stream.Position + 4 <= stream.Length)
                {
                    ushort xOffset = reader.ReadUInt16();
                    ushort runLength = reader.ReadUInt16();

                    if (xOffset == 0 && runLength == 0)
                    {
                        break;
                    }

                    for (int i = 0; i < runLength; i++)
                    {
                        if (stream.Position + 2 > stream.Length)
                        {
                            break;
                        }

                        ushort color = reader.ReadUInt16();

                        int x = xOffset + i;
                        if (x < 0 || x >= width)
                        {
                            continue;
                        }

                        WriteUoColor1555(pixels, width, x, y, (ushort)(color ^ 0x8000));
                    }
                }
            }

            return BuildBitmap(width, height, pixels);
        }
        catch
        {
            return null;
        }
    }

    private static WriteableBitmap? TryReadGumpBitmap(string mulPath, int lookup, int length, int width, int height)
    {
        try
        {
            using FileStream stream = File.OpenRead(mulPath);

            if (lookup < 0 || lookup + length > stream.Length)
            {
                return null;
            }

            byte[] data = new byte[length];
            stream.Seek(lookup, SeekOrigin.Begin);
            stream.ReadExactly(data, 0, length);

            return TryDecodeGumpRle(data, width, height);
        }
        catch
        {
            return null;
        }
    }

    private static WriteableBitmap? TryDecodeUopGumpPayload(byte[] data)
    {
        try
        {
            if (data.Length <= 8)
            {
                return null;
            }

            int width = BitConverter.ToInt32(data, 0);
            int height = BitConverter.ToInt32(data, 4);

            if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            {
                return null;
            }

            byte[] rleData = new byte[data.Length - 8];
            Buffer.BlockCopy(data, 8, rleData, 0, rleData.Length);

            return TryDecodeGumpRle(rleData, width, height);
        }
        catch
        {
            return null;
        }
    }

    private static WriteableBitmap? TryDecodeGumpRle(byte[] data, int width, int height)
    {
        try
        {
            if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            {
                return null;
            }

            using MemoryStream stream = new(data);
            using BinaryReader reader = new(stream);

            if (stream.Length < height * 4L)
            {
                return null;
            }

            int[] lineOffsets = new int[height];

            for (int y = 0; y < height; y++)
            {
                lineOffsets[y] = reader.ReadInt32();
            }

            byte[] pixels = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                long lineStart = lineOffsets[y] * 4L;

                if (lineStart < 0 || lineStart >= stream.Length)
                {
                    continue;
                }

                stream.Seek(lineStart, SeekOrigin.Begin);

                int x = 0;

                while (x < width && stream.Position + 4 <= stream.Length)
                {
                    ushort color = reader.ReadUInt16();
                    ushort runLength = reader.ReadUInt16();

                    if (runLength == 0)
                    {
                        break;
                    }

                    if (color != 0)
                    {
                        ushort fixedColor = (ushort)(color ^ 0x8000);

                        for (int i = 0; i < runLength && x + i < width; i++)
                        {
                            WriteUoColor1555(pixels, width, x + i, y, fixedColor);
                        }
                    }

                    x += runLength;
                }
            }

            return BuildBitmap(width, height, pixels);
        }
        catch
        {
            return null;
        }
    }

    private static WriteableBitmap? TryDecodeTgaBitmap(byte[] data)
    {
        try
        {
            if (data.Length < 18)
            {
                return null;
            }

            int idLength = data[0];
            int colorMapType = data[1];
            int imageType = data[2];

            if (colorMapType != 0)
            {
                return null;
            }

            if (imageType != 2 && imageType != 3)
            {
                return null;
            }

            int width = data[12] | (data[13] << 8);
            int height = data[14] | (data[15] << 8);
            int bitsPerPixel = data[16];

            if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            {
                return null;
            }

            if (bitsPerPixel != 16 && bitsPerPixel != 24 && bitsPerPixel != 32 && bitsPerPixel != 8)
            {
                return null;
            }

            int bytesPerPixel = bitsPerPixel / 8;
            int pixelStart = 18 + idLength;

            if (pixelStart < 0 || pixelStart >= data.Length)
            {
                return null;
            }

            int expected = width * height * bytesPerPixel;
            if (pixelStart + expected > data.Length)
            {
                return null;
            }

            bool topOrigin = (data[17] & 0x20) != 0;
            byte[] pixels = new byte[width * height * 4];

            int source = pixelStart;

            for (int y = 0; y < height; y++)
            {
                int destY = topOrigin ? y : height - 1 - y;

                for (int x = 0; x < width; x++)
                {
                    int dest = ((destY * width) + x) * 4;

                    if (bitsPerPixel == 8)
                    {
                        byte value = data[source++];
                        pixels[dest + 0] = value;
                        pixels[dest + 1] = value;
                        pixels[dest + 2] = value;
                        pixels[dest + 3] = value == 0 ? (byte)0 : (byte)255;
                    }
                    else if (bitsPerPixel == 16)
                    {
                        ushort color = (ushort)(data[source] | (data[source + 1] << 8));
                        source += 2;

                        if (color != 0)
                        {
                            color = (ushort)(color | 0x8000);
                        }

                        WriteUoColor1555(pixels, width, x, destY, color);
                    }
                    else if (bitsPerPixel == 24)
                    {
                        byte b = data[source++];
                        byte g = data[source++];
                        byte r = data[source++];

                        pixels[dest + 0] = b;
                        pixels[dest + 1] = g;
                        pixels[dest + 2] = r;
                        pixels[dest + 3] = (b == 0 && g == 0 && r == 0) ? (byte)0 : (byte)255;
                    }
                    else
                    {
                        byte b = data[source++];
                        byte g = data[source++];
                        byte r = data[source++];
                        byte a = data[source++];

                        pixels[dest + 0] = b;
                        pixels[dest + 1] = g;
                        pixels[dest + 2] = r;
                        pixels[dest + 3] = a;
                    }
                }
            }

            return BuildBitmap(width, height, pixels);
        }
        catch
        {
            return null;
        }
    }

    private static WriteableBitmap BuildBitmap(int width, int height, byte[] pixels)
    {
        WriteableBitmap bitmap = new(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);

        return bitmap;
    }

    private static void WriteUoColor1555(byte[] pixels, int width, int x, int y, ushort color)
    {
        int offset = ((y * width) + x) * 4;

        if ((color & 0x8000) == 0 || color == 0)
        {
            pixels[offset + 0] = 0;
            pixels[offset + 1] = 0;
            pixels[offset + 2] = 0;
            pixels[offset + 3] = 0;
            return;
        }

        byte r = (byte)(((color >> 10) & 0x1F) * 255 / 31);
        byte g = (byte)(((color >> 5) & 0x1F) * 255 / 31);
        byte b = (byte)((color & 0x1F) * 255 / 31);

        pixels[offset + 0] = b;
        pixels[offset + 1] = g;
        pixels[offset + 2] = r;
        pixels[offset + 3] = 255;
    }

    public List<EquipmentArtPickerEntry> LoadArtIndexEntries(string uoFolderPath)
    {
        List<EquipmentArtPickerEntry> results = new();

        AddMulArtIndexEntries(results, uoFolderPath);
        AddUopArtIndexEntries(results, uoFolderPath);

        return results
            .GroupBy(x => x.ArtId)
            .Select(x => x.First())
            .OrderBy(x => x.ArtId)
            .ToList();
    }

    public List<EquipmentGumpPickerEntry> LoadGumpIndexEntries(string uoFolderPath)
    {
        List<EquipmentGumpPickerEntry> results = new();

        AddMulGumpIndexEntries(results, uoFolderPath);
        AddUopGumpIndexEntries(results, uoFolderPath);

        return results
            .GroupBy(x => x.GumpId)
            .Select(x => x.First())
            .OrderBy(x => x.GumpId)
            .ToList();
    }

    public WriteableBitmap? LoadArtThumbnail(string uoFolderPath, int artId)
    {
        WriteableBitmap? mulBitmap = LoadMulArtThumbnail(uoFolderPath, artId);
        if (mulBitmap != null)
        {
            return mulBitmap;
        }

        return LoadUopArtThumbnail(uoFolderPath, artId);
    }

    public WriteableBitmap? LoadGumpThumbnail(string uoFolderPath, int gumpId)
    {
        WriteableBitmap? mulBitmap = LoadMulGumpThumbnail(uoFolderPath, gumpId);
        if (mulBitmap != null)
        {
            return mulBitmap;
        }

        return LoadUopGumpThumbnail(uoFolderPath, gumpId);
    }

    private void AddMulArtIndexEntries(List<EquipmentArtPickerEntry> results, string uoFolderPath)
    {
        string idxPath = Path.Combine(uoFolderPath, "artidx.mul");
        string mulPath = Path.Combine(uoFolderPath, "art.mul");

        if (!File.Exists(idxPath) || !File.Exists(mulPath))
            return;

        List<IdxEntry> idxEntries = ReadIdxEntries(idxPath);

        for (int artId = 0; artId <= 0xFFFF; artId++)
        {
            int idxIndex = artId + StaticArtOffset;
            if (idxIndex < 0 || idxIndex >= idxEntries.Count)
                break;

            if (!idxEntries[idxIndex].IsValid)
                continue;

            results.Add(new EquipmentArtPickerEntry
            {
                ArtId = artId,
                SourceType = "MUL",
                DisplayText = "0x" + artId.ToString("X4") + "\n" + artId
            });
        }
    }

    private void AddUopArtIndexEntries(List<EquipmentArtPickerEntry> results, string uoFolderPath)
    {
        string uopPath = Path.Combine(uoFolderPath, "artLegacyMUL.uop");
        if (!File.Exists(uopPath))
            return;

        UopFileReader reader = new(uopPath);
        if (!reader.Load())
            return;

        for (int artId = 0; artId <= 0xFFFF; artId++)
        {
            int fileIndex = artId + StaticArtOffset;
            if (!UopEntryExists(reader, "artlegacymul", fileIndex))
                continue;

            results.Add(new EquipmentArtPickerEntry
            {
                ArtId = artId,
                SourceType = "UOP",
                DisplayText = "0x" + artId.ToString("X4") + "\n" + artId
            });
        }
    }

    private void AddMulGumpIndexEntries(List<EquipmentGumpPickerEntry> results, string uoFolderPath)
    {
        string idxPath = Path.Combine(uoFolderPath, "gumpidx.mul");
        string mulPath = Path.Combine(uoFolderPath, "gumpart.mul");
        string bodyDefPath = Path.Combine(uoFolderPath, "body.def");

        if (!File.Exists(idxPath) || !File.Exists(mulPath))
            return;

        BodyDefService bodyDefService = new();
        HashSet<int> usedBodyDefIds = new(bodyDefService.Load(bodyDefPath).Keys);

        List<IdxEntry> idxEntries = ReadIdxEntries(idxPath);

        for (int gumpId = 0; gumpId < idxEntries.Count; gumpId++)
        {
            IdxEntry entry = idxEntries[gumpId];
            if (!entry.IsValid || entry.Extra <= 0)
                continue;

            bool isUsed = usedBodyDefIds.Contains(gumpId);

            results.Add(new EquipmentGumpPickerEntry
            {
                GumpId = gumpId,
                SourceType = "MUL",
                DisplayText =
    "0x" + gumpId.ToString("X4") +
    "\n" + gumpId +
    (isUsed ? "\nUSED" : ""),
                IsUsed = isUsed
            });
        }
    }

    private void AddUopGumpIndexEntries(List<EquipmentGumpPickerEntry> results, string uoFolderPath)
    {
        string uopPath = Path.Combine(uoFolderPath, "gumpartLegacyMUL.uop");
        string bodyDefPath = Path.Combine(uoFolderPath, "body.def");

        if (!File.Exists(uopPath))
            return;

        UopFileReader reader = new(uopPath);
        if (!reader.Load())
            return;

        BodyDefService bodyDefService = new();
        HashSet<int> usedBodyDefIds = new(bodyDefService.Load(bodyDefPath).Keys);

        for (int gumpId = 0; gumpId <= 0xFFFF; gumpId++)
        {
            if (!UopEntryExists(reader, "gumpartlegacymul", gumpId))
                continue;

            bool isUsed = usedBodyDefIds.Contains(gumpId);

            results.Add(new EquipmentGumpPickerEntry
            {
                GumpId = gumpId,
                SourceType = "UOP",
                DisplayText =
    "0x" + gumpId.ToString("X4") +
    "\n" + gumpId +
    (isUsed ? "\nUSED" : ""),
                IsUsed = isUsed
            });
        }
    }

    private static bool UopEntryExists(UopFileReader reader, string pattern, int index)
    {
        string virtualPath = "build/" + pattern.ToLowerInvariant() + "/" + index.ToString("D8") + ".tga";
        ulong hash = UopFileReader.CreateHash(virtualPath);
        return reader.GetEntryByHash(hash).HasValue;
    }

    private WriteableBitmap? LoadMulArtThumbnail(string uoFolderPath, int artId)
    {
        string idxPath = Path.Combine(uoFolderPath, "artidx.mul");
        string mulPath = Path.Combine(uoFolderPath, "art.mul");

        if (!File.Exists(idxPath) || !File.Exists(mulPath))
            return null;

        List<IdxEntry> idxEntries = ReadIdxEntries(idxPath);
        int idxIndex = artId + StaticArtOffset;

        if (idxIndex < 0 || idxIndex >= idxEntries.Count)
            return null;

        IdxEntry entry = idxEntries[idxIndex];
        return entry.IsValid ? TryReadStaticArtBitmap(mulPath, entry.Lookup, entry.Length) : null;
    }

    private WriteableBitmap? LoadUopArtThumbnail(string uoFolderPath, int artId)
    {
        string uopPath = Path.Combine(uoFolderPath, "artLegacyMUL.uop");
        if (!File.Exists(uopPath))
            return null;

        UopFileReader reader = new(uopPath);
        if (!reader.Load())
            return null;

        int fileIndex = artId + StaticArtOffset;
        byte[]? data = ReadUopIndexedTga(reader, "artlegacymul", fileIndex);

        if (data == null || data.Length == 0)
            return null;

        return TryReadStaticArtBitmapFromBytes(data) ?? TryDecodeTgaBitmap(data);
    }

    private WriteableBitmap? LoadMulGumpThumbnail(string uoFolderPath, int gumpId)
    {
        string idxPath = Path.Combine(uoFolderPath, "gumpidx.mul");
        string mulPath = Path.Combine(uoFolderPath, "gumpart.mul");

        if (!File.Exists(idxPath) || !File.Exists(mulPath))
            return null;

        List<IdxEntry> idxEntries = ReadIdxEntries(idxPath);

        if (gumpId < 0 || gumpId >= idxEntries.Count)
            return null;

        IdxEntry entry = idxEntries[gumpId];
        if (!entry.IsValid || entry.Extra <= 0)
            return null;

        int width = (entry.Extra >> 16) & 0xFFFF;
        int height = entry.Extra & 0xFFFF;

        if (width <= 0 || height <= 0 || width > 2048 || height > 2048)
            return null;

        return TryReadGumpBitmap(mulPath, entry.Lookup, entry.Length, width, height);
    }

    private WriteableBitmap? LoadUopGumpThumbnail(string uoFolderPath, int gumpId)
    {
        string uopPath = Path.Combine(uoFolderPath, "gumpartLegacyMUL.uop");
        if (!File.Exists(uopPath))
            return null;

        UopFileReader reader = new(uopPath);
        if (!reader.Load())
            return null;

        byte[]? data = ReadUopIndexedTga(reader, "gumpartlegacymul", gumpId);
        if (data == null || data.Length <= 8)
            return null;

        return TryDecodeUopGumpPayload(data) ?? TryDecodeTgaBitmap(data);
    }
}