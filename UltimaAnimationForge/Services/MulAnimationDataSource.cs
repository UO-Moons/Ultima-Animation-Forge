using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public class MulAnimationDataSource : IAnimationDataSource
{
    private readonly UoFileDiscoveryService fileDiscoveryService = new();
    private readonly AnimationIdxReader idxReader = new();
    private readonly AnimationMulReader mulReader = new();
    private readonly BodyConvDefService bodyConvDefService = new();
    private readonly MobTypesService mobTypesService = new();
    private string? preferredSourceFileName;
    private List<MulSlotEntry>? cachedMulSlotEntries;
    private bool mulSlotEntriesBuilt;

    private readonly Dictionary<string, List<AnimationIdxEntry>> idxEntriesCache =
        new Dictionary<string, List<AnimationIdxEntry>>(StringComparer.OrdinalIgnoreCase);

    private readonly List<MulFileDefinition> discoveredMulFiles = new();
    private readonly Dictionary<int, MulFileDefinition> standardMulFileByType = new();

    private sealed class MulFileDefinition
    {
        public string BaseName { get; set; } = string.Empty;
        public string MulPath { get; set; } = string.Empty;
        public string IdxPath { get; set; } = string.Empty;
        public int FileType { get; set; }
        public bool UsesLegacyPerBodyLayout { get; set; }
        public int FixedAnimLength { get; set; }
        public int SortOrder { get; set; }
    }

    public void SetPreferredSourceFile(string? sourceFileName)
    {
        preferredSourceFileName = string.IsNullOrWhiteSpace(sourceFileName)
            ? null
            : Path.GetFileName(sourceFileName);
    }

    public string FolderPath { get; private set; } = string.Empty;
    public string SourceMode => "MUL";

    public Dictionary<int, BodyConvEntry> BodyConvEntries { get; private set; } = new();
    public Dictionary<int, MobTypeEntry> MobTypeEntries { get; private set; } = new();

    public bool Initialize(string folderPath)
    {
        FolderPath = folderPath ?? string.Empty;
        idxEntriesCache.Clear();
        discoveredMulFiles.Clear();
        standardMulFileByType.Clear();
        preferredSourceFileName = null;
        cachedMulSlotEntries = null;
        mulSlotEntriesBuilt = false;
        BodyConvEntries = new Dictionary<int, BodyConvEntry>();
        MobTypeEntries = new Dictionary<int, MobTypeEntry>();

        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            return false;
        }

        List<UoAnimationFile> files = fileDiscoveryService.FindAnimationFiles(FolderPath);
        BuildDiscoveredMulFiles(files);

        if (discoveredMulFiles.Count == 0)
        {
            return false;
        }

        string bodyConvPath = Path.Combine(FolderPath, "bodyconv.def");
        string mobTypesPath = Path.Combine(FolderPath, "mobtypes.txt");

        BodyConvEntries = bodyConvDefService.Load(bodyConvPath);
        MobTypeEntries = mobTypesService.Load(mobTypesPath);

        return true;
    }

    public string GetBodyTypeName(int bodyId)
    {
        if (TryGetPrimaryResolvedSource(bodyId, out int resolvedBodyId, out MulFileDefinition? definition))
        {
            int animLength = GetAnimLengthForResolvedSource(definition!, resolvedBodyId);

            return animLength switch
            {
                13 => "ANIMAL",
                22 => "MONSTER",
                35 => "HUMAN",
                _ => GetFallbackBodyTypeName(bodyId)
            };
        }

        return GetFallbackBodyTypeName(bodyId);
    }

    public int GetGroupCountForBody(int bodyId)
    {
        if (TryGetPrimaryResolvedSource(bodyId, out int resolvedBodyId, out MulFileDefinition? definition))
        {
            int animLength = GetAnimLengthForResolvedSource(definition!, resolvedBodyId);
            if (animLength > 0)
            {
                return animLength;
            }
        }

        return GetFallbackGroupCount(bodyId);
    }

    public List<int> GetAvailableActionIndices(int bodyId)
    {
        List<int> actions = new List<int>();
        int count = GetGroupCountForBody(bodyId);

        List<(int resolvedBodyId, MulFileDefinition definition, string debugText)> candidates =
            ResolveBodyAnimationSources(bodyId);

        for (int actionIndex = 0; actionIndex < count; actionIndex++)
        {
            bool foundAnyDirection = false;

            foreach ((int resolvedBodyId, MulFileDefinition definition, string debugText) in candidates)
            {
                int animLength = GetAnimLengthForResolvedSource(definition, resolvedBodyId);
                if (actionIndex >= animLength)
                {
                    continue;
                }

                for (int directionIndex = 0; directionIndex < 5; directionIndex++)
                {
                    if (TryResolveAnimationBlockFromSource(
                        bodyId,
                        actionIndex,
                        directionIndex,
                        resolvedBodyId,
                        definition,
                        debugText,
                        out _))
                    {
                        foundAnyDirection = true;
                        break;
                    }
                }

                if (foundAnyDirection)
                {
                    break;
                }
            }

            if (foundAnyDirection)
            {
                actions.Add(actionIndex);
            }
        }

        return actions;
    }

    private string GetFallbackBodyTypeName(int bodyId)
    {
        if (MobTypeEntries.TryGetValue(bodyId, out MobTypeEntry? mobTypeEntry))
        {
            return mobTypeEntry.TypeName;
        }

        if (bodyId < 200)
        {
            return "MONSTER";
        }

        if (bodyId < 400)
        {
            return "ANIMAL";
        }

        return "HUMAN";
    }

    private int GetFallbackGroupCount(int bodyId)
    {
        string bodyTypeName = GetFallbackBodyTypeName(bodyId);

        return bodyTypeName switch
        {
            "MONSTER" => 22,
            "SEA_MONSTER" => 22,
            "ANIMAL" => 13,
            "HUMAN" => 35,
            "EQUIPMENT" => 35,
            _ => bodyId < 200 ? 22 : bodyId < 400 ? 13 : 35
        };
    }

    private int GetAnimLengthForResolvedSource(MulFileDefinition definition, int resolvedBodyId)
    {
        return GetAnimLengthForFileType(definition.FileType, resolvedBodyId);
    }

    private bool TryGetPrimaryResolvedSource(int bodyId, out int resolvedBodyId, out MulFileDefinition? definition)
    {
        resolvedBodyId = bodyId;
        definition = null;

        List<(int resolvedBodyId, MulFileDefinition definition, string debugText)> candidates =
            ResolveBodyAnimationSources(bodyId);

        if (candidates.Count == 0)
        {
            return false;
        }

        foreach ((int candidateResolvedBodyId, MulFileDefinition candidateDefinition, string candidateDebugText) in candidates)
        {
            if (BodyHasAnyAnimationInSource(candidateResolvedBodyId, candidateDefinition))
            {
                resolvedBodyId = candidateResolvedBodyId;
                definition = candidateDefinition;
                return true;
            }
        }

        return false;
    }

    private bool BodyHasAnyAnimationInSource(int resolvedBodyId, MulFileDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        if (!File.Exists(definition.IdxPath))
        {
            return false;
        }

        List<AnimationIdxEntry> idxEntries = GetCachedIdxEntries(definition.IdxPath);

        int animLength = GetAnimLengthForFileType(definition.FileType, resolvedBodyId);
        int baseIndex = GetBaseIndexForBody(definition.FileType, resolvedBodyId);

        if (animLength <= 0 || baseIndex < 0 || baseIndex >= idxEntries.Count)
        {
            return false;
        }

        int endIndex = Math.Min(baseIndex + (animLength * 5), idxEntries.Count);

        for (int slotIndex = baseIndex; slotIndex < endIndex; slotIndex++)
        {
            AnimationIdxEntry idxEntry = idxEntries[slotIndex];

            if (idxEntry.Offset >= 0 && idxEntry.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    public List<AnimationEntry> BuildAnimationEntries(int maxBodyId)
    {
        List<AnimationEntry> entries = new List<AnimationEntry>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (MulFileDefinition definition in discoveredMulFiles)
        {
            string sourceFileName = Path.GetFileName(definition.MulPath);

            for (int resolvedBodyId = 0; resolvedBodyId < maxBodyId; resolvedBodyId++)
            {
                if (!BodyHasAnyAnimationInSource(resolvedBodyId, definition))
                {
                    continue;
                }

                int bodyId = resolvedBodyId;

                if (definition.FileType >= 2 && definition.FileType <= 5)
                {
                    int trueBodyId = bodyConvDefService.GetTrueBody(definition.FileType, resolvedBodyId);
                    if (trueBodyId >= 0)
                    {
                        bodyId = trueBodyId;
                    }
                }

                string entryKey = bodyId.ToString() + "|" + sourceFileName;
                if (!seen.Add(entryKey))
                {
                    continue;
                }

                int groupCount = GetAnimLengthForResolvedSource(definition, resolvedBodyId);
                if (groupCount <= 0)
                {
                    groupCount = GetFallbackGroupCount(bodyId);
                }

                string displayName = resolvedBodyId != bodyId
                    ? "Body " + resolvedBodyId + " (" + bodyId + ")"
                    : "Body " + bodyId;

                string secondaryText = GetBodyTypeName(bodyId) +
                    (resolvedBodyId != bodyId
                        ? " | True Body " + bodyId + " | MUL | " + sourceFileName
                        : " | MUL | " + sourceFileName);

                int baseIndex = GetBaseIndexForBody(definition.FileType, resolvedBodyId);

                entries.Add(new AnimationEntry
                {
                    DisplayName = displayName,
                    SecondaryText = secondaryText,
                    BodyId = bodyId,
                    ActionId = 0,
                    FrameCount = 0,
                    FrameSize = "-",
                    SourceFile = sourceFileName,
                    SourceMode = "MUL",
                    IndexNumber = baseIndex,
                    Offset = 0,
                    Length = 0,
                    Extra = 0
                });
            }
        }

        return entries;
    }

    public bool TryResolveAnimationBlock(int bodyId, int actionIndex, int directionIndex, out ResolvedAnimationBlock resolvedBlock)
    {
        resolvedBlock = new ResolvedAnimationBlock();

        List<(int resolvedBodyId, MulFileDefinition definition, string debugText)> candidates =
            ResolveBodyAnimationSources(bodyId);

        foreach ((int resolvedBodyId, MulFileDefinition definition, string debugText) in candidates)
        {
            if (TryResolveAnimationBlockFromSource(
                bodyId,
                actionIndex,
                directionIndex,
                resolvedBodyId,
                definition,
                debugText,
                out resolvedBlock))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveAnimationBlockFromSource(
        int originalBodyId,
        int actionIndex,
        int directionIndex,
        int resolvedBodyId,
        MulFileDefinition definition,
        string debugText,
        out ResolvedAnimationBlock resolvedBlock)
    {
        resolvedBlock = new ResolvedAnimationBlock();

        if (definition == null)
        {
            return false;
        }

        if (!File.Exists(definition.IdxPath) || !File.Exists(definition.MulPath))
        {
            return false;
        }

        List<AnimationIdxEntry> idxEntries = GetCachedIdxEntries(definition.IdxPath);

        int animLength = GetAnimLengthForFileType(definition.FileType, resolvedBodyId);
        int baseIndex = GetBaseIndexForBody(definition.FileType, resolvedBodyId);

        if (actionIndex < 0 || actionIndex >= animLength)
        {
            return false;
        }

        int slotIndex = baseIndex + (actionIndex * 5) + directionIndex;

        if (slotIndex < 0 || slotIndex >= idxEntries.Count)
        {
            return false;
        }

        AnimationIdxEntry idxEntry = idxEntries[slotIndex];

        if (idxEntry.Offset < 0 || idxEntry.Length <= 0)
        {
            return false;
        }

        resolvedBlock = new ResolvedAnimationBlock
        {
            BodyId = originalBodyId,
            ResolvedBodyId = resolvedBodyId,
            ActionIndex = actionIndex,
            DirectionIndex = directionIndex,
            SlotIndex = slotIndex,
            Offset = idxEntry.Offset,
            Length = idxEntry.Length,
            Extra = idxEntry.Extra,
            MulPath = definition.MulPath,
            IdxPath = definition.IdxPath,
            SourceFileName = Path.GetFileName(definition.MulPath),
            DebugText = debugText
        };

        return true;
    }

    private List<(int resolvedBodyId, MulFileDefinition definition, string debugText)> ResolveBodyAnimationSources(int bodyId)
    {
        List<(int resolvedBodyId, MulFileDefinition definition, string debugText)> results =
            new List<(int resolvedBodyId, MulFileDefinition definition, string debugText)>();

        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(int candidateBodyId, MulFileDefinition? candidateDefinition, string candidateDebugText)
        {
            if (candidateDefinition == null)
            {
                return;
            }

            string key =
                candidateBodyId.ToString() + "|" +
                candidateDefinition.FileType.ToString() + "|" +
                candidateDefinition.MulPath;

            if (seen.Add(key))
            {
                results.Add((candidateBodyId, candidateDefinition, candidateDebugText));
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredSourceFileName) &&
            !string.Equals(preferredSourceFileName, "All Files", StringComparison.OrdinalIgnoreCase))
        {
            string preferredMulName = preferredSourceFileName!;

            if (preferredMulName.EndsWith(".idx", StringComparison.OrdinalIgnoreCase))
            {
                preferredMulName = Path.ChangeExtension(preferredMulName, ".mul");
            }

            MulFileDefinition? preferredDefinition = discoveredMulFiles.FirstOrDefault(x =>
                string.Equals(Path.GetFileName(x.MulPath), preferredMulName, StringComparison.OrdinalIgnoreCase));

            AddCandidate(bodyId, preferredDefinition, "Preferred file: " + preferredMulName);
        }

        MulFileDefinition? primaryDefinition =
            standardMulFileByType.TryGetValue(1, out MulFileDefinition? animDefinition)
                ? animDefinition
                : discoveredMulFiles.FirstOrDefault();

        AddCandidate(bodyId, primaryDefinition, string.Empty);

        if (BodyConvEntries.TryGetValue(bodyId, out BodyConvEntry? entry))
        {
            int mappedBodyId = entry.NewBodyId;
            int mappedFileType = entry.FileIndex + 1;

            MulFileDefinition? mappedDefinition = null;

            if (!string.IsNullOrWhiteSpace(preferredSourceFileName))
            {
                string preferredMulName = preferredSourceFileName!;

                if (preferredMulName.EndsWith(".idx", StringComparison.OrdinalIgnoreCase))
                {
                    preferredMulName = Path.ChangeExtension(preferredMulName, ".mul");
                }

                mappedDefinition = discoveredMulFiles.FirstOrDefault(x =>
                    x.FileType == mappedFileType &&
                    string.Equals(Path.GetFileName(x.MulPath), preferredMulName, StringComparison.OrdinalIgnoreCase));
            }

            if (mappedDefinition == null &&
                standardMulFileByType.TryGetValue(mappedFileType, out MulFileDefinition? standardDefinition))
            {
                mappedDefinition = standardDefinition;
            }

            if (mappedDefinition == null)
            {
                mappedDefinition = discoveredMulFiles.FirstOrDefault(x => x.FileType == mappedFileType);
            }

            AddCandidate(
                mappedBodyId,
                mappedDefinition,
                "BodyConv: body " + bodyId + " -> " + mappedBodyId + " using " + entry.SourceFileName);
        }

        if (results.Count == 0)
        {
            MulFileDefinition? fallbackDefinition =
                discoveredMulFiles.FirstOrDefault(x => x.FileType == 1) ??
                discoveredMulFiles.FirstOrDefault();

            AddCandidate(bodyId, fallbackDefinition, string.Empty);
        }

        return results;
    }

    public byte[] ReadAnimationBlock(ResolvedAnimationBlock resolvedBlock)
    {
        if (resolvedBlock == null)
        {
            return Array.Empty<byte>();
        }

        return mulReader.ReadBlock(resolvedBlock.MulPath, resolvedBlock.Offset, resolvedBlock.Length);
    }

    public List<MulSlotEntry> GetMulSlotEntries()
    {
        if (mulSlotEntriesBuilt && cachedMulSlotEntries != null)
        {
            return new List<MulSlotEntry>(cachedMulSlotEntries);
        }

        List<MulSlotEntry> results = new List<MulSlotEntry>();

        foreach (MulFileDefinition definition in discoveredMulFiles)
        {
            AddFreeBodyEntries(results, definition);
        }

        cachedMulSlotEntries = results;
        mulSlotEntriesBuilt = true;

        return new List<MulSlotEntry>(cachedMulSlotEntries);
    }

    private void BuildDiscoveredMulFiles(List<UoAnimationFile> files)
    {
        Dictionary<string, string> mulPathsByBaseName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> idxPathsByBaseName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (UoAnimationFile file in files)
        {
            if (!string.Equals(file.Type, "MUL", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string extension = Path.GetExtension(file.FileName);
            string baseName = Path.GetFileNameWithoutExtension(file.FileName);

            if (string.Equals(extension, ".mul", StringComparison.OrdinalIgnoreCase))
            {
                mulPathsByBaseName[baseName] = file.FullPath;
            }
            else if (string.Equals(extension, ".idx", StringComparison.OrdinalIgnoreCase))
            {
                idxPathsByBaseName[baseName] = file.FullPath;
            }
        }

        foreach (KeyValuePair<string, string> pair in mulPathsByBaseName)
        {
            if (!idxPathsByBaseName.TryGetValue(pair.Key, out string? idxPath))
            {
                continue;
            }

            int fileType = ParseAnimFileType(pair.Key);

            bool usesLegacy = fileType >= 1;
            int fixedAnimLength = 0;

            MulFileDefinition definition = new MulFileDefinition
            {
                BaseName = pair.Key,
                MulPath = pair.Value,
                IdxPath = idxPath,
                FileType = fileType,
                UsesLegacyPerBodyLayout = usesLegacy,
                FixedAnimLength = fixedAnimLength,
                SortOrder = GetMulFileSortOrder(pair.Key, fileType)
            };

            discoveredMulFiles.Add(definition);

            if (fileType >= 1)
            {
                standardMulFileByType[fileType] = definition;
            }
        }

        discoveredMulFiles.Sort((left, right) =>
        {
            int sortCompare = left.SortOrder.CompareTo(right.SortOrder);
            if (sortCompare != 0)
            {
                return sortCompare;
            }

            return string.Compare(left.BaseName, right.BaseName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private List<AnimationIdxEntry> GetCachedIdxEntries(string idxPath)
    {
        if (idxEntriesCache.TryGetValue(idxPath, out List<AnimationIdxEntry>? cachedEntries))
        {
            return cachedEntries;
        }

        List<AnimationIdxEntry> loadedEntries = idxReader.Read(idxPath);
        idxEntriesCache[idxPath] = loadedEntries;
        return loadedEntries;
    }

    private void AddFreeBodyEntries(List<MulSlotEntry> results, MulFileDefinition definition)
    {
        if (!File.Exists(definition.IdxPath))
        {
            return;
        }

        List<AnimationIdxEntry> idxEntries = GetCachedIdxEntries(definition.IdxPath);
        if (idxEntries.Count == 0)
        {
            return;
        }

        string fileName = Path.GetFileName(definition.IdxPath);

        for (int bodyIndex = 0; bodyIndex <= 64000; bodyIndex++)
        {
            int animLength = GetAnimLengthForFileType(definition.FileType, bodyIndex);
            int baseIndex = GetBaseIndexForBody(definition.FileType, bodyIndex);

            if (baseIndex >= idxEntries.Count)
            {
                break;
            }

            bool hasAnyDefinedAction = false;

            for (int actionIndex = 0; actionIndex < animLength && !hasAnyDefinedAction; actionIndex++)
            {
                for (int directionIndex = 0; directionIndex < 5; directionIndex++)
                {
                    int slotIndex = baseIndex + (actionIndex * 5) + directionIndex;

                    if (slotIndex < 0 || slotIndex >= idxEntries.Count)
                    {
                        break;
                    }

                    AnimationIdxEntry idxEntry = idxEntries[slotIndex];
                    if (idxEntry.Offset >= 0 && idxEntry.Length > 0)
                    {
                        hasAnyDefinedAction = true;
                        break;
                    }
                }
            }

            if (hasAnyDefinedAction)
            {
                continue;
            }

            int trueBodyId = definition.FileType >= 1 && definition.FileType <= 5
                ? bodyConvDefService.GetTrueBody(definition.FileType, bodyIndex)
                : bodyIndex;

            results.Add(new MulSlotEntry
            {
                FileName = fileName,
                FileType = definition.FileType,
                BodyIndex = bodyIndex,
                TrueBodyId = trueBodyId >= 0 ? trueBodyId : bodyIndex,
                AnimLength = animLength,
                IsEmpty = true
            });
        }
    }

    private int ParseAnimFileType(string baseName)
    {
        if (string.Equals(baseName, "anim", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (baseName.StartsWith("anim", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(baseName.Substring(4), out int number) &&
            number > 0)
        {
            return number;
        }

        return 1000 + Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(baseName));
    }

    private int GetMulFileSortOrder(string baseName, int fileType)
    {
        if (string.Equals(baseName, "anim", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (baseName.StartsWith("anim", StringComparison.OrdinalIgnoreCase) && fileType > 1 && fileType < 1000)
        {
            return fileType;
        }

        return 10000 + Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(baseName));
    }

    private int GetBaseIndexForBody(int fileType, int bodyIndex)
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

            _ => bodyIndex < 200
                ? bodyIndex * 110
                : bodyIndex < 400
                    ? 22000 + ((bodyIndex - 200) * 65)
                    : 35000 + ((bodyIndex - 400) * 175)
        };
    }

    private int GetAnimLengthForFileType(int fileType, int bodyIndex)
    {
        return fileType switch
        {
            2 => bodyIndex < 200 ? 22 : 13,
            3 => bodyIndex < 300 ? 13 : bodyIndex < 400 ? 22 : 35,
            4 => bodyIndex < 200 ? 22 : bodyIndex < 400 ? 13 : 35,
            5 => bodyIndex < 200 ? 22 : bodyIndex < 400 ? 13 : 35,
            _ => bodyIndex < 200 ? 22 : bodyIndex < 400 ? 13 : 35
        };
    }
}