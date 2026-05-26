using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class ClilocDataService
{
    private int loadedHeader1;
    private short loadedHeader2;

    public List<ClilocEntry> Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new List<ClilocEntry>();
        }

        byte[] fileBytes = File.ReadAllBytes(filePath);

        try
        {
            byte[] decompressed = MythicDecompress.Decompress(fileBytes);

            if (TryParse(decompressed, out List<ClilocEntry> compressedEntries) && compressedEntries.Count > 0)
            {
                MarkClean(compressedEntries);
                return compressedEntries;
            }
        }
        catch
        {
        }

        if (TryParse(fileBytes, out List<ClilocEntry> normalEntries) && normalEntries.Count > 0)
        {
            MarkClean(normalEntries);
            return normalEntries;
        }

        return new List<ClilocEntry>();
    }

    public void Save(string filePath, IEnumerable<ClilocEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Cliloc path is required.", nameof(filePath));
        }

        string? folder = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException("Cliloc folder was not found.");
        }

        if (File.Exists(filePath))
        {
            string backupPath = filePath + ".bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(filePath, backupPath, false);
            }
        }

        using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using BinaryWriter writer = new(stream, Encoding.UTF8);

        writer.Write(loadedHeader1);
        writer.Write(loadedHeader2);

        foreach (ClilocEntry entry in entries)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(entry.Text ?? string.Empty);

            if (textBytes.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException("Cliloc entry " + entry.Number + " is too long.");
            }

            writer.Write(entry.Number);
            writer.Write(entry.Flag);
            writer.Write((ushort)textBytes.Length);
            writer.Write(textBytes);
        }

        foreach (ClilocEntry entry in entries)
        {
            entry.AcceptChanges();
        }
    }

    private bool TryParse(byte[] data, out List<ClilocEntry> entries)
    {
        entries = new List<ClilocEntry>();

        if (data == null || data.Length < 6)
        {
            return false;
        }

        using MemoryStream stream = new(data, false);
        using BinaryReader reader = new(stream, Encoding.UTF8);

        int header1 = reader.ReadInt32();
        short header2 = reader.ReadInt16();

        while (stream.Position < stream.Length)
        {
            if (stream.Length - stream.Position < 7)
            {
                break;
            }

            int number = reader.ReadInt32();
            byte flag = reader.ReadByte();
            ushort length = reader.ReadUInt16();

            if (length > stream.Length - stream.Position)
            {
                return false;
            }

            byte[] textBytes = reader.ReadBytes(length);
            string text = Encoding.UTF8.GetString(textBytes);

            entries.Add(new ClilocEntry
            {
                Number = number,
                Flag = flag,
                Text = text,
                IsDirty = false
            });
        }

        if (!LooksLikeValidCliloc(entries))
        {
            entries.Clear();
            return false;
        }

        loadedHeader1 = header1;
        loadedHeader2 = header2;

        return entries.Count > 0;
    }

    private static bool LooksLikeValidCliloc(List<ClilocEntry> entries)
    {
        if (entries.Count == 0)
        {
            return false;
        }

        int badNumbers = 0;

        foreach (ClilocEntry entry in entries)
        {
            if (entry.Number < 0 || entry.Number > 10000000)
            {
                badNumbers++;
            }
        }

        return badNumbers <= Math.Max(5, entries.Count / 20);
    }

    private static void MarkClean(IEnumerable<ClilocEntry> entries)
    {
        foreach (ClilocEntry entry in entries)
        {
            entry.AcceptChanges();
        }
    }
}