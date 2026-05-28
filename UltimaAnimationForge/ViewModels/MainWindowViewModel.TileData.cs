// ViewModels/MainWindowViewModel.TileData.cs

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    public ObservableCollection<TileDataEntry> TileDataEntries { get; } = new();
    public ObservableCollection<TileDataEntry> FilteredTileDataEntries { get; } = new();
    public ObservableCollection<TileDataFlagOption> SelectedTileDataFlagOptions { get; } = new();
    private readonly TileDataMulService tileDataMulService = new();
    private static readonly string[] TileDataFlagNames =
{
    "Background", "Weapon", "Transparent", "Translucent", "Wall", "Damaging", "Impassable", "Wet",
    "Unknown1", "Surface", "Bridge", "Generic", "Window", "NoShoot", "ArticleA", "ArticleAn",
    "ArticleThe", "Foliage", "PartialHue", "NoHouse", "Map", "Container", "Wearable", "LightSource",
    "Animation", "HoverOver", "NoDiagonal", "Armor", "Roof", "Door", "StairBack", "StairRight",
    "AlphaBlend", "UseNewArt", "ArtUsed", "Unused8", "NoShadow", "PixelBleed", "PlayAnimOnce",
    "MultiMovable", "Unused10", "Unused11", "Unused12", "Unused13", "Unused14", "Unused15",
    "Unused16", "Unused17", "Unused18", "Unused19", "Unused20", "Unused21", "Unused22", "Unused23",
    "Unused24", "Unused25", "Unused26", "Unused27", "Unused28", "Unused29", "Unused30", "Unused31",
    "Unused32"
};

    [ObservableProperty]
    private bool applyTileDataFieldsToCheckedArt;

    [ObservableProperty]
    private bool applyTileDataFlagsToCheckedArt;

    private string loadedTileDataFolderPath = string.Empty;

    [ObservableProperty]
    private TileDataEntry? selectedTileDataEntry;

    [ObservableProperty]
    private string tileDataSearchText = string.Empty;

    [ObservableProperty]
    private bool showLandTileData = true;

    [ObservableProperty]
    private bool showItemTileData = true;

    public ICommand RefreshTileDataCommand { get; private set; } = null!;

    private void ResetTileDataForProfileChange()
    {
        TileDataEntries.Clear();
        FilteredTileDataEntries.Clear();
        SelectedTileDataFlagOptions.Clear();
        SelectedTileDataEntry = null;
        loadedTileDataFolderPath = string.Empty;
    }

    private void InitializeTileDataCommands()
    {
        RefreshTileDataCommand = new RelayCommand(LoadTileData);
    }

    private void LoadTileData()
    {
        TileDataEntries.Clear();
        FilteredTileDataEntries.Clear();
        SelectedTileDataFlagOptions.Clear();
        SelectedTileDataEntry = null;

        string folderPath = GetCurrentFolderPath();
        loadedTileDataFolderPath = folderPath;

        string tileDataPath = Path.Combine(folderPath, "tiledata.mul");

        if (!File.Exists(tileDataPath))
        {
            StatusText = "tiledata.mul was not found.";
            return;
        }

        List<TileDataEntry> entries = tileDataMulService.Load(tileDataPath);

        foreach (TileDataEntry entry in entries)
        {
            TileDataEntries.Add(entry);
        }

        ApplyTileDataFilter();
        StatusText = "Loaded tiledata entries: " + TileDataEntries.Count;
    }

    private void ApplyTileDataFilter()
    {
        FilteredTileDataEntries.Clear();

        string search = TileDataSearchText.Trim();

        IEnumerable<TileDataEntry> query = TileDataEntries;

        if (!ShowLandTileData)
        {
            query = query.Where(x => !x.IsLand);
        }

        if (!ShowItemTileData)
        {
            query = query.Where(x => x.IsLand);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Id.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.IdText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        foreach (TileDataEntry entry in query)
        {
            FilteredTileDataEntries.Add(entry);
        }

        if (FilteredTileDataEntries.Count > 0)
        {
            SelectedTileDataEntry ??= FilteredTileDataEntries[0];
        }
        else
        {
            SelectedTileDataEntry = null;
        }
    }

    partial void OnTileDataSearchTextChanged(string value)
    {
        ApplyTileDataFilter();
    }

    partial void OnShowLandTileDataChanged(bool value)
    {
        ApplyTileDataFilter();
    }

    partial void OnShowItemTileDataChanged(bool value)
    {
        ApplyTileDataFilter();
    }

    partial void OnSelectedTileDataEntryChanged(TileDataEntry? value)
    {
        RebuildSelectedTileDataFlags();
    }

    public void RebuildSelectedTileDataFlags()
    {
        SelectedTileDataFlagOptions.Clear();

        ulong flags = SelectedTileDataEntry?.Flags ?? 0;

        for (int i = 0; i < TileDataFlagNames.Length; i++)
        {
            ulong mask = 1UL << i;

            SelectedTileDataFlagOptions.Add(new TileDataFlagOption
            {
                BitIndex = i,
                Name = TileDataFlagNames[i],
                Description = "0x" + mask.ToString("X16"),
                IsChecked = (flags & mask) != 0
            });
        }
    }

    [RelayCommand]
    private void ToggleSelectedTileDataFlag(TileDataFlagOption? option)
    {
        if (option == null)
        {
            return;
        }

        List<(TileDataEntry tileDataEntry, ArtEntry? artEntry)> targets = new();

        if (ApplyTileDataFlagsToCheckedArt)
        {
            Dictionary<string, TileDataEntry> tileDataByKey = TileDataEntries.ToDictionary(
                tile => (tile.IsLand ? "L:" : "S:") + tile.Id,
                tile => tile);

            foreach (ArtEntry artEntry in ArtEntries.Where(x => x.IsChecked))
            {
                bool isLand = string.Equals(artEntry.Type, "Land", StringComparison.OrdinalIgnoreCase);
                string key = (isLand ? "L:" : "S:") + artEntry.ArtId;

                if (tileDataByKey.TryGetValue(key, out TileDataEntry? tileDataEntry))
                {
                    targets.Add((tileDataEntry, artEntry));
                }
            }
        }
        else if (SelectedTileDataEntry != null)
        {
            targets.Add((SelectedTileDataEntry, SelectedArtEntry));
        }

        if (targets.Count == 0)
        {
            return;
        }

        foreach ((TileDataEntry target, ArtEntry? artEntry) in targets)
        {
            if (option.IsChecked)
            {
                target.Flags |= option.Mask;
            }
            else
            {
                target.Flags &= ~option.Mask;
            }

            target.IsEdited = target.Flags != target.OriginalFlags;

            if (artEntry != null)
            {
                artEntry.IsPendingTileDataChange = target.IsEdited;
            }
        }

        OnPropertyChanged(nameof(SelectedTileDataEntry));
        OnPropertyChanged(nameof(SelectedArtTileDataEntry));
        OnPropertyChanged(nameof(SelectedArtTileDataFlagNames));
    }

    [RelayCommand]
    private void SaveTileDataChanges()
    {
        List<TileDataEntry> editedEntries = TileDataEntries
            .Where(entry => entry.IsEdited)
            .ToList();

        if (editedEntries.Count == 0)
        {
            StatusText = "No TileData changes to save.";
            return;
        }

        string folderPath = GetCurrentFolderPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            StatusText = "Choose a UO folder first.";
            return;
        }

        bool success = tileDataMulService.SaveTileData(folderPath, TileDataEntries.ToList(), out string message);
        StatusText = message;

        if (!success)
        {
            return;
        }

        foreach (TileDataEntry entry in editedEntries)
        {
            entry.AcceptChanges();
        }
    }

    private List<(TileDataEntry tileDataEntry, ArtEntry? artEntry)> GetTileDataEditTargets()
    {
        List<(TileDataEntry tileDataEntry, ArtEntry? artEntry)> targets = new();

        if (ApplyTileDataFieldsToCheckedArt)
        {
            Dictionary<string, TileDataEntry> tileDataByKey = TileDataEntries.ToDictionary(
                tile => (tile.IsLand ? "L:" : "S:") + tile.Id,
                tile => tile);

            foreach (ArtEntry artEntry in ArtEntries.Where(x => x.IsChecked))
            {
                bool isLand = string.Equals(artEntry.Type, "Land", StringComparison.OrdinalIgnoreCase);
                string key = (isLand ? "L:" : "S:") + artEntry.ArtId;

                if (tileDataByKey.TryGetValue(key, out TileDataEntry? tileDataEntry))
                {
                    targets.Add((tileDataEntry, artEntry));
                }
            }
        }
        else if (SelectedArtTileDataEntry != null)
        {
            targets.Add((SelectedArtTileDataEntry, SelectedArtEntry));
        }

        return targets;
    }

    private void MarkTileDataTargetsDirty(List<(TileDataEntry tileDataEntry, ArtEntry? artEntry)> targets)
    {
        foreach ((TileDataEntry tileDataEntry, ArtEntry? artEntry) in targets)
        {
            tileDataEntry.IsEdited = tileDataEntry.IsDifferentFromOriginal();

            if (artEntry != null)
            {
                artEntry.IsPendingTileDataChange = tileDataEntry.IsEdited;
            }
        }

        OnPropertyChanged(nameof(SelectedArtTileDataEntry));
        OnPropertyChanged(nameof(SelectedArtTileDataFlagNames));
        OnPropertyChanged(nameof(SelectedArtAnimationGumpText));
    }

    public void CommitArtTileDataFieldEdits()
    {
        TileDataEntry? source = SelectedArtTileDataEntry;
        if (source == null)
        {
            return;
        }

        List<(TileDataEntry tileDataEntry, ArtEntry? artEntry)> targets = GetTileDataEditTargets();

        bool hasSelectedTarget = targets.Any(target =>
            ReferenceEquals(target.tileDataEntry, source));

        if (!hasSelectedTarget)
        {
            targets.Add((source, SelectedArtEntry));
        }

        if (targets.Count == 0)
        {
            return;
        }

        foreach ((TileDataEntry target, _) in targets)
        {
            target.Name = source.Name;
            target.Animation = source.Animation;
            target.Weight = source.Weight;
            target.Quality = source.Quality;
            target.Quantity = source.Quantity;
            target.Hue = source.Hue;
            target.StackingOffset = source.StackingOffset;
            target.Value = source.Value;
            target.Height = source.Height;
        }

        MarkTileDataTargetsDirty(targets);

        RefreshSelectedArtEquipmentGump();

        OnPropertyChanged(nameof(SelectedArtTileDataEntry));
        OnPropertyChanged(nameof(SelectedArtTileDataFlagNames));
        OnPropertyChanged(nameof(SelectedArtAnimationGumpText));
    }
}
