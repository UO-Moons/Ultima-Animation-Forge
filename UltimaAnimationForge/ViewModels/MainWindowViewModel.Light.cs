using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    public ObservableCollection<LightEntry> LightEntries { get; } = new();

    [ObservableProperty]
    private bool showRemovedLightSlots;

    [ObservableProperty]
    private int lightBrushSize = 16;

    [ObservableProperty]
    private int lightBrushStrength = 20;

    [ObservableProperty]
    private bool lightBrushErase;

    [ObservableProperty]
    private int newLightWidth = 120;

    [ObservableProperty]
    private int newLightHeight = 120;

    [ObservableProperty]
    private int lightPresetStrength = 24;

    [ObservableProperty]
    private LightEntry? selectedLightEntry;

    [ObservableProperty]
    private string lightSearchText = string.Empty;


    private sealed class LightEditSnapshot
    {
        public int Index { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Length { get; set; }
        public bool IsRemoved { get; set; }
        public byte[] RawData { get; set; } = [];
    }

    private readonly LightMulService lightMulService = new();
    private readonly Stack<LightEditSnapshot> lightUndoStack = new();
    private readonly Stack<LightEditSnapshot> lightRedoStack = new();
    private readonly List<LightEntry> allLightEntries = new();

    public ICommand CreateBlankLightCommand { get; private set; } = null!;
    public ICommand ClearSelectedLightCommand { get; private set; } = null!;
    public ICommand CreateRoundLightCommand { get; private set; } = null!;
    public ICommand CreateWindowLightCommand { get; private set; } = null!;
    public ICommand CreateDoorSlitLightCommand { get; private set; } = null!;
    public ICommand FlipSelectedLightHorizontalCommand { get; private set; } = null!;
    public ICommand FlipSelectedLightVerticalCommand { get; private set; } = null!;
    public ICommand SharpenSelectedLightCommand { get; private set; } = null!;
    public ICommand SoftenSelectedLightCommand { get; private set; } = null!;
    public ICommand UndoLightEditCommand { get; private set; } = null!;
    public ICommand RedoLightEditCommand { get; private set; } = null!;
    public ICommand ImportSelectedLightPngCommand { get; private set; } = null!;
    public ICommand ExportSelectedLightPngCommand { get; private set; } = null!;
    public ICommand ResetLightsCommand { get; private set; } = null!;
    public ICommand LoadLightsCommand { get; private set; } = null!;
    public ICommand RemoveSelectedLightCommand { get; private set; } = null!;
    public ICommand SaveLightsCommand { get; private set; } = null!;

    private void InitializeLightCommands()
    {
        LoadLightsCommand = new RelayCommand(LoadLights);
        RemoveSelectedLightCommand = new RelayCommand(RemoveSelectedLight);
        SaveLightsCommand = new RelayCommand(SaveLights);
        ImportSelectedLightPngCommand = new AsyncRelayCommand(ImportSelectedLightPngAsync);
        ExportSelectedLightPngCommand = new AsyncRelayCommand(ExportSelectedLightPngAsync);
        CreateBlankLightCommand = new RelayCommand(CreateBlankLight);
        ClearSelectedLightCommand = new RelayCommand(ClearSelectedLight);
        CreateRoundLightCommand = new RelayCommand(CreateRoundLight);
        CreateWindowLightCommand = new RelayCommand(CreateWindowLight);
        CreateDoorSlitLightCommand = new RelayCommand(CreateDoorSlitLight);
        ResetLightsCommand = new RelayCommand(ResetLights);
        UndoLightEditCommand = new RelayCommand(UndoLightEdit);
        RedoLightEditCommand = new RelayCommand(RedoLightEdit);
        SoftenSelectedLightCommand = new RelayCommand(SoftenSelectedLight);
        FlipSelectedLightHorizontalCommand = new RelayCommand(FlipSelectedLightHorizontal);
        FlipSelectedLightVerticalCommand = new RelayCommand(FlipSelectedLightVertical);
        SharpenSelectedLightCommand = new RelayCommand(SharpenSelectedLight);
    }

    public void BeginLightPaintStroke()
    {
        PushLightUndoSnapshot();
    }

    partial void OnShowRemovedLightSlotsChanged(bool value)
    {
        RebuildVisibleLightEntries();
    }

    private void LoadLights()
    {
        allLightEntries.Clear();
        LightEntries.Clear();

        string folder = GetCurrentFolderPath();
        allLightEntries.AddRange(lightMulService.Load(folder));

        RebuildVisibleLightEntries();

        SelectedLightEntry = LightEntries.FirstOrDefault(x => !x.IsRemoved) ??
                             LightEntries.FirstOrDefault();

        StatusText = $"Loaded {allLightEntries.Count} light index entries.";
    }

    private void RemoveSelectedLight()
    {
        LightEntry? entry = SelectedLightEntry;

        if (entry == null)
        {
            StatusText = "Select a light first.";
            return;
        }

        PushLightUndoSnapshot();

        int removedIndex = entry.Index;

        lightMulService.Remove(entry);

        HasUnsavedChanges = true;
        StatusText = $"Removed light {removedIndex}.";

        RebuildVisibleLightEntries();
    }

    private void SaveLights()
    {
        string folder = GetCurrentFolderPath();
        lightMulService.Save(folder, allLightEntries);
        HasUnsavedChanges = false;
        StatusText = "Saved light.mul and lightidx.mul.";
    }

    private void RebuildVisibleLightEntries()
    {
        LightEntry? selected = SelectedLightEntry;

        LightEntries.Clear();

        foreach (LightEntry entry in allLightEntries)
        {
            if (!entry.IsRemoved || ShowRemovedLightSlots)
            {
                LightEntries.Add(entry);
            }
        }

        if (selected != null && LightEntries.Contains(selected))
        {
            SelectedLightEntry = selected;
        }
        else
        {
            SelectedLightEntry = LightEntries.FirstOrDefault(x => !x.IsRemoved) ??
                                 LightEntries.FirstOrDefault();
        }
    }

    private async Task ImportSelectedLightPngAsync()
    {
        if (SelectedLightEntry == null)
        {
            StatusText = "Select a light entry first.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        PushLightUndoSnapshot();

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Import Light PNG",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                new FilePickerFileType("PNG Image")
                {
                    Patterns = new[] { "*.png" }
                }
                }
            });

        if (files.Count == 0)
        {
            StatusText = "Import cancelled.";
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = "Selected file has no local path.";
            return;
        }

        lightMulService.ImportPngIntoLight(SelectedLightEntry, path);
        HasUnsavedChanges = true;
        StatusText = $"Imported PNG into light {SelectedLightEntry.Index}.";
    }

    private async Task ExportSelectedLightPngAsync()
    {
        if (SelectedLightEntry == null)
        {
            StatusText = "Select a light entry first.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        IStorageFile? file = await mainWindow.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export Light PNG",
                SuggestedFileName = $"light_{SelectedLightEntry.Index:D3}.png",
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
            StatusText = "Export cancelled.";
            return;
        }

        string? path = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = "Selected file has no local path.";
            return;
        }

        lightMulService.ExportLightToPng(SelectedLightEntry, path);
        StatusText = $"Exported light {SelectedLightEntry.Index}.";
    }

    private void CreateBlankLight()
    {
        int width = Math.Clamp(NewLightWidth, 8, 600);
        int height = Math.Clamp(NewLightHeight, 8, 600);

        LightEntry? target = allLightEntries.FirstOrDefault(x => x.IsRemoved);

        if (target == null)
        {
            target = lightMulService.CreateBlankLight(GetNextLightIndex(), width, height);
            allLightEntries.Add(target);
        }
        else
        {
            int index = allLightEntries.IndexOf(target);
            LightEntry replacement = lightMulService.CreateBlankLight(target.Index, width, height);
            allLightEntries[index] = replacement;
            target = replacement;
        }

        RebuildVisibleLightEntries();

        if (!LightEntries.Contains(target))
        {
            LightEntries.Add(target);
        }

        SelectedLightEntry = target;
        HasUnsavedChanges = true;

        StatusText = $"Created blank light {target.Index}.";
    }

    private void ResetLights()
    {
        LoadLights();
        HasUnsavedChanges = false;
        StatusText = "Reloaded lights from disk.";
    }

    private void ClearSelectedLight()
    {
        if (SelectedLightEntry == null)
        {
            StatusText = "Select a light first.";
            return;
        }

        PushLightUndoSnapshot();

        lightMulService.ClearLight(SelectedLightEntry);
        HasUnsavedChanges = true;

        StatusText = $"Cleared light {SelectedLightEntry.Index}.";
    }

    private void CreateRoundLight()
    {
        ApplyPreset("round");
    }

    private void CreateWindowLight()
    {
        ApplyPreset("window");
    }

    private void CreateDoorSlitLight()
    {
        ApplyPreset("door");
    }

    private void ApplyPreset(string preset)
    {
        if (SelectedLightEntry == null)
        {
            CreateBlankLight();
        }

        if (SelectedLightEntry == null)
        {
            return;
        }

        PushLightUndoSnapshot();

        int width = Math.Clamp(NewLightWidth, 8, 600);
        int height = Math.Clamp(NewLightHeight, 8, 600);
        int strength = Math.Clamp(LightPresetStrength, 1, 31);

        switch (preset)
        {
            case "round":
                lightMulService.ApplyRoundPreset(SelectedLightEntry, width, height, strength);
                break;

            case "window":
                lightMulService.ApplyWindowPreset(SelectedLightEntry, width, height, strength);
                break;

            case "door":
                lightMulService.ApplyDoorSlitPreset(SelectedLightEntry, width, height, strength);
                break;
        }

        HasUnsavedChanges = true;
        StatusText = $"Applied {preset} preset to light {SelectedLightEntry.Index}.";
    }

    private int GetNextLightIndex()
    {
        if (LightEntries.Count == 0)
        {
            return 0;
        }

        HashSet<int> used = LightEntries.Select(x => x.Index).ToHashSet();

        for (int i = 0; i < 1000; i++)
        {
            if (!used.Contains(i))
            {
                return i;
            }
        }

        return LightEntries.Max(x => x.Index) + 1;
    }

    public void PaintSelectedLightAtPreviewPoint(double previewX, double previewY, double previewWidth, double previewHeight)
    {
        if (SelectedLightEntry == null)
        {
            return;
        }

        if (SelectedLightEntry.Width <= 0 || SelectedLightEntry.Height <= 0)
        {
            return;
        }

        if (previewWidth <= 0 || previewHeight <= 0)
        {
            return;
        }

        int x = (int)Math.Round((previewX / previewWidth) * SelectedLightEntry.Width);
        int y = (int)Math.Round((previewY / previewHeight) * SelectedLightEntry.Height);

        x = Math.Clamp(x, 0, SelectedLightEntry.Width - 1);
        y = Math.Clamp(y, 0, SelectedLightEntry.Height - 1);

        lightMulService.PaintLightAt(
     SelectedLightEntry,
     x,
     y,
     LightBrushSize,
     LightBrushStrength,
     LightBrushErase);

        LightEntry refreshedEntry = SelectedLightEntry;
        int listIndex = LightEntries.IndexOf(refreshedEntry);

        if (listIndex >= 0)
        {
            LightEntries[listIndex] = refreshedEntry;
        }

        SelectedLightEntry = null;
        SelectedLightEntry = refreshedEntry;

        HasUnsavedChanges = true;
    }

    private void PushLightUndoSnapshot()
    {
        if (SelectedLightEntry == null)
        {
            return;
        }

        lightUndoStack.Push(CreateSnapshot(SelectedLightEntry));
        lightRedoStack.Clear();
    }

    private static LightEditSnapshot CreateSnapshot(LightEntry entry)
    {
        return new LightEditSnapshot
        {
            Index = entry.Index,
            Width = entry.Width,
            Height = entry.Height,
            Length = entry.Length,
            IsRemoved = entry.IsRemoved,
            RawData = entry.RawData.ToArray()
        };
    }

    private void RestoreLightSnapshot(LightEditSnapshot snapshot)
    {
        LightEntry? entry = allLightEntries.FirstOrDefault(x => x.Index == snapshot.Index);
        if (entry == null)
        {
            entry = new LightEntry { Index = snapshot.Index };
            allLightEntries.Add(entry);
        }

        entry.Width = snapshot.Width;
        entry.Height = snapshot.Height;
        entry.Length = snapshot.Length;
        entry.IsRemoved = snapshot.IsRemoved;
        entry.RawData = snapshot.RawData.ToArray();
        entry.Preview = entry.IsRemoved || entry.RawData.Length == 0
            ? null
            : lightMulService.BuildPreview(entry);

        RebuildVisibleLightEntries();
        SelectedLightEntry = LightEntries.FirstOrDefault(x => x.Index == entry.Index);
    }

    private void UndoLightEdit()
    {
        if (lightUndoStack.Count == 0 || SelectedLightEntry == null)
        {
            StatusText = "No light edit to undo.";
            return;
        }

        lightRedoStack.Push(CreateSnapshot(SelectedLightEntry));

        LightEditSnapshot snapshot = lightUndoStack.Pop();
        RestoreLightSnapshot(snapshot);

        HasUnsavedChanges = true;
        StatusText = "Undid light edit.";
    }

    private void RedoLightEdit()
    {
        if (lightRedoStack.Count == 0 || SelectedLightEntry == null)
        {
            StatusText = "No light edit to redo.";
            return;
        }

        lightUndoStack.Push(CreateSnapshot(SelectedLightEntry));

        LightEditSnapshot snapshot = lightRedoStack.Pop();
        RestoreLightSnapshot(snapshot);

        HasUnsavedChanges = true;
        StatusText = "Redid light edit.";
    }

    private void SoftenSelectedLight()
    {
        if (SelectedLightEntry == null)
        {
            StatusText = "Select a light first.";
            return;
        }

        PushLightUndoSnapshot();

        lightMulService.SoftenLight(SelectedLightEntry);

        HasUnsavedChanges = true;
        RebuildVisibleLightEntries();
        SelectedLightEntry = LightEntries.FirstOrDefault(x => x.Index == SelectedLightEntry.Index);

        StatusText = $"Softened light {SelectedLightEntry?.Index}.";
    }

    private void FlipSelectedLightHorizontal()
    {
        if (SelectedLightEntry == null)
        {
            StatusText = "Select a light first.";
            return;
        }

        int selectedIndex = SelectedLightEntry.Index;

        PushLightUndoSnapshot();
        lightMulService.FlipLightHorizontal(SelectedLightEntry);

        HasUnsavedChanges = true;
        RebuildVisibleLightEntries();
        SelectedLightEntry = LightEntries.FirstOrDefault(x => x.Index == selectedIndex);

        StatusText = $"Flipped light {selectedIndex} horizontally.";
    }

    private void FlipSelectedLightVertical()
    {
        if (SelectedLightEntry == null)
        {
            StatusText = "Select a light first.";
            return;
        }

        int selectedIndex = SelectedLightEntry.Index;

        PushLightUndoSnapshot();
        lightMulService.FlipLightVertical(SelectedLightEntry);

        HasUnsavedChanges = true;
        RebuildVisibleLightEntries();
        SelectedLightEntry = LightEntries.FirstOrDefault(x => x.Index == selectedIndex);

        StatusText = $"Flipped light {selectedIndex} vertically.";
    }

    private void SharpenSelectedLight()
    {
        if (SelectedLightEntry == null)
        {
            StatusText = "Select a light first.";
            return;
        }

        int selectedIndex = SelectedLightEntry.Index;

        PushLightUndoSnapshot();
        lightMulService.SharpenLight(SelectedLightEntry);

        HasUnsavedChanges = true;
        RebuildVisibleLightEntries();
        SelectedLightEntry = LightEntries.FirstOrDefault(x => x.Index == selectedIndex);

        StatusText = $"Sharpened light {selectedIndex}.";
    }
}
