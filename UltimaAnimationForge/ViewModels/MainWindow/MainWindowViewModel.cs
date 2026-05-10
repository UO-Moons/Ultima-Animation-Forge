using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;
using UltimaAnimationForge.Views;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService settingsService;
    private readonly AppSettings appSettings;
    private Window? detachedPreviewWindow;
    private Window? detachedDebugWindow;
    private DetachedPreviewViewModel? detachedPreviewViewModel;
    private readonly Dictionary<string, WriteableBitmap?> animationBrowserThumbnailCache = new();
    private bool syncingAnimationBrowserSelection;
    private CancellationTokenSource? animationBrowserThumbnailCancellation;
    public ICommand ClearSelectedMulSlotCommand { get; }

    [ObservableProperty]
    private string animationBrowserSortMode = "Body ID Asc";

    public ObservableCollection<string> AnimationBrowserSortModeOptions { get; } = new()
{
    "Body ID Asc",
    "Body ID Desc",
    "Name",
    "Source File"
};

    [ObservableProperty]
    private bool animationBrowserShowMissingSlots = false;

    [ObservableProperty]
    private string animationBrowserSearchText = string.Empty;

    [ObservableProperty]
    private string animationBrowserSourceFilter = "All";

    [ObservableProperty]
    private string animationBrowserTypeFilter = "All";

    public ObservableCollection<string> AnimationBrowserSourceFilters { get; } = new();

    public ObservableCollection<string> AnimationBrowserTypeFilters { get; } = new();

    [ObservableProperty]
    private string animationBrowserTileSize = "Small";

    [ObservableProperty]
    private string animationBrowserCountText = "Showing 0";

    public ObservableCollection<string> AnimationBrowserTileSizeOptions { get; } = new()
    {
        "Small",
        "Medium",
        "Large"
    };

    private readonly Dictionary<string, List<WriteableBitmap>> animationBrowserHoverFrameCache = new();
    private CancellationTokenSource? animationBrowserHoverCancellation;
    private AnimationBrowserTileViewModel? activeHoverTile;

    [ObservableProperty]
    private bool animationBrowserHoverPreviewEnabled = true;

    [ObservableProperty]
    private string animationBrowserHoverSpeed = "100 ms";

    public ObservableCollection<string> AnimationBrowserHoverSpeedOptions { get; } = new()
{
    "50 ms",
    "100 ms",
    "150 ms",
    "200 ms"
};

    public int AnimationBrowserHoverFrameDelayMs =>
        AnimationBrowserHoverSpeed switch
        {
            "50 ms" => 50,
            "150 ms" => 150,
            "200 ms" => 200,
            _ => 100
        };

    public sealed class AnimationNameEntry
    {
        public int BodyId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private readonly Dictionary<int, string> animationBrowserNamesByBodyId = new();

    [ObservableProperty]
    private bool animationBrowserShowNames = true;

    public ICommand RefreshAnimationBrowserThumbnailsCommand { get; }

    public IRelayCommand OpenDiscordCommand => new RelayCommand(OpenDiscord);

    public ICommand OpenAnimationBrowserTileCommand { get; }
    public ICommand RefreshAnimationBrowserTileThumbnailCommand { get; }
    public ICommand OpenAnimationBrowserTileDetachedPreviewCommand { get; }
    public ICommand ExportAnimationBrowserTileFramesCommand { get; }
    public ICommand ExportAnimationBrowserTileVdCommand { get; }
    public ICommand DeleteAnimationBrowserTileCommand { get; }

    private void OpenDiscord()
    {
        string url = "https://discord.gg/uBAXxhF";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to open Discord link: " + ex.Message);
        }
    }

    internal IReadOnlyList<AnimationEntry> GetAnimationEntriesSnapshot()
    {
        return AnimationEntries
            .Select(entry => new AnimationEntry
            {
                DisplayName = entry.DisplayName,
                SecondaryText = entry.SecondaryText,
                BodyId = entry.BodyId,
                ActionId = entry.ActionId,
                FrameCount = entry.FrameCount,
                FrameSize = entry.FrameSize,
                SourceFile = entry.SourceFile,
                SourceMode = entry.SourceMode,
                IndexNumber = entry.IndexNumber,
                Offset = entry.Offset,
                Length = entry.Length,
                Extra = entry.Extra
            })
            .ToList();
    }

    private WriteableBitmap? loadedPropOverlayBitmap;
    private string loadedPropOverlayPath = string.Empty;

    private readonly List<WriteableBitmap> compareOverlayFrames = new();
    private string compareOverlayCacheKey = string.Empty;

    public ObservableCollection<AnimationBrowserTileViewModel> AnimationBrowserTiles { get; } = new();

    [ObservableProperty]
    private bool compareOverlayEnabled = false;

    [ObservableProperty]
    private AnimationEntry? compareSelectedAnimation;

    [ObservableProperty]
    private int compareOverlayActionIndex = 0;

    [ObservableProperty]
    private int compareOverlayDirectionIndex = 0;

    [ObservableProperty]
    private int compareOverlayFrameIndex = 0;

    [ObservableProperty]
    private int compareOverlayOffsetX = 0;

    [ObservableProperty]
    private int compareOverlayOffsetY = 0;

    [ObservableProperty]
    private double compareOverlayOpacityPercent = 50.0;

    private bool compareOverlayDragActive;
    private Avalonia.Point compareOverlayDragStartPoint;
    private int compareOverlayDragStartOffsetX;
    private int compareOverlayDragStartOffsetY;

    public string CompareOverlayOffsetXText
    {
        get => CompareOverlayOffsetX.ToString();

        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (int.TryParse(value, out int result))
            {
                CompareOverlayOffsetX = result;
            }
        }
    }

    public string CompareOverlayOffsetYText
    {
        get => CompareOverlayOffsetY.ToString();

        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (int.TryParse(value, out int result))
            {
                CompareOverlayOffsetY = result;
            }
        }
    }

    public string CompareOverlayOpacityPercentText
    {
        get => CompareOverlayOpacityPercent.ToString("0.##");

        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (double.TryParse(value, out double result))
            {
                CompareOverlayOpacityPercent = Math.Clamp(result, 0, 100);
            }
        }
    }

    [ObservableProperty]
    private bool compareOverlayDragModeEnabled = false;

    public ICommand ToggleCompareOverlayDragModeCommand { get; }
    public ICommand ClearCompareOverlayCommand { get; }
    public ICommand ApplyCompareOverlayToCurrentFrameCommand { get; }
    public ICommand ApplyCompareOverlayToCurrentDirectionCommand { get; }
    public ICommand SetupMountRiderAlignmentCommand { get; }
    public ICommand OpenMythicPackageViewerCommand { get; }
    public ICommand ShowAnimationEditorCommand { get; }
    public ICommand ShowAnimationBrowserCommand { get; }

    private readonly Dictionary<string, int> compareActionNameToIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> compareDirectionNameToIndex = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<string> CompareAnimationFileOptions { get; } = new();
    public ObservableCollection<AnimationEntry> CompareAnimationEntries { get; } = new();
    public ObservableCollection<string> CompareActionOptions { get; } = new();
    public ObservableCollection<string> CompareDirectionOptions { get; } = new();

    [ObservableProperty]
    private string? compareSelectedAnimationFile = "All Files";

    [ObservableProperty]
    private string? compareSelectedAction = null;

    [ObservableProperty]
    private string? compareSelectedDirection = null;

    public ObservableCollection<string> CompareOverlaySyncModeOptions { get; } = new()
    {
        "Same Frame",
        "Loop Secondary",
        "Clamp Secondary",
        "Manual Frame"
    };


    [ObservableProperty]
    private bool animationBrowserVisible = false;

    public bool ShowAnimationEditorPanel => !AnimationBrowserVisible;

    [ObservableProperty]
    private AnimationBrowserTileViewModel? selectedAnimationBrowserTile;

    [ObservableProperty]
    private string compareOverlaySyncMode = "Same Frame";

    partial void OnSelectedAnimationBrowserTileChanged(AnimationBrowserTileViewModel? value)
    {
        if (syncingAnimationBrowserSelection)
        {
            return;
        }

        SelectAnimationFromBrowserTile(value);
    }

    [ObservableProperty]
    private bool compareSideBySideEnabled = false;

    [ObservableProperty]
    private WriteableBitmap? compareSidePreviewBitmap;

    private sealed class CompareFramePose
    {
        public int OffsetX { get; init; }
        public int OffsetY { get; init; }
    }

    private readonly Dictionary<int, CompareFramePose> compareFramePoses = new();

    public ICommand SaveComparePoseForCurrentFrameCommand { get; }
    public ICommand CopyComparePoseFromPreviousFrameCommand { get; }
    public ICommand ClearComparePoseForCurrentFrameCommand { get; }

    [ObservableProperty]
    private bool propOverlayEnabled = false;

    [ObservableProperty]
    private string propOverlayFileName = "None";

    [ObservableProperty]
    private int propOverlayOffsetX = 0;

    [ObservableProperty]
    private int propOverlayOffsetY = 0;

    [ObservableProperty]
    private double propOverlayScalePercent = 100.0;

    [ObservableProperty]
    private double propOverlayRotationDegrees = 0.0;

    [ObservableProperty]
    private int propOverlayDrawOrderIndex = 1; // 0 = Behind, 1 = Front

    public ICommand LoadPropOverlayCommand { get; }
    public ICommand ClearPropOverlayCommand { get; }
    public ICommand ApplyPropOverlayToCurrentFrameCommand { get; }
    public ICommand ApplyPropOverlayToCurrentDirectionCommand { get; }

    [ObservableProperty]
    private bool propOverlayFlipHorizontal = false;

    [ObservableProperty]
    private bool propOverlayFlipVertical = false;

    private readonly Dictionary<int, PropFramePose> propFramePoses = new();

    [ObservableProperty]
    private int propOverlayPivotX = 0;

    [ObservableProperty]
    private int propOverlayPivotY = 0;

    public ICommand SavePropPoseForCurrentFrameCommand { get; }
    public ICommand CopyPropPoseFromPreviousFrameCommand { get; }
    public ICommand ClearPropPoseForCurrentFrameCommand { get; }

    private sealed class PropFramePose
    {
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public double RotationDegrees { get; set; }
        public bool FlipHorizontal { get; set; }
        public bool FlipVertical { get; set; }
    }

    [ObservableProperty]
    private double livePreviewScalePercent = 100.0;

    [ObservableProperty]
    private HueDataService.HueEntry? livePreviewSelectedHue;

    private readonly List<WriteableBitmap> decodedFrames = new();
    private readonly List<VdFrameData> editableFrames = new();
    private int currentFrameIndex = 0;
    private DispatcherTimer? playbackTimer;
    private bool hasFrameEdits = false;

    private readonly Stack<FrameEditSnapshot> undoFrameEditStack = new();

    private readonly Dictionary<string, int> actionNameToIndex = new Dictionary<string, int>();

    private readonly Dictionary<string, int> directionNameToIndex = new Dictionary<string, int>();
    private bool suppressDirectionReload = false;

    private readonly MobTypeAssignmentService mobTypeAssignmentService = new();

    private readonly List<AnimationEntry> allAnimationEntries = new();

    private readonly MulAnimationDataSource mulAnimationDataSource = new();
    private readonly UopAnimationDataSource uopAnimationDataSource = new();
    private IAnimationDataSource? activeAnimationDataSource;
    private ResolvedAnimationBlock? currentResolvedAnimationBlock;

    private readonly List<MulSlotEntry> allMulSlotEntries = new();

    [ObservableProperty]
    private bool showMulSlotView = false;

    [ObservableProperty]
    private bool mulSlotShowHFilter = false;

    [ObservableProperty]
    private bool mulSlotShowLFilter = false;

    [ObservableProperty]
    private bool mulSlotShowPFilter = false;

    private readonly HueDataService hueDataService = new();
    private List<HueDataService.HueEntry> cachedHueEntries = new();
    private string currentHueFilePath = string.Empty;

    [ObservableProperty]
    private MulSlotEntry? selectedMulSlot;

    public ObservableCollection<MulSlotEntry> MulSlotEntries { get; } = new();

    private readonly PendingMulImportSession pendingMulImportSession = new();

    private readonly AnimationCacheService animationCacheService = new();

    private readonly Dictionary<int, List<VdFrameData>> importedSpriteSheetDirections = new();

    private bool previewDragActive = false;
    private bool previewDragAffectsAllFrames = false;
    private Avalonia.Point previewDragStartPoint;
    private int previewDragLastAppliedDx = 0;
    private int previewDragLastAppliedDy = 0;
    private bool previewDragUndoSnapshotTaken = false;

    private void ClearImportedSpriteSheetSession()
    {
        hasImportedSpriteSheetSession = false;
        importedSpriteSheetLastActionIndex = 0;
        importedSpriteSheetDirectionCount = 5;
        importedSpriteSheetSourceName = string.Empty;
        importedSpriteSheetActions.Clear();
    }

    [ObservableProperty]
    private bool hasUnsavedChanges = false;

    [ObservableProperty]
    private ResizeSamplerMode livePreviewResizeSampler = ResizeSamplerMode.Auto;

    private readonly Random loadingTipRandom = new Random();

    private bool suppressProfileSelectionChanged = false;
    private bool isProfileLoadInProgress = false;

    private bool suppressSelectedThumbnailChanged = false;

    public ObservableCollection<AnimationFrameThumbnail> FrameThumbnails { get; } = new();

    [ObservableProperty]
    private AnimationFrameThumbnail? selectedFrameThumbnail;

    private static readonly string[] LoadingTips =
    {
    "Tip: Select a body, then change Action and Dir at the top to compare animations.",
    "Tip: MUL imports are now queued until you click Save Changes.",
    "Tip: Use the file filter to isolate a single anim.mul, anim2.mul, or UOP file.",
    "Tip: Some bodies have fewer valid actions than their full animation group count.",
    "Tip: Export Frames can export the current direction or every action and direction.",
    "Tip: UOP and MUL can expose different action layouts for the same body style.",
    "Tip: Empty MUL body slots are useful targets for staged VD imports.",
    "Tip: If an action is missing, it will now stay hidden instead of cluttering the list.",
    "Tip: The status panel on the right can help confirm which source file you are reading from.",
    "Tip: Unsaved changes stay in memory until you click Save Changes."
};

    [ObservableProperty]
    private string loadingTipText = string.Empty;

    [ObservableProperty]
    private bool previewDragModeEnabled = false;

    private sealed class FolderLoadResult
    {
        public bool MulReady { get; set; }
        public bool UopReady { get; set; }

        public List<AnimationEntry> MulEntries { get; set; } = new List<AnimationEntry>();
        public List<AnimationEntry> UopEntries { get; set; } = new List<AnimationEntry>();
        public List<MulSlotEntry> MulSlots { get; set; } = new List<MulSlotEntry>();
    }

    private readonly VdImportService vdImportService = new();
    private readonly BodyConvAssignmentService bodyConvAssignmentService = new();

    private readonly MulSlotDeleteService mulSlotDeleteService = new();
    private readonly LegacyMulIdxCreationService legacyMulIdxCreationService = new();
    private readonly MulEditedFrameSaveService mulEditedFrameSaveService = new();

    private const int MaxGifWidth = 512;
    private const int MaxGifHeight = 512;
    private const int MaxGifFrames = 120;

    private bool suppressActionReload = false;

    private readonly List<UopBodySlotEntry> allUopBodyEntries = new();

    [ObservableProperty]
    private UopBodySlotEntry? selectedUopBodySlot;

    public ObservableCollection<UopBodySlotEntry> UopBodyEntries { get; } = new();

    [ObservableProperty]
    private string statusText = "Ready. Open a UO folder to begin.";

    [ObservableProperty]
    private string selectedSourceText = "No source loaded";

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string? selectedBodyType = "All";

    [ObservableProperty]
    private string? selectedAction = "All";

    [ObservableProperty]
    private AnimationEntry? selectedAnimation;

    [ObservableProperty]
    private double zoomLevel = 1;

    [ObservableProperty]
    private double playbackSpeed = 8;

    [ObservableProperty]
    private bool showCheckerBackground = true;

    [ObservableProperty]
    private bool loopPlayback = true;

    [ObservableProperty]
    private string selectedBlockSizeText = "-";

    [ObservableProperty]
    private string selectedBlockHeaderText = "-";

    [ObservableProperty]
    private WriteableBitmap? previewBitmap;

    [ObservableProperty]
    private string? selectedDirection = "Direction 0";

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private double loadingProgress = 0;

    [ObservableProperty]
    private string loadingText = string.Empty;

    [ObservableProperty]
    private string? selectedAnimationFile = "All Files";

    [ObservableProperty]
    private bool livePreviewSharpenEnabled = false;

    [ObservableProperty]
    private bool livePreviewContrastEnabled = false;

    [ObservableProperty]
    private bool livePreviewOutlineEnabled = false;

    [ObservableProperty]
    private bool livePreviewHueEnabled = false;

    [ObservableProperty]
    private double livePreviewSharpenAmount = 1.0;

    [ObservableProperty]
    private double livePreviewContrastAmount = 0.20;

    [ObservableProperty]
    private double livePreviewOutlineStrength = 0.35;

    [ObservableProperty]
    private int livePreviewSharpenModeIndex = 0;

    private WriteableBitmap? previewSourceBitmapBeforeLiveEffects;
    private bool suppressLivePreviewRefresh = false;

    public ObservableCollection<string> AnimationFileOptions { get; } = new();

    public ObservableCollection<string> BodyTypeOptions { get; } = new();
    public ObservableCollection<string> ActionOptions { get; } = new();
    public ObservableCollection<AnimationEntry> AnimationEntries { get; } = new();
    public ObservableCollection<string> DirectionOptions { get; } = new();

    public ICommand ManageProfilesCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand ExportFramesCommand { get; }
    public ICommand ExportCurrentFrameCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand PreviousFrameCommand { get; }
    public ICommand NextFrameCommand { get; }
    public ICommand ReplaceSelectedFrameCommand { get; }
    public ICommand ImportPngSequenceCommand { get; }
    public ICommand RemoveSelectedFrameCommand { get; }
    public ICommand ReplaceFrameThumbnailCommand { get; }
    public ICommand RemoveFrameThumbnailCommand { get; }
    public ICommand UndoFrameEditCommand { get; }
    public ICommand SaveChangesCommand { get; }
    public ICommand HelpCommand { get; }
    public ICommand PopOutPreviewCommand { get; }
    public ICommand Direction0Command { get; }
    public ICommand Direction1Command { get; }
    public ICommand Direction2Command { get; }
    public ICommand Direction3Command { get; }
    public ICommand Direction4Command { get; }
    public ICommand ToggleCheckerCommand { get; }
    public ICommand ToggleLoopCommand { get; }
    public ICommand TogglePreviewDragModeCommand { get; }
    public ICommand ImportSpriteSheetCommand { get; }
    public ICommand ExportVdCommand { get; }
    public ICommand ImportVdCommand { get; }
    public ICommand DeleteAnimationCommand { get; }
    public ICommand ImportVdToUopCommand { get; }
    public ICommand CreateEmptyLegacyMulIdxCommand { get; }
    public ICommand ApplyCurrentDirectionEnhancementsCommand { get; }
    public ICommand ApplyFullAnimationEnhancementsCommand { get; }
    public ICommand ApplyLivePreviewToCurrentDirectionCommand { get; }
    public ICommand ApplyLivePreviewToFullAnimationCommand { get; }
    public ICommand ResetLivePreviewCommand { get; }
    public ICommand PopOutDebugCommand { get; }
    public ICommand ConfigureLivePreviewHueCommand { get; }
    public ICommand ConfigureLivePreviewScaleCommand { get; }
    public ICommand ApplyCurrentComparePoseToAllFramesCommand { get; }

    private readonly Dictionary<int, Dictionary<int, List<VdFrameData>>> importedSpriteSheetActions = new();

    private bool hasImportedSpriteSheetSession = false;
    private int importedSpriteSheetLastActionIndex = 0;
    private int importedSpriteSheetDirectionCount = 5;
    private string importedSpriteSheetSourceName = string.Empty;

    public bool CanUndoFrameEdit => undoFrameEditStack.Count > 0;

    private bool IsSelectedAnimationFileUop()
    {
        string normalized = NormalizeSelectedAnimationFile(SelectedAnimationFile);

        return !string.IsNullOrWhiteSpace(normalized) &&
               normalized.EndsWith(".uop", StringComparison.OrdinalIgnoreCase);
    }

    private string NormalizeSelectedAnimationFile(string? selectedFile)
    {
        if (string.IsNullOrWhiteSpace(selectedFile))
        {
            return "All Files";
        }

        string trimmed = selectedFile.Trim();

        if (string.Equals(trimmed, "All Files", StringComparison.OrdinalIgnoreCase))
        {
            return "All Files";
        }

        // 🔴 DO NOT TOUCH if already has extension
        if (trimmed.EndsWith(".uop", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".mul", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".idx", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.StartsWith("AnimationFrame", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed + ".uop";
        }

        if (trimmed.StartsWith("anim", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed + ".mul";
        }

        return trimmed;
    }

    private string NormalizeAnimationFileNameForCompare(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        string trimmed = fileName.Trim();

        // 🔴 CRITICAL: if already has extension, DO NOTHING
        if (trimmed.EndsWith(".uop", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".mul", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".idx", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        // UOP files
        if (trimmed.StartsWith("AnimationFrame", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed + ".uop";
        }

        // MUL files
        if (trimmed.StartsWith("anim", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed + ".mul";
        }

        return trimmed;
    }

    private void RefreshUnsavedChangesState()
    {
        HasUnsavedChanges = pendingMulImportSession.HasUnsavedChanges;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(HasAnyUnsavedChanges));
        OnPropertyChanged(nameof(CanSaveChanges));
        OnPropertyChanged(nameof(UnsavedChangesText));
        OnPropertyChanged(nameof(HasFrameEdits));
    }

    public MainWindowViewModel()
    {
        settingsService = new SettingsService();
        appSettings = settingsService.Load();

        OpenFolderCommand = new AsyncRelayCommand(OpenFolderAsync);

        ExportFramesCommand = new AsyncRelayCommand(ExportFramesAsync);
        ExportCurrentFrameCommand = new AsyncRelayCommand(ExportCurrentFrameAsync);

        PlayCommand = new RelayCommand(PlayPreview);
        PauseCommand = new RelayCommand(PausePreview);
        PreviousFrameCommand = new RelayCommand(PreviousFrame);
        NextFrameCommand = new RelayCommand(NextFrame);
        ExportVdCommand = new AsyncRelayCommand(ExportVdAsync);
        ReplaceSelectedFrameCommand = new AsyncRelayCommand(ReplaceSelectedFrameAsync);
        RemoveSelectedFrameCommand = new AsyncRelayCommand(RemoveSelectedFrameAsync);
        ReplaceFrameThumbnailCommand = new AsyncRelayCommand<object?>(ReplaceFrameThumbnailAsync);
        RemoveFrameThumbnailCommand = new AsyncRelayCommand<object?>(RemoveFrameThumbnailAsync);
        ImportPngSequenceCommand = new AsyncRelayCommand(ImportPngSequenceAsync);
        UndoFrameEditCommand = new RelayCommand(UndoFrameEdit, () => CanUndoFrameEdit);
        ImportVdCommand = new AsyncRelayCommand(ImportVdAsync);
        DeleteAnimationCommand = new AsyncRelayCommand(DeleteAnimationAsync);
        ImportVdToUopCommand = new AsyncRelayCommand(ImportVdToUopAsync);
        CreateEmptyLegacyMulIdxCommand = new AsyncRelayCommand(CreateEmptyLegacyMulIdxAsync);
        SaveChangesCommand = new AsyncRelayCommand(SavePendingChangesAsync);
        ManageProfilesCommand = new AsyncRelayCommand(ManageProfilesAsync);
        HelpCommand = new AsyncRelayCommand(ShowHelpAsync);
        PopOutPreviewCommand = new AsyncRelayCommand(ShowDetachedPreviewAsync);
        Direction0Command = new RelayCommand(() => SelectDirectionByIndex(0));
        Direction1Command = new RelayCommand(() => SelectDirectionByIndex(1));
        Direction2Command = new RelayCommand(() => SelectDirectionByIndex(2));
        Direction3Command = new RelayCommand(() => SelectDirectionByIndex(3));
        Direction4Command = new RelayCommand(() => SelectDirectionByIndex(4));
        ImportSpriteSheetCommand = new AsyncRelayCommand(ImportSpriteSheetAsync);
        ApplyCurrentDirectionEnhancementsCommand = new AsyncRelayCommand(ApplyCurrentDirectionEnhancementsAsync);
        ApplyFullAnimationEnhancementsCommand = new AsyncRelayCommand(ApplyFullAnimationEnhancementsAsync);
        ApplyLivePreviewToCurrentDirectionCommand = new AsyncRelayCommand(ApplyLivePreviewToCurrentDirectionAsync);
        ApplyLivePreviewToFullAnimationCommand = new AsyncRelayCommand(ApplyLivePreviewToFullAnimationAsync);
        ResetLivePreviewCommand = new RelayCommand(ResetLivePreviewSettings);
        PopOutDebugCommand = new AsyncRelayCommand(ShowDetachedDebugAsync);
        ConfigureLivePreviewHueCommand = new AsyncRelayCommand(ConfigureLivePreviewHueAsync);
        ConfigureLivePreviewScaleCommand = new AsyncRelayCommand(ConfigureLivePreviewScaleAsync);
        LoadPropOverlayCommand = new AsyncRelayCommand(LoadPropOverlayAsync);
        ClearPropOverlayCommand = new RelayCommand(ClearPropOverlay);
        ApplyPropOverlayToCurrentFrameCommand = new RelayCommand(ApplyPropOverlayToCurrentFrame);
        ApplyPropOverlayToCurrentDirectionCommand = new RelayCommand(ApplyPropOverlayToCurrentDirection);
        SavePropPoseForCurrentFrameCommand = new RelayCommand(SavePropPoseForCurrentFrame);
        CopyPropPoseFromPreviousFrameCommand = new RelayCommand(CopyPropPoseFromPreviousFrame);
        ClearPropPoseForCurrentFrameCommand = new RelayCommand(ClearPropPoseForCurrentFrame);
        ClearCompareOverlayCommand = new RelayCommand(ClearCompareOverlay);
        ShowAnimationEditorCommand = new RelayCommand(() => AnimationBrowserVisible = false);
        ShowAnimationBrowserCommand = new RelayCommand(() => AnimationBrowserVisible = true);
        ToggleCompareOverlayDragModeCommand = new RelayCommand(() =>
        {
            CompareOverlayDragModeEnabled = !CompareOverlayDragModeEnabled;
        });
        ApplyCompareOverlayToCurrentFrameCommand = new RelayCommand(ApplyCompareOverlayToCurrentFrame);
        ApplyCompareOverlayToCurrentDirectionCommand = new RelayCommand(ApplyCompareOverlayToCurrentDirection);
        OpenMythicPackageViewerCommand = new RelayCommand(OpenMythicPackageViewer);
        ApplyCurrentComparePoseToAllFramesCommand = new RelayCommand(ApplyCurrentComparePoseToAllFrames);
        RefreshAnimationBrowserThumbnailsCommand = new RelayCommand(RefreshAnimationBrowserThumbnails);
        OpenAnimationBrowserTileCommand = new RelayCommand<AnimationBrowserTileViewModel?>(OpenAnimationBrowserTile);
        RefreshAnimationBrowserTileThumbnailCommand = new RelayCommand<AnimationBrowserTileViewModel?>(RefreshAnimationBrowserTileThumbnail);
        OpenAnimationBrowserTileDetachedPreviewCommand = new RelayCommand<AnimationBrowserTileViewModel?>(OpenAnimationBrowserTileDetachedPreview);
        ExportAnimationBrowserTileFramesCommand = new AsyncRelayCommand<AnimationBrowserTileViewModel?>(ExportAnimationBrowserTileFramesAsync);
        ExportAnimationBrowserTileVdCommand = new AsyncRelayCommand<AnimationBrowserTileViewModel?>(ExportAnimationBrowserTileVdAsync);
        DeleteAnimationBrowserTileCommand = new AsyncRelayCommand<AnimationBrowserTileViewModel?>(DeleteAnimationBrowserTileAsync);
        ClearSelectedMulSlotCommand = new AsyncRelayCommand(ClearSelectedMulSlotAsync);

        TogglePreviewDragModeCommand = new RelayCommand(() =>
        {
            PreviewDragModeEnabled = !PreviewDragModeEnabled;
        });

        SaveComparePoseForCurrentFrameCommand = new RelayCommand(SaveComparePoseForCurrentFrame);
        CopyComparePoseFromPreviousFrameCommand = new RelayCommand(CopyComparePoseFromPreviousFrame);
        ClearComparePoseForCurrentFrameCommand = new RelayCommand(ClearComparePoseForCurrentFrame);
        SetupMountRiderAlignmentCommand = new RelayCommand(SetupMountRiderAlignment);

        ToggleCheckerCommand = new RelayCommand(() => ShowCheckerBackground = !ShowCheckerBackground);
        ToggleLoopCommand = new RelayCommand(() => LoopPlayback = !LoopPlayback);

        BodyTypeOptions.Add("All");
        BodyTypeOptions.Add("Monster");
        BodyTypeOptions.Add("Animal");
        BodyTypeOptions.Add("Human");
        BodyTypeOptions.Add("Equipment");

        AnimationFileOptions.Add("All Files");
        SelectedAnimationFile = "All Files";

        ActionOptions.Clear();
        ActionOptions.Add("Action 0");
        SelectedAction = "Action 0";

        DirectionOptions.Clear();
        DirectionOptions.Add("East (0)");
        DirectionOptions.Add("South (1)");
        DirectionOptions.Add("Southwest (2)");
        DirectionOptions.Add("West (3)");
        DirectionOptions.Add("Westnorth (4)");

        directionNameToIndex.Clear();
        directionNameToIndex["East (0)"] = 0;
        directionNameToIndex["South (1)"] = 1;
        directionNameToIndex["Southwest (2)"] = 2;
        directionNameToIndex["West (3)"] = 3;
        directionNameToIndex["Westnorth (4)"] = 4;

        SelectedDirection = DirectionOptions.Count > 0 ? DirectionOptions[0] : null;

        RefreshUnsavedChangesState();
        SetRandomLoadingTip();

        LoadActiveProfileIntoUi();
    }

    private Window? GetMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }

    private void OpenMythicPackageViewer()
    {
        MythicPackageWindow window = new MythicPackageWindow
        {
            Width = 1100,
            Height = 720
        };

        Window? mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            window.Show(mainWindow);
        }
        else
        {
            window.Show();
        }

        StatusText = "Opened EC / KR Mythic Package Viewer.";
    }

    private static readonly string[] MulAnimalActions =
{
    "Walk",
    "Run",
    "Idle",
    "Eat",
    "Alert",
    "Attack1",
    "Attack2",
    "GetHit",
    "Die1",
    "Idle",
    "Fidget",
    "LieDown",
    "Die2"
};

    private static readonly string[] MulMonsterActions =
    {
    "Walk",
    "Idle",
    "Die1",
    "Die2",
    "Attack1",
    "Attack2",
    "Attack3",
    "AttackBow",
    "AttackCrossBow",
    "AttackThrow",
    "GetHit",
    "Pillage",
    "Stomp",
    "Cast2",
    "Cast3",
    "BlockRight",
    "BlockLeft",
    "Idle",
    "Fidget",
    "Fly",
    "TakeOff",
    "GetHitInAir"
};

    private static readonly string[] MulHumanActions =
    {
    "Walk_01",
    "WalkStaff_01",
    "Run_01",
    "RunStaff_01",
    "Idle_01",
    "Idle_01",
    "Fidget_Yawn_Stretch_01",
    "CombatIdle1H_01",
    "CombatIdle1H_01",
    "AttackSlash1H_01",
    "AttackPierce1H_01",
    "AttackBash1H_01",
    "AttackBash2H_01",
    "AttackSlash2H_01",
    "AttackPierce2H_01",
    "CombatAdvance_1H_01",
    "Spell1",
    "Spell2",
    "AttackBow_01",
    "AttackCrossbow_01",
    "GetHit_Fr_Hi_01",
    "Die_Hard_Fwd_01",
    "Die_Hard_Back_01",
    "Horse_Walk_01",
    "Horse_Run_01",
    "Horse_Idle_01",
    "Horse_Attack1H_SlashRight_01",
    "Horse_AttackBow_01",
    "Horse_AttackCrossbow_01",
    "Horse_Attack2H_SlashRight_01",
    "Block_Shield_Hard_01",
    "Punch_Punch_Jab_01",
    "Bow_Lesser_01",
    "Salute_Armed1h_01",
    "Ingest_Eat_01"
};

    private static readonly string[] MulFlyingActions =
    {
    "Walk",
    "Run",
    "Idle",
    "Eat",
    "Alert",
    "Attack1",
    "Attack2",
    "GetHit",
    "Die1",
    "Idle",
    "Fidget",
    "LieDown",
    "Die2",
    "Attack3",
    "AttackBow",
    "AttackCrossBow",
    "AttackThrow",
    "Pillage",
    "Stomp",
    "Cast2",
    "Cast3",
    "BlockRight",
    "BlockLeft",
    "Fly",
    "TakeOff",
    "GetHitInAir"
};

    private string[] GetActionNamesForBody(int bodyId)
    {
        IAnimationDataSource? dataSource = GetDataSourceForEntry(SelectedAnimation);
        string sourceMode = SelectedAnimation?.SourceMode ?? string.Empty;
        string bodyType = dataSource?.GetBodyTypeName(bodyId) ?? string.Empty;

        List<int> availableActions = dataSource?.GetAvailableActionIndices(bodyId) ?? new List<int>();
        int highestAvailableAction = availableActions.Count > 0 ? availableActions.Max() : -1;

        // UOP naming
        if (string.Equals(sourceMode, "UOP", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(bodyType, "ANIMAL", StringComparison.OrdinalIgnoreCase))
            {
                return UopConstants.ActionNames.AnimalActions;
            }

            if (string.Equals(bodyType, "MONSTER", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(bodyType, "SEA_MONSTER", StringComparison.OrdinalIgnoreCase))
            {
                return UopConstants.ActionNames.MonsterActions;
            }

            if (string.Equals(bodyType, "HUMAN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(bodyType, "EQUIPMENT", StringComparison.OrdinalIgnoreCase))
            {
                // UOP char bodies expose actions beyond the 35-entry human table.
                if (highestAvailableAction >= UopConstants.ActionNames.HumanActions.Length)
                {
                    return UopConstants.ActionNames.CharActions;
                }

                return UopConstants.ActionNames.HumanActions;
            }

            // Fallbacks for UOP based on actual highest action index
            if (highestAvailableAction >= UopConstants.ActionNames.HumanActions.Length)
            {
                return UopConstants.ActionNames.CharActions;
            }

            if (highestAvailableAction >= UopConstants.ActionNames.MonsterActions.Length)
            {
                return UopConstants.ActionNames.HumanActions;
            }

            if (highestAvailableAction >= UopConstants.ActionNames.AnimalActions.Length)
            {
                return UopConstants.ActionNames.MonsterActions;
            }

            return UopConstants.ActionNames.AnimalActions;
        }

        // MUL naming
        int groupCount = dataSource?.GetGroupCountForBody(bodyId) ?? 0;

        if (groupCount == 13)
        {
            return MulAnimalActions;
        }

        if (groupCount == 22)
        {
            return MulMonsterActions;
        }

        if (groupCount == 26)
        {
            return MulFlyingActions;
        }

        if (groupCount == 35)
        {
            return MulHumanActions;
        }

        if (bodyId < 200)
        {
            return MulMonsterActions;
        }

        if (bodyId < 400)
        {
            return MulAnimalActions;
        }

        return MulHumanActions;
    }

    partial void OnAnimationBrowserSortModeChanged(string value)
    {
        if (AnimationBrowserVisible)
        {
            BuildAnimationBrowserTiles();
        }
    }

    partial void OnAnimationBrowserShowMissingSlotsChanged(bool value)
    {
        if (AnimationBrowserVisible)
        {
            BuildAnimationBrowserTiles();
        }
    }

    private string[] GetDirectionNames()
    {
        return new string[]
        {
        "East",
        "South",
        "Southwest",
        "West",
        "Westnorth"
        };
    }

    private void ApplyAnimationFilters()
    {
        string search = SearchText?.Trim() ?? string.Empty;
        string selectedType = SelectedBodyType?.Trim() ?? "All";
        string selectedFile = NormalizeSelectedAnimationFile(SelectedAnimationFile);

        int? previousBodyId = SelectedAnimation?.BodyId;
        string previousSourceFile = SelectedAnimation?.SourceFile ?? string.Empty;

        AnimationEntries.Clear();

        if (ShowMulSlotView)
        {
            SelectedAnimation = null;
            AnimationBrowserTiles.Clear();
            return;
        }

        foreach (AnimationEntry entry in allAnimationEntries)
        {
            bool matchesType = true;

            if (!string.Equals(selectedType, "All", StringComparison.OrdinalIgnoreCase))
            {
                string entryType = GetBodyTypeName(entry.BodyId);

                matchesType = selectedType.ToUpperInvariant() switch
                {
                    "MONSTER" => entryType == "MONSTER" || entryType == "SEA_MONSTER",
                    "ANIMAL" => entryType == "ANIMAL",
                    "HUMAN" => entryType == "HUMAN",
                    "EQUIPMENT" => entryType == "EQUIPMENT",
                    _ => true
                };
            }

            if (!matchesType)
            {
                continue;
            }

            bool matchesFile = true;

            if (!string.Equals(selectedFile, "All Files", StringComparison.OrdinalIgnoreCase))
            {
                string normalizedEntryFile = NormalizeAnimationFileNameForCompare(entry.SourceFile);
                string normalizedSelectedFile = NormalizeAnimationFileNameForCompare(selectedFile);

                matchesFile = string.Equals(
                    normalizedEntryFile,
                    normalizedSelectedFile,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (!matchesFile)
            {
                continue;
            }

            bool matchesSearch = true;

            if (!string.IsNullOrWhiteSpace(search))
            {
                matchesSearch =
                    entry.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.SecondaryText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.BodyId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
            }

            if (!matchesSearch)
            {
                continue;
            }

            AnimationEntries.Add(entry);
        }

        if (AnimationEntries.Count > 0)
        {
            AnimationEntry? matchingSelection = null;

            if (previousBodyId.HasValue)
            {
                matchingSelection = AnimationEntries.FirstOrDefault(x =>
                    x.BodyId == previousBodyId.Value &&
                    string.Equals(x.SourceFile, previousSourceFile, StringComparison.OrdinalIgnoreCase));
            }

            SelectedAnimation = matchingSelection ?? AnimationEntries[0];
        }
        else
        {
            SelectedAnimation = null;
            currentResolvedAnimationBlock = null;
            ClearDecodedFramesAndThumbnails();
            SelectedBlockSizeText = "-";
            SelectedBlockHeaderText = "-";

            OnPropertyChanged(nameof(PreviewInfoText));
            OnPropertyChanged(nameof(SelectedBlockSize));
            OnPropertyChanged(nameof(SelectedBlockHeader));
            OnPropertyChanged(nameof(SelectedSequenceRequestedAction));
            OnPropertyChanged(nameof(SelectedSequenceResolvedGroup));
            OnPropertyChanged(nameof(SelectedSequenceFrameCount));
            OnPropertyChanged(nameof(SelectedSequenceRemap));
            OnPropertyChanged(nameof(SelectedSequenceBodyMapping));
            OnPropertyChanged(nameof(SelectedUopVirtualPathDisplay));
        }

        StatusText = "Showing " + AnimationEntries.Count + " filtered animation bodies.";
        if (AnimationBrowserVisible)
        {
            BuildAnimationBrowserTiles();
        }
    }

    private IAnimationDataSource? GetDataSourceForEntry(AnimationEntry? entry)
    {
        if (entry == null)
        {
            return null;
        }

        return entry.SourceMode switch
        {
            "UOP" => uopAnimationDataSource,
            "MUL" => mulAnimationDataSource,
            _ => null
        };
    }


    private void UpdatePreferredAnimationSources()
    {
        string normalized = NormalizeSelectedAnimationFile(SelectedAnimationFile);

        if (normalized.EndsWith(".mul", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".idx", StringComparison.OrdinalIgnoreCase))
        {
            mulAnimationDataSource.SetPreferredSourceFile(normalized);
            uopAnimationDataSource.SetPreferredSourceFile(null);
            return;
        }

        if (normalized.EndsWith(".uop", StringComparison.OrdinalIgnoreCase))
        {
            mulAnimationDataSource.SetPreferredSourceFile(null);
            uopAnimationDataSource.SetPreferredSourceFile(normalized);
            return;
        }

        mulAnimationDataSource.SetPreferredSourceFile(null);
        uopAnimationDataSource.SetPreferredSourceFile(null);
    }

    private void ApplyMulSlotFilters()
    {
        MulSlotEntries.Clear();

        if (!ShowMulSlotView)
        {
            return;
        }

        string search = SearchText?.Trim() ?? string.Empty;
        string selectedFile = NormalizeSelectedAnimationFile(SelectedAnimationFile);

        foreach (MulSlotEntry entry in allMulSlotEntries)
        {
            bool matchesFile = true;

            if (!string.Equals(selectedFile, "All Files", StringComparison.OrdinalIgnoreCase))
            {
                string selectedIdxName = selectedFile.EndsWith(".mul", StringComparison.OrdinalIgnoreCase)
                    ? Path.ChangeExtension(selectedFile, ".idx")
                    : selectedFile;

                matchesFile = string.Equals(entry.FileName, selectedIdxName, StringComparison.OrdinalIgnoreCase);
            }

            if (!matchesFile)
            {
                continue;
            }

            bool anyTypeFilter = MulSlotShowHFilter || MulSlotShowLFilter || MulSlotShowPFilter;

            if (anyTypeFilter)
            {
                bool matchesType =
                    (MulSlotShowHFilter && string.Equals(entry.TypeLetter, "H", StringComparison.OrdinalIgnoreCase)) ||
                    (MulSlotShowLFilter && string.Equals(entry.TypeLetter, "L", StringComparison.OrdinalIgnoreCase)) ||
                    (MulSlotShowPFilter && string.Equals(entry.TypeLetter, "P", StringComparison.OrdinalIgnoreCase));

                if (!matchesType)
                {
                    continue;
                }
            }

            bool matchesSearch = true;

            if (!string.IsNullOrWhiteSpace(search))
            {
                matchesSearch =
                    entry.DisplayText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.FileName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.BodyIndex.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.TrueBodyId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.TypeLetter.Contains(search, StringComparison.OrdinalIgnoreCase);
            }

            if (!matchesSearch)
            {
                continue;
            }

            MulSlotEntries.Add(entry);
        }

        if (MulSlotEntries.Count > 0)
        {
            if (SelectedMulSlot == null || !MulSlotEntries.Contains(SelectedMulSlot))
            {
                SelectedMulSlot = MulSlotEntries[0];
            }
        }
        else
        {
            SelectedMulSlot = null;
        }

        StatusText = "Showing " + MulSlotEntries.Count + " free MUL body IDs.";
    }

    private void RebuildUopBodyEntries()
    {
        allUopBodyEntries.Clear();

        Dictionary<string, HashSet<int>> seenPerFile = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        foreach (AnimationEntry entry in allAnimationEntries)
        {
            if (!string.Equals(entry.SourceMode, "UOP", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.SourceFile))
            {
                continue;
            }

            if (!seenPerFile.TryGetValue(entry.SourceFile, out HashSet<int>? bodyIds))
            {
                bodyIds = new HashSet<int>();
                seenPerFile[entry.SourceFile] = bodyIds;
            }

            if (!bodyIds.Add(entry.BodyId))
            {
                continue;
            }

            IAnimationDataSource? dataSource = GetDataSourceForEntry(entry);
            int actionCount = dataSource?.GetAvailableActionIndices(entry.BodyId).Count ?? 0;

            if (actionCount == 0)
            {
                actionCount = dataSource?.GetGroupCountForBody(entry.BodyId) ?? 0;
            }

            allUopBodyEntries.Add(new UopBodySlotEntry
            {
                BodyId = entry.BodyId,
                FileName = entry.SourceFile,
                BodyType = GetBodyTypeName(entry.BodyId),
                ActionCount = actionCount
            });
        }

        allUopBodyEntries.Sort((left, right) =>
        {
            int fileCompare = string.Compare(left.FileName, right.FileName, StringComparison.OrdinalIgnoreCase);
            if (fileCompare != 0)
            {
                return fileCompare;
            }

            return left.BodyId.CompareTo(right.BodyId);
        });
    }

    private void LoadSelectedUopBodySlot()
    {
        SelectedBlockSizeText = "-";
        SelectedBlockHeaderText = "-";
        ClearDecodedFramesAndThumbnails();

        OnPropertyChanged(nameof(SelectedBlockSize));
        OnPropertyChanged(nameof(SelectedBlockHeader));
        OnPropertyChanged(nameof(PreviewInfoText));

        if (SelectedUopBodySlot == null)
        {
            return;
        }

        SelectedBlockHeaderText =
            "File: " + SelectedUopBodySlot.FileName +
            " | Body ID: " + SelectedUopBodySlot.BodyId +
            " | Type: " + SelectedUopBodySlot.BodyType +
            " | Actions: " + SelectedUopBodySlot.ActionCount +
            " | UOP target";

        StatusText = "Selected UOP body target.";
        OnPropertyChanged(nameof(SelectedBlockHeader));
    }

    private void LoadSelectedMulSlot()
    {
        SelectedBlockSizeText = "-";
        SelectedBlockHeaderText = "-";
        ClearDecodedFramesAndThumbnails();

        OnPropertyChanged(nameof(SelectedBlockSize));
        OnPropertyChanged(nameof(SelectedBlockHeader));
        OnPropertyChanged(nameof(PreviewInfoText));

        if (SelectedMulSlot == null)
        {
            return;
        }

        StatusText = "Selected free MUL body slot.";
        SelectedBlockHeaderText =
            "File: " + SelectedMulSlot.FileName +
            " | Type: " + SelectedMulSlot.TypeLetter +
            " | BodyIndex: " + SelectedMulSlot.BodyIndex +
            " | TrueBody: " + SelectedMulSlot.TrueBodyId +
            " | AnimLength: " + SelectedMulSlot.AnimLength +
            " | Free";

        OnPropertyChanged(nameof(SelectedBlockHeader));
    }

    private void RebuildAnimationFileOptions()
    {
        bool shouldLoadUop = ShouldLoadUopForActiveProfile();
        AnimationFileOptions.Clear();
        AnimationFileOptions.Add("All Files");

        HashSet<string> fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (AnimationEntry entry in allAnimationEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry.SourceFile))
            {
                fileNames.Add(entry.SourceFile);
            }
        }

        foreach (MulSlotEntry entry in allMulSlotEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.FileName))
            {
                continue;
            }

            fileNames.Add(entry.FileName);

            if (entry.FileName.EndsWith(".idx", StringComparison.OrdinalIgnoreCase))
            {
                fileNames.Add(Path.ChangeExtension(entry.FileName, ".mul"));
            }
        }

        string currentFolderPath = GetCurrentFolderPath();

        if (!string.IsNullOrWhiteSpace(currentFolderPath) && Directory.Exists(currentFolderPath))
        {
            UoFileDiscoveryService discoveryService = new UoFileDiscoveryService();
            List<UoAnimationFile> discoveredFiles = discoveryService.FindAnimationFiles(currentFolderPath);

            foreach (UoAnimationFile file in discoveredFiles)
            {
                if (string.IsNullOrWhiteSpace(file.FileName))
                {
                    continue;
                }

                // Only add discovered UOP files if they actually produced animation entries.
                bool hasBuiltEntries = allAnimationEntries.Any(x =>
                    string.Equals(x.SourceFile, file.FileName, StringComparison.OrdinalIgnoreCase));

                bool isMulOrIdx = file.FileName.EndsWith(".mul", StringComparison.OrdinalIgnoreCase) ||
                                  file.FileName.EndsWith(".idx", StringComparison.OrdinalIgnoreCase);

                bool isUop = file.FileName.EndsWith(".uop", StringComparison.OrdinalIgnoreCase);

                if ((hasBuiltEntries || isMulOrIdx) && (shouldLoadUop || !isUop))
                {
                    fileNames.Add(file.FileName);
                }
            }
        }

        List<string> sorted = new List<string>(fileNames);
        sorted.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (string fileName in sorted)
        {
            AnimationFileOptions.Add(fileName);
        }

        if (string.IsNullOrWhiteSpace(SelectedAnimationFile) || !AnimationFileOptions.Contains(SelectedAnimationFile))
        {
            SelectedAnimationFile = "All Files";
        }
    }

    public void SaveAnimationBrowserName(AnimationBrowserTileViewModel? tile, string newName)
    {
        if (tile?.SourceEntry == null)
        {
            return;
        }

        int bodyId = tile.SourceEntry.BodyId;
        string cleanName = (newName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(cleanName))
        {
            animationBrowserNamesByBodyId.Remove(bodyId);
        }
        else
        {
            animationBrowserNamesByBodyId[bodyId] = cleanName;
        }

        string path = Path.Combine(AppContext.BaseDirectory, "animation_names.json");

        Dictionary<string, string> output = animationBrowserNamesByBodyId
            .OrderBy(x => x.Key)
            .ToDictionary(
                x => x.Key.ToString(),
                x => x.Value);

        string json = System.Text.Json.JsonSerializer.Serialize(
            output,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(path, json);

        ApplyAnimationBrowserNamesToAnimationEntries();
        OnPropertyChanged(nameof(SelectedAnimationName));
        OnPropertyChanged(nameof(PreviewInfoText));

        if (AnimationBrowserVisible)
        {
            BuildAnimationBrowserTiles();
        }

        StatusText = "Saved animation name to " + Path.GetFileName(path) + ".";
    }

    private void LoadAnimationBrowserNames()
    {
        animationBrowserNamesByBodyId.Clear();

        try
        {
            string toolFolderPath = AppContext.BaseDirectory;
            string path = Path.Combine(toolFolderPath, "animation_names.json");

            if (!File.Exists(path))
            {
                return;
            }

            string json = File.ReadAllText(path);

            Dictionary<string, string>? rawNames =
                System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (rawNames == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> pair in rawNames)
            {
                if (!int.TryParse(pair.Key, out int bodyId))
                {
                    continue;
                }

                string name = pair.Value?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(name))
                {
                    animationBrowserNamesByBodyId[bodyId] = name;
                }
            }
        }
        catch
        {
            // Bad animation_names.json should not break the tool.
        }
    }

    private void ApplyAnimationBrowserNamesToAnimationEntries()
    {
        if (animationBrowserNamesByBodyId.Count == 0)
        {
            return;
        }

        foreach (AnimationEntry entry in AnimationEntries)
        {
            if (animationBrowserNamesByBodyId.TryGetValue(entry.BodyId, out string? name) &&
                !string.IsNullOrWhiteSpace(name))
            {
                entry.DisplayName = "Body " + entry.BodyId + " - " + name;
            }
        }
    }

    private string GetAnimationBrowserDisplayName(AnimationEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        if (AnimationBrowserShowNames &&
            animationBrowserNamesByBodyId.TryGetValue(entry.BodyId, out string? customName) &&
            !string.IsNullOrWhiteSpace(customName))
        {
            return entry.BodyId + " - " + customName;
        }

        return entry.BodyId.ToString();
    }

    partial void OnAnimationBrowserShowNamesChanged(bool value)
    {
        if (AnimationBrowserVisible)
        {
            BuildAnimationBrowserTiles();
        }
    }

    partial void OnAnimationBrowserHoverPreviewEnabledChanged(bool value)
    {
        if (!value)
        {
            StopAnimationBrowserTileHoverPreview();
        }
    }

    public async void StartAnimationBrowserTileHoverPreview(AnimationBrowserTileViewModel? tile)
    {
        if (!AnimationBrowserHoverPreviewEnabled)
        {
            return;
        }

        if (tile?.SourceEntry == null)
        {
            return;
        }

        StopAnimationBrowserTileHoverPreview();

        activeHoverTile = tile;
        animationBrowserHoverCancellation = new CancellationTokenSource();
        CancellationToken token = animationBrowserHoverCancellation.Token;

        try
        {
            await Task.Delay(250, token);

            if (token.IsCancellationRequested || activeHoverTile != tile)
            {
                return;
            }

            string key = GetAnimationBrowserThumbnailKey(tile.SourceEntry) + "|hover";

            if (!animationBrowserHoverFrameCache.TryGetValue(key, out List<WriteableBitmap>? frames))
            {
                frames = await Task.Run(() =>
                {
                    return LoadAnimationBrowserHoverFrames(tile.SourceEntry);
                }, token);

                animationBrowserHoverFrameCache[key] = frames;
            }

            if (frames.Count == 0)
            {
                return;
            }

            int frameIndex = 0;

            while (!token.IsCancellationRequested && activeHoverTile == tile)
            {
                tile.Thumbnail = frames[frameIndex];

                frameIndex++;
                if (frameIndex >= frames.Count)
                {
                    frameIndex = 0;
                }

                await Task.Delay(AnimationBrowserHoverFrameDelayMs, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void StopAnimationBrowserTileHoverPreview()
    {
        if (animationBrowserHoverCancellation != null)
        {
            animationBrowserHoverCancellation.Cancel();
            animationBrowserHoverCancellation.Dispose();
            animationBrowserHoverCancellation = null;
        }

        if (activeHoverTile?.SourceEntry != null)
        {
            string key = GetAnimationBrowserThumbnailKey(activeHoverTile.SourceEntry);

            if (animationBrowserThumbnailCache.TryGetValue(key, out WriteableBitmap? thumbnail))
            {
                activeHoverTile.Thumbnail = thumbnail;
            }
        }

        activeHoverTile = null;
    }

    private List<WriteableBitmap> LoadAnimationBrowserHoverFrames(AnimationEntry entry)
    {
        try
        {
            IAnimationDataSource? dataSource = GetDataSourceForEntry(entry);
            if (dataSource == null)
            {
                return new List<WriteableBitmap>();
            }

            List<int> actions = dataSource.GetAvailableActionIndices(entry.BodyId);
            if (actions.Count == 0)
            {
                actions.Add(0);
            }

            int[] preferredDirections =
            {
            1, // South
            0,
            2,
            3,
            4
        };

            foreach (int actionIndex in actions)
            {
                foreach (int directionIndex in preferredDirections)
                {
                    DetachedPreviewLoadResult result = LoadDetachedPreview(
                        entry,
                        actionIndex,
                        directionIndex);

                    if (result.Success && result.Frames.Count > 0)
                    {
                        return result.Frames;
                    }
                }
            }
        }
        catch
        {
        }

        return new List<WriteableBitmap>();
    }

    private async Task DeleteAnimationBrowserTileAsync(AnimationBrowserTileViewModel? tile)
    {
        if (tile?.SourceEntry == null)
        {
            return;
        }

        SelectAnimationFromBrowserTile(tile);

        await DeleteAnimationAsync();

        animationBrowserThumbnailCache.Remove(GetAnimationBrowserThumbnailKey(tile.SourceEntry));

        if (AnimationBrowserVisible)
        {
            BuildAnimationBrowserTiles();
        }
    }

    private async Task ExportAnimationBrowserTileFramesAsync(AnimationBrowserTileViewModel? tile)
    {
        if (tile?.SourceEntry == null)
        {
            return;
        }

        SelectAnimationFromBrowserTile(tile);
        await ExportFramesAsync();
    }

    private async Task ExportAnimationBrowserTileVdAsync(AnimationBrowserTileViewModel? tile)
    {
        if (tile?.SourceEntry == null)
        {
            return;
        }

        SelectAnimationFromBrowserTile(tile);
        await ExportVdAsync();
    }

    private async void OpenAnimationBrowserTileDetachedPreview(AnimationBrowserTileViewModel? tile)
    {
        if (tile?.SourceEntry == null)
        {
            return;
        }

        SelectAnimationFromBrowserTile(tile);
        await ShowDetachedPreviewAsync();
    }

    private void OpenAnimationBrowserTile(AnimationBrowserTileViewModel? tile)
    {
        if (tile?.SourceEntry == null)
        {
            return;
        }

        SelectAnimationFromBrowserTile(tile);
        AnimationBrowserVisible = false;
    }

    private void RefreshAnimationBrowserTileThumbnail(AnimationBrowserTileViewModel? tile)
    {
        if (tile?.SourceEntry == null)
        {
            return;
        }

        string key = GetAnimationBrowserThumbnailKey(tile.SourceEntry);

        animationBrowserThumbnailCache.Remove(key);

        tile.Thumbnail = null;
        tile.IsLoadingThumbnail = true;

        try
        {
            WriteableBitmap? thumbnail = GenerateAnimationBrowserThumbnail(tile.SourceEntry);

            animationBrowserThumbnailCache[key] = thumbnail;
            tile.Thumbnail = thumbnail;
            tile.IsLoadingThumbnail = false;

            StatusText = "Refreshed thumbnail for " + tile.SourceEntry.DisplayName + ".";
        }
        catch
        {
            tile.IsLoadingThumbnail = false;
            StatusText = "Failed to refresh thumbnail.";
        }
    }

    private void RefreshAnimationBrowserThumbnails()
    {
        CancelAnimationBrowserThumbnailLoading();

        animationBrowserThumbnailCache.Clear();

        foreach (AnimationBrowserTileViewModel tile in AnimationBrowserTiles)
        {
            tile.Thumbnail = null;
            tile.IsLoadingThumbnail = true;
        }

        StartAnimationBrowserThumbnailLoading();

        StatusText = "Animation Browser thumbnails refreshed.";
    }

    partial void OnAnimationBrowserTileSizeChanged(string value)
    {
        OnPropertyChanged(nameof(AnimationBrowserTileWidth));
        OnPropertyChanged(nameof(AnimationBrowserTileHeight));
        OnPropertyChanged(nameof(AnimationBrowserImageHostSize));
        OnPropertyChanged(nameof(AnimationBrowserImageBoxSize));
    }

    public double AnimationBrowserTileWidth =>
    AnimationBrowserTileSize switch
    {
        "Medium" => 110,
        "Large" => 145,
        _ => 82
    };

    public double AnimationBrowserTileHeight =>
        AnimationBrowserTileSize switch
        {
            "Medium" => 128,
            "Large" => 165,
            _ => 98
        };

    public double AnimationBrowserImageHostSize =>
        AnimationBrowserTileSize switch
        {
            "Medium" => 96,
            "Large" => 128,
            _ => 72
        };

    public double AnimationBrowserImageBoxSize =>
        AnimationBrowserTileSize switch
        {
            "Medium" => 90,
            "Large" => 120,
            _ => 68
        };

    private void RebuildAnimationBrowserFilters()
    {
        string oldSource = AnimationBrowserSourceFilter;
        string oldType = AnimationBrowserTypeFilter;

        AnimationBrowserSourceFilters.Clear();
        AnimationBrowserSourceFilters.Add("All");

        foreach (string source in AnimationEntries
            .Select(x => x.SourceFile)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            AnimationBrowserSourceFilters.Add(source);
        }

        AnimationBrowserTypeFilters.Clear();
        AnimationBrowserTypeFilters.Add("All");

        foreach (string type in AnimationEntries
            .Select(GetBrowserTypeFromEntry)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            AnimationBrowserTypeFilters.Add(type);
        }

        AnimationBrowserSourceFilter = AnimationBrowserSourceFilters.Contains(oldSource)
            ? oldSource
            : "All";

        AnimationBrowserTypeFilter = AnimationBrowserTypeFilters.Contains(oldType)
            ? oldType
            : "All";
    }

    private string GetBrowserTypeFromEntry(AnimationEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        string text = entry.SecondaryText ?? string.Empty;

        int pipeIndex = text.IndexOf('|');
        if (pipeIndex > 0)
        {
            return text.Substring(0, pipeIndex).Trim();
        }

        return text.Trim();
    }

    partial void OnAnimationBrowserSearchTextChanged(string value)
    {
        if (AnimationBrowserVisible)
        {
            BuildAnimationBrowserTiles();
        }
    }

    partial void OnAnimationBrowserSourceFilterChanged(string value)
    {
        if (AnimationBrowserVisible)
        {
            BuildAnimationBrowserTiles();
        }
    }

    partial void OnAnimationBrowserTypeFilterChanged(string value)
    {
        if (AnimationBrowserVisible)
        {
            BuildAnimationBrowserTiles();
        }
    }

    private void AddAnimationBrowserTile(AnimationEntry entry)
    {
        string key = GetAnimationBrowserThumbnailKey(entry);

        animationBrowserThumbnailCache.TryGetValue(key, out WriteableBitmap? cachedThumbnail);

        AnimationBrowserTiles.Add(new AnimationBrowserTileViewModel
        {
            SourceEntry = entry,
            BodyId = entry.BodyId,
            Thumbnail = cachedThumbnail,
            IsLoadingThumbnail = cachedThumbnail == null,
            DisplayName = GetAnimationBrowserDisplayName(entry),
            SecondaryText = entry.SecondaryText,
            SourceText = entry.SourceMode + " | " + entry.SourceFile
        });
    }

    private void BuildAnimationBrowserTiles()
    {
        CancelAnimationBrowserThumbnailLoading();

        LoadAnimationBrowserNames();
        ApplyAnimationBrowserNamesToAnimationEntries();
        OnPropertyChanged(nameof(SelectedAnimationName));
        OnPropertyChanged(nameof(PreviewInfoText));

        RebuildAnimationBrowserFilters();

        AnimationBrowserTiles.Clear();
        int totalCount = AnimationEntries.Count;

        IEnumerable<AnimationEntry> filteredEntries = AnimationEntries;

        if (!string.IsNullOrWhiteSpace(AnimationBrowserSearchText))
        {
            string search = AnimationBrowserSearchText.Trim();

            filteredEntries = filteredEntries.Where(entry =>
                entry.BodyId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                GetAnimationBrowserDisplayName(entry).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (entry.DisplayName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (entry.SecondaryText ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (entry.SourceFile ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (entry.SourceMode ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(AnimationBrowserSourceFilter) &&
            !string.Equals(AnimationBrowserSourceFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            filteredEntries = filteredEntries.Where(entry =>
                string.Equals(entry.SourceFile, AnimationBrowserSourceFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(AnimationBrowserTypeFilter) &&
            !string.Equals(AnimationBrowserTypeFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            filteredEntries = filteredEntries.Where(entry =>
                string.Equals(GetBrowserTypeFromEntry(entry), AnimationBrowserTypeFilter, StringComparison.OrdinalIgnoreCase));
        }

        filteredEntries = AnimationBrowserSortMode switch
        {
            "Body ID Desc" => filteredEntries.OrderByDescending(x => x.BodyId),
            "Name" => filteredEntries.OrderBy(x => GetAnimationBrowserDisplayName(x), StringComparer.OrdinalIgnoreCase),
            "Source File" => filteredEntries
                .OrderBy(x => x.SourceFile, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.BodyId),
            _ => filteredEntries.OrderBy(x => x.BodyId)
        };

        List<AnimationEntry> sortedEntries = filteredEntries.ToList();

        if (AnimationBrowserShowMissingSlots && sortedEntries.Count > 1)
        {
            int minBodyId = sortedEntries.Min(x => x.BodyId);
            int maxBodyId = sortedEntries.Max(x => x.BodyId);

            Dictionary<int, AnimationEntry> entriesByBodyId = sortedEntries
                .GroupBy(x => x.BodyId)
                .ToDictionary(x => x.Key, x => x.First());

            for (int bodyId = minBodyId; bodyId <= maxBodyId; bodyId++)
            {
                if (entriesByBodyId.TryGetValue(bodyId, out AnimationEntry? entry))
                {
                    AddAnimationBrowserTile(entry);
                }
                else
                {
                    AnimationBrowserTiles.Add(new AnimationBrowserTileViewModel
                    {
                        BodyId = bodyId,
                        IsMissingSlot = true,
                        IsLoadingThumbnail = false,
                        DisplayName = bodyId + " missing",
                        SecondaryText = "Missing slot",
                        SourceText = string.Empty
                    });
                }
            }
        }
        else
        {
            foreach (AnimationEntry entry in sortedEntries)
            {
                AddAnimationBrowserTile(entry);
            }
        }

        AnimationBrowserCountText = "Showing " + AnimationBrowserTiles.Count + " / " + totalCount;

        StartAnimationBrowserThumbnailLoading();
    }

    private string GetAnimationBrowserThumbnailKey(AnimationEntry entry)
    {
        return
            entry.SourceMode + "|" +
            entry.SourceFile + "|" +
            entry.BodyId + "|south";
    }

    private void CancelAnimationBrowserThumbnailLoading()
    {
        if (animationBrowserThumbnailCancellation != null)
        {
            animationBrowserThumbnailCancellation.Cancel();
            animationBrowserThumbnailCancellation.Dispose();
            animationBrowserThumbnailCancellation = null;
        }
    }

    private async void StartAnimationBrowserThumbnailLoading()
    {
        CancelAnimationBrowserThumbnailLoading();

        animationBrowserThumbnailCancellation = new CancellationTokenSource();
        CancellationToken token = animationBrowserThumbnailCancellation.Token;

        try
        {
            List<AnimationBrowserTileViewModel> tiles = AnimationBrowserTiles.ToList();

            foreach (AnimationBrowserTileViewModel tile in tiles)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (tile.IsMissingSlot || tile.SourceEntry == null || tile.Thumbnail != null)
                {
                    tile.IsLoadingThumbnail = false;
                    continue;
                }

                AnimationEntry entry = tile.SourceEntry;
                string key = GetAnimationBrowserThumbnailKey(entry);

                try
                {
                    WriteableBitmap? thumbnail = await Task.Run(() =>
                    {
                        if (token.IsCancellationRequested)
                        {
                            return null;
                        }

                        return GenerateAnimationBrowserThumbnail(entry);
                    }, token);

                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    animationBrowserThumbnailCache[key] = thumbnail;
                    tile.Thumbnail = thumbnail;
                    tile.IsLoadingThumbnail = false;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    tile.IsLoadingThumbnail = false;
                }

                try
                {
                    await Task.Delay(1, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    partial void OnAnimationBrowserVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowAnimationEditorPanel));

        if (value)
        {
            BuildAnimationBrowserTiles();
        }
        else
        {
            CancelAnimationBrowserThumbnailLoading();
        }
    }

    private WriteableBitmap? GenerateAnimationBrowserThumbnail(AnimationEntry entry)
    {
        try
        {
            IAnimationDataSource? dataSource = GetDataSourceForEntry(entry);
            if (dataSource == null)
            {
                return null;
            }

            List<int> actions = dataSource.GetAvailableActionIndices(entry.BodyId);
            if (actions.Count == 0)
            {
                actions.Add(0);
            }

            int[] preferredDirections =
            {
            1, // South
            0, // East fallback
            2,
            3,
            4
        };

            foreach (int actionIndex in actions)
            {
                foreach (int directionIndex in preferredDirections)
                {
                    DetachedPreviewLoadResult result = LoadDetachedPreview(
                        entry,
                        actionIndex,
                        directionIndex);

                    if (result.Success && result.Frames.Count > 0)
                    {
                        return result.Frames[0];
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private void SelectAnimationFromBrowserTile(AnimationBrowserTileViewModel? tile)
    {
        if (tile?.SourceEntry == null)
        {
            return;
        }

        AnimationEntry? match = AnimationEntries.FirstOrDefault(x =>
            x.BodyId == tile.SourceEntry.BodyId &&
            string.Equals(x.SourceFile, tile.SourceEntry.SourceFile, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.SourceMode, tile.SourceEntry.SourceMode, StringComparison.OrdinalIgnoreCase));

        SelectedAnimation = match ?? tile.SourceEntry;
    }

    private void ApplyUopBodyFilters()
    {
        UopBodyEntries.Clear();

        if (!ShowMulSlotView)
        {
            return;
        }

        if (!IsSelectedAnimationFileUop())
        {
            return;
        }

        string search = SearchText?.Trim() ?? string.Empty;
        string selectedFile = NormalizeSelectedAnimationFile(SelectedAnimationFile);

        HashSet<int> emptyMulTrueBodyIds = new HashSet<int>();

        foreach (MulSlotEntry mulSlot in allMulSlotEntries)
        {
            if (!mulSlot.IsEmpty)
            {
                continue;
            }

            emptyMulTrueBodyIds.Add(mulSlot.TrueBodyId);
        }

        foreach (UopBodySlotEntry entry in allUopBodyEntries)
        {
            bool matchesFile = true;

            if (!string.Equals(selectedFile, "All Files", StringComparison.OrdinalIgnoreCase))
            {
                string normalizedEntryFile = NormalizeAnimationFileNameForCompare(entry.FileName);
                string normalizedSelectedFile = NormalizeAnimationFileNameForCompare(selectedFile);

                matchesFile = string.Equals(
                    normalizedEntryFile,
                    normalizedSelectedFile,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (!matchesFile)
            {
                continue;
            }

            // Only show UOP bodies that have an empty MUL slot available
            if (!emptyMulTrueBodyIds.Contains(entry.BodyId))
            {
                continue;
            }

            bool matchesSearch = true;

            if (!string.IsNullOrWhiteSpace(search))
            {
                matchesSearch =
                    entry.DisplayText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.SecondaryText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.BodyId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
            }

            if (!matchesSearch)
            {
                continue;
            }

            UopBodyEntries.Add(entry);
        }

        if (UopBodyEntries.Count > 0)
        {
            if (SelectedUopBodySlot == null || !UopBodyEntries.Contains(SelectedUopBodySlot))
            {
                SelectedUopBodySlot = UopBodyEntries[0];
            }
        }
        else
        {
            SelectedUopBodySlot = null;
        }

        StatusText = "Showing " + UopBodyEntries.Count + " UOP bodies with empty MUL targets.";
    }

    private FolderLoadResult LoadAnimationDataForFolder(string folderPath, int mulBodiesToScan, int uopBodiesToScan)
    {
        FolderLoadResult result = new FolderLoadResult();

        bool mulReady = mulAnimationDataSource.Initialize(folderPath);
        bool uopReady = uopAnimationDataSource.Initialize(folderPath);

        result.MulReady = mulReady;
        result.UopReady = uopReady;

        if (mulReady)
        {
            List<AnimationEntry> mulEntries = mulAnimationDataSource.BuildAnimationEntries(mulBodiesToScan);
            foreach (AnimationEntry entry in mulEntries)
            {
                entry.SourceMode = "MUL";

                if (!entry.SecondaryText.Contains("| MUL |", StringComparison.OrdinalIgnoreCase) &&
                    !entry.SecondaryText.EndsWith("| MUL", StringComparison.OrdinalIgnoreCase))
                {
                    entry.SecondaryText += " | MUL";
                }
            }

            result.MulEntries = mulEntries;
            result.MulSlots = new List<MulSlotEntry>();
        }

        if (uopReady)
        {
            List<AnimationEntry> uopEntries = uopAnimationDataSource.BuildAnimationEntries(uopBodiesToScan);
            Console.WriteLine("UOP BuildAnimationEntries returned " + uopEntries.Count + " entries.");
            foreach (IGrouping<string, AnimationEntry> group in uopEntries
    .GroupBy(x => x.SourceFile ?? string.Empty)
    .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    "UOP SOURCE SUMMARY file=[" + group.Key + "] count=" + group.Count());
            }
            foreach (AnimationEntry entry in uopEntries
    .Where(x => string.Equals(x.SourceFile, "AnimationFrame6.uop", StringComparison.OrdinalIgnoreCase))
    .Take(50))
            {
                Console.WriteLine(
                    "UOP FRAME6 ENTRY body=" + entry.BodyId +
                    " action=" + entry.ActionId +
                    " index=" + entry.IndexNumber +
                    " source=[" + entry.SourceFile + "]");
            }

            foreach (AnimationEntry entry in uopEntries.Take(20))
            {
                Console.WriteLine(
                    "UOP ENTRY body=" + entry.BodyId +
                    " action=" + entry.ActionId +
                    " source=[" + entry.SourceFile + "]" +
                    " display=[" + entry.DisplayName + "]");
            }

            foreach (AnimationEntry entry in uopEntries)
            {
                entry.SourceMode = "UOP";

                if (!entry.SecondaryText.Contains("| UOP |", StringComparison.OrdinalIgnoreCase) &&
                    !entry.SecondaryText.EndsWith("| UOP", StringComparison.OrdinalIgnoreCase))
                {
                    entry.SecondaryText += " | UOP";
                }
            }

            result.UopEntries = uopEntries;
        }

        return result;
    }

    private void SetRandomLoadingTip()
    {
        if (LoadingTips.Length == 0)
        {
            LoadingTipText = string.Empty;
            return;
        }

        int index = loadingTipRandom.Next(LoadingTips.Length);
        LoadingTipText = LoadingTips[index];
        OnPropertyChanged(nameof(LoadingTipText));
    }

    partial void OnLoopPlaybackChanged(bool value)
    {
        SaveActiveProfileUiState();
    }

    private async Task<bool> ShowConfirmationDialogAsync(Window owner, string title, string message)
    {
        Window dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        bool confirmed = false;

        TextBlock messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        };

        Button yesButton = new Button
        {
            Content = "Yes",
            Width = 90
        };

        Button noButton = new Button
        {
            Content = "No",
            Width = 90
        };

        yesButton.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };

        noButton.Click += (_, _) =>
        {
            confirmed = false;
            dialog.Close();
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
        {
            messageText,
            new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    noButton,
                    yesButton
                }
            }
        }
        };

        await dialog.ShowDialog(owner);
        return confirmed;
    }

    private async Task ClearSelectedMulSlotAsync()
    {
        if (!ShowMulSlotView)
        {
            StatusText = "Switch to BODY IDS view first.";
            return;
        }

        if (SelectedMulSlot == null)
        {
            StatusText = "Select a BODY ID slot first.";
            return;
        }

        string currentFolderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(currentFolderPath) || !Directory.Exists(currentFolderPath))
        {
            StatusText = "Open a valid UO folder first.";
            return;
        }

        MulSlotDeleteService.DeleteResult result =
            mulSlotDeleteService.DeleteBodySlot(
                currentFolderPath,
                SelectedMulSlot.FileName,
                SelectedMulSlot.FileType,
                SelectedMulSlot.BodyIndex,
                SelectedMulSlot.AnimLength);

        StatusText = result.Message;

        if (!result.Success)
        {
            return;
        }

        int clearedBodyIndex = SelectedMulSlot.BodyIndex;
        int clearedTrueBodyId = SelectedMulSlot.TrueBodyId;
        string clearedFileName = SelectedMulSlot.FileName;

        pendingMulImportSession?.Clear();
        animationCacheService.DeleteAllCaches();

        ReloadAnimationSourcesAndLists();
        RefreshUnsavedChangesState();

        StatusText =
            "Cleared " + clearedFileName +
            " body index " + clearedBodyIndex +
            " / true body " + clearedTrueBodyId + ".";
    }
}