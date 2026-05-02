using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public class UopAnimationDataSource : IAnimationDataSource
{
    private readonly BodyConvDefService bodyConvDefService = new();
    private readonly MobTypesService mobTypesService = new();

    private readonly List<UopFileReader> animationFrameReaders = new();
    private readonly Dictionary<int, Dictionary<int, UopGroupLocation>> groupLookupByBody =
        new Dictionary<int, Dictionary<int, UopGroupLocation>>();

    private readonly Dictionary<int, Dictionary<int, int>> actionRemapByBody =
        new Dictionary<int, Dictionary<int, int>>();

    private readonly Dictionary<int, Dictionary<int, int>> frameCountByBodyAndGroup =
        new Dictionary<int, Dictionary<int, int>>();

    private UopFileReader? animationSequenceReader;

    public string FolderPath { get; private set; } = string.Empty;
    public string SourceMode => "UOP";

    public Dictionary<int, BodyConvEntry> BodyConvEntries { get; private set; } = new();
    public Dictionary<int, MobTypeEntry> MobTypeEntries { get; private set; } = new();

    private readonly Dictionary<int, Dictionary<int, List<UopGroupLocation>>> allGroupLocationsByBody =
    new Dictionary<int, Dictionary<int, List<UopGroupLocation>>>();

    private readonly HashSet<int> fallbackScannedBodies = new HashSet<int>();

    private string? preferredSourceFileName;

    private void DebugFrame6(string message)
    {
        Console.WriteLine("[FRAME6] " + message);
    }

    public bool Initialize(string folderPath)
    {
        FolderPath = folderPath ?? string.Empty;

        animationFrameReaders.Clear();
        groupLookupByBody.Clear();
        allGroupLocationsByBody.Clear();
        fallbackScannedBodies.Clear();
        actionRemapByBody.Clear();
        frameCountByBodyAndGroup.Clear();
        animationSequenceReader = null;
        preferredSourceFileName = null;

        BodyConvEntries = new Dictionary<int, BodyConvEntry>();
        MobTypeEntries = new Dictionary<int, MobTypeEntry>();

        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            return false;
        }

        string bodyConvPath = Path.Combine(FolderPath, "bodyconv.def");
        string mobTypesPath = Path.Combine(FolderPath, "mobtypes.txt");

        BodyConvEntries = bodyConvDefService.Load(bodyConvPath);
        MobTypeEntries = mobTypesService.Load(mobTypesPath);

        bool foundAnyFrameReaders = false;

        for (int fileIndex = 1; fileIndex <= 15; fileIndex++)
        {
            string fileName = "AnimationFrame" + fileIndex + ".uop";
            string fullPath = Path.Combine(FolderPath, fileName);

            if (!File.Exists(fullPath))
            {
                continue;
            }

            try
            {
                UopFileReader reader = new UopFileReader(fullPath);

                if (reader.Load())
                {
                    animationFrameReaders.Add(reader);
                    foundAnyFrameReaders = true;
                    Console.WriteLine("Loaded UOP animation frame reader: " + fullPath);
                }
                else
                {
                    Console.WriteLine("Failed to load UOP animation frame reader: " + fullPath);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Exception loading UOP animation frame reader '" + fullPath + "': " + exception.Message);
            }
        }

        string sequencePath = Path.Combine(FolderPath, "AnimationSequence.uop");

        if (File.Exists(sequencePath))
        {
            try
            {
                UopFileReader reader = new UopFileReader(sequencePath);

                if (reader.Load())
                {
                    animationSequenceReader = reader;
                    Console.WriteLine("Loaded UOP animation sequence reader: " + sequencePath);
                }
                else
                {
                    Console.WriteLine("Failed to load UOP animation sequence reader: " + sequencePath);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Exception loading UOP animation sequence reader '" + sequencePath + "': " + exception.Message);
            }
        }

        if (!foundAnyFrameReaders)
        {
            Console.WriteLine("UOP Initialize complete. No AnimationFrame#.uop files loaded.");
            return false;
        }

        ParseAnimationSequence();

        Console.WriteLine(
            "UOP Initialize complete. Loaded readers: " + animationFrameReaders.Count +
            ", sequence loaded: " + (animationSequenceReader != null));

        return true;
    }

    private void BuildFullBaseGroupLookup()
    {
        groupLookupByBody.Clear();
        allGroupLocationsByBody.Clear();
        fallbackScannedBodies.Clear();

        for (int bodyId = 0; bodyId < 65536; bodyId++)
        {
            for (int groupIndex = 0; groupIndex < 200; groupIndex++)
            {
                TryAddGroupLocation(bodyId, groupIndex);
            }
        }
    }

    public void SetPreferredSourceFile(string? sourceFileName)
    {
        preferredSourceFileName = string.IsNullOrWhiteSpace(sourceFileName)
            ? null
            : Path.GetFileName(sourceFileName);
    }

    public string GetBodyTypeName(int bodyId)
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

    public int GetGroupCountForBody(int bodyId)
    {
        string bodyTypeName = GetBodyTypeName(bodyId);

        switch (bodyTypeName)
        {
            case "MONSTER":
            case "SEA_MONSTER":
                return 22;

            case "ANIMAL":
                return 13;

            case "HUMAN":
            case "EQUIPMENT":
                return 35;
        }

        if (bodyId < 200)
        {
            return 22;
        }

        if (bodyId < 400)
        {
            return 13;
        }

        return 35;
    }

    public List<AnimationEntry> BuildAnimationEntries(int maxBodyId)
    {
        List<AnimationEntry> entries = new List<AnimationEntry>();

        // First discover raw UOP bodies directly.
        for (int bodyId = 0; bodyId < maxBodyId; bodyId++)
        {
            EnsureFallbackGroupsForBody(bodyId);
        }

        foreach (int bodyId in allGroupLocationsByBody.Keys.OrderBy(x => x))
        {
            if (bodyId < 0 || bodyId >= maxBodyId)
            {
                continue;
            }

            if (!allGroupLocationsByBody.TryGetValue(bodyId, out Dictionary<int, List<UopGroupLocation>>? allGroupMap))
            {
                continue;
            }

            if (allGroupMap.Count == 0)
            {
                continue;
            }

            Dictionary<string, Dictionary<int, UopGroupLocation>> groupsByFile =
                new Dictionary<string, Dictionary<int, UopGroupLocation>>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<int, List<UopGroupLocation>> pair in allGroupMap)
            {
                int groupIndex = pair.Key;

                foreach (UopGroupLocation location in pair.Value)
                {
                    if (location.Reader == null || string.IsNullOrWhiteSpace(location.Reader.FilePath))
                    {
                        continue;
                    }

                    string fileName = Path.GetFileName(location.Reader.FilePath);

                    if (!groupsByFile.TryGetValue(fileName, out Dictionary<int, UopGroupLocation>? fileGroupMap))
                    {
                        fileGroupMap = new Dictionary<int, UopGroupLocation>();
                        groupsByFile[fileName] = fileGroupMap;
                    }

                    if (!fileGroupMap.TryGetValue(groupIndex, out UopGroupLocation? existing) ||
                        location.ReaderIndex >= existing.ReaderIndex)
                    {
                        fileGroupMap[groupIndex] = location;
                    }
                }
            }

            foreach (KeyValuePair<string, Dictionary<int, UopGroupLocation>> filePair in
                     groupsByFile.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                string fileName = filePair.Key;
                Dictionary<int, UopGroupLocation> fileGroupMap = filePair.Value;

                if (fileGroupMap.Count == 0)
                {
                    continue;
                }

                int displayAction = fileGroupMap.Keys.Min();
                int displayGroup = displayAction;

                int actionCount = GetGroupCountForBody(bodyId);

                for (int actionIndex = 0; actionIndex < actionCount; actionIndex++)
                {
                    int resolvedGroupIndex = ResolveRequestedActionToUopGroup(bodyId, actionIndex);

                    if (fileGroupMap.ContainsKey(resolvedGroupIndex))
                    {
                        displayAction = actionIndex;
                        displayGroup = resolvedGroupIndex;
                        break;
                    }
                }

                UopGroupLocation location = fileGroupMap[displayGroup];

                string secondaryText = GetBodyTypeName(bodyId) + " | " + fileName;

                entries.Add(new AnimationEntry
                {
                    DisplayName = "Body " + bodyId,
                    SecondaryText = secondaryText + " | UOP",
                    BodyId = bodyId,
                    ActionId = displayAction,
                    FrameCount = 0,
                    FrameSize = "-",
                    SourceFile = fileName,
                    SourceMode = "UOP",
                    IndexNumber = displayGroup,
                    Offset = (int)location.Header.Offset,
                    Length = (int)location.Header.DecompressedSize,
                    Extra = (int)location.Header.HeaderSize
                });
            }
        }

        int frame6EntryCount = entries.Count(x =>
            string.Equals(x.SourceFile, "AnimationFrame6.uop", StringComparison.OrdinalIgnoreCase));

        DebugFrame6("BUILD COMPLETE totalEntries=" + entries.Count + " frame6Entries=" + frame6EntryCount);

        return entries;
    }

    private void EnsureFallbackGroupsForBody(int bodyId)
    {
        if (bodyId < 0)
        {
            return;
        }

        if (!fallbackScannedBodies.Add(bodyId))
        {
            return;
        }

        for (int groupIndex = 0; groupIndex < 200; groupIndex++)
        {
            bool alreadyKnown =
                allGroupLocationsByBody.TryGetValue(bodyId, out Dictionary<int, List<UopGroupLocation>>? allGroupMap) &&
                allGroupMap.ContainsKey(groupIndex);

            if (alreadyKnown)
            {
                continue;
            }

            TryAddGroupLocation(bodyId, groupIndex);
        }
    }

    public List<int> GetAvailableActionIndices(int bodyId)
    {
        HashSet<int> availableActions = new HashSet<int>();

        Dictionary<int, UopGroupLocation>? groupMap = null;
        groupLookupByBody.TryGetValue(bodyId, out groupMap);

        if (groupMap != null)
        {
            foreach (int groupIndex in groupMap.Keys)
            {
                availableActions.Add(groupIndex);
            }
        }

        if (groupMap != null && actionRemapByBody.TryGetValue(bodyId, out Dictionary<int, int>? remaps))
        {
            foreach (KeyValuePair<int, int> pair in remaps)
            {
                if (groupMap.ContainsKey(pair.Value))
                {
                    availableActions.Add(pair.Key);
                }
            }
        }

        List<int> sortedActions = new List<int>(availableActions);
        sortedActions.Sort();
        return sortedActions;
    }

    public bool TryResolveAnimationBlock(int bodyId, int actionIndex, int directionIndex, out ResolvedAnimationBlock resolvedBlock)
    {
        resolvedBlock = new ResolvedAnimationBlock();

        if (directionIndex < 0 || directionIndex > 4)
        {
            return false;
        }

        int resolvedBodyId = bodyId;
        string debugText = string.Empty;

        int resolvedGroupIndex = ResolveRequestedActionToUopGroup(resolvedBodyId, actionIndex);

        bool usedSequenceRemap = resolvedGroupIndex != actionIndex;
        int remapTargetGroupIndex = usedSequenceRemap ? resolvedGroupIndex : -1;

        if (!TryGetGroupLocation(resolvedBodyId, resolvedGroupIndex, out UopGroupLocation? location))
        {
            if (bodyId < 5)
            {
                Console.WriteLine(
                    "UOP MISS body=" + bodyId +
                    " resolvedBody=" + resolvedBodyId +
                    " action=" + actionIndex +
                    " direction=" + directionIndex +
                    " resolvedGroup=" + resolvedGroupIndex);
            }

            return false;
        }

        if (location == null || location.Reader == null || string.IsNullOrWhiteSpace(location.Reader.FilePath))
        {
            return false;
        }

        string resolvedFileName = Path.GetFileName(location.Reader.FilePath);

        if (string.Equals(preferredSourceFileName, "AnimationFrame6.uop", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(resolvedFileName, "AnimationFrame6.uop", StringComparison.OrdinalIgnoreCase))
        {
            DebugFrame6(
                "RESOLVE body=" + bodyId +
                " resolvedBody=" + resolvedBodyId +
                " action=" + actionIndex +
                " dir=" + directionIndex +
                " resolvedGroup=" + resolvedGroupIndex +
                " preferred=" + (preferredSourceFileName ?? "(null)") +
                " chosen=" + resolvedFileName);
        }

        int knownFrameCount = GetKnownFrameCount(resolvedBodyId, resolvedGroupIndex);

        resolvedBlock = new ResolvedAnimationBlock
        {
            BodyId = bodyId,
            ResolvedBodyId = resolvedBodyId,
            ActionIndex = actionIndex,
            DirectionIndex = directionIndex,
            SlotIndex = resolvedGroupIndex,
            Offset = (int)location.Header.Offset,
            Length = (int)location.Header.DecompressedSize,
            Extra = (int)location.Header.HeaderSize,
            MulPath = string.Empty,
            IdxPath = string.Empty,
            SourceFileName = Path.GetFileName(location.Reader.FilePath),
            DebugText =
                (string.IsNullOrWhiteSpace(debugText) ? string.Empty : debugText + " | ") +
                "RequestedAction=" + actionIndex +
                " | ResolvedUopGroup=" + resolvedGroupIndex +
                (usedSequenceRemap ? " | SequenceRemap=" + actionIndex + "->" + resolvedGroupIndex : string.Empty) +
                " | UOP hash path: " + location.VirtualPath +
                " | HeaderSize=" + location.Header.HeaderSize +
                " | DecompressedSize=" + location.Header.DecompressedSize +
                " | Flag=" + location.Header.Flag +
                (knownFrameCount > 0 ? " | SequenceFrameCount=" + knownFrameCount : string.Empty),
            IsUop = true,
            UopFileIndex = location.ReaderIndex,
            UopVirtualPath = location.VirtualPath,

            RequestedActionIndex = actionIndex,
            ResolvedUopGroupIndex = resolvedGroupIndex,
            SequenceFrameCount = knownFrameCount,
            UsedSequenceRemap = usedSequenceRemap,
            RemapTargetGroupIndex = remapTargetGroupIndex
        };

        if (bodyId < 5)
        {
            Console.WriteLine(
                "UOP MATCH body=" + bodyId +
                " resolvedBody=" + resolvedBodyId +
                " action=" + actionIndex +
                " direction=" + directionIndex +
                " resolvedGroup=" + resolvedGroupIndex +
                " reader=" + Path.GetFileName(location.Reader.FilePath) +
                " hashPath=" + location.VirtualPath);
        }

        return true;
    }
    public byte[] ReadAnimationBlock(ResolvedAnimationBlock resolvedBlock)
    {
        if (resolvedBlock == null || !resolvedBlock.IsUop)
        {
            return Array.Empty<byte>();
        }

        if (resolvedBlock.UopFileIndex < 0 || resolvedBlock.UopFileIndex >= animationFrameReaders.Count)
        {
            return Array.Empty<byte>();
        }

        if (string.IsNullOrWhiteSpace(resolvedBlock.UopVirtualPath))
        {
            return Array.Empty<byte>();
        }

        UopFileReader? reader = animationFrameReaders[resolvedBlock.UopFileIndex];

        if (reader == null || string.IsNullOrWhiteSpace(reader.FilePath))
        {
            return Array.Empty<byte>();
        }

        ulong hash = UopFileReader.CreateHash(resolvedBlock.UopVirtualPath);
        UopDataHeader? header = reader.GetEntryByHash(hash);

        if (!header.HasValue)
        {
            return Array.Empty<byte>();
        }

        byte[]? data = reader.ReadData(header.Value);
        return data ?? Array.Empty<byte>();
    }

    private static readonly string[] CandidatePrefixes =
    {
        "build/animationlegacyframe/",
        "build/animationframe/",
        "build/animation/"
    };

    private bool TryAddGroupLocation(int bodyId, int groupIndex)
    {
        bool foundAny = false;
        UopGroupLocation? bestLocation = null;

        foreach (string prefix in CandidatePrefixes)
        {
            string virtualPath =
                (prefix +
                 bodyId.ToString("D6") +
                 "/" +
                 groupIndex.ToString("D2") +
                 ".bin").ToLowerInvariant();

            ulong hash = UopFileReader.CreateHash(virtualPath);

            for (int readerIndex = 0; readerIndex < animationFrameReaders.Count; readerIndex++)
            {
                UopFileReader? reader = animationFrameReaders[readerIndex];

                if (reader == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(reader.FilePath))
                {
                    continue;
                }

                UopDataHeader? header = reader.GetEntryByHash(hash);

                if (!header.HasValue)
                {
                    continue;
                }

                string hitFileName = Path.GetFileName(reader.FilePath);

                if (string.Equals(hitFileName, "AnimationFrame6.uop", StringComparison.OrdinalIgnoreCase))
                {
                    DebugFrame6(
                        "HASH HIT body=" + bodyId +
                        " group=" + groupIndex +
                        " readerIndex=" + readerIndex +
                        " virtualPath=" + virtualPath +
                        " offset=" + header.Value.Offset +
                        " decomp=" + header.Value.DecompressedSize);
                }

                foundAny = true;

                UopGroupLocation location = new UopGroupLocation
                {
                    Reader = reader,
                    ReaderIndex = readerIndex,
                    Header = header.Value,
                    VirtualPath = virtualPath
                };

                if (!allGroupLocationsByBody.TryGetValue(bodyId, out Dictionary<int, List<UopGroupLocation>>? allGroupMap))
                {
                    allGroupMap = new Dictionary<int, List<UopGroupLocation>>();
                    allGroupLocationsByBody[bodyId] = allGroupMap;
                }

                if (!allGroupMap.TryGetValue(groupIndex, out List<UopGroupLocation>? locationList))
                {
                    locationList = new List<UopGroupLocation>();
                    allGroupMap[groupIndex] = locationList;
                }

                int existingIndex = locationList.FindIndex(x =>
                    x.ReaderIndex == location.ReaderIndex &&
                    string.Equals(x.VirtualPath, location.VirtualPath, StringComparison.OrdinalIgnoreCase));

                if (existingIndex >= 0)
                {
                    locationList[existingIndex] = location;
                }
                else
                {
                    locationList.Add(location);
                }

                if (bestLocation == null || location.ReaderIndex >= bestLocation.ReaderIndex)
                {
                    bestLocation = location;
                }
            }
        }

        if (!foundAny || bestLocation == null)
        {
            return false;
        }

        if (!groupLookupByBody.TryGetValue(bodyId, out Dictionary<int, UopGroupLocation>? groupMap))
        {
            groupMap = new Dictionary<int, UopGroupLocation>();
            groupLookupByBody[bodyId] = groupMap;
        }

        string bestFileName = Path.GetFileName(bestLocation.Reader.FilePath);

        if (string.Equals(bestFileName, "AnimationFrame6.uop", StringComparison.OrdinalIgnoreCase))
        {
            DebugFrame6(
                "BEST LOCATION body=" + bodyId +
                " group=" + groupIndex +
                " chosenFile=" + bestFileName +
                " readerIndex=" + bestLocation.ReaderIndex +
                " virtualPath=" + bestLocation.VirtualPath);
        }

        groupMap[groupIndex] = bestLocation;
        return true;
    }

    private void ParseAnimationSequence()
    {
        actionRemapByBody.Clear();
        frameCountByBodyAndGroup.Clear();

        if (animationSequenceReader == null || !animationSequenceReader.IsLoaded)
        {
            return;
        }

        Dictionary<ulong, UopDataHeader> allEntries = animationSequenceReader.GetAllEntries();

        foreach (KeyValuePair<ulong, UopDataHeader> pair in allEntries)
        {
            byte[]? entryData = animationSequenceReader.ReadData(pair.Value);

            if (entryData == null || entryData.Length < 56)
            {
                continue;
            }

            try
            {
                ParseAnimationSequenceEntry(entryData);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Failed to parse AnimationSequence entry: " + exception.Message);
            }
        }
    }

    private void ParseAnimationSequenceEntry(byte[] entryData)
    {
        using MemoryStream memoryStream = new MemoryStream(entryData, false);
        using BinaryReader reader = new BinaryReader(memoryStream);

        if (reader.BaseStream.Length < 56)
        {
            return;
        }

        uint animationIdValue = reader.ReadUInt32();

        if (animationIdValue > int.MaxValue)
        {
            return;
        }

        int animationId = (int)animationIdValue;

        reader.BaseStream.Seek(48, SeekOrigin.Current);

        if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
        {
            return;
        }

        uint entryCount = reader.ReadUInt32();

        if (!actionRemapByBody.TryGetValue(animationId, out Dictionary<int, int>? remaps))
        {
            remaps = new Dictionary<int, int>();
            actionRemapByBody[animationId] = remaps;
        }
        else
        {
            remaps.Clear();
        }

        if (!frameCountByBodyAndGroup.TryGetValue(animationId, out Dictionary<int, int>? groupFrameCounts))
        {
            groupFrameCounts = new Dictionary<int, int>();
            frameCountByBodyAndGroup[animationId] = groupFrameCounts;
        }
        else
        {
            groupFrameCounts.Clear();
        }

        for (uint index = 0; index < entryCount; index++)
        {
            if (reader.BaseStream.Position + 72 > reader.BaseStream.Length)
            {
                break;
            }

            uint uopGroupIndexValue = reader.ReadUInt32();
            int frameCount = reader.ReadInt32();
            uint mulGroupIndexValue = reader.ReadUInt32();
            float speed = reader.ReadSingle();
            byte[] extraData = reader.ReadBytes(56);

            _ = speed;
            _ = extraData;

            if (uopGroupIndexValue >= 200)
            {
                continue;
            }

            int uopGroupIndex = (int)uopGroupIndexValue;

            if (frameCount == 0)
            {
                if (mulGroupIndexValue < 200)
                {
                    remaps[uopGroupIndex] = (int)mulGroupIndexValue;
                }
            }
            else if (frameCount > 0)
            {
                groupFrameCounts[uopGroupIndex] = frameCount;
            }
        }
    }

    private int ResolveRequestedActionToUopGroup(int resolvedBodyId, int actionIndex)
    {
        if (actionRemapByBody.TryGetValue(resolvedBodyId, out Dictionary<int, int>? remaps))
        {
            if (remaps.TryGetValue(actionIndex, out int mappedGroupIndex))
            {
                return mappedGroupIndex;
            }
        }

        return actionIndex;
    }

    private bool TryGetGroupLocation(int resolvedBodyId, int resolvedGroupIndex, out UopGroupLocation? location)
    {
        location = null;

        if (!groupLookupByBody.TryGetValue(resolvedBodyId, out Dictionary<int, UopGroupLocation>? groupMap))
        {
            return false;
        }

        if (!groupMap.TryGetValue(resolvedGroupIndex, out UopGroupLocation? foundLocation))
        {
            return false;
        }

        location = foundLocation;
        return true;
    }

    private int GetKnownFrameCount(int resolvedBodyId, int resolvedGroupIndex)
    {
        if (frameCountByBodyAndGroup.TryGetValue(resolvedBodyId, out Dictionary<int, int>? frameCounts))
        {
            if (frameCounts.TryGetValue(resolvedGroupIndex, out int frameCount))
            {
                return frameCount;
            }
        }

        return 0;
    }

    private sealed class UopGroupLocation
    {
        public UopFileReader Reader { get; set; } = null!;
        public int ReaderIndex { get; set; }
        public UopDataHeader Header { get; set; }
        public string VirtualPath { get; set; } = string.Empty;
    }

    public List<int> GetAvailableActionIndicesForSourceFile(int bodyId, string? sourceFileName)
    {
        HashSet<int> availableActions = new HashSet<int>();

        int resolvedBodyId = bodyId;

        string? preferredFileName = string.IsNullOrWhiteSpace(sourceFileName)
            ? null
            : Path.GetFileName(sourceFileName);

        if (!string.IsNullOrWhiteSpace(preferredFileName) &&
            allGroupLocationsByBody.TryGetValue(resolvedBodyId, out Dictionary<int, List<UopGroupLocation>>? allGroupMap))
        {
            foreach (KeyValuePair<int, List<UopGroupLocation>> pair in allGroupMap)
            {
                int groupIndex = pair.Key;

                bool fileHasGroup = pair.Value.Any(x =>
                    string.Equals(
                        Path.GetFileName(x.Reader.FilePath),
                        preferredFileName,
                        StringComparison.OrdinalIgnoreCase));

                if (fileHasGroup)
                {
                    availableActions.Add(groupIndex);
                }
            }

            if (actionRemapByBody.TryGetValue(resolvedBodyId, out Dictionary<int, int>? remaps))
            {
                foreach (KeyValuePair<int, int> pair in remaps)
                {
                    if (availableActions.Contains(pair.Value))
                    {
                        availableActions.Add(pair.Key);
                    }
                }
            }
        }
        else
        {
            return GetAvailableActionIndices(bodyId);
        }

        List<int> sortedActions = new List<int>(availableActions);
        sortedActions.Sort();
        return sortedActions;
    }
}