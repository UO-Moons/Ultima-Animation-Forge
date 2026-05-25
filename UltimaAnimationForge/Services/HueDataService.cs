using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class HueDataService
{
    private const int ColorsPerHue = 32;
    private const int HuesPerBlock = 8;
    private const int HueNameLength = 20;
    private const int HueEntryByteLength = 88;
    private const int HueBlockByteLength = 4 + (HuesPerBlock * HueEntryByteLength);

    public sealed class HueEntry
    {
        public int HueId { get; set; }
        public string Name { get; set; } = string.Empty;
        public ushort TableStart { get; set; }
        public ushort TableEnd { get; set; }
        public List<Color> Colors { get; set; } = new(ColorsPerHue);
        public List<ushort> RawColors { get; set; } = new(ColorsPerHue);

        public string DisplayText =>
            string.IsNullOrWhiteSpace(Name) ? HueId.ToString() : HueId + " - " + Name;
    }

    public List<HueEntry> LoadHueEntries(string hueFilePath)
    {
        List<HueEntry> hueEntries = new();

        if (string.IsNullOrWhiteSpace(hueFilePath) || !File.Exists(hueFilePath))
        {
            return hueEntries;
        }

        using FileStream fileStream = File.OpenRead(hueFilePath);
        using BinaryReader binaryReader = new(fileStream);

        int hueId = 0;

        while (fileStream.Position <= fileStream.Length - HueBlockByteLength)
        {
            binaryReader.ReadInt32();

            for (int entryIndex = 0; entryIndex < HuesPerBlock; entryIndex++)
            {
                List<Color> colors = new(ColorsPerHue);
                List<ushort> rawColors = new(ColorsPerHue);

                for (int colorIndex = 0; colorIndex < ColorsPerHue; colorIndex++)
                {
                    ushort rawColor = binaryReader.ReadUInt16();
                    rawColors.Add(rawColor);
                    colors.Add(ConvertUoColorToAvaloniaColor(rawColor));
                }

                ushort tableStart = binaryReader.ReadUInt16();
                ushort tableEnd = binaryReader.ReadUInt16();

                byte[] nameBytes = binaryReader.ReadBytes(HueNameLength);
                string hueName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0', ' ');

                hueEntries.Add(new HueEntry
                {
                    HueId = hueId,
                    Name = hueName,
                    TableStart = tableStart,
                    TableEnd = tableEnd,
                    Colors = colors,
                    RawColors = rawColors
                });

                hueId++;
            }
        }

        return hueEntries;
    }

    public List<HueEditorEntry> LoadHueEditorEntries(string hueFilePath)
    {
        List<HueEditorEntry> result = new();

        foreach (HueEntry source in LoadHueEntries(hueFilePath))
        {
            HueEditorEntry entry = new()
            {
                HueId = source.HueId,
                Name = source.Name,
                TableStart = source.TableStart,
                TableEnd = source.TableEnd
            };

            for (int i = 0; i < ColorsPerHue; i++)
            {
                ushort raw = i < source.RawColors.Count ? source.RawColors[i] : ConvertAvaloniaColorToUoColor(source.Colors[i]);
                Color color = i < source.Colors.Count ? source.Colors[i] : ConvertUoColorToAvaloniaColor(raw);

                entry.Colors.Add(new HueEditorColorSlot
                {
                    Index = i,
                    RawValue = raw,
                    Color = color
                });
            }

            result.Add(entry);
        }

        return result;
    }

    public void SaveHueEditorEntries(string hueFilePath, IReadOnlyList<HueEditorEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(hueFilePath))
        {
            throw new ArgumentException("Hue file path is required.", nameof(hueFilePath));
        }

        if (entries == null || entries.Count == 0)
        {
            throw new InvalidOperationException("There are no hue entries to save.");
        }

        string backupPath = hueFilePath + ".bak";
        if (File.Exists(hueFilePath) && !File.Exists(backupPath))
        {
            File.Copy(hueFilePath, backupPath, false);
        }

        using FileStream fileStream = new(hueFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using BinaryWriter writer = new(fileStream);

        int index = 0;
        while (index < entries.Count)
        {
            writer.Write(0);

            for (int blockEntry = 0; blockEntry < HuesPerBlock; blockEntry++)
            {
                HueEditorEntry? entry = index < entries.Count ? entries[index] : null;

                for (int colorIndex = 0; colorIndex < ColorsPerHue; colorIndex++)
                {
                    ushort raw = 0;
                    if (entry != null && colorIndex < entry.Colors.Count)
                    {
                        raw = ConvertAvaloniaColorToUoColor(entry.Colors[colorIndex].Color);
                    }
                    writer.Write(raw);
                }

                writer.Write(entry?.TableStart ?? 0);
                writer.Write(entry?.TableEnd ?? 0);
                writer.Write(BuildNameBytes(entry?.Name));

                index++;
            }
        }
    }

    public static Color ConvertUoColorToAvaloniaColor(ushort rawColor)
    {
        int red = (rawColor >> 10) & 0x1F;
        int green = (rawColor >> 5) & 0x1F;
        int blue = rawColor & 0x1F;

        byte redByte = (byte)((red * 255) / 31);
        byte greenByte = (byte)((green * 255) / 31);
        byte blueByte = (byte)((blue * 255) / 31);

        return Color.FromRgb(redByte, greenByte, blueByte);
    }

    public static ushort ConvertAvaloniaColorToUoColor(Color color)
    {
        ushort red = ScaleByteToFiveBit(color.R);
        ushort green = ScaleByteToFiveBit(color.G);
        ushort blue = ScaleByteToFiveBit(color.B);
        return (ushort)((red << 10) | (green << 5) | blue);
    }

    private static ushort ScaleByteToFiveBit(byte value)
    {
        if (value == 0)
        {
            return 0;
        }

        ushort scaled = (ushort)Math.Round(value * 31.0 / 255.0);
        return scaled == 0 ? (ushort)1 : scaled;
    }

    private static byte[] BuildNameBytes(string? name)
    {
        byte[] buffer = new byte[HueNameLength];

        if (string.IsNullOrWhiteSpace(name))
        {
            return buffer;
        }

        byte[] source = Encoding.ASCII.GetBytes(name.Trim());
        int length = Math.Min(source.Length, HueNameLength);
        Array.Copy(source, buffer, length);
        return buffer;
    }

    public void ExportHueText(string filePath, HueEditorEntry entry)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        using StreamWriter writer = new(filePath, false, Encoding.UTF8);

        writer.WriteLine("# Ultima Animation Forge Hue Export");
        writer.WriteLine("HueId=" + entry.HueId);
        writer.WriteLine("Name=" + entry.Name);
        writer.WriteLine("TableStart=" + entry.TableStart);
        writer.WriteLine("TableEnd=" + entry.TableEnd);

        for (int i = 0; i < ColorsPerHue; i++)
        {
            ushort raw = i < entry.Colors.Count
                ? ConvertAvaloniaColorToUoColor(entry.Colors[i].Color)
                : (ushort)0;

            writer.WriteLine("Color" + i.ToString("00") + "=0x" + raw.ToString("X4"));
        }
    }

    public HueEditorEntry ImportHueText(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("Hue import file was not found.", filePath);
        }

        HueEditorEntry entry = new();

        Dictionary<int, ushort> colorsByIndex = new();

        foreach (string rawLine in File.ReadAllLines(filePath))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            int equalsIndex = line.IndexOf('=');
            if (equalsIndex < 0)
            {
                continue;
            }

            string key = line.Substring(0, equalsIndex).Trim();
            string value = line.Substring(equalsIndex + 1).Trim();

            if (key.Equals("HueId", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int hueId))
            {
                entry.HueId = hueId;
            }
            else if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                entry.Name = value;
            }
            else if (key.Equals("TableStart", StringComparison.OrdinalIgnoreCase) && ushort.TryParse(value, out ushort tableStart))
            {
                entry.TableStart = tableStart;
            }
            else if (key.Equals("TableEnd", StringComparison.OrdinalIgnoreCase) && ushort.TryParse(value, out ushort tableEnd))
            {
                entry.TableEnd = tableEnd;
            }
            else if (key.StartsWith("Color", StringComparison.OrdinalIgnoreCase))
            {
                string indexText = key.Substring(5);
                if (int.TryParse(indexText, out int colorIndex) && colorIndex >= 0 && colorIndex < ColorsPerHue)
                {
                    colorsByIndex[colorIndex] = ParseUShortFlexible(value);
                }
            }
        }

        for (int i = 0; i < ColorsPerHue; i++)
        {
            ushort raw = colorsByIndex.TryGetValue(i, out ushort found) ? found : (ushort)0;

            entry.Colors.Add(new HueEditorColorSlot
            {
                Index = i,
                RawValue = raw,
                Color = ConvertUoColorToAvaloniaColor(raw)
            });
        }

        return entry;
    }

    private static ushort ParseUShortFlexible(string value)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToUInt16(value.Substring(2), 16);
        }

        return Convert.ToUInt16(value);
    }
}
