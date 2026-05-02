using System;
using System.Collections.Generic;
using System.IO;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class VdImportService
{
    public sealed class ImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ImportedEntries { get; set; }
    }

    private struct VdEntry
    {
        public int Lookup;
        public int Length;
        public int Extra;
    }

    public sealed class PendingImportEntry
    {
        public int TargetIndex { get; set; }
        public int Extra { get; set; }
        public byte[] BlockData { get; set; } = Array.Empty<byte>();
    }

    public sealed class ImportPlan
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public string IdxPath { get; set; } = string.Empty;
        public string MulPath { get; set; } = string.Empty;

        public int BaseIndex { get; set; }
        public int EntryCount { get; set; }
        public int ImportedEntries { get; set; }

        public List<AnimationIdxEntry> WorkingIdxEntries { get; set; } = new();
        public List<PendingImportEntry> PopulatedEntries { get; set; } = new();
    }

    public ImportResult ImportVdIntoMulSlot(string uoFolderPath, string vdPath, MulSlotEntry targetSlot)
    {
        if (string.IsNullOrWhiteSpace(uoFolderPath) || !Directory.Exists(uoFolderPath))
        {
            return Fail("UO folder path is invalid.");
        }

        if (string.IsNullOrWhiteSpace(vdPath) || !File.Exists(vdPath))
        {
            return Fail("VD file was not found.");
        }

        if (targetSlot == null)
        {
            return Fail("No target MUL slot is selected.");
        }

        string idxPath = Path.Combine(uoFolderPath, targetSlot.FileName);
        string mulPath = Path.Combine(uoFolderPath, Path.ChangeExtension(targetSlot.FileName, ".mul"));

        if (!File.Exists(idxPath))
        {
            return Fail("Target IDX file was not found: " + Path.GetFileName(idxPath));
        }

        if (!File.Exists(mulPath))
        {
            return Fail("Target MUL file was not found: " + Path.GetFileName(mulPath));
        }

        short expectedAnimType = GetAnimTypeFromLength(targetSlot.AnimLength);
        if (expectedAnimType < 0)
        {
            return Fail("Unsupported target animation length: " + targetSlot.AnimLength);
        }

        using FileStream vdStream = new FileStream(vdPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader vdReader = new BinaryReader(vdStream);

        short version = vdReader.ReadInt16();
        short animType = vdReader.ReadInt16();

        if (version != 6)
        {
            return Fail("Not a valid VD file. Expected version 6.");
        }

        if (animType != expectedAnimType)
        {
            return Fail(
                "VD anim type does not match target slot. " +
                "VD=" + animType + ", Target=" + expectedAnimType + ", AnimLength=" + targetSlot.AnimLength);
        }

        int entryCount = targetSlot.AnimLength * 5;
        List<VdEntry> vdEntries = new List<VdEntry>(entryCount);

        for (int i = 0; i < entryCount; i++)
        {
            if (vdReader.BaseStream.Position + 12 > vdReader.BaseStream.Length)
            {
                return Fail("VD entry table is truncated.");
            }

            vdEntries.Add(new VdEntry
            {
                Lookup = vdReader.ReadInt32(),
                Length = vdReader.ReadInt32(),
                Extra = vdReader.ReadInt32()
            });
        }

        List<AnimationIdxEntry> idxEntries = ReadIdxEntries(idxPath);

        int baseIndex = GetLegacyBaseIndexForBody(targetSlot.FileType, targetSlot.BodyIndex);

        int requiredEntryCount = baseIndex + entryCount;

        while (idxEntries.Count < requiredEntryCount)
        {
            idxEntries.Add(new AnimationIdxEntry
            {
                Offset = -1,
                Length = -1,
                Extra = -1,
                Index = idxEntries.Count
            });
        }

        for (int i = 0; i < entryCount; i++)
        {
            AnimationIdxEntry current = idxEntries[baseIndex + i];
            if (current.Offset >= 0 && current.Length > 0)
            {
                return Fail(
                    "Target body slot is not fully empty. " +
                    "BodyIndex=" + targetSlot.BodyIndex +
                    ", slot=" + (baseIndex + i));
            }
        }

        int importedEntries = 0;

        using FileStream mulStream = new FileStream(mulPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
        {
            Position = new FileInfo(mulPath).Length
        };

        foreach ((VdEntry vdEntry, int localIndex) in EnumerateWithIndex(vdEntries))
        {
            int targetIndex = baseIndex + localIndex;

            if (vdEntry.Lookup <= 0 || vdEntry.Length <= 0)
            {
                idxEntries[targetIndex].Offset = -1;
                idxEntries[targetIndex].Length = -1;
                idxEntries[targetIndex].Extra = -1;
                continue;
            }

            if (vdEntry.Lookup + vdEntry.Length > vdReader.BaseStream.Length)
            {
                return Fail("VD entry points outside the file at local entry " + localIndex + ".");
            }

            vdReader.BaseStream.Seek(vdEntry.Lookup, SeekOrigin.Begin);
            byte[] block = vdReader.ReadBytes(vdEntry.Length);

            if (block.Length != vdEntry.Length)
            {
                return Fail("Failed to read complete VD block for local entry " + localIndex + ".");
            }

            long mulOffset = mulStream.Position;
            mulStream.Write(block, 0, block.Length);

            idxEntries[targetIndex].Offset = checked((int)mulOffset);
            idxEntries[targetIndex].Length = block.Length;
            idxEntries[targetIndex].Extra = vdEntry.Extra;
            importedEntries++;
        }

        WriteIdxEntries(idxPath, idxEntries);

        return new ImportResult
        {
            Success = true,
            ImportedEntries = importedEntries,
            Message =
                "Imported VD into " + Path.GetFileName(mulPath) +
                " body slot " + targetSlot.BodyIndex +
                " (" + importedEntries + " populated entries)."
        };
    }

    private static short GetAnimTypeFromLength(int animLength)
    {
        return animLength switch
        {
            22 => 0,
            13 => 1,
            35 => 2,
            _ => (short)-1
        };
    }

    private static List<AnimationIdxEntry> ReadIdxEntries(string idxPath)
    {
        List<AnimationIdxEntry> entries = new List<AnimationIdxEntry>();

        using BinaryReader reader = new BinaryReader(File.Open(idxPath, FileMode.Open, FileAccess.Read, FileShare.Read));

        int index = 0;
        while (reader.BaseStream.Position + 12 <= reader.BaseStream.Length)
        {
            entries.Add(new AnimationIdxEntry
            {
                Offset = reader.ReadInt32(),
                Length = reader.ReadInt32(),
                Extra = reader.ReadInt32(),
                Index = index
            });
            index++;
        }

        return entries;
    }

    private static void WriteIdxEntries(string idxPath, List<AnimationIdxEntry> entries)
    {
        using BinaryWriter writer = new BinaryWriter(File.Open(idxPath, FileMode.Create, FileAccess.Write, FileShare.None));

        for (int i = 0; i < entries.Count; i++)
        {
            writer.Write(entries[i].Offset);
            writer.Write(entries[i].Length);
            writer.Write(entries[i].Extra);
        }
    }

    private static IEnumerable<(T item, int index)> EnumerateWithIndex<T>(IEnumerable<T> source)
    {
        int index = 0;
        foreach (T item in source)
        {
            yield return (item, index);
            index++;
        }
    }

    private static ImportResult Fail(string message)
    {
        return new ImportResult
        {
            Success = false,
            Message = message,
            ImportedEntries = 0
        };
    }

    internal static int GetLegacyBaseIndexForBody(int fileType, int bodyIndex)
    {
        return fileType switch
        {
            2 => bodyIndex < 200
                ? bodyIndex * 110
                : 22000 + ((bodyIndex - 200) * 65),

            3 => bodyIndex < 300
                ? bodyIndex * 65
                : bodyIndex < 400
                    ? 33000 + ((bodyIndex - 300) * 110)
                    : 35000 + ((bodyIndex - 400) * 175),

            4 => bodyIndex < 200
                ? bodyIndex * 110
                : bodyIndex < 400
                    ? 22000 + ((bodyIndex - 200) * 65)
                    : 35000 + ((bodyIndex - 400) * 175),

            5 => (bodyIndex < 200 && bodyIndex != 34)
                ? bodyIndex * 110
                : bodyIndex < 400
                    ? 22000 + ((bodyIndex - 200) * 65)
                    : 35000 + ((bodyIndex - 400) * 175),

            6 => bodyIndex < 200
                ? bodyIndex * 110
                : bodyIndex < 400
                    ? 22000 + ((bodyIndex - 200) * 65)
                    : 35000 + ((bodyIndex - 400) * 175),

            _ => bodyIndex * 110
        };
    }

    public ImportPlan BuildMulImportPlan(string uoFolderPath, string vdPath, MulSlotEntry targetSlot)
    {
        ImportPlan plan = new ImportPlan();

        if (string.IsNullOrWhiteSpace(uoFolderPath) || !Directory.Exists(uoFolderPath))
        {
            plan.Message = "UO folder path is invalid.";
            return plan;
        }

        if (string.IsNullOrWhiteSpace(vdPath) || !File.Exists(vdPath))
        {
            plan.Message = "VD file was not found.";
            return plan;
        }

        if (targetSlot == null)
        {
            plan.Message = "No target MUL slot is selected.";
            return plan;
        }

        string idxPath = Path.Combine(uoFolderPath, targetSlot.FileName);
        string mulPath = Path.Combine(uoFolderPath, Path.ChangeExtension(targetSlot.FileName, ".mul"));

        if (!File.Exists(idxPath))
        {
            plan.Message = "Target IDX file was not found: " + Path.GetFileName(idxPath);
            return plan;
        }

        if (!File.Exists(mulPath))
        {
            plan.Message = "Target MUL file was not found: " + Path.GetFileName(mulPath);
            return plan;
        }

        short expectedAnimType = GetAnimTypeFromLength(targetSlot.AnimLength);
        if (expectedAnimType < 0)
        {
            plan.Message = "Unsupported target animation length: " + targetSlot.AnimLength;
            return plan;
        }

        using FileStream vdStream = new FileStream(vdPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader vdReader = new BinaryReader(vdStream);

        short version = vdReader.ReadInt16();
        short animType = vdReader.ReadInt16();

        if (version != 6)
        {
            plan.Message = "Not a valid VD file. Expected version 6.";
            return plan;
        }

        if (animType != expectedAnimType)
        {
            plan.Message =
                "VD anim type does not match target slot. " +
                "VD=" + animType + ", Target=" + expectedAnimType + ", AnimLength=" + targetSlot.AnimLength;
            return plan;
        }

        int entryCount = targetSlot.AnimLength * 5;
        List<VdEntry> vdEntries = new List<VdEntry>(entryCount);

        for (int i = 0; i < entryCount; i++)
        {
            if (vdReader.BaseStream.Position + 12 > vdReader.BaseStream.Length)
            {
                plan.Message = "VD entry table is truncated.";
                return plan;
            }

            vdEntries.Add(new VdEntry
            {
                Lookup = vdReader.ReadInt32(),
                Length = vdReader.ReadInt32(),
                Extra = vdReader.ReadInt32()
            });
        }

        List<AnimationIdxEntry> idxEntries = ReadIdxEntries(idxPath);

        int baseIndex = GetLegacyBaseIndexForBody(targetSlot.FileType, targetSlot.BodyIndex);

        int requiredEntryCount = baseIndex + entryCount;

        while (idxEntries.Count < requiredEntryCount)
        {
            idxEntries.Add(new AnimationIdxEntry
            {
                Offset = -1,
                Length = -1,
                Extra = -1,
                Index = idxEntries.Count
            });
        }

        for (int i = 0; i < entryCount; i++)
        {
            AnimationIdxEntry current = idxEntries[baseIndex + i];
            if (current.Offset >= 0 && current.Length > 0)
            {
                plan.Message =
                    "Target body slot is not fully empty. " +
                    "BodyIndex=" + targetSlot.BodyIndex +
                    ", slot=" + (baseIndex + i);
                return plan;
            }
        }

        int importedEntries = 0;
        List<PendingImportEntry> populatedEntries = new List<PendingImportEntry>();

        foreach ((VdEntry vdEntry, int localIndex) in EnumerateWithIndex(vdEntries))
        {
            int targetIndex = baseIndex + localIndex;

            if (vdEntry.Lookup <= 0 || vdEntry.Length <= 0)
            {
                idxEntries[targetIndex].Offset = -1;
                idxEntries[targetIndex].Length = -1;
                idxEntries[targetIndex].Extra = -1;
                continue;
            }

            if (vdEntry.Lookup + vdEntry.Length > vdReader.BaseStream.Length)
            {
                plan.Message = "VD entry points outside the file at local entry " + localIndex + ".";
                return plan;
            }

            vdReader.BaseStream.Seek(vdEntry.Lookup, SeekOrigin.Begin);
            byte[] block = vdReader.ReadBytes(vdEntry.Length);

            if (block.Length != vdEntry.Length)
            {
                plan.Message = "Failed to read complete VD block for local entry " + localIndex + ".";
                return plan;
            }

            idxEntries[targetIndex].Offset = -2;
            idxEntries[targetIndex].Length = block.Length;
            idxEntries[targetIndex].Extra = vdEntry.Extra;

            populatedEntries.Add(new PendingImportEntry
            {
                TargetIndex = targetIndex,
                Extra = vdEntry.Extra,
                BlockData = block
            });

            importedEntries++;
        }

        plan.Success = true;
        plan.Message =
            "Queued VD import into " + Path.GetFileName(mulPath) +
            " body slot " + targetSlot.BodyIndex +
            " (" + importedEntries + " populated entries).";
        plan.IdxPath = idxPath;
        plan.MulPath = mulPath;
        plan.BaseIndex = baseIndex;
        plan.EntryCount = entryCount;
        plan.ImportedEntries = importedEntries;
        plan.WorkingIdxEntries = idxEntries;
        plan.PopulatedEntries = populatedEntries;

        return plan;
    }
}

public sealed class PendingMulImportSession
{
    public sealed class PendingMulBlock
    {
        public int SlotIndex { get; set; }
        public int Extra { get; set; }
        public byte[] BlockData { get; set; } = Array.Empty<byte>();
    }

    public sealed class PendingBodyConvEntry
    {
        public int BodyId { get; set; }
        public int FileType { get; set; }
        public int SlotBodyIndex { get; set; }
        public string? Comment { get; set; }
    }

    public sealed class PendingMobTypeEntry
    {
        public int BodyId { get; set; }
        public string MobType { get; set; } = string.Empty;
        public string? Comment { get; set; }
    }

    public sealed class PendingMulFileEdit
    {
        public string MulPath { get; set; } = string.Empty;
        public string IdxPath { get; set; } = string.Empty;
        public string MulFileName { get; set; } = string.Empty;
        public string IdxFileName { get; set; } = string.Empty;

        public List<AnimationIdxEntry> WorkingIdxEntries { get; set; } = new();
        public Dictionary<int, PendingMulBlock> PendingBlocksBySlotIndex { get; } = new();

        public bool IsDirty =>
            WorkingIdxEntries.Count > 0 ||
            PendingBlocksBySlotIndex.Count > 0;
    }

    private readonly Dictionary<string, PendingMulFileEdit> editsByIdxPath =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<int, PendingBodyConvEntry> pendingBodyConvByBodyId =
        new();

    private readonly Dictionary<int, PendingMobTypeEntry> pendingMobTypesByBodyId =
        new();

    public IReadOnlyDictionary<string, PendingMulFileEdit> FileEdits => editsByIdxPath;
    public IReadOnlyDictionary<int, PendingBodyConvEntry> PendingBodyConvEntries => pendingBodyConvByBodyId;
    public IReadOnlyDictionary<int, PendingMobTypeEntry> PendingMobTypeEntries => pendingMobTypesByBodyId;

    public bool HasUnsavedChanges =>
        editsByIdxPath.Count > 0 ||
        pendingBodyConvByBodyId.Count > 0 ||
        pendingMobTypesByBodyId.Count > 0;

    public int DirtyFileCount => editsByIdxPath.Count;

    public PendingMulFileEdit GetOrCreateFileEdit(
        string mulPath,
        string idxPath,
        List<AnimationIdxEntry> workingIdxEntries)
    {
        if (!editsByIdxPath.TryGetValue(idxPath, out PendingMulFileEdit? edit))
        {
            edit = new PendingMulFileEdit
            {
                MulPath = mulPath,
                IdxPath = idxPath,
                MulFileName = System.IO.Path.GetFileName(mulPath),
                IdxFileName = System.IO.Path.GetFileName(idxPath),
                WorkingIdxEntries = workingIdxEntries
            };

            editsByIdxPath[idxPath] = edit;
        }

        return edit;
    }

    public bool TryGetFileEdit(string idxPath, out PendingMulFileEdit? edit)
    {
        return editsByIdxPath.TryGetValue(idxPath, out edit);
    }

    public void AddOrReplaceBodyConvEntry(int bodyId, int fileType, int slotBodyIndex, string? comment)
    {
        pendingBodyConvByBodyId[bodyId] = new PendingBodyConvEntry
        {
            BodyId = bodyId,
            FileType = fileType,
            SlotBodyIndex = slotBodyIndex,
            Comment = comment
        };
    }

    public void AddOrReplaceMobTypeEntry(int bodyId, string mobType, string? comment)
    {
        pendingMobTypesByBodyId[bodyId] = new PendingMobTypeEntry
        {
            BodyId = bodyId,
            MobType = mobType,
            Comment = comment
        };
    }

    public void Clear()
    {
        editsByIdxPath.Clear();
        pendingBodyConvByBodyId.Clear();
        pendingMobTypesByBodyId.Clear();
    }
}