using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    public ObservableCollection<RadarColEntry> RadarColEntries { get; } = new();

    [ObservableProperty]
    private RadarColEntry? selectedRadarColEntry;

    [ObservableProperty]
    private string radarColSearchText = string.Empty;

    [ObservableProperty]
    private bool showRadarColLand = true;

    [ObservableProperty]
    private bool showRadarColStatics = true;

    [ObservableProperty]
    private bool showRadarColDifferentOnly;

    [ObservableProperty]
    private int radarColRed;

    [ObservableProperty]
    private int radarColGreen;

    [ObservableProperty]
    private int radarColBlue;

    [ObservableProperty]
    private int radarColRangeFrom;

    [ObservableProperty]
    private int radarColRangeTo;

    partial void OnRadarColSearchTextChanged(string value)
    {
        RebuildRadarColEntries();
    }

    partial void OnShowRadarColLandChanged(bool value)
    {
        RebuildRadarColEntries();
    }

    partial void OnShowRadarColStaticsChanged(bool value)
    {
        RebuildRadarColEntries();
    }

    partial void OnShowRadarColDifferentOnlyChanged(bool value)
    {
        RebuildRadarColEntries();
    }

    partial void OnSelectedRadarColEntryChanged(RadarColEntry? value)
    {
        if (value == null)
        {
            return;
        }

        Color color = RadarColService.ConvertUoColorToAvaloniaColor(value.CurrentColorValue);

        RadarColRed = color.R;
        RadarColGreen = color.G;
        RadarColBlue = color.B;
    }

    [RelayCommand]
    private void RebuildRadarColEntries()
    {
        RadarColEntries.Clear();

        string folderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            ArtStatusText = "Open a UO folder before using RadarCol.";
            return;
        }

        bool artLoaded = artDataService.Initialize(folderPath);

        if (!artLoaded)
        {
            ArtStatusText = "Could not initialize art data for RadarCol.";
            return;
        }

        if (!radarColService.IsLoaded)
        {
            bool radarLoaded = radarColService.Load(folderPath, out string radarMessage);

            if (!radarLoaded)
            {
                ArtStatusText = radarMessage;
                return;
            }
        }

        if (!radarColService.IsLoaded)
        {
            ArtStatusText = "radarcol.mul is not loaded.";
            return;
        }

        List<CachedRadarColEntry> cachedEntries = LoadCachedOrBuildRadarColEntries(folderPath);

        foreach (CachedRadarColEntry cachedEntry in cachedEntries)
        {
            if (cachedEntry.IsLand && !ShowRadarColLand)
            {
                continue;
            }

            if (!cachedEntry.IsLand && !ShowRadarColStatics)
            {
                continue;
            }

            if (!MatchesRadarColSearch(cachedEntry, RadarColSearchText))
            {
                continue;
            }

            ArtEntry artEntry = new()
            {
                ArtId = cachedEntry.ArtId,
                FileIndex = cachedEntry.FileIndex,
                Type = cachedEntry.Type,
                SecondaryText = cachedEntry.SecondaryText,
                IsFreeSlot = cachedEntry.IsFreeSlot
            };

            ushort currentColor = radarColService.GetColor(artEntry);

            if (ShowRadarColDifferentOnly && currentColor == cachedEntry.GeneratedColorValue)
            {
                continue;
            }

            RadarColEntries.Add(new RadarColEntry
            {
                IsLand = cachedEntry.IsLand,
                ArtId = cachedEntry.ArtId,
                CurrentColorValue = currentColor,
                GeneratedColorValue = cachedEntry.GeneratedColorValue,
                Thumbnail = artDataService.LoadThumbnailCached(artEntry),
                IsPendingChange = radarColService.HasPendingChange(artEntry)
            });
        }

        SelectedRadarColEntry = RadarColEntries.Count > 0
            ? RadarColEntries[0]
            : null;

        ArtStatusText = "Loaded " + RadarColEntries.Count + " RadarCol entries.";
    }

    private List<CachedRadarColEntry> LoadCachedOrBuildRadarColEntries(string folderPath)
    {
        if (activeProfile == null)
        {
            activeProfile = GetActiveProfile();
        }

        string profileId = activeProfile?.ProfileId ?? "default";

        RadarColCacheData? cache = artAndRadarCacheService.LoadRadarColCache(profileId);

        if (artAndRadarCacheService.IsRadarColCacheValid(cache, folderPath) && cache?.Entries != null)
        {
            return cache.Entries;
        }

        List<CachedRadarColEntry> builtEntries = new();

        foreach (ArtEntry artEntry in artDataService.BuildEntries(
                     true,
                     true,
                     false,
                     string.Empty))
        {
            WriteableBitmap? bitmap = artDataService.LoadThumbnailCached(artEntry);

            Color generatedColor = artRadarColorService.GetAverageVisibleColor(bitmap);
            ushort generatedUoColor = artRadarColorService.ConvertColorToUoColor(generatedColor);

            bool isLand = string.Equals(artEntry.Type, "Land", StringComparison.OrdinalIgnoreCase);

            builtEntries.Add(new CachedRadarColEntry
            {
                IsLand = isLand,
                ArtId = artEntry.ArtId,
                FileIndex = artEntry.FileIndex,
                Type = artEntry.Type,
                SecondaryText = artEntry.SecondaryText,
                IsFreeSlot = artEntry.IsFreeSlot,
                GeneratedColorValue = generatedUoColor
            });
        }

        artAndRadarCacheService.SaveRadarColCache(profileId, folderPath, builtEntries);

        return builtEntries;
    }

    private static bool MatchesRadarColSearch(CachedRadarColEntry entry, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        string search = searchText.Trim();

        return entry.ArtId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
               ("0x" + entry.ArtId.ToString("X4")).Contains(search, StringComparison.OrdinalIgnoreCase) ||
               entry.Type.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void ApplyGeneratedRadarColToSelected()
    {
        if (SelectedRadarColEntry == null)
        {
            ArtStatusText = "No RadarCol entry selected.";
            return;
        }

        ArtEntry artEntry = BuildArtEntryFromRadarColEntry(SelectedRadarColEntry);

        bool success = radarColService.SetColor(
            artEntry,
            SelectedRadarColEntry.GeneratedColorValue,
            out string message);

        ArtStatusText = message;

        if (success)
        {
            SelectedRadarColEntry.CurrentColorValue = SelectedRadarColEntry.GeneratedColorValue;
            SelectedRadarColEntry.IsPendingChange = radarColService.HasPendingChange(artEntry);

            RebuildRadarColEntries();
            RefreshSelectedArtRadarColInfo();
        }
    }

    [RelayCommand]
    private void ApplyGeneratedRadarColToChecked()
    {
        int applied = 0;
        int failed = 0;
        string lastError = string.Empty;

        foreach (RadarColEntry entry in RadarColEntries.Where(x => x.IsChecked))
        {
            ArtEntry artEntry = BuildArtEntryFromRadarColEntry(entry);

            bool success = radarColService.SetColor(
                artEntry,
                entry.GeneratedColorValue,
                out string message);

            if (success)
            {
                applied++;
            }
            else
            {
                failed++;
                lastError = message;
            }
        }

        RebuildRadarColEntries();
        RefreshSelectedArtRadarColInfo();

        ArtStatusText =
            "Applied generated RadarCol colors. Applied " +
            applied +
            ", failed " +
            failed +
            (string.IsNullOrWhiteSpace(lastError) ? "." : ". Last error: " + lastError);
    }

    [RelayCommand]
    private void RevertSelectedRadarCol()
    {
        if (SelectedRadarColEntry == null)
        {
            ArtStatusText = "No RadarCol entry selected.";
            return;
        }

        ArtEntry artEntry = BuildArtEntryFromRadarColEntry(SelectedRadarColEntry);

        bool success = radarColService.RevertSelected(artEntry, out string message);
        ArtStatusText = message;

        if (success)
        {
            RebuildRadarColEntries();
            RefreshSelectedArtRadarColInfo();
        }
    }

    [RelayCommand]
    private void SaveRadarCol()
    {
        bool success = radarColService.Save(out string message);
        ArtStatusText = message;

        if (success)
        {
            RebuildRadarColEntries();
            RefreshSelectedArtRadarColInfo();
        }
    }

    private static ArtEntry BuildArtEntryFromRadarColEntry(RadarColEntry radarEntry)
    {
        return new ArtEntry
        {
            ArtId = radarEntry.ArtId,
            FileIndex = radarEntry.IsLand ? radarEntry.ArtId : radarEntry.ArtId + 0x4000,
            Type = radarEntry.IsLand ? "Land" : "Static"
        };
    }

    [RelayCommand]
    private void LoadRadarCol()
    {
        string folderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            ArtStatusText = "Open a UO folder before loading radarcol.mul.";
            return;
        }

        // Make RadarCol tab independent from opening the Art tab first.
        bool artLoaded = artDataService.Initialize(folderPath);

        if (!artLoaded)
        {
            ArtStatusText = "Could not initialize art data for RadarCol.";
            return;
        }

        bool success = radarColService.Load(folderPath, out string message);
        ArtStatusText = message;

        if (success)
        {
            RebuildRadarColEntries();
            RefreshSelectedArtRadarColInfo();
        }
    }

    [RelayCommand]
    private void ApplyManualRadarColToSelected()
    {
        if (SelectedRadarColEntry == null)
        {
            ArtStatusText = "No RadarCol entry selected.";
            return;
        }

        Color color = Color.FromRgb(
            (byte)Math.Clamp(RadarColRed, 0, 255),
            (byte)Math.Clamp(RadarColGreen, 0, 255),
            (byte)Math.Clamp(RadarColBlue, 0, 255));

        ushort uoColor = artRadarColorService.ConvertColorToUoColor(color);
        ArtEntry artEntry = BuildArtEntryFromRadarColEntry(SelectedRadarColEntry);

        bool success = radarColService.SetColor(artEntry, uoColor, out string message);
        ArtStatusText = message;

        if (success)
        {
            RebuildRadarColEntries();
            RefreshSelectedArtRadarColInfo();
        }
    }

    [RelayCommand]
    private void ApplyGeneratedRadarColToRange()
    {
        int from = Math.Min(RadarColRangeFrom, RadarColRangeTo);
        int to = Math.Max(RadarColRangeFrom, RadarColRangeTo);

        int applied = 0;
        int failed = 0;
        string lastError = string.Empty;

        foreach (RadarColEntry entry in RadarColEntries.Where(x => x.ArtId >= from && x.ArtId <= to))
        {
            ArtEntry artEntry = BuildArtEntryFromRadarColEntry(entry);

            bool success = radarColService.SetColor(
                artEntry,
                entry.GeneratedColorValue,
                out string message);

            if (success)
            {
                applied++;
            }
            else
            {
                failed++;
                lastError = message;
            }
        }

        RebuildRadarColEntries();
        RefreshSelectedArtRadarColInfo();

        ArtStatusText =
            "Applied generated RadarCol colors to range " +
            from + "-" + to +
            ". Applied " + applied +
            ", failed " + failed +
            (string.IsNullOrWhiteSpace(lastError) ? "." : ". Last error: " + lastError);
    }

    [RelayCommand]
    private void RevertAllRadarCol()
    {
        bool success = radarColService.RevertAll(out string message);
        ArtStatusText = message;

        if (success)
        {
            RebuildRadarColEntries();
            RefreshSelectedArtRadarColInfo();
        }
    }

    private bool TryGetAverageGeneratedColorFromCheckedRadarCol(out ushort averageColor, out string message)
    {
        averageColor = 0;

        List<RadarColEntry> checkedEntries = RadarColEntries
            .Where(entry => entry.IsChecked)
            .ToList();

        if (checkedEntries.Count == 0)
        {
            message = "No checked RadarCol entries.";
            return false;
        }

        long totalR = 0;
        long totalG = 0;
        long totalB = 0;

        foreach (RadarColEntry entry in checkedEntries)
        {
            Color color = RadarColService.ConvertUoColorToAvaloniaColor(entry.GeneratedColorValue);

            totalR += color.R;
            totalG += color.G;
            totalB += color.B;
        }

        Color averageColorRgb = Color.FromRgb(
            (byte)(totalR / checkedEntries.Count),
            (byte)(totalG / checkedEntries.Count),
            (byte)(totalB / checkedEntries.Count));

        averageColor = artRadarColorService.ConvertColorToUoColor(averageColorRgb);
        message = "Average generated color calculated from " + checkedEntries.Count + " checked entries.";
        return true;
    }

    [RelayCommand]
    private void ApplyCheckedAverageRadarColToSelected()
    {
        if (SelectedRadarColEntry == null)
        {
            ArtStatusText = "No RadarCol entry selected.";
            return;
        }

        if (!TryGetAverageGeneratedColorFromCheckedRadarCol(out ushort averageColor, out string averageMessage))
        {
            ArtStatusText = averageMessage;
            return;
        }

        ArtEntry artEntry = BuildArtEntryFromRadarColEntry(SelectedRadarColEntry);

        bool success = radarColService.SetColor(
            artEntry,
            averageColor,
            out string message);

        ArtStatusText = success
            ? "Applied checked average to selected. " + averageMessage
            : message;

        if (success)
        {
            RebuildRadarColEntries();
            RefreshSelectedArtRadarColInfo();
        }
    }

    [RelayCommand]
    private void ApplyCheckedAverageRadarColToChecked()
    {
        if (!TryGetAverageGeneratedColorFromCheckedRadarCol(out ushort averageColor, out string averageMessage))
        {
            ArtStatusText = averageMessage;
            return;
        }

        List<RadarColEntry> checkedEntries = RadarColEntries
            .Where(entry => entry.IsChecked)
            .ToList();

        int applied = 0;
        int failed = 0;
        string lastError = string.Empty;

        foreach (RadarColEntry entry in checkedEntries)
        {
            ArtEntry artEntry = BuildArtEntryFromRadarColEntry(entry);

            bool success = radarColService.SetColor(
                artEntry,
                averageColor,
                out string message);

            if (success)
            {
                applied++;
            }
            else
            {
                failed++;
                lastError = message;
            }
        }

        RebuildRadarColEntries();
        RefreshSelectedArtRadarColInfo();

        ArtStatusText =
            "Applied checked average color. Applied " +
            applied +
            ", failed " +
            failed +
            ". " +
            averageMessage +
            (string.IsNullOrWhiteSpace(lastError) ? string.Empty : " Last error: " + lastError);
    }
}