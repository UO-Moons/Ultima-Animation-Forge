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
using System.Threading.Tasks;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    [ObservableProperty]
    private int wearableWizardPreviewBaseBodyId = 400;

    [ObservableProperty]
    private int wearableWizardPreviewAction = 0;

    [ObservableProperty]
    private int wearableWizardPreviewDirection = 0;

    [ObservableProperty]
    private int wearableWizardPreviewFrameIndex = 0;

    [ObservableProperty]
    private bool wearableWizardOverlayPreviewEnabled = true;

    [ObservableProperty]
    private WriteableBitmap? wearableWizardOverlayPreviewBitmap;

    [ObservableProperty]
    private double wearableWizardOverlayOffsetX = 0;

    [ObservableProperty]
    private double wearableWizardOverlayOffsetY = 0;

    [ObservableProperty]
    private double wearableWizardOverlayScale = 1.0;

    [ObservableProperty]
    private string wearableWizardOverlayPreviewStatus = "No overlay preview loaded.";

    [ObservableProperty]
    private Bitmap? wearableWizardPaperdollPreview;

    [ObservableProperty]
    private Bitmap? wearableWizardFemalePaperdollPreview;

    [ObservableProperty]
    private Bitmap? wearableWizardArtPreview;

    public ObservableCollection<AnimationEntry> WearableWizardSourceAnimations { get; } = new();

    [ObservableProperty]
    private AnimationEntry? wearableWizardSelectedSourceAnimation;

    [ObservableProperty]
    private string wearableWizardSourceAnimationSearchText = string.Empty;

    [ObservableProperty]
    private bool wearableWizardPartialHue = true;

    [ObservableProperty]
    private bool wearableWizardMaleGumpConflict;

    [ObservableProperty]
    private bool wearableWizardFemaleGumpConflict;

    [ObservableProperty]
    private bool wearableWizardArtConflict;

    [ObservableProperty]
    private string wearableWizardConflictText = string.Empty;

    public ObservableCollection<MulSlotEntry> WearableWizardFreeSlots { get; } = new();

    [ObservableProperty]
    private MulSlotEntry? wearableWizardSelectedFreeSlot;

    [ObservableProperty]
    private string wearableWizardSlotSearchText = string.Empty;

    [ObservableProperty]
    private bool wearableWizardSlotShowHFilter = false;

    [ObservableProperty]
    private bool wearableWizardSlotShowLFilter = false;

    [ObservableProperty]
    private bool wearableWizardSlotShowPFilter = true;

    [ObservableProperty]
    private string wearableWizardSelectedSlotFile = "All Files";

    public ObservableCollection<string> WearableWizardSlotFileOptions { get; } = new();

    public ObservableCollection<string> WearableWizardTypes { get; } = new()
    {
        "Robe",
        "Shirt",
        "Pants",
        "Cloak",
        "Hat / Helm",
        "Gloves",
        "Sleeves",
        "Shoes / Boots",
        "Armor",
        "Custom"
    };

    public ObservableCollection<string> WearableWizardLayers { get; } = new()
{
    "0x01 - One Handed",
    "0x02 - Two Handed / Shield",
    "0x03 - Shoes / Boots",
    "0x04 - Pants",
    "0x05 - Shirt / Chest Clothing",
    "0x06 - Helm / Head",
    "0x07 - Gloves",
    "0x08 - Ring",
    "0x09 - Talisman",
    "0x0A - Neck",
    "0x0B - Hair",
    "0x0C - Waist / Half Apron",
    "0x0D - Inner Torso / Chest Armor",
    "0x0E - Bracelet",
    "0x10 - Facial Hair",
    "0x11 - Middle Torso / Tunic / Sash",
    "0x12 - Earrings",
    "0x13 - Arms",
    "0x14 - Cloak / Back",
    "0x15 - Backpack",
    "0x16 - Outer Torso / Robe",
    "0x17 - Outer Legs / Skirt / Kilt",
    "0x18 - Inner Legs / Leg Armor"
};

    public ObservableCollection<string> WearableWizardAnimationModes { get; } = new()
    {
        "Reuse Existing Animation",
        "Import New VD Later",
        "No Animation Yet"
    };

    [ObservableProperty]
    private string wearableWizardName = "New Wearable";

    [ObservableProperty]
    private string wearableWizardType = "Robe";

    [ObservableProperty]
    private string wearableWizardLayer = "0x16 - Outer Torso / Robe";

    [ObservableProperty]
    private string wearableWizardAnimationMode = "Reuse Existing Animation";

    [ObservableProperty]
    private string wearableWizardPaperdollImagePath = string.Empty;

    [ObservableProperty]
    private string wearableWizardFemalePaperdollImagePath = string.Empty;

    [ObservableProperty]
    private string wearableWizardArtImagePath = string.Empty;

    [ObservableProperty]
    private bool wearableWizardCreateFemaleVariant = true;

    [ObservableProperty]
    private bool wearableWizardUseSittingSafeRange = true;

    [ObservableProperty]
    private int wearableWizardMaleGumpId = 50400;

    [ObservableProperty]
    private int wearableWizardFemaleGumpId = 60400;

    [ObservableProperty]
    private int wearableWizardArtId = 50400;

    [ObservableProperty]
    private int wearableWizardAnimationId = 400;

    [ObservableProperty]
    private int wearableWizardExistingAnimationId = 435;

    [ObservableProperty]
    private string wearableWizardHue = "0";

    [ObservableProperty]
    private string wearableWizardPlanText = "No plan generated yet.";

    [ObservableProperty]
    private string wearableWizardStatusText = "Ready.";

    private Bitmap? LoadWearablePreviewBitmap(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    [RelayCommand]
    private async Task BrowseWearablePaperdollImageAsync()
    {
        string? path = await PickWearableImageAsync("Choose Male / Default Paperdoll Gump Image");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }


        WearableWizardPaperdollImagePath = path;
        WearableWizardPaperdollPreview = LoadWearablePreviewBitmap(path);
        RebuildWearableWizardPlan();
        RefreshWearableWizardOverlayPreview();
    }

    [RelayCommand]
    private async Task BrowseWearableFemalePaperdollImageAsync()
    {
        string? path = await PickWearableImageAsync("Choose Female Paperdoll Gump Image");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        WearableWizardFemalePaperdollImagePath = path;
        WearableWizardFemalePaperdollPreview = LoadWearablePreviewBitmap(path);
        WearableWizardCreateFemaleVariant = true;
        RebuildWearableWizardPlan();
    }

    [RelayCommand]
    private async Task BrowseWearableArtImageAsync()
    {
        string? path = await PickWearableImageAsync("Choose Inventory / World Art Image");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        WearableWizardArtImagePath = path;
        WearableWizardArtPreview = LoadWearablePreviewBitmap(path);
        RebuildWearableWizardPlan();
    }

    [RelayCommand]
    private void AutoPickWearableWizardIds()
    {
        int maleStart = WearableWizardUseSittingSafeRange ? 50400 : 50000;

        int maleGumpId = FindNextLikelyFreeWearableId(maleStart, 50999);
        int animationId = maleGumpId - 50000;

        WearableWizardMaleGumpId = maleGumpId;
        WearableWizardFemaleGumpId = 60000 + animationId;
        WearableWizardArtId = maleGumpId;
        WearableWizardAnimationId = animationId;

        RebuildWearableWizardPlan();
    }

    [RelayCommand]
    private void RebuildWearableWizardPlan()
    {
        WearableWizardAnimationId = WearableWizardMaleGumpId - 50000;

        if (WearableWizardCreateFemaleVariant)
        {
            WearableWizardFemaleGumpId = 60000 + WearableWizardAnimationId;
        }

        WearableWizardArtId = WearableWizardMaleGumpId;

        string bodyDefLine = WearableWizardAnimationMode == "Reuse Existing Animation"
            ? WearableWizardAnimationId + " {" + WearableWizardExistingAnimationId + "} " + NormalizeWearableHue()
            : "(no Body.def line yet)";

        WearableWizardPlanText =
            "Wearable: " + WearableWizardName + Environment.NewLine +
            "Type: " + WearableWizardType + Environment.NewLine +
            "Layer: " + WearableWizardLayer + Environment.NewLine +
            Environment.NewLine +
            "Male/default gump: " + WearableWizardMaleGumpId + " [0x" + WearableWizardMaleGumpId.ToString("X") + "]" + Environment.NewLine +
            "Female gump: " + (WearableWizardCreateFemaleVariant ? WearableWizardFemaleGumpId + " [0x" + WearableWizardFemaleGumpId.ToString("X") + "]" : "not created") + Environment.NewLine +
            "Art/static ID: " + WearableWizardArtId + " [0x" + WearableWizardArtId.ToString("X") + "]" + Environment.NewLine +
            "TileData Animation: " + WearableWizardAnimationId + Environment.NewLine +
            Environment.NewLine +
            "Body.def:" + Environment.NewLine +
            bodyDefLine + Environment.NewLine +
            Environment.NewLine +
            "Files:" + Environment.NewLine +
            "Paperdoll: " + DisplayPathOrNone(WearableWizardPaperdollImagePath) + Environment.NewLine +
            "Female paperdoll: " + DisplayPathOrNone(WearableWizardFemalePaperdollImagePath) + Environment.NewLine +
            "Art: " + DisplayPathOrNone(WearableWizardArtImagePath);

        WearableWizardStatusText = "Plan updated.";
        ValidateWearableWizardConflicts();
    }

    [RelayCommand]
    private void ResetWearableWizard()
    {
        WearableWizardName = "New Wearable";
        WearableWizardType = "Robe";
        WearableWizardLayer = "0x16 - Outer Torso / Robe";
        WearableWizardAnimationMode = "Reuse Existing Animation";
        WearableWizardPaperdollImagePath = string.Empty;
        WearableWizardFemalePaperdollImagePath = string.Empty;
        WearableWizardArtImagePath = string.Empty;
        WearableWizardCreateFemaleVariant = true;
        WearableWizardUseSittingSafeRange = true;
        WearableWizardMaleGumpId = 50400;
        WearableWizardFemaleGumpId = 60400;
        WearableWizardArtId = 50400;
        WearableWizardAnimationId = 400;
        WearableWizardExistingAnimationId = 435;
        WearableWizardHue = "0";
        WearableWizardPlanText = "No plan generated yet.";
        WearableWizardStatusText = "Ready.";
        WearableWizardPaperdollPreview = null;
        WearableWizardFemalePaperdollPreview = null;
        WearableWizardArtPreview = null;
    }

    [RelayCommand]
    private void ApplyWearableWizard()
    {
        ValidateWearableWizardConflicts();

        if (WearableWizardMaleGumpConflict ||
            WearableWizardFemaleGumpConflict ||
            WearableWizardArtConflict)
        {
            WearableWizardStatusText =
                "Resolve slot conflicts before applying.";

            return;
        }

        RebuildWearableWizardPlan();

        WearableCreationService service = new();

        WearableCreationService.Result result = service.Apply(new WearableCreationService.Request
        {
            FolderPath = GetCurrentFolderPath(),
            Name = WearableWizardName,
            Layer = WearableWizardLayer,

            MaleGumpImagePath = WearableWizardPaperdollImagePath,
            FemaleGumpImagePath = WearableWizardFemalePaperdollImagePath,
            ArtImagePath = WearableWizardArtImagePath,

            CreateFemaleVariant = WearableWizardCreateFemaleVariant,

            MaleGumpId = WearableWizardMaleGumpId,
            FemaleGumpId = WearableWizardFemaleGumpId,
            ArtId = WearableWizardArtId,
            AnimationId = WearableWizardAnimationId,
            ExistingAnimationId = WearableWizardExistingAnimationId,
            Hue = WearableWizardHue,
            WriteBodyDef = WearableWizardAnimationMode == "Reuse Existing Animation"
        });

        WearableWizardStatusText = result.Message;
        StatusText = result.Message;

        if (result.Success)
        {
            InitializeGumpsForCurrentFolder();
            LoadArtTab();
            LoadTileData();
            RebuildWearableWizardPlan();
        }
    }

    partial void OnWearableWizardNameChanged(string value)
    {
        RebuildWearableWizardPlan();
    }

    partial void OnWearableWizardTypeChanged(string value)
    {
        RebuildWearableWizardPlan();
    }

    partial void OnWearableWizardLayerChanged(string value)
    {
        RebuildWearableWizardPlan();
    }

    partial void OnWearableWizardAnimationModeChanged(string value)
    {
        RebuildWearableWizardPlan();
    }

    partial void OnWearableWizardCreateFemaleVariantChanged(bool value)
    {
        RebuildWearableWizardPlan();
    }

    partial void OnWearableWizardUseSittingSafeRangeChanged(bool value)
    {
        RebuildWearableWizardPlan();
    }

    partial void OnWearableWizardMaleGumpIdChanged(int value)
    {
        RebuildWearableWizardPlan();
    }

    partial void OnWearableWizardExistingAnimationIdChanged(int value)
    {
        RebuildWearableWizardPlan();
    }

    partial void OnWearableWizardHueChanged(string value)
    {
        RebuildWearableWizardPlan();
    }

    private async Task<string?> PickWearableImageAsync(string title)
    {
        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            WearableWizardStatusText = "Could not locate main window.";
            return null;
        }

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image files")
                    {
                        Patterns = new[] { "*.png", "*.bmp", "*.jpg", "*.jpeg", "*.tif", "*.tiff" }
                    }
                }
            });

        if (files.Count == 0)
        {
            WearableWizardStatusText = "Image selection cancelled.";
            return null;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            WearableWizardStatusText = "Selected image does not have a local path.";
            return null;
        }

        WearableWizardStatusText = "Selected " + Path.GetFileName(path) + ".";
        return path;
    }

    private int FindNextLikelyFreeWearableId(int start, int end)
    {
        if (start < 50000)
        {
            start = 50000;
        }

        if (end > 50999)
        {
            end = 50999;
        }

        for (int id = start; id <= end; id++)
        {
            bool gumpUsed = GumpEntries.Any(entry => entry.GumpId == id && entry.IsValid);
            bool artUsed = ArtEntries.Any(entry => entry.ArtId == id && !entry.IsFreeSlot);

            if (!gumpUsed && !artUsed)
            {
                return id;
            }
        }

        return start;
    }

    private string NormalizeWearableHue()
    {
        string value = (WearableWizardHue ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            return "0";
        }

        return value;
    }

    private static string DisplayPathOrNone(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? "None"
            : path;
    }

    private void ValidateWearableWizardConflicts()
    {
        bool maleConflict =
            GumpEntries.Any(x => x.GumpId == WearableWizardMaleGumpId && x.IsValid);

        bool femaleConflict =
            WearableWizardCreateFemaleVariant &&
            GumpEntries.Any(x => x.GumpId == WearableWizardFemaleGumpId && x.IsValid);

        bool artConflict =
            ArtEntries.Any(x =>
                x.ArtId == WearableWizardArtId &&
                !x.IsFreeSlot);

        WearableWizardMaleGumpConflict = maleConflict;
        WearableWizardFemaleGumpConflict = femaleConflict;
        WearableWizardArtConflict = artConflict;

        List<string> warnings = new();

        if (maleConflict)
        {
            warnings.Add("Male/default gump ID already exists.");
        }

        if (femaleConflict)
        {
            warnings.Add("Female gump ID already exists.");
        }

        if (artConflict)
        {
            warnings.Add("Art ID already exists.");
        }

        WearableWizardConflictText =
            warnings.Count == 0
                ? "No slot conflicts detected."
                : string.Join(Environment.NewLine, warnings);
    }

    [RelayCommand]
    private void RefreshWearableWizardFreeSlots()
    {
        RebuildWearableWizardSlotFileOptions();
        ApplyWearableWizardSlotFilters();
    }

    [RelayCommand]
    private void UseWearableWizardSelectedSlot()
    {
        if (WearableWizardSelectedFreeSlot == null)
        {
            WearableWizardStatusText = "Select a free animation slot first.";
            return;
        }

        int bodyId = WearableWizardSelectedFreeSlot.TrueBodyId;

        WearableWizardAnimationId = bodyId;

        if (bodyId >= 400)
        {
            WearableWizardMaleGumpId = 50000 + bodyId;
            WearableWizardFemaleGumpId = 60000 + bodyId;
            WearableWizardArtId = 50000 + bodyId;
        }

        WearableWizardStatusText =
            "Selected free animation slot " +
            WearableWizardSelectedFreeSlot.DisplayText +
            ". Body.def will map new animation " +
            WearableWizardAnimationId +
            " to existing animation " +
            WearableWizardExistingAnimationId +
            ".";

        RebuildWearableWizardPlan();
    }

    private void RebuildWearableWizardSlotFileOptions()
    {
        string previous = WearableWizardSelectedSlotFile;

        WearableWizardSlotFileOptions.Clear();
        WearableWizardSlotFileOptions.Add("All Files");

        foreach (string fileName in allMulSlotEntries
                     .Select(x => x.FileName)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            WearableWizardSlotFileOptions.Add(fileName);
        }

        WearableWizardSelectedSlotFile =
            WearableWizardSlotFileOptions.Contains(previous)
                ? previous
                : "All Files";
    }

    private void ApplyWearableWizardSlotFilters()
    {
        WearableWizardFreeSlots.Clear();

        string search = WearableWizardSlotSearchText?.Trim() ?? string.Empty;
        string selectedFile = WearableWizardSelectedSlotFile ?? "All Files";

        foreach (MulSlotEntry entry in allMulSlotEntries)
        {
            if (!string.Equals(selectedFile, "All Files", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.FileName, selectedFile, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool anyTypeFilter =
                WearableWizardSlotShowHFilter ||
                WearableWizardSlotShowLFilter ||
                WearableWizardSlotShowPFilter;

            if (anyTypeFilter)
            {
                bool matchesType =
                    (WearableWizardSlotShowHFilter && string.Equals(entry.TypeLetter, "H", StringComparison.OrdinalIgnoreCase)) ||
                    (WearableWizardSlotShowLFilter && string.Equals(entry.TypeLetter, "L", StringComparison.OrdinalIgnoreCase)) ||
                    (WearableWizardSlotShowPFilter && string.Equals(entry.TypeLetter, "P", StringComparison.OrdinalIgnoreCase));

                if (!matchesType)
                {
                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                bool matchesSearch =
                    entry.DisplayText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.FileName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.BodyIndex.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.TrueBodyId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.TypeLetter.Contains(search, StringComparison.OrdinalIgnoreCase);

                if (!matchesSearch)
                {
                    continue;
                }
            }

            WearableWizardFreeSlots.Add(entry);
        }

        if (WearableWizardFreeSlots.Count > 0)
        {
            if (WearableWizardSelectedFreeSlot == null ||
                !WearableWizardFreeSlots.Contains(WearableWizardSelectedFreeSlot))
            {
                WearableWizardSelectedFreeSlot = WearableWizardFreeSlots[0];
            }
        }
        else
        {
            WearableWizardSelectedFreeSlot = null;
        }

        WearableWizardStatusText =
            "Showing " + WearableWizardFreeSlots.Count + " wearable animation slot candidates.";
    }

    partial void OnWearableWizardSlotSearchTextChanged(string value)
    {
        ApplyWearableWizardSlotFilters();
    }

    partial void OnWearableWizardSlotShowHFilterChanged(bool value)
    {
        ApplyWearableWizardSlotFilters();
    }

    partial void OnWearableWizardSlotShowLFilterChanged(bool value)
    {
        ApplyWearableWizardSlotFilters();
    }

    partial void OnWearableWizardSlotShowPFilterChanged(bool value)
    {
        ApplyWearableWizardSlotFilters();
    }

    partial void OnWearableWizardSelectedSlotFileChanged(string value)
    {
        ApplyWearableWizardSlotFilters();
    }

    [RelayCommand]
    private void RefreshWearableWizardSourceAnimations()
    {
        ApplyWearableWizardSourceAnimationFilter();
    }

    [RelayCommand]
    private void UseWearableWizardSourceAnimation()
    {
        if (WearableWizardSelectedSourceAnimation == null)
        {
            WearableWizardStatusText = "Select a source animation first.";
            return;
        }

        WearableWizardExistingAnimationId = WearableWizardSelectedSourceAnimation.BodyId;

        WearableWizardStatusText =
            "Using source animation Body " +
            WearableWizardExistingAnimationId +
            " for Body.def inheritance.";

        RebuildWearableWizardPlan();
    }

    private void ApplyWearableWizardSourceAnimationFilter()
    {
        WearableWizardSourceAnimations.Clear();

        string search = WearableWizardSourceAnimationSearchText?.Trim() ?? string.Empty;

        HashSet<int> seenBodies = new();

        foreach (AnimationEntry entry in AnimationEntries.OrderBy(x => x.BodyId))
        {
            // Wearable/equipment animations are P slots / human-style 35-action bodies.
            if (entry.BodyId < 400)
            {
                continue;
            }

            if (!seenBodies.Add(entry.BodyId))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                bool matches =
                    entry.BodyId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.SecondaryText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.SourceFile.Contains(search, StringComparison.OrdinalIgnoreCase);

                if (!matches)
                {
                    continue;
                }
            }

            WearableWizardSourceAnimations.Add(entry);
        }

        if (WearableWizardSourceAnimations.Count > 0)
        {
            if (WearableWizardSelectedSourceAnimation == null ||
                !WearableWizardSourceAnimations.Contains(WearableWizardSelectedSourceAnimation))
            {
                WearableWizardSelectedSourceAnimation = WearableWizardSourceAnimations[0];
            }
        }
        else
        {
            WearableWizardSelectedSourceAnimation = null;
        }
    }

    partial void OnWearableWizardSourceAnimationSearchTextChanged(string value)
    {
        ApplyWearableWizardSourceAnimationFilter();
    }

    [RelayCommand]
    private void RefreshWearableWizardOverlayPreview()
    {
        if (!WearableWizardOverlayPreviewEnabled)
        {
            WearableWizardOverlayPreviewBitmap = null;
            WearableWizardOverlayPreviewStatus = "Overlay preview disabled.";
            return;
        }

        AnimationEntry? baseEntry = AnimationEntries
            .FirstOrDefault(x => x.BodyId == WearableWizardPreviewBaseBodyId);

        if (baseEntry == null)
        {
            WearableWizardOverlayPreviewBitmap = null;
            WearableWizardOverlayPreviewStatus = "Base body " + WearableWizardPreviewBaseBodyId + " was not found.";
            return;
        }

        int wearableBodyId = WearableWizardExistingAnimationId;

        AnimationEntry? wearableEntry = AnimationEntries
            .FirstOrDefault(x => x.BodyId == wearableBodyId);

        if (wearableEntry == null)
        {
            WearableWizardOverlayPreviewBitmap = null;
            WearableWizardOverlayPreviewStatus = "Wearable animation body " + wearableBodyId + " was not found.";
            return;
        }

        DetachedPreviewLoadResult baseResult = LoadDetachedPreview(
            baseEntry,
            WearableWizardPreviewAction,
            WearableWizardPreviewDirection);

        if (!baseResult.Success || baseResult.FrameData.Count == 0)
        {
            WearableWizardOverlayPreviewBitmap = null;
            WearableWizardOverlayPreviewStatus = "Could not load base body preview: " + baseResult.Message;
            return;
        }

        DetachedPreviewLoadResult wearableResult = LoadDetachedPreview(
            wearableEntry,
            WearableWizardPreviewAction,
            WearableWizardPreviewDirection);

        if (!wearableResult.Success || wearableResult.FrameData.Count == 0)
        {
            WearableWizardOverlayPreviewBitmap = null;
            WearableWizardOverlayPreviewStatus = "Could not load wearable animation preview: " + wearableResult.Message;
            return;
        }

        int frameIndex = WearableWizardPreviewFrameIndex;
        if (frameIndex < 0)
        {
            frameIndex = 0;
        }

        VdFrameData baseFrame = baseResult.FrameData[frameIndex % baseResult.FrameData.Count];
        VdFrameData wearableFrame = wearableResult.FrameData[frameIndex % wearableResult.FrameData.Count];

        WearableWizardOverlayPreviewBitmap = BuildWearableAnimationOverlayPreview(
            baseFrame,
            wearableFrame,
            WearableWizardOverlayOffsetX,
            WearableWizardOverlayOffsetY,
            WearableWizardOverlayScale);

        WearableWizardOverlayPreviewStatus =
            "Previewing base body " + WearableWizardPreviewBaseBodyId +
            " + wearable animation " + wearableBodyId +
            " | Action " + WearableWizardPreviewAction +
            " | Direction " + WearableWizardPreviewDirection +
            " | Frame " + frameIndex + ".";
    }

    private WriteableBitmap BuildWearableAnimationOverlayPreview(
        VdFrameData baseFrame,
        VdFrameData wearableFrame,
        double offsetX,
        double offsetY,
        double scale)
    {
        int padding = 160;

        int baseWidth = baseFrame.Bitmap.PixelSize.Width;
        int baseHeight = baseFrame.Bitmap.PixelSize.Height;

        int wearableWidth = Math.Max(1, (int)(wearableFrame.Bitmap.PixelSize.Width * scale));
        int wearableHeight = Math.Max(1, (int)(wearableFrame.Bitmap.PixelSize.Height * scale));

        int outputWidth = Math.Max(baseWidth, wearableWidth) + padding;
        int outputHeight = Math.Max(baseHeight, wearableHeight) + padding;

        WriteableBitmap output = new WriteableBitmap(
            new Avalonia.PixelSize(outputWidth, outputHeight),
            new Avalonia.Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            Avalonia.Platform.AlphaFormat.Premul);

        byte[] outputPixels = new byte[outputWidth * outputHeight * 4];

        int originX = outputWidth / 2;
        int originY = (outputHeight / 2) + 50;

        int baseX = originX - baseFrame.CenterX;
        int baseY = originY - baseFrame.Bitmap.PixelSize.Height - baseFrame.CenterY;

        int wearableX = originX - (int)(wearableFrame.CenterX * scale) + (int)offsetX;
        int wearableY = originY - wearableHeight - (int)(wearableFrame.CenterY * scale) + (int)offsetY;

        CopyBitmapToBuffer(baseFrame.Bitmap, outputPixels, outputWidth, baseX, baseY);
        CopyBitmapScaledToBuffer(
            wearableFrame.Bitmap,
            outputPixels,
            outputWidth,
            wearableX,
            wearableY,
            wearableWidth,
            wearableHeight);

        using Avalonia.Platform.ILockedFramebuffer framebuffer = output.Lock();
        System.Runtime.InteropServices.Marshal.Copy(outputPixels, 0, framebuffer.Address, outputPixels.Length);

        return output;
    }

    private void CopyBitmapToBuffer(
    WriteableBitmap source,
    byte[] destination,
    int destinationWidth,
    int destinationX,
    int destinationY)
    {
        using Avalonia.Platform.ILockedFramebuffer framebuffer = source.Lock();

        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int srcRowBytes = framebuffer.RowBytes;
        byte[] src = new byte[srcRowBytes * height];

        System.Runtime.InteropServices.Marshal.Copy(framebuffer.Address, src, 0, src.Length);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcOffset = (y * srcRowBytes) + (x * 4);
                int dstX = destinationX + x;
                int dstY = destinationY + y;

                if (dstX < 0 || dstY < 0)
                {
                    continue;
                }

                int dstOffset = ((dstY * destinationWidth) + dstX) * 4;
                if (dstOffset < 0 || dstOffset + 3 >= destination.Length)
                {
                    continue;
                }

                destination[dstOffset + 0] = src[srcOffset + 0];
                destination[dstOffset + 1] = src[srcOffset + 1];
                destination[dstOffset + 2] = src[srcOffset + 2];
                destination[dstOffset + 3] = src[srcOffset + 3];
            }
        }
    }

    private void CopyBitmapScaledToBuffer(
        WriteableBitmap source,
        byte[] destination,
        int destinationWidth,
        int destinationX,
        int destinationY,
        int scaledWidth,
        int scaledHeight)
    {
        using Avalonia.Platform.ILockedFramebuffer framebuffer = source.Lock();

        int sourceWidth = framebuffer.Size.Width;
        int sourceHeight = framebuffer.Size.Height;
        int srcRowBytes = framebuffer.RowBytes;
        byte[] src = new byte[srcRowBytes * sourceHeight];

        System.Runtime.InteropServices.Marshal.Copy(framebuffer.Address, src, 0, src.Length);

        for (int y = 0; y < scaledHeight; y++)
        {
            int srcY = Math.Clamp((int)((double)y / scaledHeight * sourceHeight), 0, sourceHeight - 1);

            for (int x = 0; x < scaledWidth; x++)
            {
                int srcX = Math.Clamp((int)((double)x / scaledWidth * sourceWidth), 0, sourceWidth - 1);
                int srcOffset = (srcY * srcRowBytes) + (srcX * 4);

                byte alpha = src[srcOffset + 3];
                if (alpha == 0)
                {
                    continue;
                }

                int dstX = destinationX + x;
                int dstY = destinationY + y;

                if (dstX < 0 || dstY < 0)
                {
                    continue;
                }

                int dstOffset = ((dstY * destinationWidth) + dstX) * 4;
                if (dstOffset < 0 || dstOffset + 3 >= destination.Length)
                {
                    continue;
                }

                double a = alpha / 255.0;
                destination[dstOffset + 0] = (byte)((src[srcOffset + 0] * a) + (destination[dstOffset + 0] * (1.0 - a)));
                destination[dstOffset + 1] = (byte)((src[srcOffset + 1] * a) + (destination[dstOffset + 1] * (1.0 - a)));
                destination[dstOffset + 2] = (byte)((src[srcOffset + 2] * a) + (destination[dstOffset + 2] * (1.0 - a)));
                destination[dstOffset + 3] = 255;
            }
        }
    }

    partial void OnWearableWizardPreviewBaseBodyIdChanged(int value)
    {
        RefreshWearableWizardOverlayPreview();
    }

    partial void OnWearableWizardPreviewActionChanged(int value)
    {
        RefreshWearableWizardOverlayPreview();
    }

    partial void OnWearableWizardPreviewDirectionChanged(int value)
    {
        RefreshWearableWizardOverlayPreview();
    }

    partial void OnWearableWizardPreviewFrameIndexChanged(int value)
    {
        RefreshWearableWizardOverlayPreview();
    }

    partial void OnWearableWizardOverlayOffsetXChanged(double value)
    {
        RefreshWearableWizardOverlayPreview();
    }

    partial void OnWearableWizardOverlayOffsetYChanged(double value)
    {
        RefreshWearableWizardOverlayPreview();
    }

    partial void OnWearableWizardOverlayScaleChanged(double value)
    {
        RefreshWearableWizardOverlayPreview();
    }

    partial void OnWearableWizardOverlayPreviewEnabledChanged(bool value)
    {
        RefreshWearableWizardOverlayPreview();
    }
}