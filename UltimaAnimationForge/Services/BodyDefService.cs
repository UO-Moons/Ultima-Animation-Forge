using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace UltimaAnimationForge.Services;

public sealed class BodyDefEntry
{
    public int DisplayBodyId { get; set; }
    public int AnimationBodyId { get; set; }
    public int Hue { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public sealed class BodyDefService
{
    private static readonly Regex BodyDefRegex = new Regex(
        @"^\s*(\d+)\s*\{\s*(\d+)\s*\}\s*(-?\d+)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Dictionary<int, BodyDefEntry> Load(string bodyDefPath)
    {
        Dictionary<int, BodyDefEntry> entries = new();

        if (string.IsNullOrWhiteSpace(bodyDefPath) || !File.Exists(bodyDefPath))
        {
            return entries;
        }

        foreach (string rawLine in File.ReadAllLines(bodyDefPath))
        {
            if (!TryParseLine(rawLine, out BodyDefEntry? entry) || entry == null)
            {
                continue;
            }

            entries[entry.DisplayBodyId] = entry;
        }

        return entries;
    }

    public bool EntryExists(string bodyDefPath, int displayBodyId)
    {
        return Load(bodyDefPath).ContainsKey(displayBodyId);
    }

    public BodyDefEntry? GetEntry(string bodyDefPath, int displayBodyId)
    {
        Dictionary<int, BodyDefEntry> entries = Load(bodyDefPath);

        if (entries.TryGetValue(displayBodyId, out BodyDefEntry? entry))
        {
            return entry;
        }

        return null;
    }

    public string BuildLine(int displayBodyId, int animationBodyId, int hue, string? comment)
    {
        string line = displayBodyId + " {" + animationBodyId + "} " + hue;

        string cleanedComment = NormalizeComment(comment);
        if (!string.IsNullOrWhiteSpace(cleanedComment))
        {
            line += " # " + cleanedComment;
        }

        return line;
    }

    public void AddOrUpdateEntry(
        string bodyDefPath,
        int displayBodyId,
        int animationBodyId,
        int hue,
        string? comment)
    {
        if (string.IsNullOrWhiteSpace(bodyDefPath))
        {
            throw new ArgumentException("Body.def path is required.", nameof(bodyDefPath));
        }

        if (displayBodyId < 0 || displayBodyId > 65534)
        {
            throw new ArgumentOutOfRangeException(nameof(displayBodyId), "Display body/gump ID must be between 0 and 65534.");
        }

        if (animationBodyId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(animationBodyId), "Animation body ID must be 0 or greater.");
        }

        string newLine = BuildLine(displayBodyId, animationBodyId, hue, comment);

        List<string> lines = File.Exists(bodyDefPath)
            ? new List<string>(File.ReadAllLines(bodyDefPath))
            : new List<string>();

        bool replaced = false;

        for (int i = 0; i < lines.Count; i++)
        {
            if (TryParseLine(lines[i], out BodyDefEntry? existing) &&
                existing != null &&
                existing.DisplayBodyId == displayBodyId)
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

        File.WriteAllLines(bodyDefPath, lines);
    }

    public int FindNextFreeDisplayBodyId(string bodyDefPath, int startAt = 4000)
    {
        Dictionary<int, BodyDefEntry> entries = Load(bodyDefPath);

        int candidate = Math.Clamp(startAt, 1, 65534);

        while (candidate <= 65534)
        {
            if (!entries.ContainsKey(candidate))
            {
                return candidate;
            }

            candidate++;
        }

        return -1;
    }

    private static bool TryParseLine(string rawLine, out BodyDefEntry? entry)
    {
        entry = null;

        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return false;
        }

        string working = rawLine.Trim();

        if (working.Length == 0 ||
            working.StartsWith("#", StringComparison.Ordinal) ||
            working.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        string comment = string.Empty;
        int commentIndex = working.IndexOf('#');

        if (commentIndex >= 0)
        {
            comment = working[(commentIndex + 1)..].Trim();
            working = working[..commentIndex].Trim();
        }

        Match match = BodyDefRegex.Match(working);
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups[1].Value, out int displayBodyId))
        {
            return false;
        }

        if (!int.TryParse(match.Groups[2].Value, out int animationBodyId))
        {
            return false;
        }

        int hue = 0;
        if (match.Groups.Count >= 4 && match.Groups[3].Success)
        {
            int.TryParse(match.Groups[3].Value, out hue);
        }

        entry = new BodyDefEntry
        {
            DisplayBodyId = displayBodyId,
            AnimationBodyId = animationBodyId,
            Hue = hue,
            Comment = comment
        };

        return true;
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
            cleaned = cleaned[1..].TrimStart();
        }

        return cleaned;
    }
}
