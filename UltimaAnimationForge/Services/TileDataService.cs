using System;
using System.IO;

namespace UltimaAnimationForge.Services;

public sealed class TileDataService
{
    private const int LandCount = 0x4000;
    private const int EntriesPerGroup = 32;
    private const int GroupHeaderSize = 4;

    private const int OldLandRecordSize = 26;
    private const int NewLandRecordSize = 30;

    private const int OldItemRecordSize = 37;
    private const int NewItemRecordSize = 41;

    private const int OldItemAnimationOffset = 10;
    private const int NewItemAnimationOffset = 14;

    public sealed class TileDataItemAnimationInfo
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ItemArtId { get; set; }
        public short AnimationId { get; set; }
        public bool UsesNewFormat { get; set; }
        public long AnimationOffset { get; set; }
    }

    public TileDataItemAnimationInfo ReadItemAnimation(string tileDataPath, int itemArtId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tileDataPath) || !File.Exists(tileDataPath))
            {
                return Fail("tiledata.mul was not found.", itemArtId);
            }

            if (itemArtId < 0)
            {
                return Fail("Item Art ID is invalid.", itemArtId);
            }

            FileInfo fileInfo = new FileInfo(tileDataPath);
            bool usesNewFormat = DetectNewFormat(fileInfo.Length);

            int itemRecordSize = usesNewFormat ? NewItemRecordSize : OldItemRecordSize;
            int animationOffsetInRecord = usesNewFormat ? NewItemAnimationOffset : OldItemAnimationOffset;

            long itemRecordOffset = GetItemRecordOffset(itemArtId, usesNewFormat, itemRecordSize);
            long animationOffset = itemRecordOffset + animationOffsetInRecord;

            if (animationOffset < 0 || animationOffset + 2 > fileInfo.Length)
            {
                return Fail("Item Art ID is outside tiledata.mul range.", itemArtId);
            }

            using FileStream stream = new FileStream(tileDataPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader reader = new BinaryReader(stream);

            stream.Seek(animationOffset, SeekOrigin.Begin);
            short animationId = reader.ReadInt16();

            return new TileDataItemAnimationInfo
            {
                Success = true,
                ItemArtId = itemArtId,
                AnimationId = animationId,
                UsesNewFormat = usesNewFormat,
                AnimationOffset = animationOffset,
                Message = "Read tiledata animation " + animationId + " for item art " + itemArtId + "."
            };
        }
        catch (Exception exception)
        {
            return Fail("Failed reading tiledata.mul: " + exception.Message, itemArtId);
        }
    }

    public TileDataItemAnimationInfo WriteItemAnimation(string tileDataPath, int itemArtId, int animationId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tileDataPath) || !File.Exists(tileDataPath))
            {
                return Fail("tiledata.mul was not found.", itemArtId);
            }

            if (itemArtId < 0)
            {
                return Fail("Item Art ID is invalid.", itemArtId);
            }

            if (animationId < short.MinValue || animationId > short.MaxValue)
            {
                return Fail("TileData animation must fit in a signed 16-bit value.", itemArtId);
            }

            FileInfo fileInfo = new FileInfo(tileDataPath);
            bool usesNewFormat = DetectNewFormat(fileInfo.Length);

            int itemRecordSize = usesNewFormat ? NewItemRecordSize : OldItemRecordSize;
            int animationOffsetInRecord = usesNewFormat ? NewItemAnimationOffset : OldItemAnimationOffset;

            long itemRecordOffset = GetItemRecordOffset(itemArtId, usesNewFormat, itemRecordSize);
            long animationOffset = itemRecordOffset + animationOffsetInRecord;

            if (animationOffset < 0 || animationOffset + 2 > fileInfo.Length)
            {
                return Fail("Item Art ID is outside tiledata.mul range.", itemArtId);
            }

            string backupPath = tileDataPath + ".bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(tileDataPath, backupPath, false);
            }

            using FileStream stream = new FileStream(tileDataPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using BinaryWriter writer = new BinaryWriter(stream);

            stream.Seek(animationOffset, SeekOrigin.Begin);
            writer.Write((short)animationId);

            return new TileDataItemAnimationInfo
            {
                Success = true,
                ItemArtId = itemArtId,
                AnimationId = (short)animationId,
                UsesNewFormat = usesNewFormat,
                AnimationOffset = animationOffset,
                Message = "Updated tiledata.mul item art " + itemArtId + " animation to " + animationId + "."
            };
        }
        catch (Exception exception)
        {
            return Fail("Failed writing tiledata.mul: " + exception.Message, itemArtId);
        }
    }

    public string BuildPreviewLine(int itemArtId, int animationId)
    {
        return "tiledata.mul: item 0x" + itemArtId.ToString("X4") +
               " (" + itemArtId + ") Animation = " + animationId;
    }

    private static long GetItemRecordOffset(int itemArtId, bool usesNewFormat, int itemRecordSize)
    {
        long landSectionSize =
            GetGroupedSectionSize(
                LandCount,
                usesNewFormat ? NewLandRecordSize : OldLandRecordSize);

        long itemGroup = itemArtId / EntriesPerGroup;
        long itemIndexInGroup = itemArtId % EntriesPerGroup;

        return landSectionSize +
               (itemGroup * (GroupHeaderSize + (EntriesPerGroup * itemRecordSize))) +
               GroupHeaderSize +
               (itemIndexInGroup * itemRecordSize);
    }

    private static long GetGroupedSectionSize(int entryCount, int recordSize)
    {
        long groupCount = (entryCount + EntriesPerGroup - 1) / EntriesPerGroup;
        return (groupCount * GroupHeaderSize) + ((long)entryCount * recordSize);
    }

    private static bool DetectNewFormat(long fileLength)
    {
        long oldLandSectionSize = GetGroupedSectionSize(LandCount, OldLandRecordSize);
        long newLandSectionSize = GetGroupedSectionSize(LandCount, NewLandRecordSize);

        long oldRemaining = fileLength - oldLandSectionSize;
        long newRemaining = fileLength - newLandSectionSize;

        long oldItemGroupSize = GroupHeaderSize + (EntriesPerGroup * OldItemRecordSize);
        long newItemGroupSize = GroupHeaderSize + (EntriesPerGroup * NewItemRecordSize);

        bool oldLooksValid = oldRemaining > 0 && oldRemaining % oldItemGroupSize == 0;
        bool newLooksValid = newRemaining > 0 && newRemaining % newItemGroupSize == 0;

        if (newLooksValid && !oldLooksValid)
        {
            return true;
        }

        return false;
    }

    private static TileDataItemAnimationInfo Fail(string message, int itemArtId)
    {
        return new TileDataItemAnimationInfo
        {
            Success = false,
            ItemArtId = itemArtId,
            Message = message
        };
    }
}