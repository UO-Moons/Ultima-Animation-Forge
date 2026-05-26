using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class UoxDictionaryDataService
{
    public List<ClilocEntry> Load(string filePath)
    {
        List<ClilocEntry> entries = new();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return entries;
        }

        string currentCategory = "DICTIONARY";

        foreach (string rawLine in File.ReadAllLines(filePath, Encoding.UTF8))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith("//") || line.StartsWith("/"))
            {
                continue;
            }

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentCategory = line.Trim('[', ']').Trim();
                continue;
            }

            if (line == "{" || line == "}")
            {
                continue;
            }

            int equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            string idText = line[..equalsIndex].Trim();
            string valueText = line[(equalsIndex + 1)..].Trim();

            if (!int.TryParse(idText, out int id))
            {
                continue;
            }

            entries.Add(new ClilocEntry
            {
                Number = id,
                Flag = 0,
                Text = valueText,
                Category = currentCategory,
                IsDirty = false
            });
        }

        return entries.OrderBy(x => x.Category).ThenBy(x => x.Number).ToList();
    }

    public void Save(string filePath, IEnumerable<ClilocEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Dictionary path is required.", nameof(filePath));
        }

        string? folder = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException("Dictionary folder was not found.");
        }

        if (File.Exists(filePath))
        {
            string backupPath = filePath + ".bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(filePath, backupPath, false);
            }
        }

        List<string> lines = new();

        foreach (IGrouping<string, ClilocEntry> group in entries
                     .OrderBy(x => x.Category)
                     .ThenBy(x => x.Number)
                     .GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "DICTIONARY" : x.Category))
        {
            lines.Add("[" + group.Key + "]");
            lines.Add("{");

            foreach (ClilocEntry entry in group)
            {
                lines.Add(entry.Number + "=" + (entry.Text ?? string.Empty));
                entry.AcceptChanges();
            }

            lines.Add("}");
            lines.Add(string.Empty);
        }

        File.WriteAllLines(filePath, lines, Encoding.UTF8);
    }
}