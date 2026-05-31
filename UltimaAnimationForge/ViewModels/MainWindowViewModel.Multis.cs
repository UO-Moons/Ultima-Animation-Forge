using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;
using UltimaAnimationForge.Views;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    private readonly MultiDataService multiDataService = new();

    public ObservableCollection<MultiEntry> MultiEntries { get; } = new();

    public ObservableCollection<string> MultiPreviewModes { get; } = new()
{
    "Component Map",
    "Rendered Tiles"
};

    public ObservableCollection<string> MultiSourceOptions { get; } = new()
{
    "multi.mul / multi.idx",
    "multicollection.uop"
};

    [ObservableProperty]
    private string multiSearchText = string.Empty;

    [ObservableProperty]
    private string selectedMultiFilter = "All";

    public ObservableCollection<string> MultiFilterOptions { get; } = new()
{
    "All",
    "Used Only",
    "Free Only"
};

    private readonly List<MultiEntry> allMultiEntries = new();

    [ObservableProperty]
    private string selectedMultiSource = "multi.mul / multi.idx";

    [ObservableProperty]
    private string selectedMultiPreviewMode = "Component Map";

    [ObservableProperty]
    private MultiEntry? selectedMulti;

    [ObservableProperty]
    private WriteableBitmap? selectedMultiPreview;

    [ObservableProperty]
    private string selectedMultiComponentsText = string.Empty;

    [ObservableProperty]
    private string multiStatusText = "Load multis to begin.";

    [ObservableProperty]
    private bool showMultiFreeSlots;

    [ObservableProperty]
    private int multiHeightCut = 127;

    [ObservableProperty]
    private bool useUohsaMultiFormat;

    partial void OnSelectedMultiChanged(MultiEntry? value)
    {
        RefreshSelectedMulti();
    }

    partial void OnMultiHeightCutChanged(int value)
    {
        RefreshSelectedMultiPreview();
    }

    partial void OnSelectedMultiPreviewModeChanged(string value)
    {
        RefreshSelectedMultiPreview();
    }

    partial void OnSelectedMultiSourceChanged(string value)
    {
        LoadMultis();
    }

    partial void OnMultiSearchTextChanged(string value)
    {
        ApplyMultiFilter();
    }

    partial void OnSelectedMultiFilterChanged(string value)
    {
        ApplyMultiFilter();
    }

    [RelayCommand]
    private void LoadMultis()
    {
        string folderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            MultiStatusText = "Open a valid UO folder first.";
            return;
        }

        bool loaded;

        if (SelectedMultiSource == "multicollection.uop")
        {
            loaded = multiDataService.LoadUop(folderPath, out string message);
            MultiStatusText = message;
        }
        else
        {
            loaded = multiDataService.Load(folderPath, UseUohsaMultiFormat, out string message);
            MultiStatusText = message;
        }

        allMultiEntries.Clear();
        MultiEntries.Clear();

        for (int id = 0; id < MultiDataService.MaximumMultiIndex; id++)
        {
            bool exists = multiDataService.LoadedMultis.TryGetValue(id, out var parts);
            bool empty = !exists || parts == null || parts.Count == 0;

            if (!ShowMultiFreeSlots && empty)
            {
                continue;
            }

            allMultiEntries.Add(new MultiEntry
            {
                Id = id,
                IsEmpty = empty,
                ComponentCount = parts?.Count ?? 0
            });
        }

        ApplyMultiFilter();
    }

    [RelayCommand]
    private void ToggleMultiFreeSlots()
    {
        ShowMultiFreeSlots = !ShowMultiFreeSlots;
        LoadMultis();
    }

    private void RefreshSelectedMulti()
    {
        SelectedMultiPreview = null;
        SelectedMultiComponentsText = string.Empty;

        if (SelectedMulti == null)
        {
            return;
        }

        var components = multiDataService.GetComponents(SelectedMulti.Id);

        if (components.Count == 0)
        {
            MultiStatusText = $"Multi {SelectedMulti.Id} ({SelectedMulti.IdHex}) is empty.";
            return;
        }

        BuildMultiComponentsText(components);
        RefreshSelectedMultiPreview();

        MultiStatusText = $"Multi {SelectedMulti.Id} ({SelectedMulti.IdHex}) loaded. Components: {components.Count}.";
    }

    private void RefreshSelectedMultiPreview()
    {
        if (SelectedMulti == null)
        {
            SelectedMultiPreview = null;
            return;
        }

        var components = multiDataService.GetComponents(SelectedMulti.Id);

        if (SelectedMultiPreviewMode == "Rendered Tiles")
        {
            SelectedMultiPreview = multiDataService.BuildRenderedPreview(
     components,
     MultiHeightCut,
     TryGetStaticArtBitmap,
     GetMultiTileDataHeight,
     GetMultiTileDataBackground);

            return;
        }

        SelectedMultiPreview = multiDataService.BuildSimplePreview(components, MultiHeightCut);
    }

    private void BuildMultiComponentsText(List<MultiComponentEntry> components)
    {
        StringBuilder builder = new();

        builder.AppendLine("ItemID   X    Y    Z   Flags      Unknown");
        builder.AppendLine("------------------------------------------");

        foreach (MultiComponentEntry part in components)
        {
            builder.AppendLine(
                $"0x{part.ItemId:X4} {part.X,4} {part.Y,4} {part.Z,4} 0x{part.Flags:X8} {part.Unknown,6}");
        }

        SelectedMultiComponentsText = builder.ToString();
    }

    private WriteableBitmap? TryGetStaticArtBitmap(int artId)
    {
        ArtEntry? entry = ArtEntries.FirstOrDefault(x =>
            x.ArtId == artId &&
            !x.IsFreeSlot &&
            string.Equals(x.Type, "Static", StringComparison.OrdinalIgnoreCase));

        return entry?.Thumbnail;
    }

    private int GetMultiTileDataHeight(int itemId)
    {
        TileDataEntry? entry = TileDataEntries.FirstOrDefault(x =>
            !x.IsLand &&
            x.Id == itemId);

        return entry?.Height ?? 0;
    }

    private bool GetMultiTileDataBackground(int itemId)
    {
        TileDataEntry? entry = TileDataEntries.FirstOrDefault(x =>
            !x.IsLand &&
            x.Id == itemId);

        if (entry == null)
        {
            return true;
        }

        const ulong BackgroundFlag = 1UL << 0;
        return (entry.Flags & BackgroundFlag) != 0;
    }

    [RelayCommand]
    private async Task ExportSelectedMultiUox3()
    {
        if (SelectedMulti == null)
        {
            MultiStatusText = "No multi selected.";
            return;
        }

        List<MultiComponentEntry> components = multiDataService.GetComponents(SelectedMulti.Id);

        if (components.Count == 0)
        {
            MultiStatusText = "Selected multi is empty.";
            return;
        }

        Window? mainWindow = GetMainWindow();

        if (mainWindow == null)
        {
            MultiStatusText = "Could not locate main window.";
            return;
        }

        string suggestedName = "multi_0x" + SelectedMulti.Id.ToString("X4") + "_uox3.dfn";

        IStorageFile? file = await mainWindow.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export Multi as UOX3 DFN",
                SuggestedFileName = suggestedName,
                FileTypeChoices = new[]
                {
                new FilePickerFileType("UOX3 DFN")
                {
                    Patterns = new[] { "*.dfn", "*.txt" }
                }
                }
            });

        if (file == null)
        {
            MultiStatusText = "Export cancelled.";
            return;
        }

        string? path = file.TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(path))
        {
            MultiStatusText = "Selected export path is invalid.";
            return;
        }

        multiDataService.ExportUox3(path, SelectedMulti.Id, components);

        MultiStatusText = "Exported multi " + SelectedMulti.IdHex + " to UOX3 DFN.";
    }

    [RelayCommand]
    private async Task ExportSelectedMultiPng()
    {
        if (SelectedMulti == null)
        {
            MultiStatusText = "No multi selected.";
            return;
        }

        if (SelectedMultiPreview == null)
        {
            MultiStatusText = "No preview image to export.";
            return;
        }

        Window? mainWindow = GetMainWindow();

        if (mainWindow == null)
        {
            MultiStatusText = "Could not locate main window.";
            return;
        }

        string suggestedName = "multi_0x" + SelectedMulti.Id.ToString("X4") + ".png";

        IStorageFile? file = await mainWindow.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export Multi Preview PNG",
                SuggestedFileName = suggestedName,
                FileTypeChoices = new[]
                {
                new FilePickerFileType("PNG Image")
                {
                    Patterns = new[] { "*.png" }
                }
                }
            });

        if (file == null)
        {
            MultiStatusText = "PNG export cancelled.";
            return;
        }

        string? path = file.TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(path))
        {
            MultiStatusText = "Selected export path is invalid.";
            return;
        }

        await using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        SelectedMultiPreview.Save(stream);

        MultiStatusText = "Exported preview PNG for multi " + SelectedMulti.IdHex + ".";
    }

    [RelayCommand]
    private async Task ExportSelectedMultiTxt()
    {
        await ExportSelectedMultiPartsAsync("TXT", "*.txt");
    }

    [RelayCommand]
    private async Task ExportSelectedMultiCsv()
    {
        await ExportSelectedMultiPartsAsync("CSV", "*.csv");
    }

    private async Task ExportSelectedMultiPartsAsync(string format, string pattern)
    {
        if (SelectedMulti == null)
        {
            MultiStatusText = "No multi selected.";
            return;
        }

        List<MultiComponentEntry> components = multiDataService.GetComponents(SelectedMulti.Id);

        if (components.Count == 0)
        {
            MultiStatusText = "Selected multi is empty.";
            return;
        }

        Window? mainWindow = GetMainWindow();

        if (mainWindow == null)
        {
            MultiStatusText = "Could not locate main window.";
            return;
        }

        string extension = pattern.Replace("*", "");
        string suggestedName = "multi_0x" + SelectedMulti.Id.ToString("X4") + extension;

        IStorageFile? file = await mainWindow.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export Multi " + format,
                SuggestedFileName = suggestedName,
                FileTypeChoices = new[]
                {
                new FilePickerFileType(format + " File")
                {
                    Patterns = new[] { pattern }
                }
                }
            });

        if (file == null)
        {
            MultiStatusText = format + " export cancelled.";
            return;
        }

        string? path = file.TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(path))
        {
            MultiStatusText = "Selected export path is invalid.";
            return;
        }

        if (format == "CSV")
        {
            multiDataService.ExportCsv(path, components);
        }
        else
        {
            multiDataService.ExportText(path, components);
        }

        MultiStatusText = "Exported multi " + SelectedMulti.IdHex + " as " + format + ".";
    }

    private void ApplyMultiFilter()
    {
        MultiEntries.Clear();

        IEnumerable<MultiEntry> query = allMultiEntries;

        if (SelectedMultiFilter == "Used Only")
        {
            query = query.Where(x => !x.IsEmpty);
        }
        else if (SelectedMultiFilter == "Free Only")
        {
            query = query.Where(x => x.IsEmpty);
        }

        string search = MultiSearchText.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Id.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.IdHex.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        foreach (MultiEntry entry in query)
        {
            MultiEntries.Add(entry);
        }

        SelectedMulti = MultiEntries.FirstOrDefault();

        MultiStatusText = "Showing " + MultiEntries.Count + " of " + allMultiEntries.Count + " multis.";
    }

    [RelayCommand]
    private async Task ImportSelectedMulti()
    {
        if (SelectedMulti == null)
        {
            MultiStatusText = "Select a multi slot first.";
            return;
        }

        Window? mainWindow = GetMainWindow();

        if (mainWindow == null)
        {
            MultiStatusText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Import Multi",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                new FilePickerFileType("Multi files")
                {
                    Patterns = new[] { "*.txt", "*.csv", "*.dfn" }
                }
                }
            });

        if (files.Count == 0)
        {
            MultiStatusText = "Import cancelled.";
            return;
        }

        string? path = files[0].TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(path))
        {
            MultiStatusText = "Selected import file has no local path.";
            return;
        }

        bool success = multiDataService.ImportPartsFile(path, out List<MultiComponentEntry> components, out string message);

        if (!success)
        {
            MultiStatusText = message;
            return;
        }

        multiDataService.ReplaceMulti(SelectedMulti.Id, components);

        SelectedMulti.ComponentCount = components.Count;
        SelectedMulti.IsEmpty = components.Count == 0;

        BuildMultiComponentsText(components);
        RefreshSelectedMultiPreview();

        MultiStatusText = "Imported into multi " + SelectedMulti.IdHex + ". " + message;
    }

    [RelayCommand]
    private void SaveMultis()
    {
        if (SelectedMultiSource == "multicollection.uop")
        {
            MultiStatusText = "Saving multicollection.uop is not supported yet. Switch to multi.mul / multi.idx.";
            return;
        }

        string folderPath = GetCurrentFolderPath();

        bool success = multiDataService.SaveMul(folderPath, UseUohsaMultiFormat, out string message);

        MultiStatusText = message;
    }

    [RelayCommand]
    private async Task OpenMultiEditor()
    {
        if (SelectedMulti == null)
        {
            MultiStatusText = "Select a multi first.";
            return;
        }

        List<MultiComponentEntry> components = multiDataService.GetComponents(SelectedMulti.Id);

        Window? owner = GetMainWindow();

        if (owner == null)
        {
            MultiStatusText = "Could not locate main window.";
            return;
        }

        MultiEditorWindow window = new()
        {
            DataContext = new MultiEditorViewModel(
                SelectedMulti.Id,
                components,
                ArtEntries.ToList(),
                TileDataEntries.ToList())
        };

        await window.ShowDialog(owner);

        if (window.DataContext is MultiEditorViewModel editor &&
            editor.WasApplied)
        {
            multiDataService.ReplaceMulti(SelectedMulti.Id, editor.ToComponents());

            SelectedMulti.ComponentCount = editor.Components.Count;
            SelectedMulti.IsEmpty = editor.Components.Count == 0;

            BuildMultiComponentsText(editor.ToComponents());
            RefreshSelectedMultiPreview();

            MultiStatusText = "Applied editor changes to multi " + SelectedMulti.IdHex + ". Save Multis to write changes.";
        }
    }
}