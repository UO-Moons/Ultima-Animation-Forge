using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class VdFolderAnimationDataSource : IAnimationDataSource
{
    private sealed class VdFileEntry
    {
        public int BodyId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public short Version { get; set; }
        public short AnimType { get; set; }
        public int ActionCount { get; set; }
        public VdIndexEntry[,] IndexEntries { get; set; } = new VdIndexEntry[0, 0];
    }

    private struct VdIndexEntry
    {
        public int Lookup;
        public int Length;
        public int Extra;
    }

    private readonly Dictionary<int, VdFileEntry> entriesByBodyId = new();

    public string FolderPath { get; private set; } = string.Empty;
    public string SourceMode => "VD";

    public Dictionary<int, BodyConvEntry> BodyConvEntries { get; } = new();
    public Dictionary<int, MobTypeEntry> MobTypeEntries { get; } = new();

    public bool Initialize(string folderPath)
    {
        FolderPath = folderPath ?? string.Empty;
        entriesByBodyId.Clear();

        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            return false;
        }

        string[] vdFiles = Directory.GetFiles(FolderPath, "*.vd", SearchOption.TopDirectoryOnly);

        int bodyId = 900000;

        foreach (string vdPath in vdFiles.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (TryReadVdHeader(vdPath, bodyId, out VdFileEntry? entry) && entry != null)
            {
                entriesByBodyId[bodyId] = entry;
                bodyId++;
            }
        }

        return entriesByBodyId.Count > 0;
    }

    public string GetBodyTypeName(int bodyId)
    {
        if (!entriesByBodyId.TryGetValue(bodyId, out VdFileEntry? entry))
        {
            return "VD";
        }

        return entry.AnimType switch
        {
            0 => "MONSTER",
            1 => "ANIMAL",
            2 => "HUMAN",
            _ => "VD"
        };
    }

    public int GetGroupCountForBody(int bodyId)
    {
        return entriesByBodyId.TryGetValue(bodyId, out VdFileEntry? entry)
            ? entry.ActionCount
            : 0;
    }

    public List<int> GetAvailableActionIndices(int bodyId)
    {
        List<int> actions = new();

        if (!entriesByBodyId.TryGetValue(bodyId, out VdFileEntry? entry))
        {
            return actions;
        }

        for (int action = 0; action < entry.ActionCount; action++)
        {
            for (int direction = 0; direction < 5; direction++)
            {
                VdIndexEntry index = entry.IndexEntries[action, direction];

                if (index.Lookup > 0 && index.Length > 0)
                {
                    actions.Add(action);
                    break;
                }
            }
        }

        return actions;
    }

    public List<AnimationEntry> BuildAnimationEntries(int maxBodyId)
    {
        List<AnimationEntry> results = new();

        foreach (VdFileEntry entry in entriesByBodyId.Values.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase))
        {
            results.Add(new AnimationEntry
            {
                DisplayName = Path.GetFileNameWithoutExtension(entry.FileName),
                SecondaryText = GetBodyTypeName(entry.BodyId) + " | VD | " + entry.FileName,
                BodyId = entry.BodyId,
                ActionId = 0,
                FrameCount = 0,
                FrameSize = "-",
                SourceFile = entry.FileName,
                SourceMode = "VD",
                IndexNumber = 0,
                Offset = 0,
                Length = 0,
                Extra = 0
            });
        }

        return results;
    }

    public bool TryResolveAnimationBlock(
        int bodyId,
        int actionIndex,
        int directionIndex,
        out ResolvedAnimationBlock resolvedBlock)
    {
        resolvedBlock = new ResolvedAnimationBlock();

        if (!entriesByBodyId.TryGetValue(bodyId, out VdFileEntry? entry))
        {
            return false;
        }

        if (actionIndex < 0 || actionIndex >= entry.ActionCount)
        {
            return false;
        }

        if (directionIndex < 0 || directionIndex > 4)
        {
            return false;
        }

        VdIndexEntry index = entry.IndexEntries[actionIndex, directionIndex];

        if (index.Lookup <= 0 || index.Length <= 0)
        {
            return false;
        }

        resolvedBlock = new ResolvedAnimationBlock
        {
            BodyId = bodyId,
            ResolvedBodyId = bodyId,
            ActionIndex = actionIndex,
            DirectionIndex = directionIndex,
            SlotIndex = (actionIndex * 5) + directionIndex,
            Offset = index.Lookup,
            Length = index.Length,
            Extra = index.Extra,
            MulPath = entry.FilePath,
            IdxPath = string.Empty,
            SourceFileName = entry.FileName,
            DebugText =
                "VD file: " + entry.FileName +
                " | Version: " + entry.Version +
                " | AnimType: " + entry.AnimType +
                " | ActionCount: " + entry.ActionCount,
            IsUop = false
        };

        return true;
    }

    public byte[] ReadAnimationBlock(ResolvedAnimationBlock resolvedBlock)
    {
        if (resolvedBlock == null ||
            string.IsNullOrWhiteSpace(resolvedBlock.MulPath) ||
            !File.Exists(resolvedBlock.MulPath) ||
            resolvedBlock.Offset <= 0 ||
            resolvedBlock.Length <= 0)
        {
            return Array.Empty<byte>();
        }

        using FileStream stream = new(resolvedBlock.MulPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader reader = new(stream);

        if (resolvedBlock.Offset >= stream.Length)
        {
            return Array.Empty<byte>();
        }

        stream.Seek(resolvedBlock.Offset, SeekOrigin.Begin);

        int safeLength = resolvedBlock.Length;
        long remaining = stream.Length - resolvedBlock.Offset;

        if (safeLength > remaining)
        {
            safeLength = (int)remaining;
        }

        return reader.ReadBytes(safeLength);
    }

    private static bool TryReadVdHeader(string vdPath, int bodyId, out VdFileEntry? entry)
    {
        entry = null;

        try
        {
            using FileStream stream = File.OpenRead(vdPath);
            using BinaryReader reader = new(stream);

            if (stream.Length < 4)
            {
                return false;
            }

            short version = reader.ReadInt16();
            short animType = reader.ReadInt16();

            if (version != 6)
            {
                return false;
            }

            int actionCount = GetActionCountFromAnimType(animType);
            if (actionCount <= 0)
            {
                return false;
            }

            long tableBytes = actionCount * 5L * 12L;
            if (stream.Length < 4 + tableBytes)
            {
                return false;
            }

            VdIndexEntry[,] indexes = new VdIndexEntry[actionCount, 5];

            for (int action = 0; action < actionCount; action++)
            {
                for (int direction = 0; direction < 5; direction++)
                {
                    indexes[action, direction] = new VdIndexEntry
                    {
                        Lookup = reader.ReadInt32(),
                        Length = reader.ReadInt32(),
                        Extra = reader.ReadInt32()
                    };
                }
            }

            entry = new VdFileEntry
            {
                BodyId = bodyId,
                FilePath = vdPath,
                FileName = Path.GetFileName(vdPath),
                Version = version,
                AnimType = animType,
                ActionCount = actionCount,
                IndexEntries = indexes
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int GetActionCountFromAnimType(short animType)
    {
        return animType switch
        {
            0 => 22,
            1 => 13,
            2 => 35,
            _ => 0
        };
    }
}