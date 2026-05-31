using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    public enum MainToolTab
    {
        AnimationEditor = 0,
        AnimationBrowser = 1,
        Gumps = 2,
        TileData = 3,
        Art = 4,
        AnimData = 5,
        Wearables = 6,
        Lights = 7,
        GumpBuilder = 8,
        Hues = 9,
        Cliloc = 10,
        RadarCol = 11
    }

    public string HeaderStatusText => ShowGumpEditorPanel ? GumpInfoText : StatusText;

    public bool ShowMulActionDirectionControls => ShowMulSlotView && !IsSelectedAnimationFileUop();

    public bool ShowActionDirectionControls => ShowAnimationOnlyControls || ShowMulActionDirectionControls;

    public bool ShowAnimationView => !ShowMulSlotView;
    public bool ShowSlotView => ShowMulSlotView;
    public bool ShowAnimationOnlyControls => !ShowMulSlotView;

    public string ZoomText => ZoomLevel.ToString("0.0") + "x";
    public string PlaybackSpeedText => PlaybackSpeed.ToString("0.0") + "x";

    public string PreviewDragModeButtonText => PreviewDragModeEnabled ? "Move: ON" : "Move: OFF";

    public bool HasPropOverlayLoaded => loadedPropOverlayBitmap != null;

    public string PropOverlaySummaryText =>
        !PropOverlayEnabled || loadedPropOverlayBitmap == null
            ? "None"
            : PropOverlayFileName +
              " | X " + PropOverlayOffsetX +
              " | Y " + PropOverlayOffsetY +
              " | " + PropOverlayScalePercent.ToString("0") + "%" +
              " | " + PropOverlayRotationDegrees.ToString("0") + "°" +
              (PropOverlayFlipHorizontal ? " | FlipH" : string.Empty) +
              (PropOverlayFlipVertical ? " | FlipV" : string.Empty) +
              " | " + (PropOverlayDrawOrderIndex == 0 ? "Behind" : "Front");

    public bool HasCompareOverlayTarget => CompareSelectedAnimation != null;

    public string CompareOverlayDragModeButtonText => CompareOverlayDragModeEnabled ? "Compare Drag: On" : "Compare Drag: Off";

    public bool CanApplyCompareOverlay =>
    CompareOverlayEnabled &&
    CompareSelectedAnimation != null &&
    editableFrames.Count > 0;

    public string CompareOverlayTargetText =>
    CompareSelectedAnimation == null
        ? "None"
        : CompareSelectedAnimation.DisplayName;

    public string CompareOverlaySummaryText =>
    !CompareOverlayEnabled || CompareSelectedAnimation == null
        ? "None"
        : CompareSelectedAnimation.DisplayName +
          " | A " + CompareOverlayActionIndex +
          " | D " + Math.Clamp(CompareOverlayDirectionIndex, 0, 4) +
          " | " + CompareOverlayFrameModeText +
          " | X " + CompareOverlayOffsetX +
          " | Y " + CompareOverlayOffsetY +
          " | " + CompareOverlayOpacityPercent.ToString("0") + "%";

    public string CompareOverlayFrameModeText =>
        CompareOverlaySyncMode switch
        {
            "Same Frame" => "Same Frame",
            "Loop Secondary" => "Loop Secondary",
            "Clamp Secondary" => "Clamp Secondary",
            "Manual Frame" => "Manual Frame " + Math.Max(0, CompareOverlayFrameIndex),
            _ => CompareOverlaySyncMode
        };

    public bool IsCompareOverlayManualFrameMode =>
    string.Equals(CompareOverlaySyncMode, "Manual Frame", StringComparison.Ordinal);

    public string CompareOverlayOffsetText =>
        "X " + CompareOverlayOffsetX + " | Y " + CompareOverlayOffsetY;

    public string CompareOverlayOpacityText =>
        CompareOverlayOpacityPercent.ToString("0") + "%";

    public bool HasCompareAnimationEntries => CompareAnimationEntries.Count > 0;

    public string CompareSelectedAnimationDisplayText =>
        CompareSelectedAnimation?.DisplayName ?? "None";

    public string CompareOverlayFileText =>
    string.IsNullOrWhiteSpace(CompareSelectedAnimation?.SourceFile)
        ? "None"
        : CompareSelectedAnimation.SourceFile;

    public bool HasPropPoseForCurrentFrame =>
    currentFrameIndex >= 0 && propFramePoses.ContainsKey(currentFrameIndex);

    public string CurrentPropPoseText =>
        currentFrameIndex < 0
            ? "No frame"
            : propFramePoses.TryGetValue(currentFrameIndex, out PropFramePose? pose)
                ? "Frame Pose | X " + pose.OffsetX +
                  " | Y " + pose.OffsetY +
                  " | Rot " + pose.RotationDegrees.ToString("0") + "°" +
                  (pose.FlipHorizontal ? " | FlipH" : string.Empty) +
                  (pose.FlipVertical ? " | FlipV" : string.Empty)
                : "Frame Pose | None";

    public bool ShowCompareSideBySidePreview =>
        ShowAnimationView &&
        CompareSideBySideEnabled &&
        CompareSelectedAnimation != null;

    public double ComparePreviewImageWidth =>
        CompareSidePreviewBitmap != null ? CompareSidePreviewBitmap.PixelSize.Width * PreviewScale : 0;

    public double ComparePreviewImageHeight =>
        CompareSidePreviewBitmap != null ? CompareSidePreviewBitmap.PixelSize.Height * PreviewScale : 0;

    public string CompareSidePreviewInfoText
    {
        get
        {
            if (CompareSelectedAnimation == null)
            {
                return "No compare animation selected.";
            }

            if (CompareSidePreviewBitmap == null)
            {
                return "No compare frame loaded.";
            }

            return
                "Compare: " + CompareSelectedAnimation.DisplayName +
                " | " + CompareOverlayFrameModeText;
        }
    }

    public bool ShowSinglePreviewOnly =>
    !ShowCompareSideBySidePreview;

    public bool ShowSplitPreview =>
        ShowCompareSideBySidePreview;

    public bool HasComparePoseForCurrentFrame =>
        currentFrameIndex >= 0 && compareFramePoses.ContainsKey(GetComparePoseKey(currentFrameIndex));

    public string CurrentComparePoseText =>
        currentFrameIndex < 0
            ? "No frame"
            : compareFramePoses.TryGetValue(GetComparePoseKey(currentFrameIndex), out CompareFramePose? pose)
                ? "Frame Pose | X " + pose.OffsetX + " | Y " + pose.OffsetY
                : "Frame Pose | None";

    public string PropOverlaySourceText => string.IsNullOrWhiteSpace(PropOverlayFileName) ? "None" : PropOverlayFileName;

    public string PropOverlayOffsetText => "X " + PropOverlayOffsetX + " | Y " + PropOverlayOffsetY;

    public string PropOverlayScaleText => PropOverlayScalePercent.ToString("0") + "%";

    public string PropOverlayRotationText => PropOverlayRotationDegrees.ToString("0.#") + "°";

    public string PropOverlayLayerText => PropOverlayDrawOrderIndex == 1 ? "Front" : "Behind";

    public string PropOverlayPivotText => "X " + PropOverlayPivotX + " | Y " + PropOverlayPivotY;

    public bool ShowCompareSelectorBar => CompareOverlayEnabled || CompareSideBySideEnabled;

    public string CompareTargetHeaderText =>  ShowCompareSelectorBar ? "Compare" : "Compare Target (Off)";

    partial void OnCompareSideBySideEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCompareSideBySidePreview));
        OnPropertyChanged(nameof(ShowSinglePreviewOnly));
        OnPropertyChanged(nameof(ShowSplitPreview));
        OnPropertyChanged(nameof(ShowCompareSelectorBar));
        OnPropertyChanged(nameof(CompareTargetHeaderText));
        OnPropertyChanged(nameof(CompareSidePreviewInfoText));
        RefreshLivePreviewImage();
    }

    partial void OnCompareSidePreviewBitmapChanged(WriteableBitmap? value)
    {
        OnPropertyChanged(nameof(ComparePreviewImageWidth));
        OnPropertyChanged(nameof(ComparePreviewImageHeight));
        OnPropertyChanged(nameof(CompareSidePreviewInfoText));
    }

    partial void OnPropOverlayEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(PropOverlaySummaryText));
        OnPropertyChanged(nameof(PropOverlaySourceText));
        RefreshLivePreviewImage();
    }

    partial void OnPropOverlayFileNameChanged(string value)
    {
        OnPropertyChanged(nameof(PropOverlaySummaryText));
        OnPropertyChanged(nameof(PropOverlaySourceText));
    }

    partial void OnPropOverlayDrawOrderIndexChanged(int value)
    {
        OnPropertyChanged(nameof(PropOverlaySummaryText));
        OnPropertyChanged(nameof(PropOverlayLayerText));
        RefreshLivePreviewImage();
    }

    partial void OnPropOverlayFlipHorizontalChanged(bool value)
    {
        OnPropertyChanged(nameof(PropOverlaySummaryText));
        OnPropertyChanged(nameof(CurrentPropPoseText));
        RefreshLivePreviewImage();
    }

    partial void OnPropOverlayFlipVerticalChanged(bool value)
    {
        OnPropertyChanged(nameof(PropOverlaySummaryText));
        OnPropertyChanged(nameof(CurrentPropPoseText));
        RefreshLivePreviewImage();
    }

    partial void OnPropOverlayOffsetXChanged(int value)
    {
        OnPropertyChanged(nameof(PropOverlaySummaryText));
        OnPropertyChanged(nameof(PropOverlayOffsetText));
        OnPropertyChanged(nameof(PropOverlayOffsetXValue));
        OnPropertyChanged(nameof(CurrentPropPoseText));
        RefreshLivePreviewImage();
    }

    partial void OnPropOverlayOffsetYChanged(int value)
    {
        OnPropertyChanged(nameof(PropOverlaySummaryText));
        OnPropertyChanged(nameof(PropOverlayOffsetText));
        OnPropertyChanged(nameof(PropOverlayOffsetYValue));
        OnPropertyChanged(nameof(CurrentPropPoseText));
        RefreshLivePreviewImage();
    }

    partial void OnPropOverlayScalePercentChanged(double value)
    {
        OnPropertyChanged(nameof(PropOverlaySummaryText));
        OnPropertyChanged(nameof(PropOverlayScaleText));
        OnPropertyChanged(nameof(PropOverlayScalePercentValue));
        RefreshLivePreviewImage();
    }

    partial void OnPropOverlayRotationDegreesChanged(double value)
    {
        OnPropertyChanged(nameof(PropOverlaySummaryText));
        OnPropertyChanged(nameof(PropOverlayRotationText));
        OnPropertyChanged(nameof(PropOverlayRotationDegreesValue));
        OnPropertyChanged(nameof(CurrentPropPoseText));
        RefreshLivePreviewImage();
    }

    partial void OnPropOverlayPivotXChanged(int value)
    {
        OnPropertyChanged(nameof(PropOverlayPivotText));
        OnPropertyChanged(nameof(PropOverlayPivotXValue));
        RefreshLivePreviewImage();
    }

    partial void OnPropOverlayPivotYChanged(int value)
    {
        OnPropertyChanged(nameof(PropOverlayPivotText));
        OnPropertyChanged(nameof(PropOverlayPivotYValue));
        RefreshLivePreviewImage();
    }

    partial void OnCompareOverlayEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CompareOverlaySummaryText));
        OnPropertyChanged(nameof(CanApplyCompareOverlay));
        OnPropertyChanged(nameof(ShowCompareSelectorBar));
        OnPropertyChanged(nameof(CompareTargetHeaderText));
        OnPropertyChanged(nameof(ShowCompareSideBySidePreview));
        OnPropertyChanged(nameof(ShowSinglePreviewOnly));
        OnPropertyChanged(nameof(ShowSplitPreview));
        OnPropertyChanged(nameof(CompareSidePreviewInfoText));
        RefreshLivePreviewImage();
    }

    partial void OnCompareOverlayActionIndexChanged(int value)
    {
        string? matchingLabel = CompareActionOptions.FirstOrDefault(x =>
    compareActionNameToIndex.TryGetValue(x, out int idx) && idx == CompareOverlayActionIndex);

        if (!string.IsNullOrWhiteSpace(matchingLabel) &&
            !string.Equals(CompareSelectedAction, matchingLabel, StringComparison.Ordinal))
        {
            CompareSelectedAction = matchingLabel;
        }
        InvalidateCompareOverlayCache();
        OnPropertyChanged(nameof(CompareOverlaySummaryText));
        OnPropertyChanged(nameof(CompareOverlayFrameModeText));
        RefreshLivePreviewImage();
    }

    partial void OnCompareOverlayDirectionIndexChanged(int value)
    {
        if (CompareOverlayDirectionIndex < 0)
        {
            CompareOverlayDirectionIndex = 0;
            return;
        }

        if (CompareOverlayDirectionIndex > 4)
        {
            CompareOverlayDirectionIndex = 4;
            return;
        }

        string? matchingLabel = CompareDirectionOptions.FirstOrDefault(x =>
    compareDirectionNameToIndex.TryGetValue(x, out int idx) && idx == CompareOverlayDirectionIndex);

        if (!string.IsNullOrWhiteSpace(matchingLabel) &&
            !string.Equals(CompareSelectedDirection, matchingLabel, StringComparison.Ordinal))
        {
            CompareSelectedDirection = matchingLabel;
        }

        InvalidateCompareOverlayCache();
        OnPropertyChanged(nameof(CompareOverlaySummaryText));
        OnPropertyChanged(nameof(CompareOverlayFrameModeText));
        RefreshLivePreviewImage();
    }

    partial void OnCompareOverlayFrameIndexChanged(int value)
    {
        if (CompareOverlayFrameIndex < 0)
        {
            CompareOverlayFrameIndex = 0;
            return;
        }

        OnPropertyChanged(nameof(CompareOverlaySummaryText));
        OnPropertyChanged(nameof(CompareOverlayFrameModeText));
        RefreshLivePreviewImage();
    }

    partial void OnCompareOverlaySyncModeChanged(string value)
    {
        OnPropertyChanged(nameof(CompareOverlaySummaryText));
        OnPropertyChanged(nameof(CompareOverlayFrameModeText));
        OnPropertyChanged(nameof(IsCompareOverlayManualFrameMode));
        RefreshLivePreviewImage();
    }

    partial void OnCompareOverlayOffsetXChanged(int value)
    {
        OnPropertyChanged(nameof(CompareOverlayOffsetXText));
        OnPropertyChanged(nameof(CompareOverlaySummaryText));
        OnPropertyChanged(nameof(CompareOverlayOffsetText));
        OnPropertyChanged(nameof(CurrentComparePoseText));
        RefreshLivePreviewImage();
    }

    partial void OnCompareOverlayOffsetYChanged(int value)
    {
        OnPropertyChanged(nameof(CompareOverlayOffsetYText));
        OnPropertyChanged(nameof(CompareOverlaySummaryText));
        OnPropertyChanged(nameof(CompareOverlayOffsetText));
        OnPropertyChanged(nameof(CurrentComparePoseText));
        RefreshLivePreviewImage();
    }

    partial void OnCompareOverlayOpacityPercentChanged(double value)
    {
        if (CompareOverlayOpacityPercent < 0)
        {
            CompareOverlayOpacityPercent = 0;
            return;
        }

        if (CompareOverlayOpacityPercent > 100)
        {
            CompareOverlayOpacityPercent = 100;
            return;
        }

        OnPropertyChanged(nameof(CompareOverlayOpacityPercentText));
        OnPropertyChanged(nameof(CompareOverlaySummaryText));
        OnPropertyChanged(nameof(CompareOverlayOpacityText));
        RefreshLivePreviewImage();
    }

    partial void OnCompareSelectedAnimationFileChanged(string? value)
    {
        ApplyCompareAnimationFilters();
        InvalidateCompareOverlayCache();
        RefreshLivePreviewImage();
    }

    partial void OnCompareSelectedAnimationChanged(AnimationEntry? value)
    {
        InvalidateCompareOverlayCache();

        RebuildCompareActionListForBody(value);
        RebuildCompareDirectionList();

        OnPropertyChanged(nameof(CompareSelectedAnimationDisplayText));
        OnPropertyChanged(nameof(CompareOverlayTargetText));
        OnPropertyChanged(nameof(HasCompareOverlayTarget));
        OnPropertyChanged(nameof(CanApplyCompareOverlay));
        OnPropertyChanged(nameof(ShowCompareSideBySidePreview));
        OnPropertyChanged(nameof(ShowSinglePreviewOnly));
        OnPropertyChanged(nameof(ShowSplitPreview));
        OnPropertyChanged(nameof(CompareSidePreviewInfoText));

        RefreshLivePreviewImage();
    }

    partial void OnCompareSelectedActionChanged(string? value)
    {
        int selectedIndex = GetSelectedCompareActionIndex();

        if (CompareOverlayActionIndex != selectedIndex)
        {
            CompareOverlayActionIndex = selectedIndex;
            return;
        }

        InvalidateCompareOverlayCache();
        RefreshLivePreviewImage();
    }

    partial void OnCompareSelectedDirectionChanged(string? value)
    {
        int selectedIndex = GetSelectedCompareDirectionIndex();

        if (CompareOverlayDirectionIndex != selectedIndex)
        {
            CompareOverlayDirectionIndex = selectedIndex;
            return;
        }

        InvalidateCompareOverlayCache();
        RefreshLivePreviewImage();
    }

    public int? PropOverlayOffsetXValue
    {
        get => PropOverlayOffsetX;
        set
        {
            PropOverlayOffsetX = value ?? 0;
        }
    }

    public int? PropOverlayOffsetYValue
    {
        get => PropOverlayOffsetY;
        set
        {
            PropOverlayOffsetY = value ?? 0;
        }
    }

    public double? PropOverlayScalePercentValue
    {
        get => PropOverlayScalePercent;
        set
        {
            PropOverlayScalePercent = value ?? 100.0;
        }
    }

    public double? PropOverlayRotationDegreesValue
    {
        get => PropOverlayRotationDegrees;
        set
        {
            PropOverlayRotationDegrees = value ?? 0.0;
        }
    }

    public int? PropOverlayPivotXValue
    {
        get => PropOverlayPivotX;
        set
        {
            PropOverlayPivotX = value ?? 0;
        }
    }

    public int? PropOverlayPivotYValue
    {
        get => PropOverlayPivotY;
        set
        {
            PropOverlayPivotY = value ?? 0;
        }
    }

    public double? LivePreviewSharpenAmountValue
    {
        get => LivePreviewSharpenAmount;
        set
        {
            LivePreviewSharpenAmount = value ?? 1.0;
        }
    }

    public double? LivePreviewContrastAmountValue
    {
        get => LivePreviewContrastAmount;
        set
        {
            LivePreviewContrastAmount = value ?? 0.20;
        }
    }

    public double? LivePreviewOutlineStrengthValue
    {
        get => LivePreviewOutlineStrength;
        set
        {
            LivePreviewOutlineStrength = value ?? 0.35;
        }
    }

    public int SelectedDirectionSliderValue
    {
        get
        {
            return GetSelectedDirectionIndex();
        }
        set
        {
            int clamped = Math.Clamp(value, 0, 4);

            string? matchingDirection = DirectionOptions.FirstOrDefault(x =>
                directionNameToIndex.TryGetValue(x, out int idx) && idx == clamped);

            if (!string.IsNullOrWhiteSpace(matchingDirection) &&
                !string.Equals(SelectedDirection, matchingDirection, StringComparison.Ordinal))
            {
                SelectedDirection = matchingDirection;
                OnPropertyChanged(nameof(SelectedDirectionSliderValue));
            }
        }
    }

    public string LivePreviewHueText =>
        LivePreviewHueEnabled && LivePreviewSelectedHue != null
            ? LivePreviewSelectedHue.DisplayText
            : "None";

    public string LivePreviewScaleText =>
        LivePreviewScalePercent.ToString("0") + "% / " + LivePreviewResizeSampler;

    public bool HasAnyUnsavedChanges => HasUnsavedChanges || hasFrameEdits;

    public bool CanSaveChanges => HasAnyUnsavedChanges;

    public string UnsavedChangesText => HasAnyUnsavedChanges ? "Unsaved changes" : string.Empty;

    public bool HasFrameThumbnails => FrameThumbnails.Count > 0;

    public string CurrentFrameDisplayText => decodedFrames.Count == 0 ? "Frame: - / -" : "Frame: " + (currentFrameIndex + 1) + " / " + decodedFrames.Count;

    public bool HasFrameEdits => hasFrameEdits;

    public bool ShowUopSequenceDetails => ShowAnimationView && currentResolvedAnimationBlock != null && currentResolvedAnimationBlock.IsUop;

    public IEnumerable<HueDataService.HueEntry> CachedHueEntries => cachedHueEntries;

    partial void OnLivePreviewResizeSamplerChanged(ResizeSamplerMode value)
    {
        OnPropertyChanged(nameof(LivePreviewScaleText));
        RefreshLivePreviewImage();
    }

    partial void OnLivePreviewHueEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(LivePreviewHueText));
        RefreshLivePreviewImage();
    }

    partial void OnLivePreviewScalePercentChanged(double value)
    {
        OnPropertyChanged(nameof(LivePreviewScaleText));
        RefreshLivePreviewImage();
    }

    partial void OnLivePreviewSelectedHueChanged(HueDataService.HueEntry? value)
    {
        OnPropertyChanged(nameof(LivePreviewHueText));
        RefreshLivePreviewImage();
    }

    partial void OnLivePreviewSharpenEnabledChanged(bool value) => RefreshLivePreviewImage();
    partial void OnLivePreviewContrastEnabledChanged(bool value) => RefreshLivePreviewImage();
    partial void OnLivePreviewOutlineEnabledChanged(bool value) => RefreshLivePreviewImage();
    partial void OnLivePreviewSharpenAmountChanged(double value) => RefreshLivePreviewImage();
    partial void OnLivePreviewContrastAmountChanged(double value) => RefreshLivePreviewImage();
    partial void OnLivePreviewOutlineStrengthChanged(double value) => RefreshLivePreviewImage();
    partial void OnLivePreviewSharpenModeIndexChanged(int value) => RefreshLivePreviewImage();

    public string SelectedMulSlotFile => SelectedMulSlot?.FileName ?? "-";
    public string SelectedMulSlotIndex => SelectedMulSlot != null ? SelectedMulSlot.BodyIndex.ToString() : "-";
    public string SelectedMulSlotOffset => SelectedMulSlot != null ? SelectedMulSlot.TrueBodyId.ToString() : "-";
    public string SelectedMulSlotLength => SelectedMulSlot != null ? SelectedMulSlot.AnimLength.ToString() : "-";
    public string SelectedMulSlotExtra => SelectedMulSlot != null ? SelectedMulSlot.TypeLetter : "-";

    public double PreviewScale => ZoomLevel;

    public string SelectedBlockSize => SelectedBlockSizeText;
    public string SelectedBlockHeader => SelectedBlockHeaderText;

    public string SelectedIndexNumber => SelectedAnimation != null ? SelectedAnimation.IndexNumber.ToString() : "-";
    public string SelectedOffset => SelectedAnimation != null ? SelectedAnimation.Offset.ToString() : "-";
    public string SelectedLength => SelectedAnimation != null ? SelectedAnimation.Length.ToString() : "-";
    public string SelectedExtra => SelectedAnimation != null ? SelectedAnimation.Extra.ToString() : "-";

    public string SelectedUopBodyFile => SelectedUopBodySlot?.FileName ?? "-";
    public string SelectedUopBodyId => SelectedUopBodySlot != null ? SelectedUopBodySlot.BodyId.ToString() : "-";
    public string SelectedUopBodyType => SelectedUopBodySlot?.BodyType ?? "-";
    public string SelectedUopBodyActionCount => SelectedUopBodySlot != null ? SelectedUopBodySlot.ActionCount.ToString() : "-";

    public string SelectedSequenceRequestedAction =>
        currentResolvedAnimationBlock != null && currentResolvedAnimationBlock.IsUop
            ? currentResolvedAnimationBlock.RequestedActionIndex.ToString()
            : "-";

    public string SelectedSequenceResolvedGroup =>
        currentResolvedAnimationBlock != null && currentResolvedAnimationBlock.IsUop
            ? currentResolvedAnimationBlock.ResolvedUopGroupIndex.ToString()
            : "-";

    public string SelectedSequenceFrameCount =>
        currentResolvedAnimationBlock != null &&
        currentResolvedAnimationBlock.IsUop &&
        currentResolvedAnimationBlock.SequenceFrameCount >= 0
            ? currentResolvedAnimationBlock.SequenceFrameCount.ToString()
            : "-";

    public string SelectedSequenceRemap =>
        currentResolvedAnimationBlock != null && currentResolvedAnimationBlock.IsUop
            ? (currentResolvedAnimationBlock.UsedSequenceRemap
                ? currentResolvedAnimationBlock.RequestedActionIndex + " -> " + currentResolvedAnimationBlock.ResolvedUopGroupIndex
                : "No")
            : "-";

    public string SelectedSequenceBodyMapping =>
        currentResolvedAnimationBlock != null && currentResolvedAnimationBlock.IsUop
            ? (currentResolvedAnimationBlock.BodyId == currentResolvedAnimationBlock.ResolvedBodyId
                ? currentResolvedAnimationBlock.BodyId.ToString()
                : currentResolvedAnimationBlock.BodyId + " -> " + currentResolvedAnimationBlock.ResolvedBodyId)
            : "-";

    public string SelectedUopVirtualPathDisplay =>
        currentResolvedAnimationBlock != null && currentResolvedAnimationBlock.IsUop
            ? currentResolvedAnimationBlock.UopVirtualPath
            : "-";

    public bool ShowMulSlotList =>
        ShowMulSlotView &&
        !IsSelectedAnimationFileUop();

    public bool ShowUopSlotList =>
        ShowMulSlotView &&
        IsSelectedAnimationFileUop();

    public string SlotPaneTitle =>
        ShowMulSlotView
            ? (IsSelectedAnimationFileUop() ? "UOP Body Targets" : "BODY IDS")
            : "Animations";

    public string PreviewInfoText
    {
        get
        {
            if (ShowMulSlotView)
            {
                if (IsSelectedAnimationFileUop())
                {
                    if (SelectedUopBodySlot == null)
                    {
                        return "No UOP body target selected";
                    }

                    return "Selected UOP Body: " + SelectedUopBodySlot.DisplayText;
                }

                if (SelectedMulSlot == null)
                {
                    return "No MUL slot selected";
                }

                return "Selected Free Body ID: " + SelectedMulSlot.DisplayText;
            }

            if (SelectedAnimation == null)
            {
                return "No animation selected";
            }

            return "Selected: " + SelectedAnimation.DisplayName + "   |   Frames: " + SelectedAnimation.FrameCount;
        }
    }

    public IBrush PreviewBackgroundBrush
    {
        get
        {
            return ShowCheckerBackground
                ? new SolidColorBrush(Avalonia.Media.Color.Parse("#20242B"))
                : new SolidColorBrush(Avalonia.Media.Color.Parse("#111317"));
        }
    }

    public string SelectedAnimationName
    {
        get
        {
            if (SelectedAnimation == null)
            {
                return "None";
            }

            if (AnimationBrowserShowNames &&
                animationBrowserNamesByBodyId.TryGetValue(SelectedAnimation.BodyId, out string? name) &&
                !string.IsNullOrWhiteSpace(name))
            {
                return "Body " + SelectedAnimation.BodyId + " - " + name;
            }

            return SelectedAnimation.DisplayName;
        }
    }
    public string SelectedBodyId => SelectedAnimation != null ? SelectedAnimation.BodyId.ToString() : "-";
    public string SelectedActionId => SelectedAnimation != null ? SelectedAnimation.ActionId.ToString() : "-";
    public string SelectedFrameCount => SelectedAnimation != null ? SelectedAnimation.FrameCount.ToString() : "-";
    public string SelectedFrameSize => SelectedAnimation?.FrameSize ?? "-";
    public string SelectedAnimationSource => SelectedAnimation?.SourceFile ?? "-";

    public double PreviewImageWidth
    {
        get
        {
            return PreviewBitmap != null ? PreviewBitmap.PixelSize.Width * PreviewScale : 0;
        }
    }

    public double PreviewImageHeight
    {
        get
        {
            return PreviewBitmap != null ? PreviewBitmap.PixelSize.Height * PreviewScale : 0;
        }
    }

    partial void OnPreviewDragModeEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(PreviewDragModeButtonText));

        if (!value)
        {
            EndPreviewDrag();
            StatusText = "Preview drag mode disabled.";
        }
        else
        {
            StatusText = "Preview drag mode enabled. Drag preview to move frames.";
        }
    }

    partial void OnCompareOverlayDragModeEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CompareOverlayDragModeButtonText));

        if (!value)
        {
            EndCompareOverlayDrag();

            if (!PreviewDragModeEnabled)
            {
                StatusText = "Compare overlay drag disabled.";
            }
        }
        else
        {
            if (PreviewDragModeEnabled)
            {
                PreviewDragModeEnabled = false;
            }

            StatusText = "Compare overlay drag enabled. Drag preview to align compare layer.";
        }
    }

    partial void OnShowCheckerBackgroundChanged(bool value)
    {
        OnPropertyChanged(nameof(PreviewBackgroundBrush));
    }

    partial void OnPlaybackSpeedChanged(double value)
    {
        OnPropertyChanged(nameof(PlaybackSpeedText));

        if (playbackTimer != null && playbackTimer.IsEnabled)
        {
            double framesPerSecond = value;
            if (framesPerSecond < 1)
            {
                framesPerSecond = 1;
            }

            playbackTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / framesPerSecond);
        }
    }

    partial void OnZoomLevelChanged(double value)
    {
        OnPropertyChanged(nameof(PreviewScale));
        OnPropertyChanged(nameof(ZoomText));
        OnPropertyChanged(nameof(ComparePreviewImageWidth));
        OnPropertyChanged(nameof(ComparePreviewImageHeight));
        SaveActiveProfileUiState();
    }

    partial void OnPreviewBitmapChanged(WriteableBitmap? value)
    {
        OnPropertyChanged(nameof(PreviewImageWidth));
        OnPropertyChanged(nameof(PreviewImageHeight));
        OnPropertyChanged(nameof(CurrentFrameDisplayText));
    }
}
