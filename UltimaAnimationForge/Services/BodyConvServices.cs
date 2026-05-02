using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public class BodyConvDefService
{
    // Backward-compatible legacy tables for anim2..anim6
    public int[] Table1 { get; private set; } = Array.Empty<int>(); // anim2
    public int[] Table2 { get; private set; } = Array.Empty<int>(); // anim3
    public int[] Table3 { get; private set; } = Array.Empty<int>(); // anim4
    public int[] Table4 { get; private set; } = Array.Empty<int>(); // anim5
    public int[] Table5 { get; private set; } = Array.Empty<int>(); // anim6

    // New dynamic tables:
    // key = fileType (2=anim2, 3=anim3, 7=anim7, etc.)
    // value = table indexed by original body id, containing mapped body index or -1
    public Dictionary<int, int[]> DynamicTables { get; private set; } = new();

    public Dictionary<int, BodyConvEntry> Load(string filePath)
    {
        Dictionary<int, BodyConvEntry> compatibilityResults = new Dictionary<int, BodyConvEntry>();

        Table1 = Array.Empty<int>();
        Table2 = Array.Empty<int>();
        Table3 = Array.Empty<int>();
        Table4 = Array.Empty<int>();
        Table5 = Array.Empty<int>();
        DynamicTables = new Dictionary<int, int[]>();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return compatibilityResults;
        }

        Dictionary<int, List<int>> flatPairsByFileType = new Dictionary<int, List<int>>();
        Dictionary<int, int> maxOriginalBodyByFileType = new Dictionary<int, int>();

        string[] lines = File.ReadAllLines(filePath);

        foreach (string rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            string line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal) ||
                line.StartsWith("//", StringComparison.Ordinal) ||
                line.StartsWith("\"", StringComparison.Ordinal))
            {
                continue;
            }

            int commentIndex = line.IndexOf('#');
            if (commentIndex >= 0)
            {
                line = line.Substring(0, commentIndex).Trim();
            }

            if (line.Length == 0)
            {
                continue;
            }

            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                continue;
            }

            if (!TryParseInt(parts[0], out int originalBodyId))
            {
                continue;
            }

            for (int partIndex = 1; partIndex < parts.Length; partIndex++)
            {
                if (!TryParseInt(parts[partIndex], out int mappedBodyId))
                {
                    mappedBodyId = -1;
                }

                if (mappedBodyId == -1)
                {
                    continue;
                }

                int fileType = partIndex + 1; // part 1 => fileType 2 (anim2), part 6 => fileType 7 (anim7)

                // Preserve old UOFiddler special case on anim2 only
                if (fileType == 2 && mappedBodyId == 68)
                {
                    mappedBodyId = 122;
                }

                if (!flatPairsByFileType.TryGetValue(fileType, out List<int>? flatPairs))
                {
                    flatPairs = new List<int>();
                    flatPairsByFileType[fileType] = flatPairs;
                }

                flatPairs.Add(originalBodyId);
                flatPairs.Add(mappedBodyId);

                if (!maxOriginalBodyByFileType.TryGetValue(fileType, out int currentMax) || originalBodyId > currentMax)
                {
                    maxOriginalBodyByFileType[fileType] = originalBodyId;
                }
            }
        }

        foreach (KeyValuePair<int, List<int>> pair in flatPairsByFileType)
        {
            int fileType = pair.Key;
            int maxOriginalBodyId = maxOriginalBodyByFileType.TryGetValue(fileType, out int maxVal) ? maxVal : -1;

            DynamicTables[fileType] = BuildTable(maxOriginalBodyId, pair.Value);
        }

        Table1 = DynamicTables.TryGetValue(2, out int[]? t1) ? t1 : Array.Empty<int>();
        Table2 = DynamicTables.TryGetValue(3, out int[]? t2) ? t2 : Array.Empty<int>();
        Table3 = DynamicTables.TryGetValue(4, out int[]? t3) ? t3 : Array.Empty<int>();
        Table4 = DynamicTables.TryGetValue(5, out int[]? t4) ? t4 : Array.Empty<int>();
        Table5 = DynamicTables.TryGetValue(6, out int[]? t5) ? t5 : Array.Empty<int>();

        HashSet<int> allBodies = new HashSet<int>();

        foreach (List<int> flatPairs in flatPairsByFileType.Values)
        {
            AddOriginalBodies(allBodies, flatPairs);
        }

        foreach (int originalBodyId in allBodies)
        {
            if (TryGetMappedBody(originalBodyId, out BodyConvEntry entry))
            {
                compatibilityResults[originalBodyId] = entry;
            }
        }

        return compatibilityResults;
    }

    public bool Contains(int body)
    {
        foreach (int[] table in DynamicTables.Values)
        {
            if (table.Length > 0 && body >= 0 && body < table.Length && table[body] != -1)
            {
                return true;
            }
        }

        return false;
    }

    public int Convert(ref int body)
    {
        // Preserve legacy priority order first
        for (int fileType = 2; fileType <= 6; fileType++)
        {
            if (TryConvertUsingFileType(fileType, ref body))
            {
                return fileType;
            }
        }

        // Then allow anim7+
        List<int> extraFileTypes = new List<int>(DynamicTables.Keys);
        extraFileTypes.Sort();

        foreach (int fileType in extraFileTypes)
        {
            if (fileType <= 6)
            {
                continue;
            }

            if (TryConvertUsingFileType(fileType, ref body))
            {
                return fileType;
            }
        }

        return 1;
    }

    public int GetTrueBody(int fileType, int index)
    {
        if (fileType <= 1)
        {
            return index;
        }

        if (!DynamicTables.TryGetValue(fileType, out int[]? table))
        {
            return -1;
        }

        return FindOriginalBody(table, index);
    }

    public bool TryGetMappedBody(int originalBodyId, out BodyConvEntry entry)
    {
        entry = new BodyConvEntry();

        // Preserve old preference order for anim2..anim6
        for (int fileType = 2; fileType <= 6; fileType++)
        {
            if (TryGetMappedBodyForFileType(originalBodyId, fileType, out entry))
            {
                return true;
            }
        }

        // Then anim7+
        List<int> extraFileTypes = new List<int>(DynamicTables.Keys);
        extraFileTypes.Sort();

        foreach (int fileType in extraFileTypes)
        {
            if (fileType <= 6)
            {
                continue;
            }

            if (TryGetMappedBodyForFileType(originalBodyId, fileType, out entry))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetMappedBodyForFileType(int originalBodyId, int fileType, out BodyConvEntry entry)
    {
        entry = new BodyConvEntry();

        if (!DynamicTables.TryGetValue(fileType, out int[]? table))
        {
            return false;
        }

        if (table.Length == 0 || originalBodyId < 0 || originalBodyId >= table.Length)
        {
            return false;
        }

        if (table[originalBodyId] == -1)
        {
            return false;
        }

        entry = new BodyConvEntry
        {
            OriginalBodyId = originalBodyId,
            FileIndex = fileType - 1,
            NewBodyId = table[originalBodyId],
            SourceFileName = GetMulFileNameForFileType(fileType)
        };

        return true;
    }

    private bool TryConvertUsingFileType(int fileType, ref int body)
    {
        if (!DynamicTables.TryGetValue(fileType, out int[]? table))
        {
            return false;
        }

        if (table.Length == 0 || body < 0 || body >= table.Length)
        {
            return false;
        }

        int value = table[body];
        if (value == -1)
        {
            return false;
        }

        body = value;
        return true;
    }

    private static string GetMulFileNameForFileType(int fileType)
    {
        return fileType <= 1 ? "anim.mul" : "anim" + fileType + ".mul";
    }

    private static int[] BuildTable(int maxOriginalBodyId, List<int> flatPairs)
    {
        if (maxOriginalBodyId < 0)
        {
            return Array.Empty<int>();
        }

        int[] table = new int[maxOriginalBodyId + 1];

        for (int index = 0; index < table.Length; index++)
        {
            table[index] = -1;
        }

        for (int index = 0; index + 1 < flatPairs.Count; index += 2)
        {
            int original = flatPairs[index];
            int mapped = flatPairs[index + 1];

            if (original >= 0 && original < table.Length)
            {
                table[original] = mapped;
            }
        }

        return table;
    }

    private static void AddOriginalBodies(HashSet<int> target, List<int> flatPairs)
    {
        for (int index = 0; index + 1 < flatPairs.Count; index += 2)
        {
            target.Add(flatPairs[index]);
        }
    }

    private static int FindOriginalBody(int[] table, int mappedBodyId)
    {
        if (table == null || table.Length == 0 || mappedBodyId < 0)
        {
            return -1;
        }

        for (int index = 0; index < table.Length; index++)
        {
            if (table[index] == mappedBodyId)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryParseInt(string value, out int result)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(
                value.Substring(2),
                System.Globalization.NumberStyles.HexNumber,
                null,
                out result);
        }

        return int.TryParse(value, out result);
    }
}

public sealed class BodyConvAssignmentService
{
    public sealed class PreviewResult
    {
        public int BodyId { get; set; }
        public bool Exists { get; set; }
        public string PreviewLine { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public int GetNextFreeBodyId(string bodyConvPath)
    {
        HashSet<int> usedBodyIds = GetUsedBodyIds(bodyConvPath);

        int candidate = 1;
        while (usedBodyIds.Contains(candidate))
        {
            candidate++;
        }

        return candidate;
    }

    public bool BodyIdExists(string bodyConvPath, int bodyId)
    {
        if (bodyId < 1)
        {
            return false;
        }

        return GetUsedBodyIds(bodyConvPath).Contains(bodyId);
    }

    public HashSet<int> GetUsedBodyIds(string bodyConvPath)
    {
        HashSet<int> used = new HashSet<int>();

        if (!File.Exists(bodyConvPath))
        {
            return used;
        }

        string[] lines = File.ReadAllLines(bodyConvPath);

        foreach (string rawLine in lines)
        {
            if (!TryParseBodyConvLine(rawLine, out int bodyId, out _))
            {
                continue;
            }

            if (bodyId >= 1)
            {
                used.Add(bodyId);
            }
        }

        return used;
    }

    public PreviewResult BuildPreview(string bodyConvPath, int bodyId, int fileType, int slotBodyIndex, string? comment = null)
    {
        bool exists = BodyIdExists(bodyConvPath, bodyId);

        string previewLine = exists || bodyId < 1
            ? string.Empty
            : BuildLine(bodyId, fileType, slotBodyIndex, null, comment);

        return new PreviewResult
        {
            BodyId = bodyId,
            Exists = exists,
            PreviewLine = previewLine,
            Comment = NormalizeComment(comment)
        };
    }

    public void AddNewEntry(string bodyConvPath, int bodyId, int fileType, int slotBodyIndex, string? comment = null)
    {
        if (bodyId < 1)
        {
            throw new InvalidOperationException("Body ID must be 1 or greater.");
        }

        if (BodyIdExists(bodyConvPath, bodyId))
        {
            throw new InvalidOperationException("Body ID " + bodyId + " already exists in bodyconv.def.");
        }

        List<string> lines = File.Exists(bodyConvPath)
            ? new List<string>(File.ReadAllLines(bodyConvPath))
            : new List<string>();

        lines.Add(BuildLine(bodyId, fileType, slotBodyIndex, null, comment));
        File.WriteAllLines(bodyConvPath, lines);
    }

    private string BuildLine(int bodyId, int fileType, int slotBodyIndex, int[]? existingColumns, string? comment = null)
    {
        int columnIndex = GetColumnIndex(fileType);

        if (columnIndex < 0)
        {
            throw new InvalidOperationException("BodyConv assignment is only supported for anim2 and higher.");
        }

        int requiredLength = Math.Max(columnIndex + 1, 5);

        int[] columns;

        if (existingColumns != null && existingColumns.Length > 0)
        {
            columns = new int[Math.Max(existingColumns.Length, requiredLength)];

            for (int i = 0; i < columns.Length; i++)
            {
                columns[i] = i < existingColumns.Length ? existingColumns[i] : -1;
            }
        }
        else
        {
            columns = Enumerable.Repeat(-1, requiredLength).ToArray();
        }

        columns[columnIndex] = slotBodyIndex;

        string line = bodyId + " " + string.Join(" ", columns);

        string cleanedComment = NormalizeComment(comment);
        if (!string.IsNullOrWhiteSpace(cleanedComment))
        {
            line += " # " + cleanedComment;
        }

        return line;
    }

    private static string NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return string.Empty;
        }

        string cleaned = comment.Trim();

        while (cleaned.StartsWith("#", StringComparison.Ordinal))
        {
            cleaned = cleaned.Substring(1).TrimStart();
        }

        return cleaned;
    }

    private int GetColumnIndex(int fileType)
    {
        // fileType 2 => anim2 => column 0
        // fileType 3 => anim3 => column 1
        // ...
        // fileType 7 => anim7 => column 5
        return fileType >= 2 ? fileType - 2 : -1;
    }

    private bool TryParseBodyConvLine(string rawLine, out int bodyId, out int[] columns)
    {
        bodyId = -1;
        columns = Array.Empty<int>();

        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return false;
        }

        string line = rawLine.Trim();

        if (line.Length == 0 ||
            line.StartsWith("#", StringComparison.Ordinal) ||
            line.StartsWith("//", StringComparison.Ordinal) ||
            line.StartsWith("\"", StringComparison.Ordinal))
        {
            return false;
        }

        int commentIndex = line.IndexOf('#');
        if (commentIndex >= 0)
        {
            line = line.Substring(0, commentIndex).Trim();
        }

        if (line.Length == 0)
        {
            return false;
        }

        string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return false;
        }

        if (!TryParseInt(parts[0], out bodyId))
        {
            return false;
        }

        columns = new int[parts.Length - 1];

        for (int i = 1; i < parts.Length; i++)
        {
            if (!TryParseInt(parts[i], out columns[i - 1]))
            {
                columns[i - 1] = -1;
            }
        }

        return true;
    }

    private static bool TryParseInt(string value, out int result)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(
                value.Substring(2),
                System.Globalization.NumberStyles.HexNumber,
                null,
                out result);
        }

        return int.TryParse(value, out result);
    }

    public bool RemoveBodyId(string bodyConvPath, int bodyId)
    {
        if (!File.Exists(bodyConvPath))
        {
            return false;
        }

        List<string> lines = new List<string>(File.ReadAllLines(bodyConvPath));
        bool removed = false;

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (!TryParseBodyConvLine(lines[i], out int existingBodyId, out _))
            {
                continue;
            }

            if (existingBodyId == bodyId)
            {
                lines.RemoveAt(i);
                removed = true;
            }
        }

        if (removed)
        {
            File.WriteAllLines(bodyConvPath, lines);
        }

        return removed;
    }
}