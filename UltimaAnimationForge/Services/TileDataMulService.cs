using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class TileDataMulService
{
    private const int LandTileCount = 0x4000;
    private const int GroupSize = 32;
    private const int HeaderSize = 4;

    private const int OldLandRecordSize = 26;
    private const int NewLandRecordSize = 30;

    private const int OldItemRecordSize = 37;
    private const int NewItemRecordSize = 41;

    private readonly List<int> landHeaders = new();
    private readonly List<int> itemHeaders = new();

    private bool useNewTileDataFormat;
    private int loadedItemCount;

    public List<TileDataEntry> Load(string tileDataPath)
    {
        List<TileDataEntry> entries = new();

        landHeaders.Clear();
        itemHeaders.Clear();
        useNewTileDataFormat = false;
        loadedItemCount = 0;

        if (string.IsNullOrWhiteSpace(tileDataPath) || !File.Exists(tileDataPath))
        {
            return entries;
        }

        byte[] data = File.ReadAllBytes(tileDataPath);

        useNewTileDataFormat = LooksLikeNewFormat(data.Length);

        int offset = 0;

        ReadLandTiles(data, ref offset, entries, useNewTileDataFormat);
        ReadItemTiles(data, ref offset, entries, useNewTileDataFormat);

        loadedItemCount = entries.Count(entry => !entry.IsLand);

        return entries;
    }

    private static bool LooksLikeNewFormat(int fileLength)
    {
        int newLandBytes = GetGroupedTableSize(LandTileCount, NewLandRecordSize);

        if (fileLength >= newLandBytes)
        {
            int remaining = fileLength - newLandBytes;
            int newItemGroupBytes = HeaderSize + (GroupSize * NewItemRecordSize);

            if (remaining >= 0 && remaining % newItemGroupBytes == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetGroupedTableSize(int entryCount, int recordSize)
    {
        int groupCount = entryCount / GroupSize;
        return groupCount * (HeaderSize + (GroupSize * recordSize));
    }

    private void ReadLandTiles(
        byte[] data,
        ref int offset,
        List<TileDataEntry> entries,
        bool useNewFormat)
    {
        int recordSize = useNewFormat ? NewLandRecordSize : OldLandRecordSize;

        for (int tileId = 0; tileId < LandTileCount; tileId += GroupSize)
        {
            if (!CanRead(data, offset, HeaderSize))
            {
                return;
            }

            landHeaders.Add(BitConverter.ToInt32(data, offset));
            offset += HeaderSize;

            for (int i = 0; i < GroupSize; i++)
            {
                if (!CanRead(data, offset, recordSize))
                {
                    return;
                }

                ulong flags = useNewFormat
                    ? BitConverter.ToUInt64(data, offset)
                    : BitConverter.ToUInt32(data, offset);

                int local = offset + (useNewFormat ? 8 : 4);

                ushort textureId = BitConverter.ToUInt16(data, local);
                local += 2;

                string name = ReadName(data, local, 20);

                TileDataEntry entry = new()
                {
                    IsLand = true,
                    Id = tileId + i,
                    Flags = flags,
                    TextureId = textureId,
                    Name = name
                };

                entry.AcceptChanges();
                entries.Add(entry);

                offset += recordSize;
            }
        }
    }

    private void ReadItemTiles(
        byte[] data,
        ref int offset,
        List<TileDataEntry> entries,
        bool useNewFormat)
    {
        int recordSize = useNewFormat ? NewItemRecordSize : OldItemRecordSize;
        int itemGroupBytes = HeaderSize + (GroupSize * recordSize);

        int groupCount = (data.Length - offset) / itemGroupBytes;

        for (int group = 0; group < groupCount; group++)
        {
            if (!CanRead(data, offset, HeaderSize))
            {
                return;
            }

            itemHeaders.Add(BitConverter.ToInt32(data, offset));
            offset += HeaderSize;

            int groupStartId = group * GroupSize;

            for (int i = 0; i < GroupSize; i++)
            {
                if (!CanRead(data, offset, recordSize))
                {
                    return;
                }

                ulong flags = useNewFormat
                    ? BitConverter.ToUInt64(data, offset)
                    : BitConverter.ToUInt32(data, offset);

                int local = offset + (useNewFormat ? 8 : 4);

                byte weight = data[local++];
                byte quality = data[local++];

                ushort miscData = BitConverter.ToUInt16(data, local);
                local += 2;

                byte unknown2 = data[local++];
                byte quantity = data[local++];

                short animation = BitConverter.ToInt16(data, local);
                local += 2;

                byte unknown3 = data[local++];
                byte hue = data[local++];
                byte stackingOffset = data[local++];
                byte value = data[local++];
                byte height = data[local++];

                string name = ReadName(data, local, 20);

                TileDataEntry entry = new()
                {
                    IsLand = false,
                    Id = groupStartId + i,
                    Flags = flags,
                    Weight = weight,
                    Quality = quality,
                    MiscData = miscData,
                    Unknown2 = unknown2,
                    Quantity = quantity,
                    Animation = animation,
                    Unknown3 = unknown3,
                    Hue = hue,
                    StackingOffset = stackingOffset,
                    Value = value,
                    Height = height,
                    Name = name
                };

                entry.AcceptChanges();
                entries.Add(entry);

                offset += recordSize;
            }
        }
    }

    private static bool CanRead(byte[] data, int offset, int length)
    {
        return offset >= 0 && length >= 0 && offset + length <= data.Length;
    }

    private static string ReadName(byte[] data, int offset, int length)
    {
        if (!CanRead(data, offset, length))
        {
            return string.Empty;
        }

        return Encoding.ASCII
            .GetString(data, offset, length)
            .TrimEnd('\0', ' ');
    }

    public bool SaveTileData(string folderPath, List<TileDataEntry> entries, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            message = "UO folder path is invalid.";
            return false;
        }

        string tileDataPath = Path.Combine(folderPath, "tiledata.mul");

        if (!File.Exists(tileDataPath))
        {
            message = "tiledata.mul was not found.";
            return false;
        }

        try
        {
            string backupPath = tileDataPath + ".bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(tileDataPath, backupPath, false);
            }

            List<TileDataEntry> landEntries = entries
                .Where(entry => entry.IsLand)
                .OrderBy(entry => entry.Id)
                .ToList();

            List<TileDataEntry> itemEntries = entries
                .Where(entry => !entry.IsLand)
                .OrderBy(entry => entry.Id)
                .ToList();

            int editedCount = entries.Count(entry => entry.IsEdited);

            using FileStream stream = new FileStream(tileDataPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using BinaryWriter writer = new BinaryWriter(stream);

            WriteLandEntries(writer, landEntries);
            WriteItemEntries(writer, itemEntries);

            message = "Saved " + editedCount + " TileData changes.";
            return true;
        }
        catch (Exception exception)
        {
            message = "Failed saving TileData: " + exception.Message;
            return false;
        }
    }

    private void WriteLandEntries(BinaryWriter writer, List<TileDataEntry> entries)
    {
        if (entries.Count < LandTileCount)
        {
            throw new InvalidOperationException("TileData land table is incomplete.");
        }

        int headerIndex = 0;

        for (int i = 0; i < LandTileCount; i++)
        {
            if ((i & 0x1F) == 0)
            {
                int header = headerIndex < landHeaders.Count ? landHeaders[headerIndex] : 0;
                writer.Write(header);
                headerIndex++;
            }

            TileDataEntry entry = entries[i];

            if (useNewTileDataFormat)
            {
                writer.Write(entry.Flags);
            }
            else
            {
                writer.Write((uint)entry.Flags);
            }

            writer.Write(entry.TextureId);
            WriteFixedName(writer, entry.Name);
        }
    }

    private void WriteItemEntries(BinaryWriter writer, List<TileDataEntry> entries)
    {
        int itemCount = loadedItemCount > 0 ? loadedItemCount : entries.Count;

        if (entries.Count < itemCount)
        {
            throw new InvalidOperationException("TileData item table is incomplete.");
        }

        int headerIndex = 0;

        for (int i = 0; i < itemCount; i++)
        {
            if ((i & 0x1F) == 0)
            {
                int header = headerIndex < itemHeaders.Count ? itemHeaders[headerIndex] : 0;
                writer.Write(header);
                headerIndex++;
            }

            TileDataEntry entry = entries[i];

            if (useNewTileDataFormat)
            {
                writer.Write(entry.Flags);
            }
            else
            {
                writer.Write((uint)entry.Flags);
            }

            writer.Write(entry.Weight);
            writer.Write(entry.Quality);
            writer.Write(entry.MiscData);
            writer.Write(entry.Unknown2);
            writer.Write(entry.Quantity);
            writer.Write(entry.Animation);
            writer.Write(entry.Unknown3);
            writer.Write(entry.Hue);
            writer.Write(entry.StackingOffset);
            writer.Write(entry.Value);
            writer.Write(entry.Height);

            WriteFixedName(writer, entry.Name);
        }
    }

    private static void WriteFixedName(BinaryWriter writer, string? name)
    {
        byte[] buffer = new byte[20];

        if (!string.IsNullOrWhiteSpace(name))
        {
            byte[] source = Encoding.ASCII.GetBytes(name);
            Buffer.BlockCopy(source, 0, buffer, 0, Math.Min(source.Length, buffer.Length));
        }

        writer.Write(buffer);
    }
}