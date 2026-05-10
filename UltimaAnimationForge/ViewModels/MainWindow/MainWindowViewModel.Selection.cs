using System;
using System.Collections.Generic;
using System.Linq;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    private void SelectDirectionByIndex(int directionIndex)
    {
        int clamped = Math.Clamp(directionIndex, 0, 4);

        string? matchingDirection = DirectionOptions.FirstOrDefault(x =>
            directionNameToIndex.TryGetValue(x, out int idx) && idx == clamped);

        if (!string.IsNullOrWhiteSpace(matchingDirection))
        {
            SelectedDirection = matchingDirection;
        }
    }

    partial void OnSelectedAnimationChanged(AnimationEntry? value)
    {
        OnPropertyChanged(nameof(PreviewInfoText));
        OnPropertyChanged(nameof(SelectedAnimationName));
        OnPropertyChanged(nameof(SelectedBodyId));
        OnPropertyChanged(nameof(SelectedActionId));
        OnPropertyChanged(nameof(SelectedFrameCount));
        OnPropertyChanged(nameof(SelectedFrameSize));
        OnPropertyChanged(nameof(SelectedAnimationSource));
        OnPropertyChanged(nameof(SelectedIndexNumber));
        OnPropertyChanged(nameof(SelectedOffset));
        OnPropertyChanged(nameof(SelectedLength));
        OnPropertyChanged(nameof(SelectedExtra));
        OnPropertyChanged(nameof(SelectedBlockSize));
        OnPropertyChanged(nameof(SelectedBlockHeader));
        OnPropertyChanged(nameof(SelectedSequenceRequestedAction));
        OnPropertyChanged(nameof(SelectedSequenceResolvedGroup));
        OnPropertyChanged(nameof(SelectedSequenceFrameCount));
        OnPropertyChanged(nameof(SelectedSequenceRemap));
        OnPropertyChanged(nameof(SelectedSequenceBodyMapping));
        OnPropertyChanged(nameof(SelectedUopVirtualPathDisplay));

        ClearImportedSpriteSheetSession();

        bool resumePlayback = WasPlaybackRunning();

        if (playbackTimer != null)
        {
            playbackTimer.Stop();
        }

        if (!QueueCurrentEditedMulAnimation())
        {
            return;
        }

        ClearDecodedFramesAndThumbnails();

        if (value == null)
        {
            return;
        }

        int bodyId = GetEffectiveSelectedBodyId(value);
        if (bodyId >= 0)
        {
            RebuildActionListForBody(bodyId);
            RebuildDirectionList();

            int actionToLoad = value.ActionId >= 0 ? value.ActionId : GetSelectedActionIndex();

            if (TryLoadBodyActionDirection(bodyId, actionToLoad, GetSelectedDirectionIndex()))
            {
                LoadSelectedAnimationBlock();
                ResumePlaybackIfNeeded(resumePlayback);
            }
        }
        else
        {
            LoadSelectedAnimationBlock();
            ResumePlaybackIfNeeded(resumePlayback);
        }
        SyncAnimationBrowserSelection(value);
        detachedPreviewViewModel?.SyncFromMainIfFollowing();
    }

    private void SyncAnimationBrowserSelection(AnimationEntry? entry)
    {
        if (entry == null || AnimationBrowserTiles.Count == 0)
        {
            return;
        }

        syncingAnimationBrowserSelection = true;

        SelectedAnimationBrowserTile = AnimationBrowserTiles.FirstOrDefault(x =>
            x.SourceEntry != null &&
            x.SourceEntry.BodyId == entry.BodyId &&
            string.Equals(x.SourceEntry.SourceFile, entry.SourceFile, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.SourceEntry.SourceMode, entry.SourceMode, StringComparison.OrdinalIgnoreCase));

        syncingAnimationBrowserSelection = false;
    }

    partial void OnMulSlotShowHFilterChanged(bool value)
    {
        ApplyMulSlotFilters();
    }

    partial void OnMulSlotShowLFilterChanged(bool value)
    {
        ApplyMulSlotFilters();
    }

    partial void OnMulSlotShowPFilterChanged(bool value)
    {
        ApplyMulSlotFilters();
    }

    partial void OnSelectedActionChanged(string? value)
    {
        if (suppressActionReload)
        {
            return;
        }

        if (ShowMulSlotView)
        {
            if (hasImportedSpriteSheetSession)
            {
                int actionIndex = GetSelectedActionIndex();
                importedSpriteSheetLastActionIndex = actionIndex;

                int directionIndex = GetSelectedDirectionIndex();

                if (TryGetImportedSpriteSheetDirectionsForAction(actionIndex, out _) &&
                    HasImportedSpriteSheetPreviewForCurrentSelection())
                {
                    if (playbackTimer != null)
                    {
                        playbackTimer.Stop();
                    }

                    LoadImportedSpriteSheetDirectionPreview(directionIndex);

                    StatusText =
                        "Showing imported sprite sheet action " + actionIndex +
                        ", direction " + directionIndex +
                        " from " + importedSpriteSheetSourceName + ".";

                    return;
                }
            }

            LoadSelectedMulSlot();
            return;
        }

        if (SelectedAnimation == null)
        {
            return;
        }

        int bodyId = GetEffectiveSelectedBodyId(SelectedAnimation);
        if (bodyId < 0)
        {
            return;
        }

        bool resumePlayback = WasPlaybackRunning();

        if (playbackTimer != null)
        {
            playbackTimer.Stop();
        }

        if (!QueueCurrentEditedMulAnimation())
        {
            return;
        }

        ClearDecodedFramesAndThumbnails();

        if (TryLoadBodyActionDirection(bodyId, GetSelectedActionIndex(), GetSelectedDirectionIndex()))
        {
            LoadSelectedAnimationBlock();
            ResumePlaybackIfNeeded(resumePlayback);
        }
        detachedPreviewViewModel?.SyncFromMainIfFollowing();
    }

    partial void OnSelectedDirectionChanged(string? value)
    {
        OnPropertyChanged(nameof(SelectedDirectionSliderValue));

        if (hasImportedSpriteSheetSession)
        {
            int actionIndex = GetSelectedActionIndex();
            int directionIndex = GetSelectedDirectionIndex();

            if (TryGetImportedSpriteSheetDirectionsForAction(actionIndex, out Dictionary<int, List<VdFrameData>> directions) &&
                directions.ContainsKey(directionIndex))
            {
                if (playbackTimer != null)
                {
                    playbackTimer.Stop();
                }

                LoadImportedSpriteSheetDirectionPreview(directionIndex);

                StatusText =
                    "Showing imported sprite sheet action " + actionIndex +
                    ", direction " + directionIndex +
                    " from " + importedSpriteSheetSourceName + ".";

                return;
            }
        }

        if (suppressDirectionReload)
        {
            return;
        }

        SaveActiveProfileUiState();

        if (ShowMulSlotView)
        {
            LoadSelectedMulSlot();
            return;
        }

        if (SelectedAnimation == null)
        {
            return;
        }

        int bodyId = GetEffectiveSelectedBodyId(SelectedAnimation);
        if (bodyId < 0)
        {
            return;
        }

        bool resumePlayback = WasPlaybackRunning();

        if (playbackTimer != null)
        {
            playbackTimer.Stop();
        }

        if (!QueueCurrentEditedMulAnimation())
        {
            return;
        }

        ClearDecodedFramesAndThumbnails();

        if (TryLoadBodyActionDirection(bodyId, GetSelectedActionIndex(), GetSelectedDirectionIndex()))
        {
            LoadSelectedAnimationBlock();
            ResumePlaybackIfNeeded(resumePlayback);
        }
        detachedPreviewViewModel?.SyncFromMainIfFollowing();
    }

    partial void OnSelectedBodyTypeChanged(string? value)
    {
        ApplyAnimationFilters();
        SaveActiveProfileUiState();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyAnimationFilters();
        ApplyMulSlotFilters();
        ApplyUopBodyFilters();
        SaveActiveProfileUiState();
    }

    partial void OnShowMulSlotViewChanged(bool value)
    {
        if (!QueueCurrentEditedMulAnimation())
        {
            return;
        }

        OnPropertyChanged(nameof(ShowAnimationView));
        OnPropertyChanged(nameof(ShowSlotView));
        OnPropertyChanged(nameof(ShowAnimationOnlyControls));
        OnPropertyChanged(nameof(ShowMulActionDirectionControls));
        OnPropertyChanged(nameof(ShowActionDirectionControls));

        ApplyAnimationFilters();

        if (value)
        {
            if (!IsSelectedAnimationFileUop())
            {
                EnsureMulSlotEntriesLoaded();
            }

            ApplyMulSlotFilters();
            ApplyUopBodyFilters();

            SelectedAnimation = null;
            ClearDecodedFramesAndThumbnails();
        }
        else
        {
            SelectedMulSlot = null;
            SelectedUopBodySlot = null;

            ApplyMulSlotFilters();
            ApplyUopBodyFilters();
        }

        OnPropertyChanged(nameof(ShowMulSlotList));
        OnPropertyChanged(nameof(ShowUopSlotList));
        OnPropertyChanged(nameof(SlotPaneTitle));
    }

    partial void OnSelectedMulSlotChanged(MulSlotEntry? value)
    {
        if (value == null)
        {
            return;
        }

        if (!ShowMulSlotView)
        {
            return;
        }

        if (playbackTimer != null)
        {
            playbackTimer.Stop();
        }

        ClearImportedSpriteSheetSession();
        ClearDecodedFramesAndThumbnails();

        RebuildActionListForSelectedMulSlot();
        RebuildDirectionList();

        OnPropertyChanged(nameof(SelectedMulSlotFile));
        OnPropertyChanged(nameof(SelectedMulSlotIndex));
        OnPropertyChanged(nameof(SelectedMulSlotOffset));
        OnPropertyChanged(nameof(SelectedMulSlotLength));
        OnPropertyChanged(nameof(SelectedMulSlotExtra));
        OnPropertyChanged(nameof(PreviewInfoText));

        LoadSelectedMulSlot();
    }

    partial void OnSelectedUopBodySlotChanged(UopBodySlotEntry? value)
    {
        if (value == null)
        {
            return;
        }

        if (!ShowMulSlotView || !IsSelectedAnimationFileUop())
        {
            return;
        }

        if (playbackTimer != null)
        {
            playbackTimer.Stop();
        }

        ClearDecodedFramesAndThumbnails();

        OnPropertyChanged(nameof(SelectedUopBodyFile));
        OnPropertyChanged(nameof(SelectedUopBodyId));
        OnPropertyChanged(nameof(SelectedUopBodyType));
        OnPropertyChanged(nameof(SelectedUopBodyActionCount));
        OnPropertyChanged(nameof(PreviewInfoText));

        LoadSelectedUopBodySlot();
    }

    partial void OnSelectedAnimationFileChanged(string? value)
    {
        if (!QueueCurrentEditedMulAnimation())
        {
            return;
        }

        UpdatePreferredAnimationSources();

        ApplyAnimationFilters();
        ApplyMulSlotFilters();
        ApplyUopBodyFilters();
        OnPropertyChanged(nameof(ShowMulSlotList));
        OnPropertyChanged(nameof(ShowUopSlotList));
        OnPropertyChanged(nameof(SlotPaneTitle));
        OnPropertyChanged(nameof(ShowMulActionDirectionControls));
        OnPropertyChanged(nameof(ShowActionDirectionControls));
        SaveActiveProfileUiState();

        if (!ShowMulSlotView && SelectedAnimation != null)
        {
            int bodyId = GetEffectiveSelectedBodyId(SelectedAnimation);
            if (bodyId >= 0 && TryLoadBodyActionDirection(bodyId, GetSelectedActionIndex(), GetSelectedDirectionIndex()))
            {
                LoadSelectedAnimationBlock();
            }
        }
    }

    private int GetEffectiveSelectedBodyId(AnimationEntry? entry)
    {
        if (entry != null && entry.BodyId >= 0)
        {
            return entry.BodyId;
        }

        if (entry != null && TryParseDisplayedBodyId(entry.DisplayName, out int parsedBodyId))
        {
            return parsedBodyId;
        }

        return -1;
    }

    private int GetGroupCountForBody(int bodyId)
    {
        IAnimationDataSource? dataSource = GetDataSourceForEntry(SelectedAnimation);

        if (dataSource == null)
        {
            if (bodyId < 200)
            {
                return 22;
            }

            if (bodyId < 400)
            {
                return 13;
            }

            return 35;
        }

        return dataSource.GetGroupCountForBody(bodyId);
    }

    private string GetBodyTypeName(int bodyId)
    {
        IAnimationDataSource? dataSource = GetDataSourceForEntry(SelectedAnimation);

        if (dataSource == null)
        {
            if (bodyId < 200)
            {
                return "MONSTER";
            }

            if (bodyId < 400)
            {
                return "ANIMAL";
            }

            return "HUMAN";
        }

        return dataSource.GetBodyTypeName(bodyId);
    }

    private void RebuildActionListForBody(int bodyId)
    {
        suppressActionReload = true;

        ActionOptions.Clear();
        actionNameToIndex.Clear();

        List<int> availableActions;

        IAnimationDataSource? dataSource = GetDataSourceForEntry(SelectedAnimation);

        if (dataSource != null)
        {
            availableActions = dataSource.GetAvailableActionIndices(bodyId);
        }
        else
        {
            availableActions = new List<int>();
        }

        if (availableActions.Count == 0)
        {
            int fallbackCount = GetGroupCountForBody(bodyId);

            for (int actionIndex = 0; actionIndex < fallbackCount; actionIndex++)
            {
                availableActions.Add(actionIndex);
            }
        }

        availableActions.Sort();

        string[] actionNames = GetActionNamesForBody(bodyId);

        foreach (int actionIndex in availableActions)
        {
            string actionLabel;

            if (actionIndex >= 0 &&
                actionIndex < actionNames.Length &&
                !string.IsNullOrWhiteSpace(actionNames[actionIndex]))
            {
                actionLabel = actionNames[actionIndex] + " (" + actionIndex + ")";
            }
            else
            {
                actionLabel = "Action " + actionIndex;
            }

            ActionOptions.Add(actionLabel);
            actionNameToIndex[actionLabel] = actionIndex;
        }

        string? preferredLabel = null;

        if (SelectedAnimation != null)
        {
            foreach (string label in ActionOptions)
            {
                if (actionNameToIndex.TryGetValue(label, out int mappedIndex) &&
                    mappedIndex == SelectedAnimation.ActionId)
                {
                    preferredLabel = label;
                    break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredLabel))
        {
            SelectedAction = preferredLabel;
        }
        else if (ActionOptions.Count > 0)
        {
            SelectedAction = ActionOptions[0];
        }
        else
        {
            SelectedAction = null;
        }

        suppressActionReload = false;
    }

    private int GetSelectedActionIndex()
    {
        if (SelectedAction != null && actionNameToIndex.TryGetValue(SelectedAction, out int actionIndex))
        {
            return actionIndex;
        }

        return 0;
    }

    private bool TryLoadBodyActionDirection(int originalBodyId, int actionIndex, int directionIndex)
    {
        currentResolvedAnimationBlock = null;

        IAnimationDataSource? dataSource = GetDataSourceForEntry(SelectedAnimation);

        if (dataSource == null)
        {
            StatusText = "No animation data source is available for the selected entry.";
            return false;
        }

        if (!dataSource.TryResolveAnimationBlock(originalBodyId, actionIndex, directionIndex, out ResolvedAnimationBlock resolvedBlock))
        {
            StatusText = "No animation data for body " + originalBodyId + ", action " + actionIndex + ", direction " + directionIndex + ".";
            return false;
        }

        currentResolvedAnimationBlock = resolvedBlock;

        if (SelectedAnimation != null)
        {
            SelectedAnimation.Offset = resolvedBlock.Offset;
            SelectedAnimation.Length = resolvedBlock.Length;
            SelectedAnimation.Extra = resolvedBlock.Extra;
            SelectedAnimation.IndexNumber = resolvedBlock.SlotIndex;
            SelectedAnimation.BodyId = originalBodyId;
            SelectedAnimation.ActionId = actionIndex;

            string actionLabel = SelectedAction ?? ("Action " + actionIndex);
            string directionLabel = SelectedDirection ?? ("Direction " + directionIndex);

            string mappingText = resolvedBlock.ResolvedBodyId != originalBodyId
                ? " | Slot " + resolvedBlock.ResolvedBodyId + " | Body " + originalBodyId
                : string.Empty;

            SelectedAnimation.SecondaryText =
                actionLabel + " | " + directionLabel + mappingText + " | " + resolvedBlock.SourceFileName;

            OnPropertyChanged(nameof(SelectedIndexNumber));
            OnPropertyChanged(nameof(SelectedOffset));
            OnPropertyChanged(nameof(SelectedLength));
            OnPropertyChanged(nameof(SelectedExtra));
            OnPropertyChanged(nameof(SelectedBodyId));
            OnPropertyChanged(nameof(SelectedActionId));
            OnPropertyChanged(nameof(SelectedAnimationSource));
        }

        return true;
    }

    private void RebuildDirectionList()
    {
        suppressDirectionReload = true;

        string? previousDirection = SelectedDirection;
        int previousDirectionIndex = GetSelectedDirectionIndex();

        DirectionOptions.Clear();
        directionNameToIndex.Clear();

        string[] directionNames = GetDirectionNames();

        for (int directionIndex = 0; directionIndex < 5; directionIndex++)
        {
            string name;

            if (directionIndex < directionNames.Length)
            {
                name = directionNames[directionIndex] + " (" + directionIndex + ")";
            }
            else
            {
                name = "Direction " + directionIndex;
            }

            DirectionOptions.Add(name);
            directionNameToIndex[name] = directionIndex;
        }

        string? preferredDirection = null;

        if (!string.IsNullOrWhiteSpace(previousDirection) && DirectionOptions.Contains(previousDirection))
        {
            preferredDirection = previousDirection;
        }
        else
        {
            preferredDirection = DirectionOptions.FirstOrDefault(x =>
                directionNameToIndex.TryGetValue(x, out int idx) && idx == previousDirectionIndex);
        }

        if (!string.IsNullOrWhiteSpace(preferredDirection))
        {
            SelectedDirection = preferredDirection;
        }
        else if (DirectionOptions.Count > 0)
        {
            SelectedDirection = DirectionOptions[0];
        }
        else
        {
            SelectedDirection = null;
        }

        suppressDirectionReload = false;
        OnPropertyChanged(nameof(SelectedDirectionSliderValue));
    }

    private int GetSelectedDirectionIndex()
    {
        if (SelectedDirection != null && directionNameToIndex.TryGetValue(SelectedDirection, out int directionIndex))
        {
            return directionIndex;
        }

        return 0;
    }

    private bool TryParseDisplayedBodyId(string displayName, out int bodyId)
    {
        bodyId = -1;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        const string prefix = "Body ";

        if (!displayName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string remainder = displayName.Substring(prefix.Length).Trim();

        int endIndex = 0;
        while (endIndex < remainder.Length && char.IsDigit(remainder[endIndex]))
        {
            endIndex++;
        }

        if (endIndex == 0)
        {
            return false;
        }

        return int.TryParse(remainder.Substring(0, endIndex), out bodyId);
    }

    internal List<string> BuildActionOptionsForAnimation(AnimationEntry animation)
    {
        List<string> results = new();

        IAnimationDataSource? dataSource = GetDataSourceForEntry(animation);
        if (dataSource == null)
        {
            return results;
        }

        List<int> actionIndices =
            dataSource is UopAnimationDataSource uopSource
                ? uopSource.GetAvailableActionIndicesForSourceFile(animation.BodyId, animation.SourceFile)
                : dataSource.GetAvailableActionIndices(animation.BodyId);

        string[] actionNames = GetActionNamesForEntry(animation);

        foreach (int actionIndex in actionIndices.OrderBy(x => x))
        {
            string label;

            if (actionIndex >= 0 && actionIndex < actionNames.Length)
            {
                label = actionNames[actionIndex] + " (" + actionIndex + ")";
            }
            else
            {
                label = "Action " + actionIndex;
            }

            results.Add(label);
        }

        return results;
    }

    internal List<string> BuildDirectionOptionsForAnimation(AnimationEntry animation, int actionIndex)
    {
        List<string> results = new();

        string[] directionNames = GetDirectionNames();

        for (int directionIndex = 0; directionIndex < 5; directionIndex++)
        {
            string name =
                directionIndex < directionNames.Length
                    ? directionNames[directionIndex] + " (" + directionIndex + ")"
                    : "Direction " + directionIndex;

            results.Add(name);
        }

        return results;
    }

    private string[] GetActionNamesForEntry(AnimationEntry animation)
    {
        string bodyTypeName;

        IAnimationDataSource? dataSource = GetDataSourceForEntry(animation);
        if (dataSource != null)
        {
            bodyTypeName = dataSource.GetBodyTypeName(animation.BodyId);
        }
        else
        {
            bodyTypeName = "MONSTER";
        }

        return bodyTypeName switch
        {
            "ANIMAL" => UopConstants.ActionNames.AnimalActions,
            "HUMAN" => UopConstants.ActionNames.CharActions,
            "EQUIPMENT" => UopConstants.ActionNames.CharActions,
            "SEA_MONSTER" => UopConstants.ActionNames.MonsterActions,
            _ => UopConstants.ActionNames.MonsterActions
        };
    }

    private void RebuildCompareFileOptions()
    {
        string previous = CompareSelectedAnimationFile ?? "All Files";

        CompareAnimationFileOptions.Clear();
        CompareAnimationFileOptions.Add("All Files");

        foreach (string fileName in allAnimationEntries
                     .Select(x => x.SourceFile)
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Select(NormalizeAnimationFileNameForCompare)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            CompareAnimationFileOptions.Add(fileName);
        }

        if (CompareAnimationFileOptions.Contains(previous))
        {
            CompareSelectedAnimationFile = previous;
        }
        else if (CompareAnimationFileOptions.Count > 0)
        {
            CompareSelectedAnimationFile = CompareAnimationFileOptions[0];
        }
        else
        {
            CompareSelectedAnimationFile = "All Files";
        }
    }

    private void ApplyCompareAnimationFilters()
    {
        string selectedFile = NormalizeAnimationFileNameForCompare(CompareSelectedAnimationFile);
        int? previousBodyId = CompareSelectedAnimation?.BodyId;
        string? previousSourceFile = CompareSelectedAnimation?.SourceFile;

        CompareAnimationEntries.Clear();

        foreach (AnimationEntry source in allAnimationEntries)
        {
            AnimationEntry entry = new AnimationEntry
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

            bool matchesFile = true;

            if (!string.Equals(selectedFile, "All Files", StringComparison.OrdinalIgnoreCase))
            {
                string normalizedEntryFile = NormalizeAnimationFileNameForCompare(entry.SourceFile);
                matchesFile = string.Equals(
                    normalizedEntryFile,
                    selectedFile,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (!matchesFile)
            {
                continue;
            }

            CompareAnimationEntries.Add(entry);
        }

        if (CompareAnimationEntries.Count > 0)
        {
            AnimationEntry? matchingSelection = null;

            if (previousBodyId.HasValue)
            {
                matchingSelection = CompareAnimationEntries.FirstOrDefault(x =>
                    x.BodyId == previousBodyId.Value &&
                    string.Equals(x.SourceFile, previousSourceFile, StringComparison.OrdinalIgnoreCase));
            }

            CompareSelectedAnimation = matchingSelection ?? CompareAnimationEntries[0];
        }
        else
        {
            CompareSelectedAnimation = null;
        }

        OnPropertyChanged(nameof(HasCompareAnimationEntries));
    }

    private void RebuildCompareActionListForBody(AnimationEntry? animation)
    {
        CompareActionOptions.Clear();
        compareActionNameToIndex.Clear();

        if (animation == null)
        {
            CompareSelectedAction = null;
            return;
        }

        int bodyId = GetEffectiveSelectedBodyId(animation);
        if (bodyId < 0)
        {
            CompareSelectedAction = null;
            return;
        }

        IAnimationDataSource? dataSource = GetDataSourceForEntry(animation);
        List<int> availableActions = dataSource?.GetAvailableActionIndices(bodyId) ?? new List<int>();

        if (availableActions.Count == 0)
        {
            int fallbackCount = dataSource?.GetGroupCountForBody(bodyId) ?? 0;
            for (int actionIndex = 0; actionIndex < fallbackCount; actionIndex++)
            {
                availableActions.Add(actionIndex);
            }
        }

        availableActions.Sort();

        string[] actionNames = dataSource != null
            ? GetActionNamesForBody(bodyId)
            : Array.Empty<string>();

        foreach (int actionIndex in availableActions)
        {
            string label;

            if (actionIndex >= 0 &&
                actionIndex < actionNames.Length &&
                !string.IsNullOrWhiteSpace(actionNames[actionIndex]))
            {
                label = actionNames[actionIndex] + " (" + actionIndex + ")";
            }
            else
            {
                label = "Action " + actionIndex;
            }

            CompareActionOptions.Add(label);
            compareActionNameToIndex[label] = actionIndex;
        }

        string? preferredLabel = CompareActionOptions.FirstOrDefault(x =>
            compareActionNameToIndex.TryGetValue(x, out int mappedIndex) &&
            mappedIndex == CompareOverlayActionIndex);

        if (!string.IsNullOrWhiteSpace(preferredLabel))
        {
            CompareSelectedAction = preferredLabel;
        }
        else if (CompareActionOptions.Count > 0)
        {
            CompareSelectedAction = CompareActionOptions[0];
        }
        else
        {
            CompareSelectedAction = null;
        }
    }

    private void RebuildCompareDirectionList()
    {
        CompareDirectionOptions.Clear();
        compareDirectionNameToIndex.Clear();

        string[] names =
        {
            "North (0)",
            "Right (1)",
            "East (2)",
            "Down (3)",
            "Left (4)"
        };

        for (int i = 0; i < names.Length; i++)
        {
            CompareDirectionOptions.Add(names[i]);
            compareDirectionNameToIndex[names[i]] = i;
        }

        string? preferred = CompareDirectionOptions.FirstOrDefault(x =>
            compareDirectionNameToIndex.TryGetValue(x, out int idx) &&
            idx == CompareOverlayDirectionIndex);

        if (!string.IsNullOrWhiteSpace(preferred))
        {
            CompareSelectedDirection = preferred;
        }
        else if (CompareDirectionOptions.Count > 0)
        {
            CompareSelectedDirection = CompareDirectionOptions[0];
        }
        else
        {
            CompareSelectedDirection = null;
        }
    }

    private int GetSelectedCompareActionIndex()
    {
        if (CompareSelectedAction != null &&
            compareActionNameToIndex.TryGetValue(CompareSelectedAction, out int actionIndex))
        {
            return actionIndex;
        }

        return CompareOverlayActionIndex;
    }

    private int GetSelectedCompareDirectionIndex()
    {
        if (CompareSelectedDirection != null &&
            compareDirectionNameToIndex.TryGetValue(CompareSelectedDirection, out int directionIndex))
        {
            return directionIndex;
        }

        return CompareOverlayDirectionIndex;
    }
}
