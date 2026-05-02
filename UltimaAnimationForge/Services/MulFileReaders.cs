using System;
using System.Collections.Generic;
using System.IO;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public class AnimationIdxReader
{
    public List<AnimationIdxEntry> Read(string idxPath)
    {
        List<AnimationIdxEntry> entries = new List<AnimationIdxEntry>();

        if (!File.Exists(idxPath))
        {
            return entries;
        }

        using (BinaryReader reader = new BinaryReader(File.OpenRead(idxPath)))
        {
            int index = 0;

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                int offset = reader.ReadInt32();
                int length = reader.ReadInt32();
                int extra = reader.ReadInt32();

                entries.Add(new AnimationIdxEntry
                {
                    Offset = offset,
                    Length = length,
                    Extra = extra,
                    Index = index
                });

                index++;
            }
        }

        return entries;
    }
}

public class AnimationMulReader
{
    public byte[] ReadBlock(string mulPath, int offset, int length)
    {
        if (string.IsNullOrWhiteSpace(mulPath))
        {
            return Array.Empty<byte>();
        }

        if (!File.Exists(mulPath))
        {
            return Array.Empty<byte>();
        }

        if (offset < 0 || length <= 0)
        {
            return Array.Empty<byte>();
        }

        using FileStream fileStream = new FileStream(mulPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader binaryReader = new BinaryReader(fileStream);

        if (offset >= fileStream.Length)
        {
            return Array.Empty<byte>();
        }

        fileStream.Seek(offset, SeekOrigin.Begin);

        int safeLength = length;
        long remainingBytes = fileStream.Length - offset;

        if (safeLength > remainingBytes)
        {
            safeLength = (int)remainingBytes;
        }

        return binaryReader.ReadBytes(safeLength);
    }
}
