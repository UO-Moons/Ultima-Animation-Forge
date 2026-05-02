using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

public class UopFileReader
{
    private const uint UopIdentifier = 0x50594D;
    private const uint SupportedVersion = 5;
    private const int TableOffsetLocation = 12;
    private const int TableEntrySize = 34;

    private readonly Dictionary<ulong, UopDataHeader> entriesByHash =
        new Dictionary<ulong, UopDataHeader>();

    public string FilePath { get; }

    public bool IsLoaded { get; private set; }

    public UopFileReader(string filePath)
    {
        FilePath = filePath ?? string.Empty;
    }

    public bool Load()
    {
        entriesByHash.Clear();
        IsLoaded = false;

        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
        {
            return false;
        }

        using FileStream fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader reader = new BinaryReader(fileStream);

        if (!IsValidUop(reader))
        {
            return false;
        }

        List<long> tableEntryOffsets = GatherTableEntryOffsets(reader);

        foreach (long entryOffset in tableEntryOffsets)
        {
            fileStream.Seek(entryOffset, SeekOrigin.Begin);

            UopDataHeader entry = ReadTableEntry(reader);

            if (entry.Offset == 0 || entry.CompressedSize == 0)
            {
                continue;
            }

            entriesByHash[entry.Hash] = entry;
        }

        IsLoaded = entriesByHash.Count > 0;
        return IsLoaded;
    }

    public UopDataHeader? GetEntryByHash(ulong hash)
    {
        if (entriesByHash.TryGetValue(hash, out UopDataHeader entry))
        {
            return entry;
        }

        return null;
    }

    public byte[]? ReadData(UopDataHeader entry)
    {
        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
        {
            return null;
        }

        using FileStream fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader reader = new BinaryReader(fileStream);

        long dataOffset = (long)entry.Offset + entry.HeaderSize;

        if (dataOffset < 0 || dataOffset >= fileStream.Length)
        {
            return null;
        }

        fileStream.Seek(dataOffset, SeekOrigin.Begin);

        byte[] rawData = reader.ReadBytes((int)entry.CompressedSize);

        if (rawData.Length == 0)
        {
            return null;
        }

        if (entry.Flag == 0)
        {
            return rawData;
        }

        if (entry.Flag == 1)
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

        if (entry.Flag == 3)
        {
            try
            {
                return MythicDecompress.Decompress(rawData);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public byte[]? ReadRawData(UopDataHeader entry)
    {
        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
        {
            return null;
        }

        using FileStream fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader reader = new BinaryReader(fileStream);

        long dataOffset = (long)entry.Offset + entry.HeaderSize;

        if (dataOffset < 0 || dataOffset >= fileStream.Length)
        {
            return null;
        }

        fileStream.Seek(dataOffset, SeekOrigin.Begin);
        return reader.ReadBytes((int)entry.CompressedSize);
    }

    public Dictionary<ulong, UopDataHeader> GetAllEntries()
    {
        return new Dictionary<ulong, UopDataHeader>(entriesByHash);
    }

    public static ulong CreateHash(string s)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(s.ToLowerInvariant());
        uint length = (uint)bytes.Length;

        int num;
        uint c = (uint)(num = (int)length - 559038737);
        uint b = (uint)num;
        uint a = (uint)num;

        int startIndex = 0;

        while (length > 12U)
        {
            a += BitConverter.ToUInt32(bytes, startIndex);
            b += BitConverter.ToUInt32(bytes, startIndex + 4);
            c += BitConverter.ToUInt32(bytes, startIndex + 8);

            Mix(ref a, ref b, ref c);

            length -= 12U;
            startIndex += 12;
        }

        switch ((int)length - 1)
        {
            case 0:
                a += (uint)bytes[startIndex];
                break;
            case 1:
                a += (uint)bytes[startIndex + 1] << 8;
                goto case 0;
            case 2:
                a += (uint)bytes[startIndex + 2] << 16;
                goto case 1;
            case 3:
                a += (uint)bytes[startIndex + 3] << 24;
                goto case 2;
            case 4:
                b += (uint)bytes[startIndex + 4];
                goto case 3;
            case 5:
                b += (uint)bytes[startIndex + 5] << 8;
                goto case 4;
            case 6:
                b += (uint)bytes[startIndex + 6] << 16;
                goto case 5;
            case 7:
                b += (uint)bytes[startIndex + 7] << 24;
                goto case 6;
            case 8:
                c += (uint)bytes[startIndex + 8];
                goto case 7;
            case 9:
                c += (uint)bytes[startIndex + 9] << 8;
                goto case 8;
            case 10:
                c += (uint)bytes[startIndex + 10] << 16;
                goto case 9;
            case 11:
                c += (uint)bytes[startIndex + 11] << 24;
                goto case 10;
        }

        FinalMix(ref a, ref b, ref c);

        return ((ulong)b << 32) | c;
    }

    private static void Mix(ref uint a, ref uint b, ref uint c)
    {
        a -= c;
        a ^= Rot(c, 4);
        c += b;

        b -= a;
        b ^= Rot(a, 6);
        a += c;

        c -= b;
        c ^= Rot(b, 8);
        b += a;

        a -= c;
        a ^= Rot(c, 16);
        c += b;

        b -= a;
        b ^= Rot(a, 19);
        a += c;

        c -= b;
        c ^= Rot(b, 4);
        b += a;
    }

    private static void FinalMix(ref uint a, ref uint b, ref uint c)
    {
        c ^= b;
        c -= Rot(b, 14);

        a ^= c;
        a -= Rot(c, 11);

        b ^= a;
        b -= Rot(a, 25);

        c ^= b;
        c -= Rot(b, 16);

        a ^= c;
        a -= Rot(c, 4);

        b ^= a;
        b -= Rot(a, 14);

        c ^= b;
        c -= Rot(b, 24);
    }

    private static uint Rot(uint x, int k)
    {
        return (x << k) | (x >> (32 - k));
    }

    private static bool IsValidUop(BinaryReader reader)
    {
        long current = reader.BaseStream.Position;
        reader.BaseStream.Seek(0, SeekOrigin.Begin);

        uint identifier = reader.ReadUInt32();
        uint version = reader.ReadUInt32();

        reader.BaseStream.Seek(current, SeekOrigin.Begin);

        return identifier == UopIdentifier && version <= SupportedVersion;
    }

    private static List<long> GatherTableEntryOffsets(BinaryReader reader)
    {
        List<long> offsets = new List<long>();

        long current = reader.BaseStream.Position;
        reader.BaseStream.Seek(TableOffsetLocation, SeekOrigin.Begin);

        ulong tableLocation = reader.ReadUInt64();

        while (tableLocation != 0 && reader.BaseStream.Position < reader.BaseStream.Length)
        {
            reader.BaseStream.Seek((long)tableLocation, SeekOrigin.Begin);

            uint tableSize = reader.ReadUInt32();
            ulong nextTableLocation = reader.ReadUInt64();

            long firstEntryOffset = reader.BaseStream.Position;

            for (uint index = 0; index < tableSize; index++)
            {
                offsets.Add(firstEntryOffset + (index * TableEntrySize));
            }

            tableLocation = nextTableLocation;
        }

        reader.BaseStream.Seek(current, SeekOrigin.Begin);

        return offsets;
    }

    private static UopDataHeader ReadTableEntry(BinaryReader reader)
    {
        ulong offset = reader.ReadUInt64();
        uint headerSize = reader.ReadUInt32();
        uint compressedSize = reader.ReadUInt32();
        uint decompressedSize = reader.ReadUInt32();
        ulong hash = reader.ReadUInt64();
        reader.ReadUInt32(); // data block hash, unused here
        ushort flag = (ushort)reader.ReadInt16();

        return new UopDataHeader(offset, headerSize, compressedSize, decompressedSize, hash, flag);
    }
}

public static class UopFileWriter
{
    private sealed class TocEntry
    {
        public ulong Offset { get; set; }
        public uint HeaderSize { get; set; }
        public uint CompressedSize { get; set; }
        public uint DecompressedSize { get; set; }
        public ulong Hash { get; set; }
        public ushort Flag { get; set; }
    }

    public static void WriteUopFile(
        string filePath,
        IEnumerable<UopFileData> fileEntries,
        uint blockCapacity = 1000)
    {
        List<UopFileData> entries = fileEntries
            .OrderBy(x => x.Hash)
            .ToList();

        if (blockCapacity == 0)
        {
            blockCapacity = 100;
        }

        List<TocEntry> tocEntries = new List<TocEntry>();
        long currentDataOffset = 32;

        using FileStream output = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using BinaryWriter writer = new BinaryWriter(output);

        writer.Write(5265741U);
        writer.Write(5U);
        writer.Write(4246989891U);
        writer.Write(0UL);
        writer.Write(blockCapacity);
        writer.Write((uint)entries.Count);
        writer.Write(0U);

        if (output.Position != 32)
        {
            throw new InvalidOperationException("UOP writer error: Header size is not 32 bytes.");
        }

        foreach (UopFileData entry in entries)
        {
            if (entry.IsEmpty)
            {
                tocEntries.Add(new TocEntry
                {
                    Offset = 0,
                    HeaderSize = 0,
                    CompressedSize = 0,
                    DecompressedSize = 0,
                    Hash = entry.Hash,
                    Flag = 0
                });

                continue;
            }

            if (entry.HeaderSize > 0)
            {
                if (entry.HeaderBytes != null && entry.HeaderBytes.Length == entry.HeaderSize)
                {
                    writer.Write(entry.HeaderBytes);
                }
                else
                {
                    writer.Write(new byte[entry.HeaderSize]);
                }
            }

            byte[] payload;
            uint compressedSize;
            uint decompressedSize;
            bool isCompressedFlag;

            if (entry.PrecompressedData != null)
            {
                payload = entry.PrecompressedData;
                compressedSize = (uint)payload.Length;
                decompressedSize = entry.DecompressedSize;
                isCompressedFlag = true;
            }
            else
            {
                decompressedSize = (uint)entry.Data.Length;
                isCompressedFlag = entry.IsCompressed;

                if (entry.IsCompressed)
                {
                    using MemoryStream compressed = new MemoryStream();
                    using (ZLibStream zlib = new ZLibStream(compressed, CompressionMode.Compress, true))
                    {
                        zlib.Write(entry.Data, 0, entry.Data.Length);
                    }

                    payload = compressed.ToArray();
                    compressedSize = (uint)payload.Length;
                }
                else
                {
                    payload = entry.Data;
                    compressedSize = decompressedSize;
                }
            }

            writer.Write(payload, 0, payload.Length);

            tocEntries.Add(new TocEntry
            {
                Offset = (ulong)currentDataOffset,
                HeaderSize = entry.HeaderSize,
                CompressedSize = compressedSize,
                DecompressedSize = decompressedSize,
                Hash = entry.Hash,
                Flag = (ushort)(isCompressedFlag ? 1 : 0)
            });

            currentDataOffset = output.Position;
        }

        long tocStart = output.Position;

        for (int start = 0; start < tocEntries.Count; start += (int)blockCapacity)
        {
            long blockStart = output.Position;

            List<TocEntry> block = tocEntries
                .Skip(start)
                .Take((int)blockCapacity)
                .ToList();

            writer.Write((uint)block.Count);

            long nextBlockOffset = 0;
            if (start + block.Count < tocEntries.Count)
            {
                nextBlockOffset = blockStart + 4 + 8 + (block.Count * 34L);
            }

            writer.Write((ulong)nextBlockOffset);

            foreach (TocEntry toc in block)
            {
                writer.Write(toc.Offset);
                writer.Write(toc.HeaderSize);
                writer.Write(toc.CompressedSize);
                writer.Write(toc.DecompressedSize);
                writer.Write(toc.Hash);
                writer.Write(0U);
                writer.Write(toc.Flag);
            }
        }

        output.Seek(12, SeekOrigin.Begin);
        writer.Write((ulong)tocStart);
    }
}
