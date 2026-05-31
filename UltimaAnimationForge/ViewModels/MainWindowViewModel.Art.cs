using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;
using UltimaAnimationForge.Views;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    public ObservableCollection<ArtCutterSliceEntry> ArtCutterSlices { get; } = new();

    private readonly ArtRadarColorService artRadarColorService = new();
    private readonly RadarColService radarColService = new();
    private readonly ArtAndRadarCacheService artAndRadarCacheService = new();

    [ObservableProperty]
    private Color selectedArtRadarColor = Colors.Transparent;

    public IBrush SelectedArtRadarBrush =>
        SelectedArtRadarColor.A == 0
            ? Brushes.Transparent
            : new SolidColorBrush(SelectedArtRadarColor);

    public string SelectedArtRadarColorText =>
        artRadarColorService.ToHexText(SelectedArtRadarColor) +
        " | " +
        artRadarColorService.ToUoColorText(SelectedArtRadarColor);

    [ObservableProperty]
    private Color selectedArtCurrentRadarColor = Colors.Transparent;

    [ObservableProperty]
    private bool selectedArtRadarColorPending;

    public IBrush SelectedArtCurrentRadarBrush =>
        SelectedArtCurrentRadarColor.A == 0
            ? Brushes.Transparent
            : new SolidColorBrush(SelectedArtCurrentRadarColor);

    public string SelectedArtCurrentRadarColorText =>
        radarColService.IsLoaded
            ? "#" + SelectedArtCurrentRadarColor.R.ToString("X2") +
              SelectedArtCurrentRadarColor.G.ToString("X2") +
              SelectedArtCurrentRadarColor.B.ToString("X2") +
              " | 0x" + radarColService.GetColor(SelectedArtEntry).ToString("X4") +
              (SelectedArtRadarColorPending ? " | Pending" : "")
            : "radarcol.mul not loaded";

    [ObservableProperty]
    private bool showArtTileBaseOverlay;

    [ObservableProperty]
    private ArtVisibleBoundsInfo? selectedArtVisibleBounds;

    [ObservableProperty]
    private bool showArtBrowserMode;

    public bool ShowArtSinglePreview => !ShowArtBrowserMode;
    public bool ShowArtBrowserPreview => ShowArtBrowserMode;

    [ObservableProperty]
    private bool artCutterBlackTransparent = true;

    [ObservableProperty]
    private int artCutterSourceOffsetX;

    [ObservableProperty]
    private int artCutterSourceOffsetY;

    [ObservableProperty]
    private string artCutterImagePath = string.Empty;

    [ObservableProperty]
    private int artCutterStartId;

    [ObservableProperty]
    private int artCutterSliceWidth = 44;

    [ObservableProperty]
    private int artCutterSliceHeight = 44;

    [ObservableProperty]
    private bool artCutterAutoTrim = true;

    [ObservableProperty]
    private bool artCutterSkipEmpty = true;

    [ObservableProperty]
    private WriteableBitmap? artImportAdjustPreviewBitmap;

    [ObservableProperty]
    private string pendingArtImportImagePath = string.Empty;

    [ObservableProperty]
    private bool artImportAutoTrim = true;

    [ObservableProperty]
    private bool artImportCenterOnCanvas;

    [ObservableProperty]
    private int artImportCanvasWidth;

    [ObservableProperty]
    private int artImportCanvasHeight;

    [ObservableProperty]
    private int artImportOffsetX;

    [ObservableProperty]
    private int artImportOffsetY;

    [ObservableProperty]
    private string artImportAdjustStatusText = string.Empty;

    [ObservableProperty]
    private string selectedArtSharpenMode = "Gaussian";

    public ObservableCollection<string> ArtSharpenModes { get; } = new()
    {
        "Gaussian",
        "Pixel"
    };

    [ObservableProperty]
    private bool showArtTileDataEditor;

    public ObservableCollection<string> ArtSlotFilterOptions { get; } = new()
    {
        "All",
        "Used Only",
        "Free Slots"
    };

    [ObservableProperty]
    private string selectedArtSlotFilter = "Used Only";

    public ObservableCollection<ArtEraFilter> ArtEraFilters { get; } = new();

    [ObservableProperty]
    private ArtEraFilter? selectedArtEraFilter;

    public bool UseArtEraFilter => SelectedArtEraFilter != null;

    private readonly ArtDataService artDataService = new();

    public string ArtAnimDataEditButtonText =>
    SelectedArtAnimDataEntry == null ? "Create AnimData..." : "Edit AnimData...";

    public ObservableCollection<ArtEntry> ArtEntries { get; } = new();

    [ObservableProperty]
    private AnimDataFrameEntry? selectedEditAnimDataFrame;

    [ObservableProperty]
    private string editAnimDataAddFrameGraphicText = string.Empty;

    [ObservableProperty]
    private bool editAnimDataAddFrameRelative;

    [ObservableProperty]
    private bool showArtImageTools;

    [ObservableProperty]
    private double artBrightness = 0;

    [ObservableProperty]
    private double artContrast = 0;

    [ObservableProperty]
    private double artSharpness = 0;

    [ObservableProperty]
    private double artOverlayOpacity = 35;

    [ObservableProperty]
    private string selectedArtOverlayBlendMode = "Multiply";

    public ObservableCollection<string> ArtOverlayBlendModes { get; } = new()
{
    "Normal",
    "Multiply",
    "Overlay",
    "SoftLight",
    "Screen"
};

    private WriteableBitmap? originalArtEditBitmap;
    private WriteableBitmap? artOverlayBitmap;

    private DispatcherTimer? artAnimDataPlaybackTimer;
    private int artAnimDataPlaybackIndex;
    private bool isArtAnimDataPlaying;

    public string ArtAnimDataPlayButtonText => isArtAnimDataPlaying ? "Pause" : "Play";

    [ObservableProperty]
    private string selectedArtAnimDataStatusText = string.Empty;

    [ObservableProperty]
    private ArtEntry? selectedArtEntry;

    [ObservableProperty]
    private WriteableBitmap? selectedArtBitmap;

    [ObservableProperty]
    private string artSearchText = string.Empty;

    [ObservableProperty]
    private bool showLandArt = false;

    [ObservableProperty]
    private bool showStaticArt = true;

    [ObservableProperty]
    private string artStatusText = "Art not loaded.";

    [ObservableProperty]
    private bool showArtThumbnails = true;

    [ObservableProperty]
    private bool showFreeArtSlots;

    public string SelectedArtTileDataFlagNames =>
    SelectedArtTileDataEntry == null
        ? "-"
        : BuildTileDataFlagText(SelectedArtTileDataEntry.Flags);

    partial void OnShowArtBrowserModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowArtSinglePreview));
        OnPropertyChanged(nameof(ShowArtBrowserPreview));
    }

    partial void OnSelectedArtRadarColorChanged(Color value)
    {
        OnPropertyChanged(nameof(SelectedArtRadarBrush));
        OnPropertyChanged(nameof(SelectedArtRadarColorText));
        OnPropertyChanged(nameof(SelectedArtRadarMatchText));
    }

    partial void OnSelectedArtCurrentRadarColorChanged(Color value)
    {
        OnPropertyChanged(nameof(SelectedArtCurrentRadarBrush));
        OnPropertyChanged(nameof(SelectedArtCurrentRadarColorText));
        OnPropertyChanged(nameof(SelectedArtRadarMatchText));
    }

    partial void OnSelectedArtRadarColorPendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectedArtCurrentRadarColorText));
    }

    private static string BuildTileDataFlagText(ulong flags)
    {
        if (flags == 0)
        {
            return "None";
        }

        List<string> names = new();

        for (int i = 0; i < TileDataFlagNames.Length; i++)
        {
            ulong mask = 1UL << i;

            if ((flags & mask) != 0)
            {
                names.Add(TileDataFlagNames[i]);
            }
        }

        return names.Count == 0 ? "None" : string.Join(", ", names);
    }

    public string SelectedArtAnimationGumpText
    {
        get
        {
            if (SelectedArtTileDataEntry == null)
            {
                return "Animation/Gump: -";
            }

            int animation = SelectedArtTileDataEntry.Animation;

            if (SelectedArtTileDataEntry.IsLand || animation <= 0)
            {
                return "Animation/Gump: " + animation;
            }

            int maleGump = 50000 + animation;

            return "Animation/Gump: " +
                   animation +
                   " / Male Gump " +
                   maleGump +
                   " [0x" + maleGump.ToString("X") + "]";
        }
    }

    public int SelectedArtMaleEquipmentGumpId =>
    SelectedArtTileDataEntry != null && !SelectedArtTileDataEntry.IsLand && SelectedArtTileDataEntry.Animation > 0
        ? 50000 + SelectedArtTileDataEntry.Animation
        : -1;

    public string SelectedArtMaleEquipmentGumpText =>
        SelectedArtMaleEquipmentGumpId >= 0
            ? "Male equipment gump: " + SelectedArtMaleEquipmentGumpId + " [0x" + SelectedArtMaleEquipmentGumpId.ToString("X") + "]"
            : "Male equipment gump: -";

    public bool ShowSelectedArtEquipmentGump =>
        SelectedArtMaleEquipmentGumpBitmap != null;

    [ObservableProperty]
    private WriteableBitmap? selectedArtMaleEquipmentGumpBitmap;

    public AnimDataEntry? SelectedArtAnimDataEntry
    {
        get
        {
            if (SelectedArtEntry == null)
            {
                return null;
            }

            if (!string.Equals(SelectedArtEntry.Type, "Static", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return animDataMulService.AllLoadedEntries.FirstOrDefault(entry => entry.Id == SelectedArtEntry.ArtId);
        }
    }

    public bool ShowSelectedArtAnimDataPreview =>
        SelectedArtAnimDataEntry != null &&
        SelectedArtTileDataEntry != null &&
        (SelectedArtTileDataEntry.Flags & 0x01000000UL) != 0;

    public ObservableCollection<AnimDataFrameEntry> SelectedArtAnimDataFrames { get; } = new();

    [ObservableProperty]
    private AnimDataFrameEntry? selectedArtAnimDataFrame;

    [ObservableProperty]
    private WriteableBitmap? selectedArtAnimDataFrameBitmap;

    public TileDataEntry? SelectedArtTileDataEntry
    {
        get
        {
            if (SelectedArtEntry == null)
            {
                return null;
            }

            return TileDataEntries.FirstOrDefault(entry =>
                entry.IsLand == string.Equals(SelectedArtEntry.Type, "Land", StringComparison.OrdinalIgnoreCase) &&
                entry.Id == SelectedArtEntry.ArtId);
        }
    }

    public void RefreshSelectedArtEquipmentGump()
    {
        LoadSelectedArtEquipmentGump();

        OnPropertyChanged(nameof(SelectedArtMaleEquipmentGumpId));
        OnPropertyChanged(nameof(SelectedArtMaleEquipmentGumpText));
        OnPropertyChanged(nameof(ShowSelectedArtEquipmentGump));
        OnPropertyChanged(nameof(SelectedArtAnimationGumpText));
    }

    [RelayCommand]
    private void OpenArtTileDataFlags()
    {
        if (SelectedArtTileDataEntry == null)
        {
            return;
        }

        SelectedTileDataEntry = SelectedArtTileDataEntry;
        ApplyTileDataFlagsToCheckedArt = false;
        RebuildSelectedTileDataFlags();

        Window? mainWindow = GetMainWindow();

        TileDataFlagEditorWindow window = new()
        {
            DataContext = this
        };

        if (mainWindow != null)
        {
            window.ShowDialog(mainWindow);
        }
        else
        {
            window.Show();
        }
    }

    [RelayCommand]
    private void LoadArtTab()
    {
        string folderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            ArtStatusText = "Choose a UO folder first.";
            return;
        }

        if (TileDataEntries.Count == 0)
        {
            LoadTileData();
        }

        if (!animDataMulService.IsLoaded)
        {
            animDataMulService.Initialize(folderPath);
        }

        if (!artDataService.Initialize(folderPath))
        {
            ArtEntries.Clear();
            SelectedArtBitmap = null;
            ArtStatusText = "Could not find artLegacyMUL.uop or art.mul/artidx.mul.";
            return;
        }
        LoadArtEraFilters();
        RebuildArtEntries();
    }

    [RelayCommand]
    private void RebuildArtEntries()
    {
        ArtEntries.Clear();

        string folderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            ArtStatusText = "Choose a UO folder first.";
            return;
        }

        List<ArtEntry> sourceEntries = LoadCachedOrBuildArtEntries(folderPath);

        bool freeOnly =
            string.Equals(SelectedArtSlotFilter, "Free Slots", StringComparison.OrdinalIgnoreCase);

        bool usedOnly =
            string.Equals(SelectedArtSlotFilter, "Used Only", StringComparison.OrdinalIgnoreCase);

        foreach (ArtEntry entry in sourceEntries)
        {
            bool isLand = string.Equals(entry.Type, "Land", StringComparison.OrdinalIgnoreCase);

            if (isLand && !ShowLandArt)
            {
                continue;
            }

            if (!isLand && !ShowStaticArt)
            {
                continue;
            }

            if (freeOnly && !entry.IsFreeSlot && !entry.IsPendingChange)
            {
                continue;
            }

            if (usedOnly && entry.IsFreeSlot && !entry.IsPendingChange)
            {
                continue;
            }

            if (!MatchesArtSearch(entry, ArtSearchText))
            {
                continue;
            }

            if (!IsArtInEraFilter(entry, SelectedArtEraFilter))
            {
                continue;
            }

            TileDataEntry? tileDataEntry = TileDataEntries.FirstOrDefault(tile =>
                tile.IsLand == isLand &&
                tile.Id == entry.ArtId);

            entry.IsPendingArtChange = artDataService.HasPendingArtChange(entry);
            entry.IsPendingTileDataChange = tileDataEntry?.IsEdited == true;

            if (entry.IsPendingArtChange && entry.IsPendingTileDataChange)
            {
                entry.IsFreeSlot = false;
                entry.SecondaryText = "Pending import + TileData edit - not saved yet";
            }
            else if (entry.IsPendingArtChange)
            {
                entry.IsFreeSlot = false;
                entry.SecondaryText = "Pending import - not saved yet";
            }
            else if (entry.IsPendingTileDataChange)
            {
                entry.SecondaryText = "Pending TileData edit - not saved yet";
            }

            if (ShowArtThumbnails)
            {
                entry.Thumbnail = artDataService.LoadThumbnailCached(entry);
            }

            ArtEntries.Add(entry);
        }

        SelectedArtEntry = ArtEntries.Count > 0 ? ArtEntries[0] : null;

        if (SelectedArtEntry == null)
        {
            SelectedArtBitmap = null;
        }

        ArtStatusText = "Loaded " + ArtEntries.Count + " art entries.";
    }

    private List<ArtEntry> LoadCachedOrBuildArtEntries(string folderPath)
    {
        if (activeProfile == null)
        {
            activeProfile = GetActiveProfile();
        }

        string profileId = activeProfile?.ProfileId ?? "default";

        ArtCacheData? cache = artAndRadarCacheService.LoadArtCache(profileId);

        if (artAndRadarCacheService.IsArtCacheValid(cache, folderPath) && cache?.ArtEntries != null)
        {
            return cache.ArtEntries.Select(CloneCachedArtEntry).ToList();
        }

        List<ArtEntry> builtEntries = artDataService.BuildEntries(
            true,
            true,
            true,
            string.Empty);

        artAndRadarCacheService.SaveArtCache(profileId, folderPath, builtEntries);

        return builtEntries;
    }

    private static ArtEntry CloneCachedArtEntry(CachedArtEntry source)
    {
        return new ArtEntry
        {
            IsFreeSlot = source.IsFreeSlot,
            ArtId = source.ArtId,
            FileIndex = source.FileIndex,
            Type = source.Type,
            SecondaryText = source.SecondaryText
        };
    }

    private static bool MatchesArtSearch(ArtEntry entry, string searchText)
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

    partial void OnSelectedArtSlotFilterChanged(string value)
    {
        RebuildArtEntries();
    }

    partial void OnSelectedArtEraFilterChanged(ArtEraFilter? value)
    {
        RebuildArtEntries();
    }

    partial void OnSelectedArtEntryChanged(ArtEntry? value)
    {
        SelectedArtBitmap = artDataService.LoadBitmap(value);
        SelectedArtVisibleBounds = artDataService.GetVisibleBounds(SelectedArtBitmap);
        SelectedArtRadarColor = artRadarColorService.GetAverageVisibleColor(SelectedArtBitmap);
        RefreshSelectedArtRadarColInfo();
        originalArtEditBitmap = SelectedArtBitmap != null ? CloneBitmap(SelectedArtBitmap) : null;
        artOverlayBitmap = null;

        if (value != null)
        {
            ArtStatusText = value.DisplayText;
        }

        RebuildSelectedArtAnimDataFrames();
        LoadSelectedArtEquipmentGump();

        OnPropertyChanged(nameof(SelectedArtTileDataEntry));
        OnPropertyChanged(nameof(SelectedArtAnimDataEntry));
        OnPropertyChanged(nameof(ShowSelectedArtAnimDataPreview));
        OnPropertyChanged(nameof(SelectedArtMaleEquipmentGumpId));
        OnPropertyChanged(nameof(SelectedArtMaleEquipmentGumpText));
        OnPropertyChanged(nameof(ShowSelectedArtEquipmentGump));
        OnPropertyChanged(nameof(SelectedArtTileDataFlagNames));
        OnPropertyChanged(nameof(SelectedArtAnimationGumpText));
        OnPropertyChanged(nameof(ArtAnimDataEditButtonText));
    }

    partial void OnArtSearchTextChanged(string value)
    {
        RebuildArtEntries();
    }

    partial void OnShowLandArtChanged(bool value)
    {
        RebuildArtEntries();
    }

    partial void OnShowStaticArtChanged(bool value)
    {
        RebuildArtEntries();
    }

    [RelayCommand]
    private async Task ExportSelectedArtAsync()
    {
        if (SelectedArtEntry == null || SelectedArtBitmap == null)
        {
            ArtStatusText = "No art entry selected.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            ArtStatusText = "Could not locate main window.";
            return;
        }

        IStorageFile? file = await mainWindow.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export Selected Art",
                SuggestedFileName = SelectedArtEntry.Type.ToLowerInvariant() + "_0x" + SelectedArtEntry.ArtId.ToString("X4") + ".png",
                FileTypeChoices = new[]
                {
                new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                new FilePickerFileType("BMP Image") { Patterns = new[] { "*.bmp" } }
                }
            });

        if (file == null)
        {
            ArtStatusText = "Export cancelled.";
            return;
        }

        string? path = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            ArtStatusText = "Selected export path is invalid.";
            return;
        }

        artDataService.ExportBitmap(SelectedArtBitmap, path);
        ArtStatusText = "Exported " + SelectedArtEntry.DisplayText + ".";
    }

    [RelayCommand]
    private async Task MassExportArtAsync()
    {
        if (ArtEntries.Count == 0)
        {
            ArtStatusText = "No art entries loaded.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            ArtStatusText = "Could not locate main window.";
            return;
        }

        IStorageFolder? folder = await mainWindow.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Choose Art Export Folder",
                AllowMultiple = false
            }).ContinueWith(task => task.Result.Count > 0 ? task.Result[0] : null);

        if (folder == null)
        {
            ArtStatusText = "Mass export cancelled.";
            return;
        }

        string? folderPath = folder.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            ArtStatusText = "Selected export folder is invalid.";
            return;
        }

        int exported = 0;

        List<ArtEntry> entriesToExport = ArtEntries
    .Where(entry => entry.IsChecked)
    .ToList();

        if (entriesToExport.Count == 0)
        {
            ArtStatusText = "No checked art entries to export.";
            return;
        }

        foreach (ArtEntry entry in entriesToExport)
        {
            WriteableBitmap? bitmap = artDataService.LoadBitmap(entry);
            if (bitmap == null)
            {
                continue;
            }

            string fileName = entry.Type.ToLowerInvariant() + "_0x" + entry.ArtId.ToString("X4") + ".png";
            string path = Path.Combine(folderPath, fileName);

            artDataService.ExportBitmap(bitmap, path);
            exported++;
        }

        ArtStatusText = "Mass exported " + exported + " checked art entries.";
    }

    partial void OnShowArtThumbnailsChanged(bool value)
    {
        RebuildArtEntries();
    }

    [RelayCommand]
    private void CheckAllArt()
    {
        foreach (ArtEntry entry in ArtEntries)
        {
            entry.IsChecked = true;
        }

        ArtStatusText = "Checked " + ArtEntries.Count + " art entries.";
    }

    [RelayCommand]
    private void UncheckAllArt()
    {
        foreach (ArtEntry entry in ArtEntries)
        {
            entry.IsChecked = false;
        }

        ArtStatusText = "Unchecked all art entries.";
    }

    partial void OnShowFreeArtSlotsChanged(bool value)
    {
        RebuildArtEntries();
    }

    private void RebuildSelectedArtAnimDataFrames()
    {
        SelectedArtAnimDataFrames.Clear();
        SelectedArtAnimDataFrame = null;
        SelectedArtAnimDataFrameBitmap = null;
        artAnimDataPlaybackTimer?.Stop();
        isArtAnimDataPlaying = false;
        artAnimDataPlaybackIndex = 0;
        OnPropertyChanged(nameof(ArtAnimDataPlayButtonText));

        AnimDataEntry? animEntry = SelectedArtAnimDataEntry;
        if (animEntry == null)
        {
            return;
        }

        animEntry.RebuildFrames(
    GetStaticTileName,
    StaticArtExists,
    graphicId => LoadStaticArtBitmap(graphicId));

        bool hasAnyFrameArt = animEntry.Frames.Any(frame => frame.Bitmap != null);
        bool hasSelectedArt = SelectedArtBitmap != null;

        selectedArtAnimDataStatusText =
            "Frames: " + animEntry.FrameCount +
            " | Interval: " + animEntry.FrameInterval +
            " | Start: " + animEntry.FrameStart +
            ((hasSelectedArt || hasAnyFrameArt) ? string.Empty : " | Missing art");

        foreach (AnimDataFrameEntry frame in animEntry.Frames)
        {
            SelectedArtAnimDataFrames.Add(frame);
        }

        if (SelectedArtAnimDataFrames.Count > 0)
        {
            SelectedArtAnimDataFrame = SelectedArtAnimDataFrames[0];
        }
    }

    partial void OnSelectedArtAnimDataFrameChanged(AnimDataFrameEntry? value)
    {
        SelectedArtAnimDataFrameBitmap = value?.Bitmap;

        if (!isArtAnimDataPlaying && value?.Bitmap != null)
        {
            SelectedArtBitmap = value.Bitmap;
        }
    }

    [RelayCommand]
    private void ToggleArtAnimDataPlayback()
    {
        if (SelectedArtAnimDataFrames.Count == 0)
        {
            return;
        }

        if (artAnimDataPlaybackTimer == null)
        {
            artAnimDataPlaybackTimer = new DispatcherTimer();
            artAnimDataPlaybackTimer.Tick += ArtAnimDataPlaybackTimer_Tick;
        }

        if (isArtAnimDataPlaying)
        {
            artAnimDataPlaybackTimer.Stop();
            isArtAnimDataPlaying = false;
            OnPropertyChanged(nameof(ArtAnimDataPlayButtonText));
            return;
        }

        isArtAnimDataPlaying = true;
        OnPropertyChanged(nameof(ArtAnimDataPlayButtonText));

        int interval = 100;
        if (SelectedArtAnimDataEntry != null && SelectedArtAnimDataEntry.FrameInterval > 0)
        {
            interval = Math.Max(50, SelectedArtAnimDataEntry.FrameInterval * 100);
        }

        artAnimDataPlaybackTimer.Interval = TimeSpan.FromMilliseconds(interval);
        artAnimDataPlaybackTimer.Start();
    }

    private void ArtAnimDataPlaybackTimer_Tick(object? sender, EventArgs e)
    {
        if (SelectedArtAnimDataFrames.Count == 0)
        {
            artAnimDataPlaybackTimer?.Stop();
            isArtAnimDataPlaying = false;
            OnPropertyChanged(nameof(ArtAnimDataPlayButtonText));
            return;
        }

        artAnimDataPlaybackIndex++;

        if (artAnimDataPlaybackIndex >= SelectedArtAnimDataFrames.Count)
        {
            artAnimDataPlaybackIndex = 0;
        }

        SelectedArtAnimDataFrame = SelectedArtAnimDataFrames[artAnimDataPlaybackIndex];

        if (SelectedArtAnimDataFrame?.Bitmap != null)
        {
            SelectedArtBitmap = SelectedArtAnimDataFrame.Bitmap;
        }
    }

    private void LoadSelectedArtEquipmentGump()
    {
        SelectedArtMaleEquipmentGumpBitmap = null;

        int gumpId = SelectedArtMaleEquipmentGumpId;
        if (gumpId < 0)
        {
            return;
        }

        string folderPath = GetCurrentFolderPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        if (gumpDataService.Entries.Count == 0)
        {
            gumpDataService.Initialize(folderPath);
        }

        GumpEntry? gumpEntry = gumpDataService.Entries
            .FirstOrDefault(entry => entry.GumpId == gumpId && entry.IsValid);

        if (gumpEntry == null)
        {
            return;
        }

        GumpLoadResult result = gumpDataService.LoadGump(gumpEntry);
        if (!result.Success || result.Bitmap == null)
        {
            return;
        }

        SelectedArtMaleEquipmentGumpBitmap = result.Bitmap;
    }

    [RelayCommand]
    private async Task ImportSelectedArtAsync()
    {
        if (SelectedArtEntry == null)
        {
            ArtStatusText = "No art entry selected.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            ArtStatusText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Import Art Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                new FilePickerFileType("Image files")
                {
                    Patterns = new[] { "*.png", "*.bmp" }
                }
                }
            });

        if (files.Count == 0)
        {
            ArtStatusText = "Import cancelled.";
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            ArtStatusText = "Selected image path is invalid.";
            return;
        }

        PendingArtImportImagePath = path;
        ArtImportAutoTrim = true;
        ArtImportCenterOnCanvas = false;
        ArtImportCanvasWidth = 0;
        ArtImportCanvasHeight = 0;
        ArtImportOffsetX = 0;
        ArtImportOffsetY = 0;
        ArtImportAdjustStatusText = "Adjust import options, then queue the import.";

        ArtImportAdjustWindow window = new()
        {
            DataContext = this
        };
        RefreshArtImportAdjustPreview();
        await window.ShowDialog(mainWindow);
    }

    [RelayCommand]
    private async Task MassImportArtAsync()
    {
        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            ArtStatusText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await mainWindow.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Choose Art Import Folder",
                AllowMultiple = false
            });

        if (folders.Count == 0)
        {
            ArtStatusText = "Mass import cancelled.";
            return;
        }

        string? folderPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            ArtStatusText = "Selected import folder is invalid.";
            return;
        }

        string[] imagePaths = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path =>
                string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".bmp", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (imagePaths.Length == 0)
        {
            ArtStatusText = "No PNG or BMP files found in selected folder.";
            return;
        }

        List<ArtEntry> targetSlots = ArtEntries
            .Where(entry =>
                entry.IsChecked &&
                entry.IsFreeSlot &&
                string.Equals(entry.Type, "Static", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.ArtId)
            .ToList();

        string modeText = "checked free static slots";

        if (targetSlots.Count == 0)
        {
            targetSlots = artDataService
                .BuildEntries(false, true, true, string.Empty)
                .Where(entry =>
                    entry.IsFreeSlot &&
                    string.Equals(entry.Type, "Static", StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.ArtId)
                .ToList();

            modeText = "next free static slots";
        }

        if (targetSlots.Count == 0)
        {
            ArtStatusText = "No free static art slots found.";
            return;
        }

        int imported = 0;
        int failed = 0;
        int skipped = 0;
        string lastError = string.Empty;

        ArtImportAdjustOptions options = new()
        {
            AutoTrim = true,
            CenterOnCanvas = false,
            CanvasWidth = 0,
            CanvasHeight = 0,
            OffsetX = 0,
            OffsetY = 0
        };

        int count = Math.Min(imagePaths.Length, targetSlots.Count);

        for (int i = 0; i < count; i++)
        {
            ArtEntry targetEntry = targetSlots[i];
            string imagePath = imagePaths[i];

            bool success = artDataService.ImportBitmapToArt(
                targetEntry,
                imagePath,
                options,
                out string message);

            if (success)
            {
                imported++;
            }
            else
            {
                failed++;
                lastError = message;
            }
        }

        if (imagePaths.Length > targetSlots.Count)
        {
            skipped += imagePaths.Length - targetSlots.Count;
        }

        RebuildArtEntries();

        ArtStatusText =
            "Mass import complete using " + modeText +
            ". Imported " + imported +
            ", skipped " + skipped +
            ", failed " + failed +
            (string.IsNullOrWhiteSpace(lastError) ? "." : ". Last error: " + lastError);
    }

    private static bool TryParseArtImportFileName(string fileName, out bool isLand, out int artId)
    {
        isLand = false;
        artId = -1;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        string text = fileName.Trim();

        if (text.StartsWith("Land_", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("land-", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("land ", StringComparison.OrdinalIgnoreCase))
        {
            isLand = true;
            text = text.Substring(5).TrimStart('_', '-', ' ');
        }
        else if (text.StartsWith("Static_", StringComparison.OrdinalIgnoreCase) ||
                 text.StartsWith("static-", StringComparison.OrdinalIgnoreCase) ||
                 text.StartsWith("static ", StringComparison.OrdinalIgnoreCase) ||
                 text.StartsWith("Item_", StringComparison.OrdinalIgnoreCase) ||
                 text.StartsWith("item-", StringComparison.OrdinalIgnoreCase) ||
                 text.StartsWith("item ", StringComparison.OrdinalIgnoreCase))
        {
            isLand = false;
            text = text.Substring(text.IndexOfAny(new[] { '_', '-', ' ' }) + 1).Trim();
        }
        else
        {
            isLand = false;
        }

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(
                text.Substring(2),
                System.Globalization.NumberStyles.HexNumber,
                null,
                out artId);
        }

        return int.TryParse(text, out artId);
    }

    [RelayCommand]
    private void SaveArtChanges()
    {
        bool artSuccess = artDataService.SavePendingArtChanges(out string artMessage);

        List<TileDataEntry> editedTileDataEntries = TileDataEntries
            .Where(entry => entry.IsEdited)
            .ToList();

        bool tileDataSuccess = true;
        string tileDataMessage = string.Empty;

        if (editedTileDataEntries.Count > 0)
        {
            tileDataSuccess = tileDataMulService.SaveTileData(
                GetCurrentFolderPath(),
                TileDataEntries.ToList(),
                out tileDataMessage);

            if (tileDataSuccess)
            {
                foreach (TileDataEntry entry in editedTileDataEntries)
                {
                    entry.IsEdited = false;
                }
            }
        }

        ArtStatusText = string.IsNullOrWhiteSpace(tileDataMessage)
            ? artMessage
            : artMessage + " " + tileDataMessage;

        if (artSuccess && tileDataSuccess)
        {
            RebuildArtEntries();
        }
    }

    private void LoadArtEraFilters()
    {
        ArtEraFilters.Clear();

        string path = Path.Combine(AppContext.BaseDirectory, "art_era_filters.json");

        if (!File.Exists(path))
        {
            SelectedArtEraFilter = null;
            return;
        }

        try
        {
            string json = File.ReadAllText(path);

            ArtEraFilterConfig? config = JsonSerializer.Deserialize<ArtEraFilterConfig>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (config == null)
            {
                SelectedArtEraFilter = null;
                return;
            }

            foreach (ArtEraFilter era in config.Eras.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                era.FilterType = "Era";
                ArtEraFilters.Add(era);
            }

            foreach (ArtEraFilter category in config.Categories.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                category.FilterType = "Category";
                ArtEraFilters.Add(category);
            }

            SelectedArtEraFilter = null;
        }
        catch
        {
            SelectedArtEraFilter = null;
        }
    }

    private static bool IsArtInEraFilter(ArtEntry entry, ArtEraFilter? era)
    {
        if (era == null)
        {
            return true;
        }

        List<ArtEraRange> ranges = string.Equals(entry.Type, "Land", StringComparison.OrdinalIgnoreCase)
            ? era.LandRanges
            : era.StaticRanges;

        foreach (ArtEraRange range in ranges)
        {
            if (!TryParseArtNumber(range.From, out int from))
            {
                continue;
            }

            if (!TryParseArtNumber(range.To, out int to))
            {
                continue;
            }

            if (entry.ArtId >= from && entry.ArtId <= to)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseArtNumber(string text, out int value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(
                text[2..],
                System.Globalization.NumberStyles.HexNumber,
                null,
                out value);
        }

        return int.TryParse(text, out value);
    }

    [RelayCommand]
    private void OpenArtAnimDataEditor()
    {
        if (SelectedArtEntry == null || !string.Equals(SelectedArtEntry.Type, "Static", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (SelectedArtAnimDataEntry == null)
        {
            AnimDataEntry newEntry = new()
            {
                Id = SelectedArtEntry.ArtId,
                FrameCount = 0,
                FrameInterval = 1,
                FrameStart = 0,
                ArtExists = true,
                HasAnimationTileFlag = true,
                IsChecked = true
            };

            AnimDataEntries.Add(newEntry);
            animDataMulService.AddOrReplaceEntry(newEntry);
            SelectedAnimDataEntry = newEntry;
        }

        RebuildSelectedArtAnimDataFrames();

        Window? mainWindow = GetMainWindow();

        AnimDataEditorWindow window = new()
        {
            DataContext = this
        };

        if (mainWindow != null)
        {
            window.ShowDialog(mainWindow);
        }
        else
        {
            window.Show();
        }
    }

    public void RefreshArtAnimDataFromEditor()
    {
        AnimDataEntry? entry = SelectedArtAnimDataEntry;
        if (entry == null)
        {
            return;
        }

        entry.IsChecked = true;
        animDataMulService.AddOrReplaceEntry(entry);

        RebuildSelectedArtAnimDataFrames();

        OnPropertyChanged(nameof(SelectedArtAnimDataEntry));
        OnPropertyChanged(nameof(SelectedArtAnimDataStatusText));
        OnPropertyChanged(nameof(ArtAnimDataEditButtonText));
        OnPropertyChanged(nameof(SelectedArtAnimDataFrames));
    }

    [RelayCommand]
    private void AddEditAnimDataFrame()
    {
        AnimDataEntry? entry = SelectedArtAnimDataEntry;
        if (entry == null)
        {
            return;
        }

        if (entry.FrameCount >= 64)
        {
            ArtStatusText = "AnimData already has 64 frames.";
            return;
        }

        if (!TryParseAnimDataNumber(EditAnimDataAddFrameGraphicText, out int value))
        {
            ArtStatusText = "Enter a valid graphic ID or offset.";
            return;
        }

        int offset = EditAnimDataAddFrameRelative
            ? value
            : value - entry.Id;

        if (offset < sbyte.MinValue || offset > sbyte.MaxValue)
        {
            ArtStatusText = "Frame offset must be between -128 and 127.";
            return;
        }

        int newIndex = entry.FrameCount;
        entry.FrameOffsets[newIndex] = (sbyte)offset;
        entry.FrameCount++;
        entry.IsChecked = true;

        RebuildSelectedArtAnimDataFrames();
        SelectedEditAnimDataFrame = entry.Frames.Count > newIndex ? entry.Frames[newIndex] : entry.Frames.LastOrDefault();

        ArtStatusText = "Added AnimData frame.";
    }

    [RelayCommand]
    private void RemoveEditAnimDataFrame()
    {
        AnimDataEntry? entry = SelectedArtAnimDataEntry;
        if (entry == null || SelectedEditAnimDataFrame == null)
        {
            return;
        }

        int removeIndex = SelectedEditAnimDataFrame.FrameIndex;
        if (removeIndex < 0 || removeIndex >= entry.FrameCount)
        {
            return;
        }

        for (int i = removeIndex; i < entry.FrameCount - 1; i++)
        {
            entry.FrameOffsets[i] = entry.FrameOffsets[i + 1];
        }

        entry.FrameOffsets[entry.FrameCount - 1] = 0;
        entry.FrameCount--;
        entry.IsChecked = true;

        RebuildSelectedArtAnimDataFrames();

        if (entry.Frames.Count > 0)
        {
            SelectedEditAnimDataFrame = entry.Frames[Math.Min(removeIndex, entry.Frames.Count - 1)];
        }
        else
        {
            SelectedEditAnimDataFrame = null;
        }

        ArtStatusText = "Removed AnimData frame.";
    }

    [RelayCommand]
    private void MoveEditAnimDataFrameUp()
    {
        MoveEditAnimDataFrame(-1);
    }

    [RelayCommand]
    private void MoveEditAnimDataFrameDown()
    {
        MoveEditAnimDataFrame(1);
    }

    private void MoveEditAnimDataFrame(int direction)
    {
        AnimDataEntry? entry = SelectedArtAnimDataEntry;
        if (entry == null || SelectedEditAnimDataFrame == null)
        {
            return;
        }

        int index = SelectedEditAnimDataFrame.FrameIndex;
        int targetIndex = index + direction;

        if (index < 0 || targetIndex < 0 || targetIndex >= entry.FrameCount)
        {
            return;
        }

        (entry.FrameOffsets[index], entry.FrameOffsets[targetIndex]) =
            (entry.FrameOffsets[targetIndex], entry.FrameOffsets[index]);

        entry.IsChecked = true;

        RebuildSelectedArtAnimDataFrames();
        SelectedEditAnimDataFrame = entry.Frames[targetIndex];

        ArtStatusText = "Moved AnimData frame.";
    }

    [RelayCommand]
    private void RemoveCheckedArt()
    {
        List<ArtEntry> checkedEntries = ArtEntries
            .Where(entry => entry.IsChecked)
            .ToList();

        if (checkedEntries.Count == 0)
        {
            ArtStatusText = "Check one or more art entries to remove.";
            return;
        }

        bool success = artDataService.QueueRemoveArtEntries(checkedEntries, out string message);
        ArtStatusText = message;

        if (success)
        {
            RebuildArtEntries();
        }
    }

    [RelayCommand]
    private void UndoArtChange()
    {
        bool success = artDataService.UndoPendingArtChange(out string message);
        ArtStatusText = message;

        if (success)
        {
            RebuildArtEntries();
        }
    }

    [RelayCommand]
    private void ResetArtEdit()
    {
        if (originalArtEditBitmap == null)
        {
            return;
        }

        SelectedArtBitmap = CloneBitmap(originalArtEditBitmap);
        ArtStatusText = "Reset art edit preview.";
    }

    [RelayCommand]
    private void ApplyArtEnhancement()
    {
        if (SelectedArtBitmap == null)
        {
            ArtStatusText = "No art selected.";
            return;
        }

        SelectedArtBitmap = EnhanceBitmap(
            SelectedArtBitmap,
            ArtBrightness,
            ArtContrast,
            ArtSharpness,
            SelectedArtSharpenMode);

        ArtStatusText = "Applied art enhancement preview.";
    }

    [RelayCommand]
    private async Task LoadArtOverlayImageAsync()
    {
        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            ArtStatusText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Choose Art Overlay Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                new FilePickerFileType("Image files")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" }
                }
                }
            });

        if (files.Count == 0)
        {
            ArtStatusText = "Load overlay cancelled.";
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ArtStatusText = "Selected overlay image is invalid.";
            return;
        }

        artOverlayBitmap = LoadBitmapFromImageFile(path);
        ArtStatusText = "Loaded art overlay image.";
    }

    [RelayCommand]
    private void ApplyArtOverlay()
    {
        if (SelectedArtBitmap == null)
        {
            ArtStatusText = "No art selected.";
            return;
        }

        if (artOverlayBitmap == null)
        {
            ArtStatusText = "Load an overlay image first.";
            return;
        }

        SelectedArtBitmap = BlendOverlayIntoBitmap(
            SelectedArtBitmap,
            artOverlayBitmap,
            ArtOverlayOpacity / 100.0,
            SelectedArtOverlayBlendMode);

        ArtStatusText = "Applied overlay to art preview.";
    }

    [RelayCommand]
    private void QueueSelectedArtEdit()
    {
        if (SelectedArtEntry == null || SelectedArtBitmap == null)
        {
            ArtStatusText = "No art selected.";
            return;
        }

        bool success = artDataService.QueueBitmapToArt(SelectedArtEntry, SelectedArtBitmap, out string message);
        ArtStatusText = message;

        if (success)
        {
            originalArtEditBitmap = CloneBitmap(SelectedArtBitmap);
        }
    }

    private static WriteableBitmap LoadBitmapFromImageFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Bitmap bitmap = new Bitmap(stream);

        WriteableBitmap output = new WriteableBitmap(
            bitmap.PixelSize,
            new Avalonia.Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = output.Lock();
        bitmap.CopyPixels(framebuffer, AlphaFormat.Premul);

        return output;
    }

    private static WriteableBitmap BlendOverlayIntoBitmap(
    WriteableBitmap baseBitmap,
    WriteableBitmap overlayBitmap,
    double opacity,
    string blendMode)
    {
        FramePixels basePixels = ReadBitmapPixels(baseBitmap);
        FramePixels overlayPixels = ReadBitmapPixels(overlayBitmap);

        byte[] output = (byte[])basePixels.Pixels.Clone();

        opacity = Math.Clamp(opacity, 0.0, 1.0);

        for (int y = 0; y < basePixels.Height; y++)
        {
            for (int x = 0; x < basePixels.Width; x++)
            {
                int dst = ((y * basePixels.Width) + x) * 4;

                byte baseA = output[dst + 3];
                if (baseA == 0)
                {
                    continue;
                }

                int ox = x * overlayPixels.Width / basePixels.Width;
                int oy = y * overlayPixels.Height / basePixels.Height;
                int src = ((oy * overlayPixels.Width) + ox) * 4;

                double overlayAlpha = (overlayPixels.Pixels[src + 3] / 255.0) * opacity;
                if (overlayAlpha <= 0)
                {
                    continue;
                }

                byte b = BlendChannel(output[dst + 0], overlayPixels.Pixels[src + 0], overlayAlpha, blendMode);
                byte g = BlendChannel(output[dst + 1], overlayPixels.Pixels[src + 1], overlayAlpha, blendMode);
                byte r = BlendChannel(output[dst + 2], overlayPixels.Pixels[src + 2], overlayAlpha, blendMode);

                output[dst + 0] = b;
                output[dst + 1] = g;
                output[dst + 2] = r;
            }
        }

        return CreateBitmapFromPixels(basePixels.Width, basePixels.Height, output);
    }

    private static byte BlendChannel(byte baseValue, byte overlayValue, double alpha, string blendMode)
    {
        double b = baseValue / 255.0;
        double o = overlayValue / 255.0;
        double mixed;

        switch (blendMode)
        {
            case "Multiply":
                mixed = b * o;
                break;

            case "Screen":
                mixed = 1.0 - ((1.0 - b) * (1.0 - o));
                break;

            case "Overlay":
                mixed = b < 0.5
                    ? 2.0 * b * o
                    : 1.0 - (2.0 * (1.0 - b) * (1.0 - o));
                break;

            case "SoftLight":
                mixed = (1.0 - 2.0 * o) * b * b + (2.0 * o * b);
                break;

            default:
                mixed = o;
                break;
        }

        double final = b + ((mixed - b) * alpha);
        return (byte)Math.Clamp(final * 255.0, 0.0, 255.0);
    }

    private static FramePixels ReadBitmapPixels(WriteableBitmap bitmap)
    {
        using ILockedFramebuffer framebuffer = bitmap.Lock();

        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int srcRowBytes = framebuffer.RowBytes;
        int dstRowBytes = width * 4;

        byte[] src = new byte[srcRowBytes * height];
        Marshal.Copy(framebuffer.Address, src, 0, src.Length);

        byte[] pixels = new byte[dstRowBytes * height];

        for (int y = 0; y < height; y++)
        {
            Buffer.BlockCopy(src, y * srcRowBytes, pixels, y * dstRowBytes, dstRowBytes);
        }

        return new FramePixels(width, height, pixels);
    }

    private static WriteableBitmap CreateBitmapFromPixels(int width, int height, byte[] pixels)
    {
        WriteableBitmap bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);

        return bitmap;
    }

    private static WriteableBitmap EnhanceBitmap(
        WriteableBitmap source,
        double brightness,
        double contrast,
        double sharpness,
        string sharpenMode)
    {
        FramePixels frame = ReadBitmapPixels(source);
        byte[] output = (byte[])frame.Pixels.Clone();

        double brightnessOffset = brightness * 2.55;
        double contrastFactor = (259.0 * (contrast + 255.0)) / (255.0 * (259.0 - contrast));

        for (int i = 0; i < output.Length; i += 4)
        {
            if (output[i + 3] == 0)
            {
                continue;
            }

            output[i + 0] = AdjustColor(output[i + 0], brightnessOffset, contrastFactor);
            output[i + 1] = AdjustColor(output[i + 1], brightnessOffset, contrastFactor);
            output[i + 2] = AdjustColor(output[i + 2], brightnessOffset, contrastFactor);
        }

        if (sharpness > 0)
        {
            output = sharpenMode == "Pixel"
    ? PixelSharpenPixels(frame.Width, frame.Height, output, sharpness / 100.0)
    : GaussianSharpenPixels(frame.Width, frame.Height, output, sharpness / 100.0);
        }

        return CreateBitmapFromPixels(frame.Width, frame.Height, output);
    }

    private static byte AdjustColor(byte value, double brightnessOffset, double contrastFactor)
    {
        double adjusted = contrastFactor * (value - 128.0) + 128.0 + brightnessOffset;
        return (byte)Math.Clamp(adjusted, 0.0, 255.0);
    }

    private static byte[] GaussianSharpenPixels(int width, int height, byte[] source, double amount)
    {
        byte[] output = (byte[])source.Clone();

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int index = ((y * width) + x) * 4;

                if (source[index + 3] == 0)
                {
                    continue;
                }

                for (int c = 0; c < 3; c++)
                {
                    int center = source[index + c] * 5;
                    int left = source[(((y * width) + (x - 1)) * 4) + c];
                    int right = source[(((y * width) + (x + 1)) * 4) + c];
                    int up = source[((((y - 1) * width) + x) * 4) + c];
                    int down = source[((((y + 1) * width) + x) * 4) + c];

                    double sharpened = center - left - right - up - down;
                    double blended = source[index + c] + ((sharpened - source[index + c]) * amount);

                    output[index + c] = (byte)Math.Clamp(blended, 0.0, 255.0);
                }
            }
        }

        return output;
    }

    private static byte[] PixelSharpenPixels(
    int width,
    int height,
    byte[] source,
    double amount)
    {
        byte[] output = (byte[])source.Clone();

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int index = ((y * width) + x) * 4;

                if (source[index + 3] == 0)
                {
                    continue;
                }

                for (int c = 0; c < 3; c++)
                {
                    int current = source[index + c];

                    int maxNeighbor = current;

                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            if (ox == 0 && oy == 0)
                            {
                                continue;
                            }

                            int neighborIndex =
                                ((((y + oy) * width) + (x + ox)) * 4) + c;

                            int value = source[neighborIndex];

                            if (value > maxNeighbor)
                            {
                                maxNeighbor = value;
                            }
                        }
                    }

                    double boosted =
                        current + ((maxNeighbor - current) * amount);

                    output[index + c] =
                        (byte)Math.Clamp(boosted, 0, 255);
                }
            }
        }

        return output;
    }

    public void QueueAdjustedArtImport()
    {
        if (SelectedArtEntry == null)
        {
            ArtStatusText = "No art entry selected.";
            return;
        }

        if (string.IsNullOrWhiteSpace(PendingArtImportImagePath))
        {
            ArtStatusText = "No import image selected.";
            return;
        }

        ArtImportAdjustOptions options = new()
        {
            AutoTrim = ArtImportAutoTrim,
            CenterOnCanvas = ArtImportCenterOnCanvas,
            CanvasWidth = ArtImportCanvasWidth,
            CanvasHeight = ArtImportCanvasHeight,
            OffsetX = ArtImportOffsetX,
            OffsetY = ArtImportOffsetY
        };

        bool success = artDataService.ImportBitmapToArt(
            SelectedArtEntry,
            PendingArtImportImagePath,
            options,
            out string message);

        ArtStatusText = message;

        if (!success)
        {
            return;
        }

        SelectedArtBitmap = artDataService.LoadBitmap(SelectedArtEntry);
        SelectedArtEntry.Thumbnail = artDataService.LoadThumbnail(SelectedArtEntry);

        RebuildArtEntries();
    }

    [RelayCommand]
    private void RefreshArtImportAdjustPreview()
    {
        if (string.IsNullOrWhiteSpace(PendingArtImportImagePath))
        {
            ArtImportAdjustStatusText = "No import image selected.";
            return;
        }

        ArtImportAdjustOptions options = new()
        {
            AutoTrim = ArtImportAutoTrim,
            CenterOnCanvas = ArtImportCenterOnCanvas,
            CanvasWidth = ArtImportCanvasWidth,
            CanvasHeight = ArtImportCanvasHeight,
            OffsetX = ArtImportOffsetX,
            OffsetY = ArtImportOffsetY
        };

        ArtImportAdjustPreviewBitmap =
            artDataService.BuildAdjustedImportPreview(PendingArtImportImagePath, options, out string message);

        ArtImportAdjustStatusText = message;
    }

    partial void OnArtImportAutoTrimChanged(bool value)
    {
        RefreshArtImportAdjustPreview();
    }

    partial void OnArtImportCenterOnCanvasChanged(bool value)
    {
        RefreshArtImportAdjustPreview();
    }

    partial void OnArtImportCanvasWidthChanged(int value)
    {
        RefreshArtImportAdjustPreview();
    }

    partial void OnArtImportCanvasHeightChanged(int value)
    {
        RefreshArtImportAdjustPreview();
    }

    partial void OnArtImportOffsetXChanged(int value)
    {
        RefreshArtImportAdjustPreview();
    }

    partial void OnArtImportOffsetYChanged(int value)
    {
        RefreshArtImportAdjustPreview();
    }

    [RelayCommand]
    private async Task OpenArtCutterAsync()
    {
        if (SelectedArtEntry != null)
        {
            ArtCutterStartId = SelectedArtEntry.ArtId;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            ArtStatusText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Choose Large Art Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                new FilePickerFileType("Image files")
                {
                    Patterns = new[] { "*.png", "*.bmp" }
                }
                }
            });

        if (files.Count == 0)
        {
            ArtStatusText = "Art cutter cancelled.";
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            ArtStatusText = "Selected cutter image path is invalid.";
            return;
        }

        ArtCutterImagePath = path;
        ArtCutterSlices.Clear();

        ArtCutterWindow window = new()
        {
            DataContext = this
        };

        await window.ShowDialog(mainWindow);
    }

    [RelayCommand]
    private void BuildArtCutterSlices()
    {
        ArtCutterSlices.Clear();

        List<ArtCutterSliceEntry> slices = artDataService.BuildStaticArtSlices(
            ArtCutterImagePath,
            ArtCutterStartId,
            ArtCutterSliceWidth,
            ArtCutterSliceHeight,
            ArtCutterSourceOffsetX,
            ArtCutterSourceOffsetY,
            ArtCutterAutoTrim,
            ArtCutterSkipEmpty,
            ArtCutterBlackTransparent,
            out string message);

        foreach (ArtCutterSliceEntry slice in slices)
        {
            ArtCutterSlices.Add(slice);
        }

        ArtStatusText = message;
    }

    public void QueueCheckedArtCutterSlices()
    {
        bool success = artDataService.QueueStaticArtSlices(
            ArtCutterSlices,
            out string message);

        ArtStatusText = message;

        if (success)
        {
            RebuildArtEntries();
        }
    }

    private void RefreshSelectedArtRadarColInfo()
    {
        if (SelectedArtEntry == null || !radarColService.IsLoaded)
        {
            SelectedArtCurrentRadarColor = Colors.Transparent;
            SelectedArtRadarColorPending = false;
            OnPropertyChanged(nameof(SelectedArtCurrentRadarColorText));
            return;
        }

        SelectedArtCurrentRadarColor = radarColService.GetAvaloniaColor(SelectedArtEntry);
        SelectedArtRadarColorPending = radarColService.HasPendingChange(SelectedArtEntry);

        OnPropertyChanged(nameof(SelectedArtCurrentRadarBrush));
        OnPropertyChanged(nameof(SelectedArtCurrentRadarColorText));
        OnPropertyChanged(nameof(SelectedArtRadarMatchText));
    }

    [RelayCommand]
    private void ApplyGeneratedRadarColorToSelectedArt()
    {
        if (SelectedArtEntry == null)
        {
            ArtStatusText = "No art selected.";
            return;
        }

        ushort color = artRadarColorService.ConvertColorToUoColor(SelectedArtRadarColor);

        bool success = radarColService.SetColor(
            SelectedArtEntry,
            color,
            out string message);

        ArtStatusText = message;

        if (success)
        {
            RefreshSelectedArtRadarColInfo();
        }
    }

    [RelayCommand]
    private void RevertSelectedRadarColor()
    {
        bool success = radarColService.RevertSelected(SelectedArtEntry, out string message);
        ArtStatusText = message;

        if (success)
        {
            RefreshSelectedArtRadarColInfo();
        }
    }

    public string SelectedArtRadarMatchText
    {
        get
        {
            if (!radarColService.IsLoaded || SelectedArtEntry == null)
            {
                return "Radar match: radarcol.mul not loaded";
            }

            int dr = SelectedArtRadarColor.R - SelectedArtCurrentRadarColor.R;
            int dg = SelectedArtRadarColor.G - SelectedArtCurrentRadarColor.G;
            int db = SelectedArtRadarColor.B - SelectedArtCurrentRadarColor.B;

            int distance = Math.Abs(dr) + Math.Abs(dg) + Math.Abs(db);

            if (distance == 0)
            {
                return "Radar match: Exact";
            }

            if (distance <= 12)
            {
                return "Radar match: Close | ΔR " + dr + ", ΔG " + dg + ", ΔB " + db;
            }

            return "Radar match: Different | ΔR " + dr + ", ΔG " + dg + ", ΔB " + db;
        }
    }

    [RelayCommand]
    private void ApplyGeneratedRadarColorToCheckedArt()
    {
        List<ArtEntry> checkedEntries = ArtEntries
            .Where(entry => entry.IsChecked)
            .ToList();

        if (checkedEntries.Count == 0)
        {
            ArtStatusText = "No checked art entries.";
            return;
        }

        int applied = 0;
        int failed = 0;
        string lastError = string.Empty;

        foreach (ArtEntry entry in checkedEntries)
        {
            WriteableBitmap? bitmap = artDataService.LoadBitmap(entry);
            Color generatedColor = artRadarColorService.GetAverageVisibleColor(bitmap);
            ushort uoColor = artRadarColorService.ConvertColorToUoColor(generatedColor);

            bool success = radarColService.SetColor(
                entry,
                uoColor,
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

        RefreshSelectedArtRadarColInfo();

        ArtStatusText =
            "Applied generated radar colors to checked art. Applied " +
            applied +
            ", failed " +
            failed +
            (string.IsNullOrWhiteSpace(lastError) ? "." : ". Last error: " + lastError);
    }
}
