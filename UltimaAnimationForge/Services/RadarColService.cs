using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class RadarColService
{
    private const int LandColorCount = 0x4000;
    private const int StaticColorCount = 0x4000;
    private const int TotalColorCount = LandColorCount + StaticColorCount;

    private readonly ushort[] colors = new ushort[TotalColorCount];
    private readonly Dictionary<int, ushort> originalLandColors = new();
    private readonly Dictionary<int, ushort> originalStaticColors = new();

    public bool IsLoaded { get; private set; }
    public string FilePath { get; private set; } = string.Empty;

    public bool HasPendingChanges =>
        originalLandColors.Count > 0 || originalStaticColors.Count > 0;

    public bool Load(string uoFolderPath, out string message)
    {
        IsLoaded = false;
        FilePath = string.Empty;
        originalLandColors.Clear();
        originalStaticColors.Clear();
        Array.Clear(colors, 0, colors.Length);

        if (string.IsNullOrWhiteSpace(uoFolderPath) || !Directory.Exists(uoFolderPath))
        {
            message = "UO folder path is invalid.";
            return false;
        }

        string path = Path.Combine(uoFolderPath, "radarcol.mul");
        if (!File.Exists(path))
        {
            message = "radarcol.mul was not found.";
            return false;
        }

        byte[] data = File.ReadAllBytes(path);
        int availableColors = Math.Min(data.Length / 2, TotalColorCount);

        for (int i = 0; i < availableColors; i++)
        {
            colors[i] = BitConverter.ToUInt16(data, i * 2);
        }

        FilePath = path;
        IsLoaded = true;
        message = "Loaded radarcol.mul.";
        return true;
    }

    public ushort GetColor(ArtEntry? entry)
    {
        if (!IsLoaded || entry == null)
        {
            return 0;
        }

        int index = GetRadarIndex(entry);
        if (index < 0 || index >= colors.Length)
        {
            return 0;
        }

        return colors[index];
    }

    public Color GetAvaloniaColor(ArtEntry? entry)
    {
        return ConvertUoColorToAvaloniaColor(GetColor(entry));
    }

    public bool SetColor(ArtEntry? entry, ushort color, out string message)
    {
        if (!IsLoaded)
        {
            message = "radarcol.mul is not loaded.";
            return false;
        }

        if (entry == null)
        {
            message = "No art selected.";
            return false;
        }

        int index = GetRadarIndex(entry);
        if (index < 0 || index >= colors.Length)
        {
            message = "Radar color index is outside the supported range.";
            return false;
        }

        bool isLand = IsLand(entry);
        int artId = entry.ArtId;

        ushort oldColor = colors[index];

        if (oldColor == color)
        {
            message = "Radar color is already the same.";
            return true;
        }

        if (isLand)
        {
            if (!originalLandColors.ContainsKey(artId))
            {
                originalLandColors[artId] = oldColor;
            }
            else if (originalLandColors[artId] == color)
            {
                originalLandColors.Remove(artId);
            }
        }
        else
        {
            if (!originalStaticColors.ContainsKey(artId))
            {
                originalStaticColors[artId] = oldColor;
            }
            else if (originalStaticColors[artId] == color)
            {
                originalStaticColors.Remove(artId);
            }
        }

        colors[index] = color;
        message = "Queued radar color change.";
        return true;
    }

    public bool Save(out string message)
    {
        if (!IsLoaded || string.IsNullOrWhiteSpace(FilePath))
        {
            message = "radarcol.mul is not loaded.";
            return false;
        }

        try
        {
            string backupPath = FilePath + ".bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(FilePath, backupPath, false);
            }

            using FileStream stream = new(FilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using BinaryWriter writer = new(stream);

            for (int i = 0; i < colors.Length; i++)
            {
                writer.Write(colors[i]);
            }

            originalLandColors.Clear();
            originalStaticColors.Clear();

            message = "Saved radarcol.mul.";
            return true;
        }
        catch (Exception ex)
        {
            message = "Failed saving radarcol.mul: " + ex.Message;
            return false;
        }
    }

    public bool RevertSelected(ArtEntry? entry, out string message)
    {
        if (!IsLoaded || entry == null)
        {
            message = "No radar color entry selected.";
            return false;
        }

        bool isLand = IsLand(entry);
        int artId = entry.ArtId;
        int index = GetRadarIndex(entry);

        if (index < 0 || index >= colors.Length)
        {
            message = "Radar color index is outside the supported range.";
            return false;
        }

        if (isLand && originalLandColors.TryGetValue(artId, out ushort landColor))
        {
            colors[index] = landColor;
            originalLandColors.Remove(artId);
            message = "Reverted selected land radar color.";
            return true;
        }

        if (!isLand && originalStaticColors.TryGetValue(artId, out ushort staticColor))
        {
            colors[index] = staticColor;
            originalStaticColors.Remove(artId);
            message = "Reverted selected static radar color.";
            return true;
        }

        message = "Selected radar color has no pending change.";
        return false;
    }

    public bool HasPendingChange(ArtEntry? entry)
    {
        if (entry == null)
        {
            return false;
        }

        return IsLand(entry)
            ? originalLandColors.ContainsKey(entry.ArtId)
            : originalStaticColors.ContainsKey(entry.ArtId);
    }

    public static Color ConvertUoColorToAvaloniaColor(ushort color)
    {
        int r5 = (color >> 10) & 0x1F;
        int g5 = (color >> 5) & 0x1F;
        int b5 = color & 0x1F;

        byte r = (byte)((r5 << 3) | (r5 >> 2));
        byte g = (byte)((g5 << 3) | (g5 >> 2));
        byte b = (byte)((b5 << 3) | (b5 >> 2));

        return Color.FromRgb(r, g, b);
    }

    private static bool IsLand(ArtEntry entry)
    {
        return string.Equals(entry.Type, "Land", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetRadarIndex(ArtEntry entry)
    {
        return IsLand(entry)
            ? entry.ArtId
            : LandColorCount + entry.ArtId;
    }

    public bool RevertAll(out string message)
    {
        if (!IsLoaded)
        {
            message = "radarcol.mul is not loaded.";
            return false;
        }

        foreach (KeyValuePair<int, ushort> pair in originalLandColors)
        {
            int index = pair.Key;
            if (index >= 0 && index < LandColorCount)
            {
                colors[index] = pair.Value;
            }
        }

        foreach (KeyValuePair<int, ushort> pair in originalStaticColors)
        {
            int index = LandColorCount + pair.Key;
            if (index >= 0 && index < colors.Length)
            {
                colors[index] = pair.Value;
            }
        }

        originalLandColors.Clear();
        originalStaticColors.Clear();

        message = "Reverted all pending RadarCol changes.";
        return true;
    }
}