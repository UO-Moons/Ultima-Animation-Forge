using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class GumpDataService
{
    private const int MaxGumpCount = 0xFFFF;
    private const int IndexEntrySize = 12;
    private const string GumpUopFileName = "gumpartLegacyMUL.uop";
    private const string GumpIdxFileName = "Gumpidx.mul";
    private const string GumpMulFileName = "Gumpart.mul";

    private string folderPath = string.Empty;
    private string idxPath = string.Empty;
    private string mulPath = string.Empty;
    private string uopPath = string.Empty;

    private UopFileReader? gumpUopReader;
    private bool usingUop;

    private readonly Dictionary<int, UopDataHeader> uopHeadersByGumpId = new();
    private readonly List<GumpEntry> entries = new();

    public IReadOnlyList<GumpEntry> Entries => entries;
    public bool IsUsingUop => usingUop;

    public bool Initialize(string uoFolderPath)
    {
        entries.Clear();
        uopHeadersByGumpId.Clear();
        gumpUopReader = null;
        usingUop = false;

        folderPath = uoFolderPath ?? string.Empty;
        idxPath = Path.Combine(folderPath, GumpIdxFileName);
        mulPath = Path.Combine(folderPath, GumpMulFileName);
        uopPath = Path.Combine(folderPath, GumpUopFileName);

        if (!Directory.Exists(folderPath))
        {
            return false;
        }

        if (File.Exists(uopPath) && LoadUopIndex())
        {
            usingUop = true;
            return entries.Count > 0;
        }

        if (File.Exists(idxPath) && File.Exists(mulPath))
        {
            LoadMulIndex();
            usingUop = false;
            return entries.Count > 0;
        }

        return false;
    }

    private bool LoadUopIndex()
    {
        UopFileReader reader = new UopFileReader(uopPath);
        if (!reader.Load())
        {
            return false;
        }

        gumpUopReader = reader;

        for (int gumpId = 0; gumpId < MaxGumpCount; gumpId++)
        {
            string virtualPath = BuildGumpUopVirtualPath(gumpId);
            ulong hash = UopFileReader.CreateHash(virtualPath);
            UopDataHeader? header = reader.GetEntryByHash(hash);

            if (!header.HasValue)
            {
                continue;
            }

            uopHeadersByGumpId[gumpId] = header.Value;

            entries.Add(new GumpEntry
            {
                GumpId = gumpId,
                Lookup = checked((int)Math.Min(header.Value.Offset, int.MaxValue)),
                Length = checked((int)Math.Min(header.Value.DecompressedSize, int.MaxValue)),
                Extra = 0,
                Width = 0,
                Height = 0,
                IsValid = true,
                SourceFile = GumpUopFileName
            });
        }

        return entries.Count > 0;
    }

    private void LoadMulIndex()
    {
        using FileStream stream = File.OpenRead(idxPath);
        using BinaryReader reader = new BinaryReader(stream);

        int index = 0;

        while (stream.Position + IndexEntrySize <= stream.Length && index < MaxGumpCount)
        {
            int lookup = reader.ReadInt32();
            int length = reader.ReadInt32();
            int extra = reader.ReadInt32();

            int width = (extra >> 16) & 0xFFFF;
            int height = extra & 0xFFFF;

            if (!IsSaneGumpSize(width, height))
            {
                width = 0;
                height = 0;
            }

            bool valid =
                lookup >= 0 &&
                length > 0 &&
                extra != -1 &&
                width > 0 &&
                height > 0;

            entries.Add(new GumpEntry
            {
                GumpId = index,
                Lookup = lookup,
                Length = length,
                Extra = extra,
                Width = width,
                Height = height,
                IsValid = valid,
                SourceFile = GumpMulFileName
            });

            index++;
        }
    }

    public GumpLoadResult LoadGump(GumpEntry? entry)
    {
        if (entry == null)
        {
            return Fail("No gump selected.");
        }

        if (!entry.IsValid)
        {
            return Fail("Selected gump slot is empty.");
        }

        return usingUop
            ? LoadUopGump(entry)
            : LoadMulGump(entry);
    }

    private GumpLoadResult LoadUopGump(GumpEntry entry)
    {
        if (gumpUopReader == null)
        {
            return Fail("gumpartLegacyMUL.uop is not loaded.");
        }

        if (!uopHeadersByGumpId.TryGetValue(entry.GumpId, out UopDataHeader header))
        {
            return Fail("Gump " + entry.GumpId + " was not found in gumpartLegacyMUL.uop.");
        }

        try
        {
            byte[]? fullData = ReadUopGumpPayload(header);
            if (fullData == null || fullData.Length < 8)
            {
                return Fail("Selected UOP gump data is empty or truncated.");
            }

            int width = BitConverter.ToInt32(fullData, 0);
            int height = BitConverter.ToInt32(fullData, 4);

            if (!IsSaneGumpSize(width, height))
            {
                return Fail("UOP gump has invalid dimensions: " + width + "x" + height + ".");
            }

            byte[] rleData = new byte[fullData.Length - 8];
            Buffer.BlockCopy(fullData, 8, rleData, 0, rleData.Length);

            WriteableBitmap? bitmap = DecodeGumpRle(rleData, width, height);
            if (bitmap == null)
            {
                return Fail("Failed to decode UOP gump image.");
            }

            entry.Width = width;
            entry.Height = height;
            entry.Extra = (width << 16) | (height & 0xFFFF);
            entry.Length = fullData.Length;

            return new GumpLoadResult
            {
                Success = true,
                Bitmap = bitmap,
                Width = width,
                Height = height,
                Message = "Loaded UOP gump " + entry.GumpId + " (" + width + "x" + height + ")."
            };
        }
        catch (Exception exception)
        {
            return Fail("Failed to load UOP gump: " + exception.Message);
        }
    }

    private byte[]? ReadUopGumpPayload(UopDataHeader header)
    {
        if (gumpUopReader == null)
        {
            return null;
        }

        byte[]? rawData = gumpUopReader.ReadRawData(header);
        if (rawData == null || rawData.Length == 0)
        {
            return null;
        }

        if (header.Flag == 0)
        {
            return rawData;
        }

        byte[]? zlibData = TryZlibDecompress(rawData);
        if (zlibData == null || zlibData.Length == 0)
        {
            return null;
        }

        if (header.Flag == 1)
        {
            return zlibData;
        }

        if (header.Flag == 3)
        {
            return MythicDecompress.Decompress(zlibData);
        }

        return null;
    }

    private static byte[]? TryZlibDecompress(byte[] rawData)
    {
        try
        {
            using MemoryStream input = new MemoryStream(rawData);
            using ZLibStream zlib = new ZLibStream(input, CompressionMode.Decompress);
            using MemoryStream output = new MemoryStream();

            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private GumpLoadResult LoadMulGump(GumpEntry entry)
    {
        if (!File.Exists(mulPath))
        {
            return Fail("Gumpart.mul was not found.");
        }

        try
        {
            using FileStream stream = File.OpenRead(mulPath);

            if (entry.Lookup < 0 || entry.Lookup >= stream.Length)
            {
                return Fail("Gump lookup is outside Gumpart.mul.");
            }

            stream.Seek(entry.Lookup, SeekOrigin.Begin);

            byte[] data = new byte[entry.Length];
            int read = stream.Read(data, 0, data.Length);

            if (read != data.Length)
            {
                return Fail("Could not read complete gump data.");
            }

            WriteableBitmap? bitmap = DecodeGumpRle(data, entry.Width, entry.Height);

            if (bitmap == null)
            {
                return Fail("Failed to decode gump image.");
            }

            return new GumpLoadResult
            {
                Success = true,
                Bitmap = bitmap,
                Width = entry.Width,
                Height = entry.Height,
                Message = "Loaded MUL gump " + entry.GumpId + " (" + entry.Width + "x" + entry.Height + ")."
            };
        }
        catch (Exception exception)
        {
            return Fail("Failed to load MUL gump: " + exception.Message);
        }
    }

    private static string BuildGumpUopVirtualPath(int gumpId)
    {
        return ("build/gumpartlegacymul/" + gumpId.ToString("D8") + ".tga").ToLowerInvariant();
    }

    private static bool IsSaneGumpSize(int width, int height)
    {
        return width > 0 && width <= 5000 && height > 0 && height <= 5000;
    }

    private static WriteableBitmap? DecodeGumpRle(byte[] data, int width, int height)
    {
        if (data == null || data.Length == 0 || width <= 0 || height <= 0)
        {
            return null;
        }

        int lookupTableBytes = height * 4;
        if (data.Length < lookupTableBytes)
        {
            return null;
        }

        byte[] pixels = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            int lookup = BitConverter.ToInt32(data, y * 4);
            int rleOffset = lookup * 4;

            if (rleOffset < 0 || rleOffset + 4 > data.Length)
            {
                continue;
            }

            int x = 0;

            while (x < width && rleOffset + 4 <= data.Length)
            {
                ushort color = BitConverter.ToUInt16(data, rleOffset);
                ushort runLength = BitConverter.ToUInt16(data, rleOffset + 2);
                rleOffset += 4;

                if (runLength == 0)
                {
                    break;
                }

                int endX = Math.Min(width, x + runLength);

                if (color != 0)
                {
                    ushort color1555 = (ushort)(color ^ 0x8000);

                    for (; x < endX; x++)
                    {
                        WriteColor1555(pixels, width, x, y, color1555);
                    }
                }
                else
                {
                    x = endX;
                }
            }
        }

        WriteableBitmap bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);

        return bitmap;
    }

    private static void WriteColor1555(byte[] pixels, int width, int x, int y, ushort color1555)
    {
        int offset = ((y * width) + x) * 4;

        if ((color1555 & 0x8000) == 0 || color1555 == 0x8000)
        {
            pixels[offset + 0] = 0;
            pixels[offset + 1] = 0;
            pixels[offset + 2] = 0;
            pixels[offset + 3] = 0;
            return;
        }

        byte r = (byte)(((color1555 >> 10) & 0x1F) << 3);
        byte g = (byte)(((color1555 >> 5) & 0x1F) << 3);
        byte b = (byte)((color1555 & 0x1F) << 3);

        pixels[offset + 0] = b;
        pixels[offset + 1] = g;
        pixels[offset + 2] = r;
        pixels[offset + 3] = 255;
    }

    private static GumpLoadResult Fail(string message)
    {
        return new GumpLoadResult
        {
            Success = false,
            Message = message
        };
    }

    public GumpSaveResult ImportPngToSelectedGump(string pngPath, int gumpId)
    {
        if (!usingUop)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "Saving is currently supported for gumpartLegacyMUL.uop only."
            };
        }

        if (string.IsNullOrWhiteSpace(pngPath) || !File.Exists(pngPath))
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "PNG file was not found."
            };
        }

        if (gumpId < 0 || gumpId >= MaxGumpCount)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "Invalid gump ID."
            };
        }

        if (gumpUopReader == null || !File.Exists(uopPath))
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "gumpartLegacyMUL.uop is not loaded."
            };
        }

        try
        {
            WriteableBitmap bitmap = LoadBitmapFromFile(pngPath);
            byte[] gumpPayload = BuildUopGumpPayload(bitmap);

            string backupPath = uopPath + ".bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(uopPath, backupPath, false);
            }

            Dictionary<ulong, UopDataHeader> existingHeaders = gumpUopReader.GetAllEntries();
            List<UopFileData> outputEntries = new();

            ulong targetHash = UopFileReader.CreateHash(BuildGumpUopVirtualPath(gumpId));

            foreach (KeyValuePair<ulong, UopDataHeader> pair in existingHeaders)
            {
                if (pair.Key == targetHash)
                {
                    continue;
                }

                byte[]? data = ReadExistingUopEntryPayload(pair.Value);

                if (data == null || data.Length == 0)
                {
                    continue;
                }

                outputEntries.Add(new UopFileData
                {
                    Hash = pair.Key,
                    Data = data,
                    DecompressedSize = (uint)data.Length,
                    IsCompressed = true,
                    IsEmpty = false
                });
            }

            outputEntries.Add(new UopFileData
            {
                Hash = targetHash,
                Data = gumpPayload,
                DecompressedSize = (uint)gumpPayload.Length,
                IsCompressed = true,
                IsEmpty = false
            });

            UopFileWriter.WriteUopFile(uopPath, outputEntries, 1000);

            Initialize(folderPath);

            return new GumpSaveResult
            {
                Success = true,
                Message = "Imported PNG into gump " + gumpId + " [0x" + gumpId.ToString("X") + "]."
            };
        }
        catch (Exception exception)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "Failed to import PNG into gump: " + exception.Message
            };
        }
    }

    private byte[]? ReadExistingUopEntryPayload(UopDataHeader header)
    {
        if (gumpUopReader == null)
        {
            return null;
        }

        byte[]? rawData = gumpUopReader.ReadRawData(header);
        if (rawData == null || rawData.Length == 0)
        {
            return null;
        }

        if (header.Flag == 0)
        {
            return rawData;
        }

        byte[]? zlibData = TryZlibDecompress(rawData);
        if (zlibData == null || zlibData.Length == 0)
        {
            return null;
        }

        if (header.Flag == 1)
        {
            return zlibData;
        }

        if (header.Flag == 3)
        {
            return MythicDecompress.Decompress(zlibData);
        }

        return null;
    }

    private static WriteableBitmap LoadBitmapFromFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Bitmap source = new Bitmap(stream);

        WriteableBitmap bitmap = new WriteableBitmap(
            source.PixelSize,
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        source.CopyPixels(
            new PixelRect(0, 0, source.PixelSize.Width, source.PixelSize.Height),
            framebuffer.Address,
            framebuffer.RowBytes * framebuffer.Size.Height,
            framebuffer.RowBytes);

        return bitmap;
    }

    private static byte[] BuildUopGumpPayload(WriteableBitmap bitmap)
    {
        FramePixels pixels = ReadPixels(bitmap);

        using MemoryStream output = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(output);

        writer.Write(pixels.Width);
        writer.Write(pixels.Height);

        byte[] rleData = EncodeGumpRle(pixels);
        writer.Write(rleData);

        return output.ToArray();
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

    private static byte[] EncodeGumpRle(FramePixels pixels)
    {
        using MemoryStream output = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(output);

        long lookupStart = output.Position;

        for (int y = 0; y < pixels.Height; y++)
        {
            writer.Write(0);
        }

        for (int y = 0; y < pixels.Height; y++)
        {
            int rowLookup = checked((int)((output.Position - lookupStart) / 4));

            long returnPos = output.Position;
            output.Seek(lookupStart + (y * 4L), SeekOrigin.Begin);
            writer.Write(rowLookup);
            output.Seek(returnPos, SeekOrigin.Begin);

            int x = 0;

            while (x < pixels.Width)
            {
                ushort color = GetGumpColorForPixel(pixels, x, y);
                int runStart = x;
                x++;

                while (x < pixels.Width)
                {
                    ushort nextColor = GetGumpColorForPixel(pixels, x, y);

                    if (nextColor != color || x - runStart >= ushort.MaxValue)
                    {
                        break;
                    }

                    x++;
                }

                int runLength = x - runStart;

                writer.Write(color);
                writer.Write((ushort)runLength);
            }
        }

        return output.ToArray();
    }

    private static ushort GetGumpColorForPixel(FramePixels pixels, int x, int y)
    {
        int offset = ((y * pixels.Width) + x) * 4;

        byte blue = pixels.Pixels[offset + 0];
        byte green = pixels.Pixels[offset + 1];
        byte red = pixels.Pixels[offset + 2];
        byte alpha = pixels.Pixels[offset + 3];

        if (alpha < 16)
        {
            return 0;
        }

        ushort color1555 = (ushort)(
            0x8000 |
            ((red >> 3) << 10) |
            ((green >> 3) << 5) |
            (blue >> 3));

        return (ushort)(color1555 ^ 0x8000);
    }

    public GumpSaveResult RemoveGump(int gumpId)
    {
        if (!usingUop)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "Removing is currently supported for gumpartLegacyMUL.uop only."
            };
        }

        if (gumpId < 0 || gumpId >= MaxGumpCount)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "Invalid gump ID."
            };
        }

        if (gumpUopReader == null || !File.Exists(uopPath))
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "gumpartLegacyMUL.uop is not loaded."
            };
        }

        try
        {
            ulong targetHash = UopFileReader.CreateHash(BuildGumpUopVirtualPath(gumpId));

            Dictionary<ulong, UopDataHeader> existingHeaders = gumpUopReader.GetAllEntries();

            if (!existingHeaders.ContainsKey(targetHash))
            {
                return new GumpSaveResult
                {
                    Success = false,
                    Message = "Gump " + gumpId + " is already free."
                };
            }

            string backupPath = uopPath + ".bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(uopPath, backupPath, false);
            }

            List<UopFileData> outputEntries = new();

            foreach (KeyValuePair<ulong, UopDataHeader> pair in existingHeaders)
            {
                if (pair.Key == targetHash)
                {
                    continue;
                }

                byte[]? data = ReadExistingUopEntryPayload(pair.Value);

                if (data == null || data.Length == 0)
                {
                    continue;
                }

                outputEntries.Add(new UopFileData
                {
                    Hash = pair.Key,
                    Data = data,
                    DecompressedSize = (uint)data.Length,
                    IsCompressed = true,
                    IsEmpty = false
                });
            }

            UopFileWriter.WriteUopFile(uopPath, outputEntries, 1000);

            Initialize(folderPath);

            return new GumpSaveResult
            {
                Success = true,
                Message = "Removed gump " + gumpId + " [0x" + gumpId.ToString("X") + "]. Slot is now free."
            };
        }
        catch (Exception exception)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "Failed to remove gump: " + exception.Message
            };
        }
    }

    public GumpSaveResult ExportGumpToPng(GumpEntry? entry, string outputPath)
    {
        if (entry == null)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "No gump selected."
            };
        }

        if (!entry.IsValid)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "Selected gump slot is empty."
            };
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "Export path is invalid."
            };
        }

        try
        {
            GumpLoadResult loadResult = LoadGump(entry);
            if (!loadResult.Success || loadResult.Bitmap == null)
            {
                return new GumpSaveResult
                {
                    Success = false,
                    Message = loadResult.Message
                };
            }

            loadResult.Bitmap.Save(outputPath);

            return new GumpSaveResult
            {
                Success = true,
                Message = "Exported gump " + entry.GumpId + " [0x" + entry.GumpId.ToString("X") + "] to PNG."
            };
        }
        catch (Exception exception)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "Failed to export gump: " + exception.Message
            };
        }
    }

    public GumpSaveResult ReplaceGumpWithBitmap(WriteableBitmap bitmap, int gumpId, string editName)
    {
        if (!usingUop)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "Editing is currently supported for gumpartLegacyMUL.uop only."
            };
        }

        if (bitmap == null)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "No edited bitmap was supplied."
            };
        }

        if (gumpId < 0 || gumpId >= MaxGumpCount)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "Invalid gump ID."
            };
        }

        if (gumpUopReader == null || !File.Exists(uopPath))
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "gumpartLegacyMUL.uop is not loaded."
            };
        }

        try
        {
            byte[] gumpPayload = BuildUopGumpPayload(bitmap);

            string backupPath = uopPath + ".bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(uopPath, backupPath, false);
            }

            Dictionary<ulong, UopDataHeader> existingHeaders = gumpUopReader.GetAllEntries();
            List<UopFileData> outputEntries = new();

            ulong targetHash = UopFileReader.CreateHash(BuildGumpUopVirtualPath(gumpId));

            foreach (KeyValuePair<ulong, UopDataHeader> pair in existingHeaders)
            {
                if (pair.Key == targetHash)
                {
                    continue;
                }

                byte[]? data = ReadExistingUopEntryPayload(pair.Value);
                if (data == null || data.Length == 0)
                {
                    continue;
                }

                outputEntries.Add(new UopFileData
                {
                    Hash = pair.Key,
                    Data = data,
                    DecompressedSize = (uint)data.Length,
                    IsCompressed = true,
                    IsEmpty = false
                });
            }

            outputEntries.Add(new UopFileData
            {
                Hash = targetHash,
                Data = gumpPayload,
                DecompressedSize = (uint)gumpPayload.Length,
                IsCompressed = true,
                IsEmpty = false
            });

            UopFileWriter.WriteUopFile(uopPath, outputEntries, 1000);
            Initialize(folderPath);

            return new GumpSaveResult
            {
                Success = true,
                Message = editName + " applied to gump " + gumpId + " [0x" + gumpId.ToString("X") + "]."
            };
        }
        catch (Exception exception)
        {
            return new GumpSaveResult
            {
                Success = false,
                Message = "Failed to edit gump: " + exception.Message
            };
        }
    }
}