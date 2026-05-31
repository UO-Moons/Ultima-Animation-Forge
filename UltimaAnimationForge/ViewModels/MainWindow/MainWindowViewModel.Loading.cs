using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    private async Task ReportLoadingProgressAsync(double percent, string message)
    {
        LoadingProgress = percent;
        LoadingText = message;

        OnPropertyChanged(nameof(LoadingProgress));
        OnPropertyChanged(nameof(LoadingText));

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
    }

    private void ApplyLoadedData(
        List<AnimationEntry> animationEntries,
        List<MulSlotEntry> mulSlotEntries)
    {
        allAnimationEntries.Clear();
        AnimationEntries.Clear();
        allMulSlotEntries.Clear();
        allUopBodyEntries.Clear();

        allAnimationEntries.AddRange(animationEntries);
        allMulSlotEntries.AddRange(mulSlotEntries);

        RebuildCompareFileOptions();
        ApplyCompareAnimationFilters();
        RebuildCompareDirectionList();

        RebuildAnimationFileOptions();
        RebuildUopBodyEntries();
        ApplyAnimationFilters();
        ApplyMulSlotFilters();
        ApplyUopBodyFilters();
    }

    private async Task OpenFolderAsync()
    {
        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        FolderPickerOpenOptions folderPickerOpenOptions = new FolderPickerOpenOptions
        {
            Title = "Select Ultima Online Folder",
            AllowMultiple = false
        };

        string currentFolderPath = GetCurrentFolderPath();

        if (!string.IsNullOrWhiteSpace(currentFolderPath) && Directory.Exists(currentFolderPath))
        {
            folderPickerOpenOptions.SuggestedStartLocation =
                await mainWindow.StorageProvider.TryGetFolderFromPathAsync(currentFolderPath);
        }

        IReadOnlyList<IStorageFolder> selectedFolders =
            await mainWindow.StorageProvider.OpenFolderPickerAsync(folderPickerOpenOptions);

        if (selectedFolders.Count == 0)
        {
            StatusText = "Folder selection cancelled.";
            return;
        }

        string? localPath = selectedFolders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            StatusText = "Selected folder does not have a local path.";
            return;
        }

        if (activeProfile == null)
        {
            activeProfile = GetActiveProfile();
        }

        if (activeProfile == null)
        {
            activeProfile = new AnimationViewerProfile
            {
                ProfileName = "Default",
                UoFolderPath = localPath
            };

            appSettings.Profiles.Add(activeProfile);
            appSettings.LastActiveProfileId = activeProfile.ProfileId;
        }
        else
        {
            activeProfile.UoFolderPath = localPath;
            appSettings.LastActiveProfileId = activeProfile.ProfileId;
        }

        SaveActiveProfileUiState();
        RebuildProfileOptions();

        await LoadFolderAsync(localPath);
    }

    private async Task LoadFolderAsync(string localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath))
        {
            StatusText = "Open a valid UO folder first.";
            return;
        }

        IsLoading = true;
        LoadingProgress = 0;
        LoadingText = "Loading animation files...";
        SetRandomLoadingTip();

        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(LoadingProgress));
        OnPropertyChanged(nameof(LoadingText));
        OnPropertyChanged(nameof(LoadingTipText));

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

        string profileId = activeProfile?.ProfileId ?? "default";

        AnimationCacheData? cacheData = animationCacheService.LoadCache(profileId);

        if (animationCacheService.IsCacheValid(cacheData, localPath))
        {
            SelectedSourceText = localPath;
            activeAnimationDataSource = null;
            radarColService.Load(localPath, out _);

            await ReportLoadingProgressAsync(10, "Initializing animation sources for cached data...");

            bool shouldLoadUop = ShouldLoadUopForActiveProfile();

            bool mulReady = await Task.Run(() => mulAnimationDataSource.Initialize(localPath));
            bool uopReady = shouldLoadUop &&
                            await Task.Run(() => uopAnimationDataSource.Initialize(localPath));
            UpdatePreferredAnimationSources();

            if (!mulReady && !uopReady)
            {
                StatusText = "Cached data was found, but animation sources could not be initialized.";
                IsLoading = false;
                OnPropertyChanged(nameof(IsLoading));
                return;
            }

            await ReportLoadingProgressAsync(40, "Loading cached MUL data...");

            List<AnimationEntry> combinedEntries =
                cacheData!.AnimationEntries ?? new List<AnimationEntry>();

            if (uopReady)
            {
                await ReportLoadingProgressAsync(70, "Rebuilding UOP animation entries...");

                const int uopBodiesToScan = 65536;

                List<AnimationEntry> liveUopEntries =
                    await Task.Run(() => uopAnimationDataSource.BuildAnimationEntries(uopBodiesToScan));

                foreach (AnimationEntry entry in liveUopEntries)
                {
                    entry.SourceMode = "UOP";

                    if (!entry.SecondaryText.Contains("| UOP |", StringComparison.OrdinalIgnoreCase) &&
                        !entry.SecondaryText.EndsWith("| UOP", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.SecondaryText += " | UOP";
                    }
                }

                combinedEntries = combinedEntries
                    .Concat(liveUopEntries)
                    .ToList();
            }

            ApplyLoadedData(
                combinedEntries,
                new List<MulSlotEntry>());

            await ReportLoadingProgressAsync(
                100,
                shouldLoadUop
                    ? "Loaded cached MUL data and rebuilt UOP entries."
                    : "Loaded cached MUL data.");

            StatusText =
                "Loaded " + allAnimationEntries.Count +
                " animation entries using " +
                (shouldLoadUop ? "cached MUL data and live UOP scan" : "cached MUL data only") +
                " for profile " +
                (activeProfile?.ProfileName ?? "Default") + ".";

            IsLoading = false;
            OnPropertyChanged(nameof(IsLoading));
            return;
        }

        try
        {
            SelectedSourceText = localPath;
            activeAnimationDataSource = null;

            allAnimationEntries.Clear();
            AnimationEntries.Clear();
            allMulSlotEntries.Clear();
            allUopBodyEntries.Clear();

            const int mulBodiesToScan = 6000;
            const int uopBodiesToScan = 65536;

            FolderLoadResult loadResult = new FolderLoadResult();

            await ReportLoadingProgressAsync(5, "Initializing animation sources...");

            bool shouldLoadUop = ShouldLoadUopForActiveProfile();

            loadResult.MulReady = await Task.Run(() => mulAnimationDataSource.Initialize(localPath));
            await ReportLoadingProgressAsync(15, "Initialized MUL animation source.");

            if (shouldLoadUop)
            {
                loadResult.UopReady = await Task.Run(() => uopAnimationDataSource.Initialize(localPath));
                await ReportLoadingProgressAsync(25, "Initialized UOP animation source.");
            }
            else
            {
                loadResult.UopReady = false;
                await ReportLoadingProgressAsync(25, "Skipped UOP animation source for this profile.");
            }

            UpdatePreferredAnimationSources();

            if (!loadResult.MulReady && !loadResult.UopReady)
            {
                StatusText = "No supported animation data source found.";
                return;
            }

            if (loadResult.MulReady)
            {
                await ReportLoadingProgressAsync(35, "Scanning MUL animation entries...");
                loadResult.MulEntries = await Task.Run(() => mulAnimationDataSource.BuildAnimationEntries(mulBodiesToScan));

                foreach (AnimationEntry entry in loadResult.MulEntries)
                {
                    entry.SourceMode = "MUL";

                    if (!entry.SecondaryText.Contains("| MUL |", StringComparison.OrdinalIgnoreCase) &&
                        !entry.SecondaryText.EndsWith("| MUL", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.SecondaryText += " | MUL";
                    }
                }

                await ReportLoadingProgressAsync(58, "Finished scanning MUL animation entries.");
                loadResult.MulSlots = new List<MulSlotEntry>();
            }

            if (loadResult.UopReady)
            {
                await ReportLoadingProgressAsync(65, "Scanning UOP animation entries...");
                loadResult.UopEntries = await Task.Run(() => uopAnimationDataSource.BuildAnimationEntries(uopBodiesToScan));

                foreach (AnimationEntry entry in loadResult.UopEntries)
                {
                    entry.SourceMode = "UOP";

                    if (!entry.SecondaryText.Contains("| UOP |", StringComparison.OrdinalIgnoreCase) &&
                        !entry.SecondaryText.EndsWith("| UOP", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.SecondaryText += " | UOP";
                    }
                }

                await ReportLoadingProgressAsync(82, "Finished scanning UOP animation entries.");
            }

            await ReportLoadingProgressAsync(86, "Building file lists...");

            List<AnimationEntry> combinedEntries = new List<AnimationEntry>();
            combinedEntries.AddRange(loadResult.MulEntries);
            combinedEntries.AddRange(loadResult.UopEntries);

            ApplyLoadedData(combinedEntries, loadResult.MulSlots);

            await ReportLoadingProgressAsync(100, "Finished loading animations.");

            AnimationCacheData newCache = animationCacheService.BuildCacheData(
                profileId,
                localPath,
                allAnimationEntries.ToList(),
                allMulSlotEntries.ToList());

            await Task.Run(() => animationCacheService.SaveCache(newCache));

            StatusText =
                "Loaded " + allAnimationEntries.Count + " animation entries from " +
                (loadResult.MulReady && loadResult.UopReady
                    ? "MUL and UOP"
                    : loadResult.MulReady
                        ? "MUL"
                        : "UOP") + ".";
        }
        catch (Exception exception)
        {
            StatusText = "Failed loading animation data: " + exception.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    private async Task LoadCurrentProfileFolderAsync()
    {
        if (isProfileLoadInProgress)
        {
            return;
        }

        string currentFolderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(currentFolderPath))
        {
            SelectedSourceText = "No source loaded";
            StatusText = "Selected profile does not have a folder yet.";
            return;
        }

        if (!Directory.Exists(currentFolderPath))
        {
            SelectedSourceText = currentFolderPath;
            StatusText = "The folder for the selected profile was not found.";
            return;
        }

        try
        {
            isProfileLoadInProgress = true;
            await LoadFolderAsync(currentFolderPath);
        }
        finally
        {
            isProfileLoadInProgress = false;
        }
    }

    public async Task InitializeAsync()
    {
        LoadActiveProfileIntoUi();
        await LoadCurrentProfileFolderAsync();
    }

    private async Task UpdateLoadingProgressAsync(int currentStep, int totalSteps, string message)
    {
        double percent = 0;

        if (totalSteps > 0)
        {
            percent = (double)currentStep / totalSteps * 100.0;
        }

        loadingProgress = percent;
        loadingText = message + " (" + percent.ToString("0") + "%)";

        OnPropertyChanged(nameof(LoadingProgress));
        OnPropertyChanged(nameof(LoadingText));

        await Dispatcher.UIThread.InvokeAsync(() => { });
    }

    private void ReloadAnimationSourcesAndLists()
    {
        const int mulBodiesToScan = 6000;
        const int uopBodiesToScan = 65536;

        string currentFolderPath = GetCurrentFolderPath();

        FolderLoadResult loadResult = LoadAnimationDataForFolder(
            currentFolderPath,
            mulBodiesToScan,
            uopBodiesToScan);

        List<AnimationEntry> combinedEntries = new List<AnimationEntry>();
        combinedEntries.AddRange(loadResult.MulEntries);
        combinedEntries.AddRange(loadResult.UopEntries);

        ApplyLoadedData(combinedEntries, loadResult.MulSlots);

        string profileId = activeProfile?.ProfileId ?? "default";

        AnimationCacheData cacheData = animationCacheService.BuildCacheData(
            profileId,
            currentFolderPath,
            allAnimationEntries.ToList(),
            allMulSlotEntries.ToList());

        animationCacheService.SaveCache(cacheData);
    }
}