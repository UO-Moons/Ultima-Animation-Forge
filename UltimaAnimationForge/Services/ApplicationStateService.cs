using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public class AnimationCacheService
{
    private readonly string cacheFolderPath;
    private const int CurrentCacheVersion = 3;

    public AnimationCacheService()
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

    public string GetCacheFilePath(string profileId)
    {
        string safeProfileId = string.IsNullOrWhiteSpace(profileId) ? "default" : profileId;
        return Path.Combine(cacheFolderPath, safeProfileId + ".cache.json");
    }

    public AnimationCacheData? LoadCache(string profileId)
    {
        try
        {
            string cachePath = GetCacheFilePath(profileId);

            if (!File.Exists(cachePath))
            {
                return null;
            }

            string json = File.ReadAllText(cachePath);
            return JsonSerializer.Deserialize<AnimationCacheData>(json);
        }
        catch
        {
            return null;
        }
    }

    public void SaveCache(AnimationCacheData cacheData)
    {
        string cachePath = GetCacheFilePath(cacheData.ProfileId);

        JsonSerializerOptions options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(cacheData, options);
        File.WriteAllText(cachePath, json);
    }

    public bool IsCacheValid(AnimationCacheData? cacheData, string folderPath)
    {
        if (cacheData == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return false;
        }

        if (cacheData.CacheVersion != CurrentCacheVersion)
        {
            return false;
        }

        if (!string.Equals(
                NormalizePath(cacheData.UoFolderPath),
                NormalizePath(folderPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        List<CachedSourceFileStamp> currentStamps = BuildSourceFileStamps(folderPath);

        if (cacheData.SourceFiles == null)
        {
            return false;
        }

        if (cacheData.SourceFiles.Count != currentStamps.Count)
        {
            return false;
        }

        Dictionary<string, CachedSourceFileStamp> cachedByPath =
            cacheData.SourceFiles.ToDictionary(
                x => NormalizePath(x.FilePath),
                x => x,
                StringComparer.OrdinalIgnoreCase);

        foreach (CachedSourceFileStamp current in currentStamps)
        {
            string normalizedPath = NormalizePath(current.FilePath);

            if (!cachedByPath.TryGetValue(normalizedPath, out CachedSourceFileStamp? cached))
            {
                return false;
            }

            if (cached.Length != current.Length)
            {
                return false;
            }

            if (cached.LastWriteTimeUtc != current.LastWriteTimeUtc)
            {
                return false;
            }
        }

        return true;
    }

    public AnimationCacheData BuildCacheData(
        string profileId,
        string folderPath,
        List<AnimationEntry> animationEntries,
        List<MulSlotEntry> mulSlotEntries)
    {
        return new AnimationCacheData
        {
            CacheVersion = CurrentCacheVersion,
            ProfileId = profileId,
            UoFolderPath = folderPath,
            CreatedUtc = DateTime.UtcNow,
            SourceFiles = BuildSourceFileStamps(folderPath),
            AnimationEntries = animationEntries
                .Where(x => !string.Equals(x.SourceMode, "UOP", StringComparison.OrdinalIgnoreCase))
                .Select(CloneAnimationEntry)
                .ToList(),
            MulSlotEntries = mulSlotEntries.Select(CloneMulSlotEntry).ToList()
        };
    }

    private List<CachedSourceFileStamp> BuildSourceFileStamps(string folderPath)
    {
        List<CachedSourceFileStamp> result = new List<CachedSourceFileStamp>();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return result;
        }

        string[] patterns =
        {
            "anim*.mul",
            "anim*.idx",
            "AnimationFrame*.uop",
            "bodyconv.def",
            "mobtypes.txt"
        };

        HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string pattern in patterns)
        {
            foreach (string filePath in Directory.GetFiles(folderPath, pattern, SearchOption.TopDirectoryOnly))
            {
                string normalized = NormalizePath(filePath);

                if (!seenPaths.Add(normalized))
                {
                    continue;
                }

                FileInfo fileInfo = new FileInfo(filePath);

                result.Add(new CachedSourceFileStamp
                {
                    FilePath = filePath,
                    Length = fileInfo.Length,
                    LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
                });
            }
        }

        result.Sort((left, right) =>
            string.Compare(
                NormalizePath(left.FilePath),
                NormalizePath(right.FilePath),
                StringComparison.OrdinalIgnoreCase));

        return result;
    }

    private AnimationEntry CloneAnimationEntry(AnimationEntry source)
    {
        return new AnimationEntry
        {
            DisplayName = source.DisplayName,
            SecondaryText = source.SecondaryText,
            BodyId = source.BodyId,
            ActionId = source.ActionId,
            FrameCount = source.FrameCount,
            FrameSize = source.FrameSize,
            SourceFile = source.SourceFile,
            SourceMode = source.SourceMode,
            IndexNumber = source.IndexNumber,
            Offset = source.Offset,
            Length = source.Length,
            Extra = source.Extra
        };
    }

    private MulSlotEntry CloneMulSlotEntry(MulSlotEntry source)
    {
        return new MulSlotEntry
        {
            FileName = source.FileName,
            FileType = source.FileType,
            BodyIndex = source.BodyIndex,
            TrueBodyId = source.TrueBodyId,
            AnimLength = source.AnimLength,
            IsEmpty = source.IsEmpty
        };
    }

    private string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public void DeleteCache(string profileId)
    {
        try
        {
            string cachePath = GetCacheFilePath(profileId);

            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }
        catch
        {
            // Ignore cache delete failures for now.
        }
    }

    public void DeleteAllCaches()
    {
        try
        {
            if (!Directory.Exists(cacheFolderPath))
            {
                return;
            }

            foreach (string filePath in Directory.GetFiles(cacheFolderPath, "*.cache.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    // Ignore individual delete failures.
                }
            }
        }
        catch
        {
            // Ignore folder-level failures for now.
        }
    }
}

public class SettingsService
{
    private readonly string settingsFilePath;

    public SettingsService()
    {
        string appFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UltimaAnimationForge");

        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }

        settingsFilePath = Path.Combine(appFolder, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(settingsFilePath))
            {
                return CreateDefaultSettings();
            }

            string json = File.ReadAllText(settingsFilePath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);

            if (settings == null)
            {
                return CreateDefaultSettings();
            }

            EnsureSettingsDefaults(settings);
            return settings;
        }
        catch
        {
            return CreateDefaultSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        EnsureSettingsDefaults(settings);

        JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(settings, jsonSerializerOptions);
        File.WriteAllText(settingsFilePath, json);
    }

    private AppSettings CreateDefaultSettings()
    {
        AppSettings settings = new AppSettings();

        AnimationViewerProfile defaultProfile = new AnimationViewerProfile
        {
            ProfileName = "Default"
        };

        settings.Profiles.Add(defaultProfile);
        settings.LastActiveProfileId = defaultProfile.ProfileId;

        return settings;
    }

    private void EnsureSettingsDefaults(AppSettings settings)
    {
        if (settings.Profiles == null)
        {
            settings.Profiles = new System.Collections.Generic.List<AnimationViewerProfile>();
        }

        foreach (AnimationViewerProfile profile in settings.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.ProfileId))
            {
                profile.ProfileId = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(profile.ProfileName))
            {
                profile.ProfileName = "Profile";
            }

            profile.UoFolderPath ??= string.Empty;
            profile.SelectedAnimationFile ??= "All Files";
            profile.SelectedBodyType ??= "All";
            profile.SearchText ??= string.Empty;
            profile.SelectedDirection ??= string.Empty;

            if (profile.PreviewZoomLevel <= 0)
            {
                profile.PreviewZoomLevel = 1.0;
            }

            // New defaults
            // Old settings files will simply pick these up automatically.
            profile.ShowCheckerBackground = profile.ShowCheckerBackground;
            profile.LoopPlayback = profile.LoopPlayback;
        }

        if (settings.Profiles.Count == 0)
        {
            AnimationViewerProfile defaultProfile = new AnimationViewerProfile
            {
                ProfileName = "Default"
            };

            settings.Profiles.Add(defaultProfile);
        }

        if (string.IsNullOrWhiteSpace(settings.LastActiveProfileId) ||
            !settings.Profiles.Any(x => x.ProfileId == settings.LastActiveProfileId))
        {
            settings.LastActiveProfileId = settings.Profiles[0].ProfileId;
        }
    }
}