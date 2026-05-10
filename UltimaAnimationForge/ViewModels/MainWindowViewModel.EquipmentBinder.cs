using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
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
    public bool ShowNormalPreviewPanel =>
    ShowAnimationEditorPanel && !ShowEquipmentArtGumpPicker;

    private readonly List<EquipmentArtPickerEntry> allEquipmentArtPickerEntries = new();
    private readonly List<EquipmentGumpPickerEntry> allEquipmentGumpPickerEntries = new();

    [ObservableProperty]
    private string equipmentArtPickerSearchText = string.Empty;

    [ObservableProperty]
    private string equipmentGumpPickerSearchText = string.Empty;

    [ObservableProperty]
    private int equipmentBinderItemArtId;

    [ObservableProperty]
    private int equipmentBinderDisplayBodyId;

    [ObservableProperty]
    private int equipmentBinderAnimationBodyId;

    [ObservableProperty]
    private string equipmentBinderName = string.Empty;

    [ObservableProperty]
    private string equipmentBinderBodyDefLine = string.Empty;

    [ObservableProperty]
    private string equipmentBinderBodyConvLine = string.Empty;

    [ObservableProperty]
    private string equipmentBinderMobTypeLine = string.Empty;

    [ObservableProperty]
    private string equipmentBinderSummary = "Select a free MUL body ID, fill the equipment fields, then build preview.";

    [ObservableProperty]
    private bool equipmentBinderBodyDefExists;

    [ObservableProperty]
    private bool equipmentBinderBodyConvExists;

    [ObservableProperty]
    private bool equipmentBinderMobTypeExists;

    [ObservableProperty]
    private bool equipmentBinderOverwriteBodyDef = true;

    [ObservableProperty]
    private bool equipmentBinderOverwriteMobType = true;

    [ObservableProperty]
    private bool equipmentBinderUpdateTileDataAnimation = true;

    [ObservableProperty]
    private string equipmentBinderTileDataLine = string.Empty;

    private readonly UoIndexedMulImageService equipmentImageService = new();

    public ObservableCollection<EquipmentArtPickerEntry> EquipmentArtPickerEntries { get; } = new();

    public ObservableCollection<EquipmentGumpPickerEntry> EquipmentGumpPickerEntries { get; } = new();

    [ObservableProperty]
    private EquipmentArtPickerEntry? selectedEquipmentArtPickerEntry;

    [ObservableProperty]
    private EquipmentGumpPickerEntry? selectedEquipmentGumpPickerEntry;

    [ObservableProperty]
    private bool showEquipmentArtGumpPicker;

    [ObservableProperty]
    private string equipmentPickerStatusText = "Art/Gump picker not loaded.";

    public string EquipmentBinderTargetSlotText =>
        SelectedMulSlot == null
            ? "No free MUL body ID selected."
            : SelectedMulSlot.FileName +
              " | " + SelectedMulSlot.TypeLetter +
              " | Slot " + SelectedMulSlot.BodyIndex +
              " | FileType " + SelectedMulSlot.FileType +
              " | AnimLength " + SelectedMulSlot.AnimLength;

    public string EquipmentBinderExistsText
    {
        get
        {
            return
                "body.def: " + (EquipmentBinderBodyDefExists ? "exists/update" : "new") +
                " | bodyconv.def: " + (EquipmentBinderBodyConvExists ? "exists/skip" : "new") +
                " | mobtypes.txt: " + (EquipmentBinderMobTypeExists ? "exists/update" : "new");
        }
    }

    private void AutoFillEquipmentBinderDisplayBody()
    {
        string currentFolderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(currentFolderPath) || !Directory.Exists(currentFolderPath))
        {
            StatusText = "Open a valid UO folder first.";
            return;
        }

        int nextFree = equipmentBinderService.FindNextFreeDisplayBodyId(currentFolderPath, 4000);

        if (nextFree < 0)
        {
            StatusText = "Could not find a free display body/gump ID under 65534.";
            return;
        }

        EquipmentBinderDisplayBodyId = nextFree;

        if (EquipmentBinderAnimationBodyId <= 0)
        {
            EquipmentBinderAnimationBodyId = nextFree;
        }

        StatusText = "Auto-filled display body/gump ID " + nextFree + ".";
        BuildEquipmentBinderPreview();
    }

    private void BuildEquipmentBinderPreview()
    {
        string currentFolderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(currentFolderPath) || !Directory.Exists(currentFolderPath))
        {
            EquipmentBinderSummary = "Open a valid UO folder first.";
            StatusText = EquipmentBinderSummary;
            return;
        }

        if (SelectedMulSlot == null)
        {
            EquipmentBinderSummary = "Select a free MUL body ID first.";
            StatusText = EquipmentBinderSummary;
            return;
        }

        if (EquipmentBinderDisplayBodyId < 0 || EquipmentBinderDisplayBodyId > 65534)
        {
            EquipmentBinderSummary = "Display body/gump ID must be between 0 and 65534.";
            StatusText = EquipmentBinderSummary;
            return;
        }

        if (EquipmentBinderAnimationBodyId <= 0)
        {
            EquipmentBinderSummary = "Animation body ID must be 1 or greater.";
            StatusText = EquipmentBinderSummary;
            return;
        }

        try
        {
            EquipmentBinderPreview preview =
                equipmentBinderService.BuildPreview(
                    currentFolderPath,
                    EquipmentBinderItemArtId,
                    EquipmentBinderDisplayBodyId,
                    EquipmentBinderAnimationBodyId,
                    SelectedMulSlot,
                    EquipmentBinderName);

            EquipmentBinderBodyDefLine = preview.BodyDefLine;
            EquipmentBinderBodyConvLine = preview.BodyConvLine;
            EquipmentBinderMobTypeLine = preview.MobTypeLine;

            EquipmentBinderBodyDefExists = preview.BodyDefExists;
            EquipmentBinderBodyConvExists = preview.BodyConvExists;
            EquipmentBinderMobTypeExists = preview.MobTypeExists;
            EquipmentBinderTileDataLine = preview.TileDataLine;

            EquipmentBinderSummary = preview.Summary;

            OnPropertyChanged(nameof(EquipmentBinderExistsText));
            OnPropertyChanged(nameof(EquipmentBinderTargetSlotText));

            StatusText = "Equipment Binder preview built.";
        }
        catch (Exception exception)
        {
            EquipmentBinderSummary = "Equipment Binder preview failed: " + exception.Message;
            StatusText = EquipmentBinderSummary;
        }
    }

    private void ApplyEquipmentBinder()
    {
        string currentFolderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(currentFolderPath) || !Directory.Exists(currentFolderPath))
        {
            StatusText = "Open a valid UO folder first.";
            return;
        }

        if (SelectedMulSlot == null)
        {
            StatusText = "Select a free MUL body ID first.";
            return;
        }

        EquipmentBinderResult result =
            equipmentBinderService.Apply(
                currentFolderPath,
                EquipmentBinderItemArtId,
                EquipmentBinderDisplayBodyId,
                EquipmentBinderAnimationBodyId,
                SelectedMulSlot,
                EquipmentBinderName,
                EquipmentBinderOverwriteBodyDef,
                EquipmentBinderOverwriteMobType,
                EquipmentBinderUpdateTileDataAnimation);

        StatusText = result.Message;
        EquipmentBinderSummary = result.Message;

        if (!result.Success)
        {
            return;
        }

        animationCacheService.DeleteAllCaches();

        BuildEquipmentBinderPreview();

        OnPropertyChanged(nameof(PreviewInfoText));
        OnPropertyChanged(nameof(EquipmentBinderExistsText));
        OnPropertyChanged(nameof(EquipmentBinderTargetSlotText));
    }

    partial void OnEquipmentBinderItemArtIdChanged(int value)
    {
        BuildEquipmentBinderPreview();
    }

    partial void OnEquipmentBinderDisplayBodyIdChanged(int value)
    {
        BuildEquipmentBinderPreview();
    }

    partial void OnEquipmentBinderAnimationBodyIdChanged(int value)
    {
        BuildEquipmentBinderPreview();
    }

    partial void OnEquipmentBinderNameChanged(string value)
    {
        BuildEquipmentBinderPreview();
    }

    private async void LoadEquipmentArtPicker()
    {
        string currentFolderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(currentFolderPath) || !Directory.Exists(currentFolderPath))
        {
            EquipmentPickerStatusText = "Open a valid UO folder first.";
            StatusText = EquipmentPickerStatusText;
            return;
        }

        ShowEquipmentArtGumpPicker = true;
        EquipmentArtPickerEntries.Clear();
        allEquipmentArtPickerEntries.Clear();

        EquipmentPickerStatusText = "Loading art index...";
        StatusText = EquipmentPickerStatusText;

        List<EquipmentArtPickerEntry> artIndex = await Task.Run(() =>
            equipmentImageService.LoadArtIndexEntries(currentFolderPath));

        allEquipmentArtPickerEntries.AddRange(artIndex);

        EquipmentPickerStatusText =
            "Loaded art index: " + allEquipmentArtPickerEntries.Count + " entries.";

        StatusText = EquipmentPickerStatusText;

        EquipmentArtPickerSearchText = string.Empty;
        ApplyEquipmentArtPickerFilter();
    }

    private async void LoadEquipmentGumpPicker()
    {
        string currentFolderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(currentFolderPath) || !Directory.Exists(currentFolderPath))
        {
            EquipmentPickerStatusText = "Open a valid UO folder first.";
            StatusText = EquipmentPickerStatusText;
            return;
        }

        ShowEquipmentArtGumpPicker = true;
        EquipmentGumpPickerEntries.Clear();
        allEquipmentGumpPickerEntries.Clear();

        EquipmentPickerStatusText = "Loading gump index...";
        StatusText = EquipmentPickerStatusText;

        List<EquipmentGumpPickerEntry> gumpIndex = await Task.Run(() =>
            equipmentImageService.LoadGumpIndexEntries(currentFolderPath));

        allEquipmentGumpPickerEntries.AddRange(gumpIndex);

        EquipmentPickerStatusText =
            "Loaded gump index: " + allEquipmentGumpPickerEntries.Count + " entries.";

        StatusText = EquipmentPickerStatusText;

        EquipmentGumpPickerSearchText = string.Empty;
        ApplyEquipmentGumpPickerFilter();
    }

    private const int EquipmentPickerPageSize = 100;

    private void ApplyEquipmentArtPickerFilter()
    {
        EquipmentArtPickerEntries.Clear();

        string search = (EquipmentArtPickerSearchText ?? string.Empty).Trim();

        foreach (EquipmentArtPickerEntry entry in allEquipmentArtPickerEntries)
        {
            if (!MatchesPickerSearch(entry.ArtId, entry.DisplayText, search))
            {
                continue;
            }

            EquipmentArtPickerEntries.Add(entry);

            if (EquipmentArtPickerEntries.Count >= EquipmentPickerPageSize)
            {
                break;
            }
        }

        _ = LoadVisibleEquipmentArtThumbnailsAsync();
    }

    private void ApplyEquipmentGumpPickerFilter()
    {
        EquipmentGumpPickerEntries.Clear();

        string search = (EquipmentGumpPickerSearchText ?? string.Empty).Trim();

        foreach (EquipmentGumpPickerEntry entry in allEquipmentGumpPickerEntries)
        {
            if (!MatchesPickerSearch(entry.GumpId, entry.DisplayText, search))
            {
                continue;
            }

            EquipmentGumpPickerEntries.Add(entry);

            if (EquipmentGumpPickerEntries.Count >= EquipmentPickerPageSize)
            {
                break;
            }
        }

        _ = LoadVisibleEquipmentGumpThumbnailsAsync();
    }

    private async Task LoadVisibleEquipmentArtThumbnailsAsync()
    {
        string currentFolderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(currentFolderPath) || !Directory.Exists(currentFolderPath))
            return;

        List<EquipmentArtPickerEntry> targets = EquipmentArtPickerEntries
            .Where(x => x.Thumbnail == null)
            .Take(EquipmentPickerPageSize)
            .ToList();

        foreach (EquipmentArtPickerEntry entry in targets)
        {
            int artId = entry.ArtId;

            WriteableBitmap? bitmap = await Task.Run(() =>
                equipmentImageService.LoadArtThumbnail(currentFolderPath, artId));

            if (bitmap != null && EquipmentArtPickerEntries.Contains(entry))
                entry.Thumbnail = bitmap;
        }
    }

    private async Task LoadVisibleEquipmentGumpThumbnailsAsync()
    {
        string currentFolderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(currentFolderPath) || !Directory.Exists(currentFolderPath))
            return;

        List<EquipmentGumpPickerEntry> targets = EquipmentGumpPickerEntries
            .Where(x => x.Thumbnail == null)
            .Take(EquipmentPickerPageSize)
            .ToList();

        foreach (EquipmentGumpPickerEntry entry in targets)
        {
            int gumpId = entry.GumpId;

            WriteableBitmap? bitmap = await Task.Run(() =>
                equipmentImageService.LoadGumpThumbnail(currentFolderPath, gumpId));

            if (bitmap != null && EquipmentGumpPickerEntries.Contains(entry))
                entry.Thumbnail = bitmap;
        }
    }

    private static bool MatchesPickerSearch(int id, string displayText, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        if (displayText.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (id.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string hex = id.ToString("X");
        if (hex.Contains(search.TrimStart('0', 'x', 'X'), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string hex4 = "0x" + id.ToString("X4");
        return hex4.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnSelectedEquipmentArtPickerEntryChanged(EquipmentArtPickerEntry? value)
    {
        if (value == null)
        {
            return;
        }

        EquipmentBinderItemArtId = value.ArtId;
        BuildEquipmentBinderPreview();
    }

    partial void OnSelectedEquipmentGumpPickerEntryChanged(EquipmentGumpPickerEntry? value)
    {
        if (value == null)
        {
            return;
        }

        EquipmentBinderDisplayBodyId = value.GumpId;

        if (EquipmentBinderAnimationBodyId <= 0)
        {
            EquipmentBinderAnimationBodyId = value.GumpId;
        }

        BuildEquipmentBinderPreview();
    }

    partial void OnShowEquipmentArtGumpPickerChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowNormalPreviewPanel));
    }

    partial void OnEquipmentArtPickerSearchTextChanged(string value)
    {
        ApplyEquipmentArtPickerFilter();
    }

    partial void OnEquipmentGumpPickerSearchTextChanged(string value)
    {
        ApplyEquipmentGumpPickerFilter();
    }
}