using System;
using System.Collections.Generic;
using System.IO;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public class MobTypesService
{
    public Dictionary<int, MobTypeEntry> Load(string filePath)
    {
        Dictionary<int, MobTypeEntry> results = new Dictionary<int, MobTypeEntry>();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return results;
        }

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

            if (line.StartsWith("#", StringComparison.Ordinal))
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

            string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                continue;
            }

            if (!int.TryParse(parts[0], out int bodyId))
            {
                continue;
            }

            string typeName = parts[1].Trim().ToUpperInvariant();
            string flagsText = parts.Length >= 3 ? parts[2].Trim() : "0";

            results[bodyId] = new MobTypeEntry
            {
                BodyId = bodyId,
                TypeName = typeName,
                FlagsText = flagsText
            };
        }

        return results;
    }
}

public sealed class MobTypeAssignmentService
{
    private static readonly HashSet<string> ValidMobTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "MONSTER",
        "SEA_MONSTER",
        "ANIMAL",
        "HUMAN",
        "EQUIPMENT"
    };

    public bool BodyIdExists(string mobTypesPath, int bodyId)
    {
        if (string.IsNullOrWhiteSpace(mobTypesPath) || !File.Exists(mobTypesPath))
        {
            return false;
        }

        foreach (string rawLine in File.ReadAllLines(mobTypesPath))
        {
            if (TryParseLine(rawLine, out int existingBodyId, out _, out _)
                && existingBodyId == bodyId)
            {
                return true;
            }
        }

        return false;
    }

    public string? GetExistingMobType(string mobTypesPath, int bodyId)
    {
        if (string.IsNullOrWhiteSpace(mobTypesPath) || !File.Exists(mobTypesPath))
        {
            return null;
        }

        foreach (string rawLine in File.ReadAllLines(mobTypesPath))
        {
            if (TryParseLine(rawLine, out int existingBodyId, out string? mobType, out _)
                && existingBodyId == bodyId)
            {
                return mobType;
            }
        }

        return null;
    }

    public string BuildPreviewLine(int bodyId, string mobType, string? comment = null)
    {
        string normalizedMobType = NormalizeMobType(mobType);
        string line = bodyId + "\t" + normalizedMobType + "\t0";

        string cleanedComment = NormalizeComment(comment);
        if (!string.IsNullOrWhiteSpace(cleanedComment))
        {
            line += "\t# " + cleanedComment;
        }

        return line;
    }

    public void AddOrUpdateEntry(string mobTypesPath, int bodyId, string mobType, string? comment = null)
    {
        if (string.IsNullOrWhiteSpace(mobTypesPath))
        {
            throw new ArgumentException("mobtypes.txt path is required.", nameof(mobTypesPath));
        }

        if (bodyId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bodyId), "Body ID must be 1 or greater.");
        }

        string normalizedMobType = NormalizeMobType(mobType);
        string newLine = BuildPreviewLine(bodyId, normalizedMobType, comment);

        List<string> lines = File.Exists(mobTypesPath)
            ? new List<string>(File.ReadAllLines(mobTypesPath))
            : new List<string>();

        bool replaced = false;

        for (int i = 0; i < lines.Count; i++)
        {
            if (TryParseLine(lines[i], out int existingBodyId, out _, out _)
                && existingBodyId == bodyId)
            {
                lines[i] = newLine;
                replaced = true;
                break;
            }
        }

        if (!replaced)
        {
            lines.Add(newLine);
        }

        File.WriteAllLines(mobTypesPath, lines);
    }

    public void RemoveBodyId(string mobTypesPath, int bodyId)
    {
        if (string.IsNullOrWhiteSpace(mobTypesPath) || !File.Exists(mobTypesPath))
        {
            return;
        }

        List<string> kept = new List<string>();

        foreach (string rawLine in File.ReadAllLines(mobTypesPath))
        {
            if (!TryParseLine(rawLine, out int existingBodyId, out _, out _)
                || existingBodyId != bodyId)
            {
                kept.Add(rawLine);
            }
        }

        File.WriteAllLines(mobTypesPath, kept);
    }

    private static bool TryParseLine(string rawLine, out int bodyId, out string? mobType, out string? comment)
    {
        bodyId = -1;
        mobType = null;
        comment = null;

        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return false;
        }

        string workingLine = rawLine;
        int commentIndex = workingLine.IndexOf('#');

        if (commentIndex >= 0)
        {
            comment = workingLine.Substring(commentIndex + 1).Trim();
            workingLine = workingLine.Substring(0, commentIndex);
        }

        workingLine = workingLine.Trim();
        if (string.IsNullOrWhiteSpace(workingLine))
        {
            return false;
        }

        string[] parts = workingLine.Split(new[] { ' ', '\t', '=' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out bodyId))
        {
            return false;
        }

        try
        {
            mobType = NormalizeMobType(parts[1]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeMobType(string? mobType)
    {
        string value = (mobType ?? string.Empty).Trim().ToUpperInvariant();

        if (!ValidMobTypes.Contains(value))
        {
            throw new InvalidOperationException("Unsupported mob type: " + mobType);
        }

        return value;
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
}
