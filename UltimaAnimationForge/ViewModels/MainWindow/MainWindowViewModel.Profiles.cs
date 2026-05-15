using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    private AnimationViewerProfile? activeProfile;

    public ObservableCollection<string> ProfileOptions { get; } = new();

    [ObservableProperty]
    private int selectedProfileIndex = -1;

    private sealed class ManageProfilesDialogResult
    {
        public bool ChangedProfiles { get; set; }
        public bool ActiveProfileChanged { get; set; }
        public bool RequiresReload { get; set; }
    }

    private async Task ManageProfilesAsync()
    {
        Window? owner = GetMainWindow();
        if (owner == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        ManageProfilesDialogResult? result = await ShowManageProfilesDialogAsync(owner);

        if (result == null)
        {
            return;
        }

        if (result.ChangedProfiles)
        {
            settingsService.Save(appSettings);
            LoadActiveProfileIntoUi();
            OnPropertyChanged(nameof(ProfileOptions));
            OnPropertyChanged(nameof(SelectedProfileIndex));
        }

        if (result.ActiveProfileChanged || result.RequiresReload)
        {
            ResetGumpsForProfileChange();

            await LoadCurrentProfileFolderAsync();

            if (ActiveToolTab == MainToolTab.Gumps)
            {
                InitializeGumpsForCurrentFolder();
            }
        }
    }

    partial void OnSelectedProfileIndexChanged(int value)
    {
        if (suppressProfileSelectionChanged)
        {
            return;
        }

        List<AnimationViewerProfile> orderedProfiles = appSettings.Profiles
            .OrderBy(x => x.ProfileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (value < 0 || value >= orderedProfiles.Count)
        {
            return;
        }

        AnimationViewerProfile matchingProfile = orderedProfiles[value];

        if (activeProfile != null &&
            string.Equals(activeProfile.ProfileId, matchingProfile.ProfileId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        activeProfile = matchingProfile;
        appSettings.LastActiveProfileId = matchingProfile.ProfileId;
        settingsService.Save(appSettings);

        LoadActiveProfileIntoUi();

        ResetGumpsForProfileChange();

        _ = LoadCurrentProfileFolderAsync();

        if (ActiveToolTab == MainToolTab.Gumps)
        {
            InitializeGumpsForCurrentFolder();
        }
    }

    private AnimationViewerProfile? GetActiveProfile()
    {
        if (appSettings.Profiles == null || appSettings.Profiles.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(appSettings.LastActiveProfileId))
        {
            AnimationViewerProfile? matchingProfile = appSettings.Profiles.FirstOrDefault(x =>
                string.Equals(x.ProfileId, appSettings.LastActiveProfileId, StringComparison.OrdinalIgnoreCase));

            if (matchingProfile != null)
            {
                return matchingProfile;
            }
        }

        return appSettings.Profiles
            .OrderBy(x => x.ProfileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private void RebuildProfileOptions()
    {
        suppressProfileSelectionChanged = true;

        ProfileOptions.Clear();

        List<AnimationViewerProfile> orderedProfiles = appSettings.Profiles
            .OrderBy(x => x.ProfileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (AnimationViewerProfile profile in orderedProfiles)
        {
            ProfileOptions.Add(profile.ProfileName);
        }

        if (activeProfile == null)
        {
            SelectedProfileIndex = ProfileOptions.Count > 0 ? 0 : -1;
            suppressProfileSelectionChanged = false;
            return;
        }

        int selectedIndex = orderedProfiles.FindIndex(x =>
            string.Equals(x.ProfileId, activeProfile.ProfileId, StringComparison.OrdinalIgnoreCase));

        SelectedProfileIndex = selectedIndex >= 0 ? selectedIndex : (ProfileOptions.Count > 0 ? 0 : -1);

        suppressProfileSelectionChanged = false;
    }

    private void LoadActiveProfileIntoUi()
    {
        activeProfile = GetActiveProfile();

        suppressProfileSelectionChanged = true;

        if (activeProfile == null)
        {
            SelectedAnimationFile = "All Files";
            SelectedBodyType = "All";
            SearchText = string.Empty;
            SelectedDirection = null;
            ZoomLevel = 1.0;
            ShowCheckerBackground = true;
            LoopPlayback = true;

            RebuildProfileOptions();

            suppressProfileSelectionChanged = false;
            return;
        }

        SelectedAnimationFile = string.IsNullOrWhiteSpace(activeProfile.SelectedAnimationFile)
            ? "All Files"
            : activeProfile.SelectedAnimationFile;

        SelectedBodyType = string.IsNullOrWhiteSpace(activeProfile.SelectedBodyType)
            ? "All"
            : activeProfile.SelectedBodyType;

        SearchText = activeProfile.SearchText ?? string.Empty;

        ZoomLevel = activeProfile.PreviewZoomLevel > 0 ? activeProfile.PreviewZoomLevel : 1.0;
        ShowCheckerBackground = activeProfile.ShowCheckerBackground;
        LoopPlayback = activeProfile.LoopPlayback;

        RebuildDirectionList();

        if (!string.IsNullOrWhiteSpace(activeProfile.SelectedDirection) &&
            DirectionOptions.Contains(activeProfile.SelectedDirection))
        {
            SelectedDirection = activeProfile.SelectedDirection;
        }
        else
        {
            SelectedDirection = DirectionOptions.Count > 0 ? DirectionOptions[0] : null;
        }

        RebuildProfileOptions();

        suppressProfileSelectionChanged = false;
    }

    private void SaveActiveProfileUiState()
    {
        if (activeProfile == null)
        {
            return;
        }

        activeProfile.SelectedAnimationFile = SelectedAnimationFile ?? "All Files";
        activeProfile.SelectedBodyType = SelectedBodyType ?? "All";
        activeProfile.SearchText = SearchText ?? string.Empty;
        activeProfile.SelectedDirection = SelectedDirection ?? string.Empty;
        activeProfile.PreviewZoomLevel = ZoomLevel;
        activeProfile.ShowCheckerBackground = ShowCheckerBackground;
        activeProfile.LoopPlayback = LoopPlayback;
        activeProfile.LoadUopFiles = ShouldLoadUopForActiveProfile();

        appSettings.LastActiveProfileId = activeProfile.ProfileId;
        settingsService.Save(appSettings);
    }

    private string GetCurrentFolderPath()
    {
        return activeProfile?.UoFolderPath ?? string.Empty;
    }

    private bool ShouldLoadUopForActiveProfile()
    {
        return activeProfile?.LoadUopFiles ?? true;
    }
}