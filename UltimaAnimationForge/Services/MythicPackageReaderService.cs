using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class MythicPackageReaderService
{
    private global::UopFileReader? reader;
    private string packagePath = string.Empty;

    private readonly MythicHashDictionaryService hashDictionary = new();

    private readonly Dictionary<ulong, string> generatedNamesByHash = new();

    public string PackagePath => packagePath;

    public bool LoadDictionary(string dictionaryPath, out string message)
    {
        bool loaded = hashDictionary.Load(dictionaryPath, out message);
        return loaded;
    }

    private sealed class PackageIndexEntry
    {
        public ulong Hash { get; set; }
        public UopDataHeader Header { get; set; }
        public int BlockIndex { get; set; }
        public int FileIndex { get; set; }
    }

    public string DictionaryPath => hashDictionary.DictionaryPath;

    public int DictionaryEntryCount => hashDictionary.Count;

    private List<PackageIndexEntry> ReadPackageIndexEntries()
    {
        List<PackageIndexEntry> result = new();

        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            return result;
        }

        using FileStream stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader reader = new BinaryReader(stream);

        stream.Seek(12, SeekOrigin.Begin);
        ulong tableLocation = reader.ReadUInt64();

        int blockIndex = 0;

        while (tableLocation != 0 && tableLocation < (ulong)stream.Length)
        {
            stream.Seek((long)tableLocation, SeekOrigin.Begin);

            uint tableSize = reader.ReadUInt32();
            ulong nextTableLocation = reader.ReadUInt64();

            int xmlFileIndex = 0;

            for (int rawFileIndex = 0; rawFileIndex < tableSize; rawFileIndex++)
            {
                ulong offset = reader.ReadUInt64();
                uint headerSize = reader.ReadUInt32();
                uint compressedSize = reader.ReadUInt32();
                uint decompressedSize = reader.ReadUInt32();
                ulong hash = reader.ReadUInt64();

                reader.ReadUInt32();

                ushort flag = (ushort)reader.ReadInt16();

                if (offset != 0 && compressedSize != 0)
                {
                    result.Add(new PackageIndexEntry
                    {
                        Hash = hash,
                        Header = new UopDataHeader(offset, headerSize, compressedSize, decompressedSize, hash, flag),
                        BlockIndex = blockIndex,
                        FileIndex = rawFileIndex
                    });
                }
            }

            tableLocation = nextTableLocation;
            blockIndex++;
        }

        return result;
    }

    public bool Load(string filePath)
    {
        packagePath = string.Empty;
        reader = null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        global::UopFileReader newReader = new global::UopFileReader(filePath);
        if (!newReader.Load())
        {
            return false;
        }

        reader = newReader;
        packagePath = filePath;
        BuildGeneratedNameCacheForCurrentPackage();
        return true;
    }

    public List<MythicPackageEntry> GetEntries()
    {
        if (reader == null)
        {
            return new List<MythicPackageEntry>();
        }

        return ReadPackageIndexEntries()
            .OrderBy(x => x.BlockIndex)
            .ThenBy(x => x.FileIndex)
            .Select(x =>
            {
                byte[] sample = ReadEntryBytes(x.Hash, false);
                string resolvedName = hashDictionary.ResolveName(x.Hash);

                if (string.IsNullOrWhiteSpace(resolvedName) &&
                    generatedNamesByHash.TryGetValue(x.Hash, out string? generatedName))
                {
                    resolvedName = generatedName;
                }

                return new MythicPackageEntry
                {
                    Hash = x.Hash,
                    FileName = resolvedName,
                    Offset = x.Header.Offset,
                    HeaderSize = x.Header.HeaderSize,
                    CompressedSize = x.Header.CompressedSize,
                    DecompressedSize = x.Header.DecompressedSize,
                    CompressionFlag = x.Header.Flag,
                    PreviewType = GuessType(sample),
                    BlockIndex = x.BlockIndex,
                    FileIndex = x.FileIndex
                };
            })
            .ToList();
    }

    public byte[] ReadEntryBytes(ulong hash, bool fullRead = true)
    {
        if (reader == null)
        {
            return Array.Empty<byte>();
        }

        UopDataHeader? header = reader.GetEntryByHash(hash);
        if (!header.HasValue)
        {
            return Array.Empty<byte>();
        }

        byte[]? data = reader.ReadData(header.Value);
        if (data == null || data.Length == 0)
        {
            return Array.Empty<byte>();
        }

        if (!fullRead && data.Length > 32)
        {
            byte[] small = new byte[32];
            Buffer.BlockCopy(data, 0, small, 0, small.Length);
            return small;
        }

        return data;
    }

    public void ExportEntry(ulong hash, string outputPath)
    {
        byte[] data = ReadEntryBytes(hash);
        if (data.Length == 0)
        {
            throw new InvalidOperationException("Entry has no readable data.");
        }

        File.WriteAllBytes(outputPath, data);
    }

    public int ExportAll(string folderPath, IEnumerable<MythicPackageEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("Export folder does not exist.");
        }

        int count = 0;

        foreach (MythicPackageEntry entry in entries)
        {
            byte[] data = ReadEntryBytes(entry.Hash);
            if (data.Length == 0)
            {
                continue;
            }

            string extension = GetExtensionFromBytes(data);
            string fileName = entry.HashText.Replace("0x", "") + extension;
            string outputPath = Path.Combine(folderPath, fileName);

            File.WriteAllBytes(outputPath, data);
            count++;
        }

        return count;
    }

    private void BuildGeneratedNameCacheForCurrentPackage()
    {
        generatedNamesByHash.Clear();

        if (reader == null || string.IsNullOrWhiteSpace(packagePath))
        {
            return;
        }

        string fileName = Path.GetFileName(packagePath);

        if (!fileName.StartsWith("AnimationFrame", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        HashSet<ulong> packageHashes = reader.GetAllEntries().Keys.ToHashSet();

        for (int bodyId = 0; bodyId <= 65535; bodyId++)
        {
            for (int action = 0; action < 200; action++)
            {
                string path =
                    "build/animationlegacyframe/" +
                    bodyId.ToString("D6") +
                    "/" +
                    action.ToString("D2") +
                    ".bin";

                ulong hash = global::UopFileReader.CreateHash(path);

                if (packageHashes.Contains(hash) && !generatedNamesByHash.ContainsKey(hash))
                {
                    generatedNamesByHash[hash] = path;
                }
            }
        }
    }

    public static string GuessType(byte[] data)
    {
        if (data == null || data.Length < 4)
        {
            return "Unknown";
        }

        if (data.Length >= 8 &&
            data[0] == 0x89 &&
            data[1] == 0x50 &&
            data[2] == 0x4E &&
            data[3] == 0x47)
        {
            return "PNG";
        }

        if (data[0] == 0xFF && data[1] == 0xD8)
        {
            return "JPG";
        }

        if (data[0] == 0x42 && data[1] == 0x4D)
        {
            return "BMP";
        }

        if (data[0] == 0x44 &&
            data[1] == 0x44 &&
            data[2] == 0x53 &&
            data[3] == 0x20)
        {
            return "DDS";
        }

        if (data[0] == 0x41 &&
            data[1] == 0x4D &&
            data[2] == 0x4F &&
            data[3] == 0x55)
        {
            return "AMOU Animation";
        }

        return "Binary";
    }

    public static string GetExtensionFromBytes(byte[] data)
    {
        string type = GuessType(data);

        return type switch
        {
            "PNG" => ".png",
            "JPG" => ".jpg",
            "BMP" => ".bmp",
            "DDS" => ".dds",
            "AMOU Animation" => ".amou",
            _ => ".bin"
        };
    }
}