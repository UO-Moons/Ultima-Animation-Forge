using System;
using System.Collections.Generic;
using System.IO;

namespace UltimaAnimationForge.Services;

public sealed class MythicHashDictionaryService
{
    private readonly Dictionary<ulong, string> namesByHash = new();

    public string DictionaryPath { get; private set; } = string.Empty;

    public int Count => namesByHash.Count;

    public bool IsLoaded => namesByHash.Count > 0;

    public void Clear()
    {
        namesByHash.Clear();
        DictionaryPath = string.Empty;
    }

    public bool Load(string filePath, out string message)
    {
        Clear();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            message = "Dictionary file was not found.";
            return false;
        }

        string extension = Path.GetExtension(filePath);

        if (string.Equals(extension, ".uop", StringComparison.OrdinalIgnoreCase))
        {
            message =
                "string_dictionary.uop is an EC client package, not a Dictionary.dic file yet. " +
                "Next step is adding a string_dictionary.uop parser.";
            return false;
        }

        try
        {
            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader reader = new BinaryReader(stream);

            byte[] id = reader.ReadBytes(4);
            if (id.Length != 4 || id[0] != 'D' || id[1] != 'I' || id[2] != 'C' || id[3] != 0)
            {
                message = "Not a valid Dictionary.dic file.";
                return false;
            }

            _ = reader.ReadByte(); // version

            while (stream.Position < stream.Length)
            {
                ulong hash = reader.ReadUInt64();
                string name = string.Empty;

                bool hasName = reader.ReadByte() == 1;
                if (hasName)
                {
                    name = reader.ReadString();
                }

                if (!string.IsNullOrWhiteSpace(name) && !namesByHash.ContainsKey(hash))
                {
                    namesByHash[hash] = name.Replace('\\', '/');
                }
            }

            DictionaryPath = filePath;
            message = "Loaded dictionary with " + namesByHash.Count + " named hashes.";
            return true;
        }
        catch (Exception exception)
        {
            Clear();
            message = "Failed to load dictionary: " + exception.Message;
            return false;
        }
    }

    public string ResolveName(ulong hash)
    {
        return namesByHash.TryGetValue(hash, out string? name)
            ? name
            : string.Empty;
    }
}