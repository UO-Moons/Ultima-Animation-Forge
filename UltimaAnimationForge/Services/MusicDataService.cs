using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class MusicDataService
{
    public List<MusicEntry> Load(string uoFolderPath)
    {
        List<MusicEntry> entries = new();

        string musicFolder = Path.Combine(uoFolderPath, "Music", "Digital");
        string configPath = Path.Combine(musicFolder, "Config.txt");

        if (!File.Exists(configPath))
        {
            configPath = Path.Combine(uoFolderPath, "Config.txt");
        }

        if (!File.Exists(configPath))
        {
            return entries;
        }

        foreach (string rawLine in File.ReadAllLines(configPath))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith("#"))
            {
                continue;
            }

            int firstSpace = line.IndexOf(' ');
            if (firstSpace <= 0)
            {
                continue;
            }

            if (!int.TryParse(line[..firstSpace], out int id))
            {
                continue;
            }

            string data = line[(firstSpace + 1)..].Trim();

            bool loop = data.Contains(",loop", StringComparison.OrdinalIgnoreCase);
            string fileName = data.Replace(",loop", "", StringComparison.OrdinalIgnoreCase).Trim();

            if (!fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".mp3";
            }

            string fullPath = Path.Combine(musicFolder, fileName);

            entries.Add(new MusicEntry
            {
                Id = id,
                FileName = fileName,
                FullPath = fullPath,
                Loop = loop,
                InfoText = BuildMusicInfoText(fullPath, id, fileName, loop),
                Exists = File.Exists(fullPath)
            });
        }

        return entries;
    }

    private static string BuildMusicInfoText(string fullPath, int id, string fileName, bool loop)
    {
        if (!File.Exists(fullPath))
        {
            return
                "ID: " + id + "\n" +
                "File: " + fileName + "\n" +
                "Status: Missing\n" +
                "Loop: " + (loop ? "Yes" : "No");
        }

        FileInfo fileInfo = new FileInfo(fullPath);

        try
        {
            using AudioFileReader reader = new AudioFileReader(fullPath);

            return
                "ID: " + id + "\n" +
                "File: " + fileName + "\n" +
                "Duration: " + reader.TotalTime.ToString(@"mm\:ss") + "\n" +
                "Sample Rate: " + reader.WaveFormat.SampleRate + " Hz\n" +
                "Channels: " + reader.WaveFormat.Channels + "\n" +
                "Bits: " + reader.WaveFormat.BitsPerSample + "\n" +
                "Size: " + FormatBytes(fileInfo.Length) + "\n" +
                "Loop: " + (loop ? "Yes" : "No");
        }
        catch
        {
            return
                "ID: " + id + "\n" +
                "File: " + fileName + "\n" +
                "Size: " + FormatBytes(fileInfo.Length) + "\n" +
                "Loop: " + (loop ? "Yes" : "No");
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return (bytes / 1024.0 / 1024.0).ToString("0.00") + " MB";
        }

        if (bytes >= 1024)
        {
            return (bytes / 1024.0).ToString("0.00") + " KB";
        }

        return bytes + " bytes";
    }

    public MusicEntry ImportMusic(string uoFolderPath, string sourceMp3Path, bool loop)
    {
        if (string.IsNullOrWhiteSpace(uoFolderPath) || !Directory.Exists(uoFolderPath))
        {
            throw new InvalidOperationException("UO folder path is invalid.");
        }

        if (string.IsNullOrWhiteSpace(sourceMp3Path) || !File.Exists(sourceMp3Path))
        {
            throw new FileNotFoundException("MP3 file was not found.", sourceMp3Path);
        }

        string musicFolder = Path.Combine(uoFolderPath, "Music", "Digital");
        Directory.CreateDirectory(musicFolder);

        string configPath = Path.Combine(musicFolder, "Config.txt");

        string fileName = Path.GetFileName(sourceMp3Path);
        string targetPath = Path.Combine(musicFolder, fileName);

        if (File.Exists(targetPath))
        {
            string nameOnly = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            int copyIndex = 1;
            do
            {
                fileName = nameOnly + "_" + copyIndex + extension;
                targetPath = Path.Combine(musicFolder, fileName);
                copyIndex++;
            }
            while (File.Exists(targetPath));
        }

        File.Copy(sourceMp3Path, targetPath, false);

        int nextId = GetNextMusicId(configPath);

        string configLine = nextId + " " + fileName + (loop ? ",loop" : string.Empty);

        using (StreamWriter writer = new StreamWriter(configPath, append: true))
        {
            writer.WriteLine(configLine);
        }

        return new MusicEntry
        {
            Id = nextId,
            FileName = fileName,
            FullPath = targetPath,
            Loop = loop,
            Exists = true
        };
    }

    private static int GetNextMusicId(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return 0;
        }

        int maxId = -1;

        foreach (string rawLine in File.ReadAllLines(configPath))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith("#"))
            {
                continue;
            }

            int firstSpace = line.IndexOf(' ');
            if (firstSpace <= 0)
            {
                continue;
            }

            if (int.TryParse(line.Substring(0, firstSpace), out int id))
            {
                if (id > maxId)
                {
                    maxId = id;
                }
            }
        }

        return maxId + 1;
    }

    public void RemoveMusic(string uoFolderPath, MusicEntry entry, bool deleteMp3File)
    {
        if (entry == null)
        {
            return;
        }

        string musicFolder = Path.Combine(uoFolderPath, "Music", "Digital");
        string configPath = Path.Combine(musicFolder, "Config.txt");

        if (!File.Exists(configPath))
        {
            return;
        }

        List<string> keptLines = new();

        foreach (string rawLine in File.ReadAllLines(configPath))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith("#"))
            {
                keptLines.Add(rawLine);
                continue;
            }

            int firstSpace = line.IndexOf(' ');
            if (firstSpace <= 0)
            {
                keptLines.Add(rawLine);
                continue;
            }

            if (!int.TryParse(line.Substring(0, firstSpace), out int id))
            {
                keptLines.Add(rawLine);
                continue;
            }

            if (id == entry.Id)
            {
                continue;
            }

            keptLines.Add(rawLine);
        }

        File.WriteAllLines(configPath, keptLines);

        if (deleteMp3File && File.Exists(entry.FullPath))
        {
            File.Delete(entry.FullPath);
        }
    }

    public void UpdateMusicEntry(
    string uoFolderPath,
    MusicEntry originalEntry,
    int newId,
    string newFileName,
    bool loop)
    {
        if (originalEntry == null)
        {
            return;
        }

        string musicFolder = Path.Combine(uoFolderPath, "Music", "Digital");
        string configPath = Path.Combine(musicFolder, "Config.txt");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Config.txt was not found.", configPath);
        }

        newFileName = CleanMusicFileName(newFileName);

        if (!newFileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            newFileName += ".mp3";
        }

        string oldPath = originalEntry.FullPath;
        string newPath = Path.Combine(musicFolder, newFileName);

        if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(oldPath))
            {
                if (File.Exists(newPath))
                {
                    throw new InvalidOperationException("A music file with that name already exists.");
                }

                File.Move(oldPath, newPath);
            }
        }

        List<string> lines = new(File.ReadAllLines(configPath));

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i].Trim();

            if (line.Length == 0 || line.StartsWith("#"))
            {
                continue;
            }

            int firstSpace = line.IndexOf(' ');
            if (firstSpace <= 0)
            {
                continue;
            }

            if (!int.TryParse(line.Substring(0, firstSpace), out int id))
            {
                continue;
            }

            if (id == originalEntry.Id)
            {
                lines[i] = newId + " " + newFileName + (loop ? ",loop" : string.Empty);
                break;
            }
        }

        File.WriteAllLines(configPath, lines);
    }

    private static string CleanMusicFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "music.mp3";
        }

        string name = Path.GetFileName(value.Trim());

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }

        return name;
    }
}