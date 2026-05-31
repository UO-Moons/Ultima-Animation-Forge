using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class ArtAndRadarCacheService
{
    private const int ArtCacheVersion = 1;
    private const int RadarColCacheVersion = 1;

    private readonly string cacheFolderPath;

    public ArtAndRadarCacheService()
    {
        string appFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UltimaAnimationForge");

        cacheFolderPath = Path.Combine(appFolder, "Cache");

        if (!Directory.Exists(cacheFolderPath))
        {
            Directory.CreateDirectory(cacheFolderPath);
        }
    }

    public ArtCacheData? LoadArtCache(string profileId)
    {
        return LoadJson<ArtCacheData>(GetArtCachePath(profileId));
    }

    public RadarColCacheData? LoadRadarColCache(string profileId)
    {
        return LoadJson<RadarColCacheData>(GetRadarColCachePath(profileId));
    }

    public void SaveArtCache(string profileId, string folderPath, List<ArtEntry> entries)
    {
        ArtCacheData data = new()
        {
            CacheVersion = ArtCacheVersion,
            UoFolderPath = folderPath,
            CreatedUtc = DateTime.UtcNow,
            SourceFiles = BuildArtSourceFileStamps(folderPath),
            ArtEntries = entries.Select(x => new CachedArtEntry
            {
                IsFreeSlot = x.IsFreeSlot,
                ArtId = x.ArtId,
                FileIndex = x.FileIndex,
                Type = x.Type,
                SecondaryText = x.SecondaryText
            }).ToList()
        };

        SaveJson(GetArtCachePath(profileId), data);
    }

    public void SaveRadarColCache(string profileId, string folderPath, List<CachedRadarColEntry> entries)
    {
        RadarColCacheData data = new()
        {
            CacheVersion = RadarColCacheVersion,
            UoFolderPath = folderPath,
            CreatedUtc = DateTime.UtcNow,
            SourceFiles = BuildRadarColSourceFileStamps(folderPath),
            Entries = entries
        };

        SaveJson(GetRadarColCachePath(profileId), data);
    }

    public bool IsArtCacheValid(ArtCacheData? data, string folderPath)
    {
        return data != null &&
               data.CacheVersion == ArtCacheVersion &&
               PathsMatch(data.UoFolderPath, folderPath) &&
               StampsMatch(data.SourceFiles, BuildArtSourceFileStamps(folderPath));
    }

    public bool IsRadarColCacheValid(RadarColCacheData? data, string folderPath)
    {
        return data != null &&
               data.CacheVersion == RadarColCacheVersion &&
               PathsMatch(data.UoFolderPath, folderPath) &&
               StampsMatch(data.SourceFiles, BuildRadarColSourceFileStamps(folderPath));
    }

    public void DeleteArtCache(string profileId)
    {
        DeleteFile(GetArtCachePath(profileId));
    }

    public void DeleteRadarColCache(string profileId)
    {
        DeleteFile(GetRadarColCachePath(profileId));
    }

    private string GetArtCachePath(string profileId)
    {
        return Path.Combine(cacheFolderPath, SafeProfileId(profileId) + ".art.cache.json");
    }

    private string GetRadarColCachePath(string profileId)
    {
        return Path.Combine(cacheFolderPath, SafeProfileId(profileId) + ".radarcol.cache.json");
    }

    private static string SafeProfileId(string profileId)
    {
        return string.IsNullOrWhiteSpace(profileId) ? "default" : profileId;
    }

    private static List<CachedSourceFileStamp> BuildArtSourceFileStamps(string folderPath)
    {
        return BuildSourceFileStamps(folderPath, new[]
        {
            "artLegacyMUL.uop",
            "art.mul",
            "artidx.mul",
            "tiledata.mul",
            "animdata.mul"
        });
    }

    private static List<CachedSourceFileStamp> BuildRadarColSourceFileStamps(string folderPath)
    {
        return BuildSourceFileStamps(folderPath, new[]
        {
            "artLegacyMUL.uop",
            "art.mul",
            "artidx.mul",
            "radarcol.mul"
        });
    }

    private static List<CachedSourceFileStamp> BuildSourceFileStamps(string folderPath, string[] patterns)
    {
        List<CachedSourceFileStamp> result = new();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return result;
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string pattern in patterns)
        {
            foreach (string filePath in Directory.GetFiles(folderPath, pattern, SearchOption.TopDirectoryOnly))
            {
                string normalized = NormalizePath(filePath);

                if (!seen.Add(normalized))
                {
                    continue;
                }

                FileInfo info = new(filePath);

                result.Add(new CachedSourceFileStamp
                {
                    FilePath = filePath,
                    Length = info.Length,
                    LastWriteTimeUtc = info.LastWriteTimeUtc
                });
            }
        }

        return result
            .OrderBy(x => NormalizePath(x.FilePath), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool StampsMatch(List<CachedSourceFileStamp>? cached, List<CachedSourceFileStamp> current)
    {
        if (cached == null || cached.Count != current.Count)
        {
            return false;
        }

        Dictionary<string, CachedSourceFileStamp> cachedByPath = cached.ToDictionary(
            x => NormalizePath(x.FilePath),
            x => x,
            StringComparer.OrdinalIgnoreCase);

        foreach (CachedSourceFileStamp stamp in current)
        {
            string key = NormalizePath(stamp.FilePath);

            if (!cachedByPath.TryGetValue(key, out CachedSourceFileStamp? old))
            {
                return false;
            }

            if (old.Length != stamp.Length || old.LastWriteTimeUtc != stamp.LastWriteTimeUtc)
            {
                return false;
            }
        }

        return true;
    }

    private static bool PathsMatch(string left, string right)
    {
        return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static T? LoadJson<T>(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch
        {
            return default;
        }
    }

    private static void SaveJson<T>(string path, T data)
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        File.WriteAllText(path, JsonSerializer.Serialize(data, options));
    }

    private static void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}