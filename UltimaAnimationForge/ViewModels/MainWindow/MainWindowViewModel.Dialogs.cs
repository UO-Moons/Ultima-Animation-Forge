using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;
using UltimaAnimationForge.Views;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    // Move these existing methods here from MainWindowViewModel.cs exactly as they are now:

    private async Task<ManageProfilesDialogResult?> ShowManageProfilesDialogAsync(Window owner)
    {
        bool requiresReload = false;

        Window dialog = new Window
        {
            Title = "Manage Profiles",
            Width = 860,
            Height = 620,
            MinWidth = 860,
            MinHeight = 620,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        ObservableCollection<AnimationViewerProfile> workingProfiles =
            new ObservableCollection<AnimationViewerProfile>(
                appSettings.Profiles
                    .Select(profile => new AnimationViewerProfile
                    {
                        ProfileId = profile.ProfileId,
                        ProfileName = profile.ProfileName,
                        UoFolderPath = profile.UoFolderPath,
                        OutputFolderPath = profile.OutputFolderPath,
                        SelectedAnimationFile = profile.SelectedAnimationFile,
                        SelectedBodyType = profile.SelectedBodyType,
                        SearchText = profile.SearchText,
                        SelectedDirection = profile.SelectedDirection,
                        PreviewZoomLevel = profile.PreviewZoomLevel,
                        ShowCheckerBackground = profile.ShowCheckerBackground,
                        LoopPlayback = profile.LoopPlayback,
                        LoadUopFiles = profile.LoadUopFiles
                    })
                    .OrderBy(x => x.ProfileName, StringComparer.OrdinalIgnoreCase));

        string workingActiveProfileId = appSettings.LastActiveProfileId;

        ListBox profileListBox = new ListBox
        {
            Width = 220,
            Height = 360
        };

        profileListBox.ItemsSource = workingProfiles;

        TextBox profileNameTextBox = new TextBox
        {
            Watermark = "Profile name",
            IsEnabled = false
        };

        TextBox folderPathTextBox = new TextBox
        {
            Watermark = "Folder path",
            IsReadOnly = true,
            IsEnabled = false
        };

        TextBox outputFolderPathTextBox = new TextBox
        {
            Watermark = "Output folder path",
            IsReadOnly = true,
            IsEnabled = false
        };

        Button browseOutputFolderButton = new Button
        {
            Content = "Set Output",
            Width = 100,
            IsEnabled = false
        };

        CheckBox loadUopCheckBox = new CheckBox
        {
            Content = "Load UOP files for this profile",
            IsEnabled = false,
            Margin = new Thickness(0, 4, 0, 4)
        };

        CheckBox checkerBackgroundCheckBox = new CheckBox
        {
            Content = "Enable checker background by default",
            IsEnabled = false,
            Margin = new Thickness(0, 2, 0, 2)
        };

        CheckBox loopPlaybackCheckBox = new CheckBox
        {
            Content = "Enable loop playback by default",
            IsEnabled = false,
            Margin = new Thickness(0, 2, 0, 4)
        };

        TextBlock statusTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray
        };

        Button newButton = new Button
        {
            Content = "New",
            Width = 80
        };

        Button renameButton = new Button
        {
            Content = "Rename",
            Width = 80,
            IsEnabled = false
        };

        Button deleteButton = new Button
        {
            Content = "Delete",
            Width = 80,
            IsEnabled = false
        };

        Button browseButton = new Button
        {
            Content = "Set Folder",
            Width = 100,
            IsEnabled = false
        };

        Button makeActiveButton = new Button
        {
            Content = "Make Active",
            Width = 110,
            IsEnabled = false
        };

        Button duplicateButton = new Button
        {
            Content = "Duplicate",
            Width = 90,
            IsEnabled = false
        };

        Button clearCacheButton = new Button
        {
            Content = "Clear Cache",
            Width = 98,
            IsEnabled = false
        };

        Button clearAllCachesButton = new Button
        {
            Content = "Clear All Caches",
            Width = 118
        };

        Button saveButton = new Button
        {
            Content = "Save",
            Width = 90
        };

        Button cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90
        };

        ManageProfilesDialogResult? result = null;
        AnimationViewerProfile? selectedWorkingProfile = null;
        bool changedProfiles = false;
        bool activeProfileChanged = false;
        bool suppressSelectionRefresh = false;

        void RefreshProfileList()
        {
            string? targetProfileId = selectedWorkingProfile?.ProfileId;

            List<AnimationViewerProfile> sortedProfiles = workingProfiles
                .OrderBy(x => x.ProfileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            suppressSelectionRefresh = true;

            try
            {
                workingProfiles.Clear();

                foreach (AnimationViewerProfile profile in sortedProfiles)
                {
                    workingProfiles.Add(profile);
                }

                AnimationViewerProfile? targetSelection = null;

                if (!string.IsNullOrWhiteSpace(targetProfileId))
                {
                    targetSelection = workingProfiles.FirstOrDefault(x =>
                        string.Equals(x.ProfileId, targetProfileId, StringComparison.OrdinalIgnoreCase));
                }

                if (targetSelection == null && workingProfiles.Count > 0)
                {
                    targetSelection = workingProfiles[0];
                }

                selectedWorkingProfile = targetSelection;
                profileListBox.SelectedItem = targetSelection;
            }
            finally
            {
                suppressSelectionRefresh = false;
            }
        }

        void RefreshEditor()
        {
            if (!suppressSelectionRefresh)
            {
                selectedWorkingProfile = profileListBox.SelectedItem as AnimationViewerProfile;
            }

            bool hasSelection = selectedWorkingProfile != null;

            profileNameTextBox.IsEnabled = hasSelection;
            folderPathTextBox.IsEnabled = hasSelection;
            renameButton.IsEnabled = hasSelection;
            browseButton.IsEnabled = hasSelection;
            makeActiveButton.IsEnabled = hasSelection;
            duplicateButton.IsEnabled = hasSelection;
            clearCacheButton.IsEnabled = hasSelection;
            deleteButton.IsEnabled = hasSelection && workingProfiles.Count > 1;
            loadUopCheckBox.IsEnabled = hasSelection;
            checkerBackgroundCheckBox.IsEnabled = hasSelection;
            loopPlaybackCheckBox.IsEnabled = hasSelection;
            outputFolderPathTextBox.IsEnabled = hasSelection;
            browseOutputFolderButton.IsEnabled = hasSelection;

            if (!hasSelection)
            {
                profileNameTextBox.Text = string.Empty;
                folderPathTextBox.Text = string.Empty;
                loadUopCheckBox.IsChecked = false;
                checkerBackgroundCheckBox.IsChecked = false;
                loopPlaybackCheckBox.IsChecked = false;
                outputFolderPathTextBox.Text = string.Empty;
                statusTextBlock.Inlines.Clear();
                statusTextBlock.Text = "Select a profile to edit.";
                return;
            }

            profileNameTextBox.Text = selectedWorkingProfile!.ProfileName;
            folderPathTextBox.Text = selectedWorkingProfile.UoFolderPath ?? string.Empty;
            outputFolderPathTextBox.Text = selectedWorkingProfile.OutputFolderPath ?? string.Empty;
            loadUopCheckBox.IsChecked = selectedWorkingProfile.LoadUopFiles;
            checkerBackgroundCheckBox.IsChecked = selectedWorkingProfile.ShowCheckerBackground;
            loopPlaybackCheckBox.IsChecked = selectedWorkingProfile.LoopPlayback;

            bool isActive = string.Equals(
                selectedWorkingProfile.ProfileId,
                workingActiveProfileId,
                StringComparison.OrdinalIgnoreCase);

            statusTextBlock.Inlines.Clear();

            statusTextBlock.Inlines.Add(new Run("Profile ID:\n") { FontWeight = FontWeight.Bold });
            statusTextBlock.Inlines.Add(new Run(selectedWorkingProfile.ProfileId + Environment.NewLine + Environment.NewLine));

            statusTextBlock.Inlines.Add(new Run("Status:\n") { FontWeight = FontWeight.Bold });
            statusTextBlock.Inlines.Add(new Run((isActive ? "Active" : "Inactive") + Environment.NewLine + Environment.NewLine));

            statusTextBlock.Inlines.Add(new Run("Folder:\n") { FontWeight = FontWeight.Bold });
            statusTextBlock.Inlines.Add(new Run(
                string.IsNullOrWhiteSpace(selectedWorkingProfile.UoFolderPath)
                    ? "(not set)" + Environment.NewLine + Environment.NewLine
                    : selectedWorkingProfile.UoFolderPath + Environment.NewLine + Environment.NewLine));

            statusTextBlock.Inlines.Add(new Run("Output Folder:\n") { FontWeight = FontWeight.Bold });
            statusTextBlock.Inlines.Add(new Run(
                string.IsNullOrWhiteSpace(selectedWorkingProfile.OutputFolderPath)
                    ? "(not set)" + Environment.NewLine + Environment.NewLine
                    : selectedWorkingProfile.OutputFolderPath + Environment.NewLine + Environment.NewLine));

            statusTextBlock.Inlines.Add(new Run("Preview Defaults:\n") { FontWeight = FontWeight.Bold });
            statusTextBlock.Inlines.Add(new Run(
                "Checker: " + (selectedWorkingProfile.ShowCheckerBackground ? "On" : "Off") + Environment.NewLine +
                "Loop: " + (selectedWorkingProfile.LoopPlayback ? "On" : "Off") + Environment.NewLine + Environment.NewLine));

            statusTextBlock.Inlines.Add(new Run("Cache File:\n") { FontWeight = FontWeight.Bold });
            statusTextBlock.Inlines.Add(new Run(animationCacheService.GetCacheFilePath(selectedWorkingProfile.ProfileId)));
        }

        profileListBox.ItemTemplate = new FuncDataTemplate<AnimationViewerProfile?>((profile, _) =>
        {
            StackPanel panel = new StackPanel
            {
                Spacing = 2,
                Margin = new Thickness(4)
            };

            if (profile == null)
            {
                return panel;
            }

            bool isActive = string.Equals(profile.ProfileId, workingActiveProfileId, StringComparison.OrdinalIgnoreCase);

            panel.Children.Add(new TextBlock
            {
                Text = profile.ProfileName + (isActive ? "  [Active]" : string.Empty),
                FontWeight = FontWeight.Bold
            });

            panel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(profile.UoFolderPath) ? "(no folder set)" : profile.UoFolderPath,
                Foreground = Brushes.LightGray,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            return panel;
        });

        profileListBox.SelectionChanged += (_, _) =>
        {
            if (!suppressSelectionRefresh)
            {
                RefreshEditor();
            }
        };

        newButton.Click += (_, _) =>
        {
            int suffix = 1;
            string baseName = "New Profile";
            string candidateName = baseName;

            while (workingProfiles.Any(x => string.Equals(x.ProfileName, candidateName, StringComparison.OrdinalIgnoreCase)))
            {
                suffix++;
                candidateName = baseName + " " + suffix;
            }

            AnimationViewerProfile newProfile = new AnimationViewerProfile
            {
                ProfileName = candidateName,
                LoadUopFiles = true,
                ShowCheckerBackground = true,
                LoopPlayback = true
            };

            workingProfiles.Add(newProfile);
            selectedWorkingProfile = newProfile;
            workingActiveProfileId = newProfile.ProfileId;
            activeProfileChanged = true;
            changedProfiles = true;
            requiresReload = true;

            RefreshProfileList();
            RefreshEditor();
        };

        renameButton.Click += (_, _) =>
        {
            if (selectedWorkingProfile == null)
            {
                return;
            }

            string newName = (profileNameTextBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(newName))
            {
                statusTextBlock.Inlines.Clear();
                statusTextBlock.Text = "Profile name cannot be empty.";
                return;
            }

            bool duplicateNameExists = workingProfiles.Any(x =>
                !string.Equals(x.ProfileId, selectedWorkingProfile.ProfileId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ProfileName, newName, StringComparison.OrdinalIgnoreCase));

            if (duplicateNameExists)
            {
                statusTextBlock.Inlines.Clear();
                statusTextBlock.Text = "A profile with that name already exists.";
                return;
            }

            selectedWorkingProfile.ProfileName = newName;
            changedProfiles = true;

            RefreshProfileList();
            RefreshEditor();
        };

        duplicateButton.Click += (_, _) =>
        {
            if (selectedWorkingProfile == null)
            {
                return;
            }

            int suffix = 2;
            string baseName = selectedWorkingProfile.ProfileName + " Copy";
            string candidateName = baseName;

            while (workingProfiles.Any(x => string.Equals(x.ProfileName, candidateName, StringComparison.OrdinalIgnoreCase)))
            {
                candidateName = baseName + " " + suffix;
                suffix++;
            }

            AnimationViewerProfile duplicateProfile = new AnimationViewerProfile
            {
                ProfileName = candidateName,
                UoFolderPath = selectedWorkingProfile.UoFolderPath,
                SelectedAnimationFile = selectedWorkingProfile.SelectedAnimationFile,
                SelectedBodyType = selectedWorkingProfile.SelectedBodyType,
                SearchText = selectedWorkingProfile.SearchText,
                SelectedDirection = selectedWorkingProfile.SelectedDirection,
                PreviewZoomLevel = selectedWorkingProfile.PreviewZoomLevel,
                ShowCheckerBackground = selectedWorkingProfile.ShowCheckerBackground,
                OutputFolderPath = selectedWorkingProfile.OutputFolderPath,
                LoopPlayback = selectedWorkingProfile.LoopPlayback,
                LoadUopFiles = selectedWorkingProfile.LoadUopFiles
            };

            workingProfiles.Add(duplicateProfile);
            selectedWorkingProfile = duplicateProfile;
            changedProfiles = true;

            RefreshProfileList();
            RefreshEditor();
        };

        loadUopCheckBox.IsCheckedChanged += (_, _) =>
        {
            if (selectedWorkingProfile == null)
            {
                return;
            }

            bool newValue = loadUopCheckBox.IsChecked ?? true;

            if (selectedWorkingProfile.LoadUopFiles != newValue)
            {
                selectedWorkingProfile.LoadUopFiles = newValue;
                changedProfiles = true;

                if (activeProfile != null &&
                    string.Equals(selectedWorkingProfile.ProfileId, activeProfile.ProfileId, StringComparison.OrdinalIgnoreCase))
                {
                    requiresReload = true;
                }
            }
        };

        checkerBackgroundCheckBox.IsCheckedChanged += (_, _) =>
        {
            if (selectedWorkingProfile == null)
            {
                return;
            }

            bool newValue = checkerBackgroundCheckBox.IsChecked ?? true;

            if (selectedWorkingProfile.ShowCheckerBackground != newValue)
            {
                selectedWorkingProfile.ShowCheckerBackground = newValue;
                changedProfiles = true;

                if (activeProfile != null &&
                    string.Equals(selectedWorkingProfile.ProfileId, activeProfile.ProfileId, StringComparison.OrdinalIgnoreCase))
                {
                    requiresReload = true;
                }

                RefreshEditor();
            }
        };

        loopPlaybackCheckBox.IsCheckedChanged += (_, _) =>
        {
            if (selectedWorkingProfile == null)
            {
                return;
            }

            bool newValue = loopPlaybackCheckBox.IsChecked ?? true;

            if (selectedWorkingProfile.LoopPlayback != newValue)
            {
                selectedWorkingProfile.LoopPlayback = newValue;
                changedProfiles = true;

                if (activeProfile != null &&
                    string.Equals(selectedWorkingProfile.ProfileId, activeProfile.ProfileId, StringComparison.OrdinalIgnoreCase))
                {
                    requiresReload = true;
                }

                RefreshEditor();
            }
        };

        deleteButton.Click += async (_, _) =>
        {
            if (selectedWorkingProfile == null)
            {
                return;
            }

            if (workingProfiles.Count <= 1)
            {
                statusTextBlock.Inlines.Clear();
                statusTextBlock.Text = "You must keep at least one profile.";
                return;
            }

            bool confirm = await ShowConfirmationDialogAsync(
                dialog,
                "Confirm",
                "Delete profile '" + selectedWorkingProfile.ProfileName + "'?");

            if (!confirm)
            {
                return;
            }

            string deletedProfileId = selectedWorkingProfile.ProfileId;
            bool deletedWasActive = string.Equals(
                deletedProfileId,
                workingActiveProfileId,
                StringComparison.OrdinalIgnoreCase);

            int deletedIndex = workingProfiles
                .Select((profile, index) => new { profile, index })
                .Where(x => string.Equals(x.profile.ProfileId, deletedProfileId, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.index)
                .DefaultIfEmpty(-1)
                .First();

            AnimationViewerProfile? profileToRemove = workingProfiles.FirstOrDefault(x =>
                string.Equals(x.ProfileId, deletedProfileId, StringComparison.OrdinalIgnoreCase));

            if (profileToRemove != null)
            {
                workingProfiles.Remove(profileToRemove);
            }

            animationCacheService.DeleteCache(deletedProfileId);

            if (deletedWasActive && workingProfiles.Count > 0)
            {
                workingActiveProfileId = workingProfiles[0].ProfileId;
                activeProfileChanged = true;
                requiresReload = true;
            }

            if (workingProfiles.Count > 0)
            {
                int nextIndex = deletedIndex;

                if (nextIndex < 0)
                {
                    nextIndex = 0;
                }

                if (nextIndex >= workingProfiles.Count)
                {
                    nextIndex = workingProfiles.Count - 1;
                }

                selectedWorkingProfile = workingProfiles[nextIndex];
            }
            else
            {
                selectedWorkingProfile = null;
            }

            changedProfiles = true;

            RefreshProfileList();
            RefreshEditor();
        };

        browseButton.Click += async (_, _) =>
        {
            if (selectedWorkingProfile == null)
            {
                return;
            }

            FolderPickerOpenOptions folderPickerOpenOptions = new FolderPickerOpenOptions
            {
                Title = "Select Ultima Online Folder",
                AllowMultiple = false
            };

            if (!string.IsNullOrWhiteSpace(selectedWorkingProfile.UoFolderPath) &&
                Directory.Exists(selectedWorkingProfile.UoFolderPath))
            {
                folderPickerOpenOptions.SuggestedStartLocation =
                    await dialog.StorageProvider.TryGetFolderFromPathAsync(selectedWorkingProfile.UoFolderPath);
            }

            IReadOnlyList<IStorageFolder> selectedFolders =
                await dialog.StorageProvider.OpenFolderPickerAsync(folderPickerOpenOptions);

            if (selectedFolders.Count == 0)
            {
                return;
            }

            string? localPath = selectedFolders[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath))
            {
                statusTextBlock.Inlines.Clear();
                statusTextBlock.Text = "Selected folder does not have a local path.";
                return;
            }

            selectedWorkingProfile.UoFolderPath = localPath;
            folderPathTextBox.Text = localPath;
            changedProfiles = true;

            if (activeProfile != null &&
                string.Equals(selectedWorkingProfile.ProfileId, activeProfile.ProfileId, StringComparison.OrdinalIgnoreCase))
            {
                requiresReload = true;
            }

            RefreshEditor();
            RefreshProfileList();
        };

        browseOutputFolderButton.Click += async (_, _) =>
        {
            if (selectedWorkingProfile == null)
            {
                return;
            }

            FolderPickerOpenOptions folderPickerOpenOptions = new FolderPickerOpenOptions
            {
                Title = "Select Output Folder",
                AllowMultiple = false
            };

            if (!string.IsNullOrWhiteSpace(selectedWorkingProfile.OutputFolderPath) &&
                Directory.Exists(selectedWorkingProfile.OutputFolderPath))
            {
                folderPickerOpenOptions.SuggestedStartLocation =
                    await dialog.StorageProvider.TryGetFolderFromPathAsync(selectedWorkingProfile.OutputFolderPath);
            }

            IReadOnlyList<IStorageFolder> selectedFolders =
                await dialog.StorageProvider.OpenFolderPickerAsync(folderPickerOpenOptions);

            if (selectedFolders.Count == 0)
            {
                return;
            }

            string? localPath = selectedFolders[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath))
            {
                statusTextBlock.Inlines.Clear();
                statusTextBlock.Text = "Selected output folder does not have a local path.";
                return;
            }

            selectedWorkingProfile.OutputFolderPath = localPath;
            outputFolderPathTextBox.Text = localPath;
            changedProfiles = true;

            RefreshEditor();
            RefreshProfileList();
        };

        makeActiveButton.Click += (_, _) =>
        {
            if (selectedWorkingProfile == null)
            {
                return;
            }

            if (!string.Equals(workingActiveProfileId, selectedWorkingProfile.ProfileId, StringComparison.OrdinalIgnoreCase))
            {
                workingActiveProfileId = selectedWorkingProfile.ProfileId;
                activeProfileChanged = true;
                changedProfiles = true;
                requiresReload = true;
            }

            RefreshProfileList();
            RefreshEditor();
        };

        clearCacheButton.Click += async (_, _) =>
        {
            if (selectedWorkingProfile == null)
            {
                return;
            }

            bool confirm = await ShowConfirmationDialogAsync(
                dialog,
                "Confirm",
                "Clear cache for profile '" + selectedWorkingProfile.ProfileName + "'?");

            if (!confirm)
            {
                return;
            }

            animationCacheService.DeleteCache(selectedWorkingProfile.ProfileId);
            RefreshEditor();
        };

        clearAllCachesButton.Click += async (_, _) =>
        {
            bool confirm = await ShowConfirmationDialogAsync(
                dialog,
                "Confirm",
                "Clear all cached profile data?");

            if (!confirm)
            {
                return;
            }

            animationCacheService.DeleteAllCaches();

            statusTextBlock.Inlines.Clear();
            statusTextBlock.Inlines.Add(new Run("Cache Status:\n") { FontWeight = FontWeight.Bold });
            statusTextBlock.Inlines.Add(new Run("Cleared all cached profile data." + Environment.NewLine + Environment.NewLine));
            statusTextBlock.Inlines.Add(new Run("Next Load:\n") { FontWeight = FontWeight.Bold });
            statusTextBlock.Inlines.Add(new Run("The next load for any profile will rebuild its cache."));
        };

        saveButton.Click += (_, _) =>
        {
            if (selectedWorkingProfile != null)
            {
                string editedName = (profileNameTextBox.Text ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(editedName))
                {
                    statusTextBlock.Inlines.Clear();
                    statusTextBlock.Text = "Profile name cannot be empty.";
                    return;
                }

                bool duplicateNameExists = workingProfiles.Any(x =>
                    !string.Equals(x.ProfileId, selectedWorkingProfile.ProfileId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.ProfileName, editedName, StringComparison.OrdinalIgnoreCase));

                if (duplicateNameExists)
                {
                    statusTextBlock.Inlines.Clear();
                    statusTextBlock.Text = "A profile with that name already exists.";
                    return;
                }

                selectedWorkingProfile.ProfileName = editedName;
            }

            appSettings.Profiles.Clear();
            appSettings.Profiles.AddRange(
                workingProfiles
                    .OrderBy(x => x.ProfileName, StringComparer.OrdinalIgnoreCase)
                    .Select(profile => new AnimationViewerProfile
                    {
                        ProfileId = profile.ProfileId,
                        ProfileName = profile.ProfileName,
                        UoFolderPath = profile.UoFolderPath,
                        SelectedAnimationFile = profile.SelectedAnimationFile,
                        SelectedBodyType = profile.SelectedBodyType,
                        SearchText = profile.SearchText,
                        SelectedDirection = profile.SelectedDirection,
                        PreviewZoomLevel = profile.PreviewZoomLevel,
                        ShowCheckerBackground = profile.ShowCheckerBackground,
                        LoopPlayback = profile.LoopPlayback,
                        OutputFolderPath = profile.OutputFolderPath,
                        LoadUopFiles = profile.LoadUopFiles
                    }));

            appSettings.LastActiveProfileId = workingActiveProfileId;

            result = new ManageProfilesDialogResult
            {
                ChangedProfiles = changedProfiles,
                ActiveProfileChanged = activeProfileChanged,
                RequiresReload = requiresReload
            };

            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            result = null;
            dialog.Close();
        };

        Grid editorGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,*,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowSpacing = 10,
            ColumnSpacing = 10
        };

        TextBlock profileNameLabel = new TextBlock
        {
            Text = "Profile Name",
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        editorGrid.Children.Add(profileNameLabel);
        Grid.SetRow(profileNameLabel, 0);
        Grid.SetColumn(profileNameLabel, 0);

        editorGrid.Children.Add(profileNameTextBox);
        Grid.SetRow(profileNameTextBox, 0);
        Grid.SetColumn(profileNameTextBox, 1);

        TextBlock folderPathLabel = new TextBlock
        {
            Text = "Folder Path",
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        editorGrid.Children.Add(folderPathLabel);
        Grid.SetRow(folderPathLabel, 1);
        Grid.SetColumn(folderPathLabel, 0);

        editorGrid.Children.Add(folderPathTextBox);
        Grid.SetRow(folderPathTextBox, 1);
        Grid.SetColumn(folderPathTextBox, 1);

        TextBlock outputFolderPathLabel = new TextBlock
        {
            Text = "Output Folder",
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        editorGrid.Children.Add(outputFolderPathLabel);
        Grid.SetRow(outputFolderPathLabel, 2);
        Grid.SetColumn(outputFolderPathLabel, 0);

        StackPanel outputFolderPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8
        };

        outputFolderPanel.Children.Add(outputFolderPathTextBox);
        outputFolderPanel.Children.Add(browseOutputFolderButton);

        editorGrid.Children.Add(outputFolderPanel);
        Grid.SetRow(outputFolderPanel, 2);
        Grid.SetColumn(outputFolderPanel, 1);

        editorGrid.Children.Add(loadUopCheckBox);
        Grid.SetRow(loadUopCheckBox, 3);
        Grid.SetColumn(loadUopCheckBox, 1);

        editorGrid.Children.Add(checkerBackgroundCheckBox);
        Grid.SetRow(checkerBackgroundCheckBox, 4);
        Grid.SetColumn(checkerBackgroundCheckBox, 1);

        editorGrid.Children.Add(loopPlaybackCheckBox);
        Grid.SetRow(loopPlaybackCheckBox, 5);
        Grid.SetColumn(loopPlaybackCheckBox, 1);

        Border statusBorder = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#111111")),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = new ScrollViewer
            {
                Content = statusTextBlock,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            }
        };

        Grid.SetRow(statusBorder, 6);
        Grid.SetColumn(statusBorder, 0);
        Grid.SetColumnSpan(statusBorder, 2);
        editorGrid.Children.Add(statusBorder);

        WrapPanel primaryButtons = new WrapPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            ItemWidth = double.NaN,
            ItemHeight = double.NaN,
            Margin = new Thickness(0, 8, 0, 0)
        };

        primaryButtons.Children.Add(newButton);
        primaryButtons.Children.Add(renameButton);
        primaryButtons.Children.Add(duplicateButton);
        primaryButtons.Children.Add(deleteButton);
        primaryButtons.Children.Add(browseButton);
        primaryButtons.Children.Add(makeActiveButton);

        WrapPanel cacheButtons = new WrapPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            ItemWidth = double.NaN,
            ItemHeight = double.NaN,
            Margin = new Thickness(0, 4, 0, 0)
        };

        cacheButtons.Children.Add(clearCacheButton);
        cacheButtons.Children.Add(clearAllCachesButton);

        Grid.SetRow(primaryButtons, 7);
        Grid.SetColumn(primaryButtons, 0);
        Grid.SetColumnSpan(primaryButtons, 2);
        editorGrid.Children.Add(primaryButtons);

        Grid.SetRow(cacheButtons, 8);
        Grid.SetColumn(cacheButtons, 0);
        Grid.SetColumnSpan(cacheButtons, 2);
        editorGrid.Children.Add(cacheButtons);

        Grid rootGrid = new Grid
        {
            Margin = new Thickness(16),
            ColumnDefinitions = new ColumnDefinitions("240,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            ColumnSpacing = 16,
            RowSpacing = 16
        };

        Border listBorder = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#111111")),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8),
            Child = profileListBox
        };

        rootGrid.Children.Add(listBorder);
        Grid.SetRow(listBorder, 0);
        Grid.SetColumn(listBorder, 0);

        rootGrid.Children.Add(editorGrid);
        Grid.SetRow(editorGrid, 0);
        Grid.SetColumn(editorGrid, 1);

        StackPanel bottomButtons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 10,
            Children =
        {
            cancelButton,
            saveButton
        }
        };

        rootGrid.Children.Add(bottomButtons);
        Grid.SetRow(bottomButtons, 1);
        Grid.SetColumn(bottomButtons, 0);
        Grid.SetColumnSpan(bottomButtons, 2);

        dialog.Content = rootGrid;

        RefreshProfileList();
        RefreshEditor();

        await dialog.ShowDialog(owner);
        return result;
    }

    private async Task<ExportRequest?> ShowExportModeDialogAsync(Window owner)
    {
        Window dialog = new Window
        {
            Title = "Export Frames",
            Width = 460,
            Height = 540,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        ComboBox modeComboBox = new ComboBox
        {
            ItemsSource = new[]
            {
            "Current frame",
            "Current direction",
            "All directions",
            "All actions and directions"
        },
            SelectedIndex = 2,
            Margin = new Thickness(0, 6, 0, 0),
            Width = 220
        };

        ComboBox formatComboBox = new ComboBox
        {
            ItemsSource = new[]
            {
            "PNG",
            "JPG",
            "BMP",
            "Animated GIF",
            "Sprite Sheet PNG"
        },
            SelectedIndex = 0,
            Margin = new Thickness(0, 6, 0, 0),
            Width = 220
        };

        CheckBox gifLoopCheckBox = new CheckBox
        {
            Content = "Loop animated GIF",
            IsChecked = true,
            Margin = new Thickness(0, 2, 0, 0)
        };

        StackPanel gifOptionsPanel = new StackPanel
        {
            Spacing = 0,
            IsVisible = false,
            Children =
        {
            gifLoopCheckBox
        }
        };

        CheckBox resizeCheckBox = new CheckBox
        {
            Content = "Resize exported frames",
            IsChecked = false,
            Margin = new Thickness(0, 2, 0, 0)
        };

        NumericUpDown resizePercentBox = new NumericUpDown
        {
            Minimum = 10,
            Maximum = 1000,
            Increment = 5,
            Value = 100,
            Width = 120,
            IsEnabled = false
        };

        ComboBox samplerComboBox = new ComboBox
        {
            ItemsSource = new[]
{
    "Auto",
    "Nearest Neighbor",
    "Lanczos3",
    "Spline"
},
            SelectedIndex = 0,
            IsEnabled = false,
            Width = 180
        };

        StackPanel resizeOptionsPanel = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 2, 0, 0),
            Children =
        {
            resizeCheckBox,

            new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                Margin = new Thickness(24, 0, 0, 0),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Scale %",
                        Width = 70,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    },
                    resizePercentBox
                }
            },

            new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                Margin = new Thickness(24, 0, 0, 0),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Sampler",
                        Width = 70,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    },
                    samplerComboBox
                }
            }
        }
        };

        TextBlock spriteColumnsLabel = new TextBlock
        {
            Text = "Columns",
            Width = 80,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        NumericUpDown spriteColumnsBox = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 64,
            Increment = 1,
            Value = 8,
            Width = 120
        };

        TextBlock spritePaddingLabel = new TextBlock
        {
            Text = "Padding",
            Width = 80,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        NumericUpDown spritePaddingBox = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 64,
            Increment = 1,
            Value = 2,
            Width = 120
        };

        TextBlock spriteMetadataLabel = new TextBlock
        {
            Text = "Metadata",
            Width = 80,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        ComboBox spriteMetadataComboBox = new ComboBox
        {
            ItemsSource = new[]
            {
            "None",
            "CSV",
            "JSON"
        },
            SelectedIndex = 0,
            Width = 140
        };

        StackPanel spriteOptionsPanel = new StackPanel
        {
            Spacing = 6,
            IsVisible = false,
            Margin = new Thickness(0, 2, 0, 0),
            Children =
        {
            new TextBlock
            {
                Text = "Sprite sheet options",
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            },

            new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                Children =
                {
                    spriteColumnsLabel,
                    spriteColumnsBox
                }
            },

            new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                Children =
                {
                    spritePaddingLabel,
                    spritePaddingBox
                }
            },

            new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                Children =
                {
                    spriteMetadataLabel,
                    spriteMetadataComboBox
                }
            }
        }
        };

        TextBlock previewText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };

        Border previewBorder = new Border
        {
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(10),
            BorderBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#3A3A3A")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = previewText
        };

        Button exportButton = new Button
        {
            Content = "Export",
            Width = 90
        };

        Button cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90
        };

        ExportRequest? selectedRequest = null;

        void UpdateUi()
        {
            bool isGif = formatComboBox.SelectedIndex == 3;
            bool isSpriteSheet = formatComboBox.SelectedIndex == 4;
            bool resizeEnabled = resizeCheckBox.IsChecked == true;

            gifOptionsPanel.IsVisible = isGif;
            spriteOptionsPanel.IsVisible = isSpriteSheet;

            resizePercentBox.IsEnabled = resizeEnabled;
            samplerComboBox.IsEnabled = resizeEnabled;

            double resizePercent = (double)(resizePercentBox.Value ?? 100m);
            string samplerLabel = samplerComboBox.SelectedItem?.ToString() ?? "Auto";

            List<string> lines = new List<string>
        {
            "Resize: " + (resizeEnabled ? (resizePercent.ToString("0") + "%") : "Off"),
            "Sampler: " + (resizeEnabled ? samplerLabel : "Off"),
            "GIF Loop: " + (isGif ? ((gifLoopCheckBox.IsChecked ?? true) ? "On" : "Off") : "N/A")
        };

            if (isSpriteSheet)
            {
                lines.Add("Columns: " + (spriteColumnsBox.Value ?? 8m));
                lines.Add("Padding: " + (spritePaddingBox.Value ?? 2m));
                lines.Add("Metadata: " + (spriteMetadataComboBox.SelectedItem?.ToString() ?? "None"));
            }

            lines.Add(string.Empty);

            if (resizeEnabled)
            {
                lines.Add("Downscale: Lanczos3 works best.");
                lines.Add("Upscale: Nearest Neighbor = crisp pixels, Spline = smoother enlargement.");
            }
            else
            {
                lines.Add("Frames will export at original size.");
            }

            if (isSpriteSheet)
            {
                lines.Add("Sprite sheets align frame centers for cleaner motion.");
            }

            previewText.Text = string.Join(Environment.NewLine, lines);
        }

        formatComboBox.SelectionChanged += (_, _) => UpdateUi();
        resizeCheckBox.IsCheckedChanged += (_, _) => UpdateUi();
        gifLoopCheckBox.IsCheckedChanged += (_, _) => UpdateUi();
        samplerComboBox.SelectionChanged += (_, _) => UpdateUi();
        spriteMetadataComboBox.SelectionChanged += (_, _) => UpdateUi();

        resizePercentBox.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(NumericUpDown.Value))
            {
                UpdateUi();
            }
        };

        spriteColumnsBox.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(NumericUpDown.Value))
            {
                UpdateUi();
            }
        };

        spritePaddingBox.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(NumericUpDown.Value))
            {
                UpdateUi();
            }
        };

        exportButton.Click += (_, _) =>
        {
            ExportMode mode = modeComboBox.SelectedIndex switch
            {
                0 => ExportMode.CurrentFrame,
                1 => ExportMode.CurrentDirection,
                2 => ExportMode.AllDirections,
                3 => ExportMode.AllActionsAndDirections,
                _ => ExportMode.AllDirections
            };

            ExportImageFormat format = formatComboBox.SelectedIndex switch
            {
                0 => ExportImageFormat.Png,
                1 => ExportImageFormat.Jpg,
                2 => ExportImageFormat.Bmp,
                3 => ExportImageFormat.Gif,
                4 => ExportImageFormat.SpriteSheetPng,
                _ => ExportImageFormat.Png
            };

            ResizeSamplerMode resizeSampler = samplerComboBox.SelectedIndex switch
            {
                1 => ResizeSamplerMode.NearestNeighbor,
                2 => ResizeSamplerMode.Lanczos3,
                3 => ResizeSamplerMode.Spline,
                _ => ResizeSamplerMode.Auto
            };

            selectedRequest = new ExportRequest
            {
                Mode = mode,
                Format = format,
                GifLoop = gifLoopCheckBox.IsChecked ?? true,
                ResizeEnabled = resizeCheckBox.IsChecked == true,
                ResizePercent = (double)(resizePercentBox.Value ?? 100m),
                ResizeSampler = resizeSampler,
                SpriteSheetColumns = (int)(spriteColumnsBox.Value ?? 8m),
                SpriteSheetPadding = (int)(spritePaddingBox.Value ?? 2m),
                SpriteSheetMetadata = spriteMetadataComboBox.SelectedIndex switch
                {
                    1 => SpriteSheetMetadataFormat.Csv,
                    2 => SpriteSheetMetadataFormat.Json,
                    _ => SpriteSheetMetadataFormat.None
                }
            };

            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            selectedRequest = null;
            dialog.Close();
        };

        StackPanel contentPanel = new StackPanel
        {
            Spacing = 12,
            Children =
        {
            new TextBlock { Text = "Choose what to export:" },
            modeComboBox,

            new TextBlock { Text = "Choose output format:" },
            formatComboBox,

            gifOptionsPanel,
            resizeOptionsPanel,
            spriteOptionsPanel,
            previewBorder
        }
        };

        ScrollViewer scrollViewer = new ScrollViewer
        {
            Content = contentPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        StackPanel buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Children =
        {
            cancelButton,
            exportButton
        }
        };

        Grid rootGrid = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("*,Auto")
        };

        Grid.SetRow(scrollViewer, 0);
        Grid.SetRow(buttonPanel, 1);

        rootGrid.Children.Add(scrollViewer);
        rootGrid.Children.Add(buttonPanel);

        dialog.Content = rootGrid;

        UpdateUi();

        await dialog.ShowDialog(owner);
        return selectedRequest;
    }

    private async Task<VdScaleDialogResult?> ShowVdScaleDialogAsync(Window owner)
    {
        Window dialog = new Window
        {
            Title = "Export VD Scale",
            Width = 420,
            MinWidth = 420,
            Height = 430,
            MinHeight = 430,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        ComboBox presetComboBox = new ComboBox
        {
            ItemsSource = new[]
            {
            "50%",
            "75%",
            "100%",
            "125%",
            "150%",
            "200%",
            "300%"
        },
            SelectedIndex = 2,
            Margin = new Thickness(0, 8, 0, 0)
        };

        NumericUpDown customPercentBox = new NumericUpDown
        {
            Minimum = 10,
            Maximum = 1000,
            Increment = 5,
            Value = 100,
            Width = 120,
            Margin = new Thickness(0, 8, 0, 0)
        };

        ComboBox samplerComboBox = new ComboBox
        {
            ItemsSource = new[]
            {
        "Auto",
        "Nearest Neighbor",
        "Lanczos3",
        "Spline"
    },
            SelectedIndex = 0,
            Margin = new Thickness(0, 8, 0, 0)
        };

        CheckBox useCustomCheckBox = new CheckBox
        {
            Content = "Use custom scale percent",
            Margin = new Thickness(0, 12, 0, 0)
        };

        TextBlock previewText = new TextBlock
        {
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        Button okButton = new Button
        {
            Content = "OK",
            Width = 90
        };

        Button cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90
        };

        VdScaleDialogResult? result = null;

        void UpdatePreview()
        {
            double percent;

            if (useCustomCheckBox.IsChecked == true)
            {
                percent = (double)(customPercentBox.Value ?? 100);
            }
            else
            {
                string selected = presetComboBox.SelectedItem?.ToString() ?? "100%";
                selected = selected.Replace("%", "", StringComparison.Ordinal);

                if (!double.TryParse(selected, out percent))
                {
                    percent = 100;
                }

                customPercentBox.Value = (decimal?)percent;
            }

            double scaleFactor = percent / 100.0;
            string samplerLabel = samplerComboBox.SelectedItem?.ToString() ?? "Auto";

            previewText.Text =
                "Scale factor: " + scaleFactor.ToString("0.00") + "x" + Environment.NewLine +
                "Resize sampler: " + samplerLabel + Environment.NewLine +
                "This will resize all exported animation frames and scale their centers before VD export.";
        }

        presetComboBox.SelectionChanged += (_, _) =>
        {
            if (useCustomCheckBox.IsChecked != true)
            {
                UpdatePreview();
            }
        };

        samplerComboBox.SelectionChanged += (_, _) =>
        {
            UpdatePreview();
        };

        useCustomCheckBox.IsCheckedChanged += (_, _) =>
        {
            customPercentBox.IsEnabled = useCustomCheckBox.IsChecked == true;
            presetComboBox.IsEnabled = useCustomCheckBox.IsChecked != true;
            UpdatePreview();
        };

        customPercentBox.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(NumericUpDown.Value) &&
                useCustomCheckBox.IsChecked == true)
            {
                UpdatePreview();
            }
        };

        okButton.Click += (_, _) =>
        {
            double percent = useCustomCheckBox.IsChecked == true
                ? (double)(customPercentBox.Value ?? 100)
                : ParsePercentFromPreset(presetComboBox.SelectedItem?.ToString());

            if (percent <= 0)
            {
                return;
            }

            ResizeSamplerMode resizeSampler = samplerComboBox.SelectedIndex switch
            {
                1 => ResizeSamplerMode.NearestNeighbor,
                2 => ResizeSamplerMode.Lanczos3,
                3 => ResizeSamplerMode.Spline,
                _ => ResizeSamplerMode.Auto
            };

            result = new VdScaleDialogResult
            {
                Confirmed = true,
                ScaleFactor = percent / 100.0,
                ResizeSampler = resizeSampler
            };

            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            result = new VdScaleDialogResult
            {
                Confirmed = false,
                ScaleFactor = 1.0,
                ResizeSampler = ResizeSamplerMode.Auto
            };

            dialog.Close();
        };

        customPercentBox.IsEnabled = false;
        UpdatePreview();

        StackPanel contentPanel = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 8,
            Children =
        {
            new TextBlock
            {
                Text = "Choose export scale for the VD animation:"
            },
            presetComboBox,
            useCustomCheckBox,
            customPercentBox,
            new TextBlock
            {
                Text = "Resize sampler"
            },
            samplerComboBox,
            previewText,
            new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    cancelButton,
                    okButton
                }
            }
        }
        };

        dialog.Content = new ScrollViewer
        {
            Content = contentPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        await dialog.ShowDialog(owner);
        return result;
    }

    private async Task<VdHueDialogResult?> ShowVdHueDialogAsync(Window owner)
    {
        List<HueDataService.HueEntry>? hueEntries = await GetOrLoadHueEntriesAsync(owner);
        if (hueEntries == null || hueEntries.Count == 0)
        {
            return new VdHueDialogResult
            {
                Confirmed = true,
                ApplyHue = false,
                SelectedHue = null
            };
        }

        Window dialog = new Window
        {
            Title = "Export VD Hue",
            Width = 900,
            Height = 620,
            MinWidth = 700,
            MinHeight = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        CheckBox applyHueCheckBox = new CheckBox
        {
            Content = "Apply hue recolor using hues.mul",
            IsChecked = false
        };

        TextBox searchBox = new TextBox
        {
            Watermark = "Search hues by id or name...",
            Margin = new Thickness(0, 8, 0, 0),
            IsEnabled = false
        };

        ListBox hueListBox = new ListBox
        {
            IsEnabled = false
        };

        TextBlock descriptionText = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        Button okButton = new Button
        {
            Content = "OK",
            Width = 90
        };

        Button cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90
        };

        VdHueDialogResult? result = null;

        void RefreshHueList()
        {
            string search = (searchBox.Text ?? string.Empty).Trim();

            IEnumerable<HueDataService.HueEntry> filtered = hueEntries;

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(entry =>
                    entry.HueId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            hueListBox.ItemsSource = filtered.ToList();
        }

        applyHueCheckBox.IsCheckedChanged += (_, _) =>
        {
            bool enabled = applyHueCheckBox.IsChecked == true;
            searchBox.IsEnabled = enabled;
            hueListBox.IsEnabled = enabled;
            descriptionText.Text = enabled ? descriptionText.Text : "No hue change will be applied.";
        };

        searchBox.TextChanged += (_, _) => RefreshHueList();

        hueListBox.SelectionChanged += (_, _) =>
        {
            HueDataService.HueEntry? selectedHue = hueListBox.SelectedItem as HueDataService.HueEntry;
            if (selectedHue == null)
            {
                descriptionText.Text = string.Empty;
                return;
            }

            string hueName = string.IsNullOrWhiteSpace(selectedHue.Name) ? "(no name)" : selectedHue.Name;

            descriptionText.Text =
                "Hue " + selectedHue.HueId + Environment.NewLine +
                "Name: " + hueName + Environment.NewLine +
                "Range: " + selectedHue.TableStart + " - " + selectedHue.TableEnd;
        };

        okButton.Click += (_, _) =>
        {
            bool applyHue = applyHueCheckBox.IsChecked == true;
            HueDataService.HueEntry? selectedHue = hueListBox.SelectedItem as HueDataService.HueEntry;

            if (applyHue && selectedHue == null)
            {
                descriptionText.Text = "Select a hue or turn off hue recolor.";
                return;
            }

            result = new VdHueDialogResult
            {
                Confirmed = true,
                ApplyHue = applyHue,
                SelectedHue = selectedHue
            };

            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            result = new VdHueDialogResult
            {
                Confirmed = false,
                ApplyHue = false,
                SelectedHue = null
            };

            dialog.Close();
        };

        hueListBox.ItemTemplate = new FuncDataTemplate<HueDataService.HueEntry?>((hueEntry, _) =>
        {
            StackPanel rootPanel = new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(4)
            };

            if (hueEntry == null)
            {
                return rootPanel;
            }

            string hueName = string.IsNullOrWhiteSpace(hueEntry.Name) ? "(no name)" : hueEntry.Name;

            rootPanel.Children.Add(new TextBlock
            {
                Text = hueEntry.HueId + " - " + hueName,
                FontWeight = FontWeight.Bold
            });

            StackPanel swatchPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 1
            };

            int previewCount = Math.Min(16, hueEntry.Colors.Count);
            for (int colorIndex = 0; colorIndex < previewCount; colorIndex++)
            {
                swatchPanel.Children.Add(new Border
                {
                    Width = 18,
                    Height = 18,
                    Background = new SolidColorBrush(hueEntry.Colors[colorIndex]),
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1)
                });
            }

            rootPanel.Children.Add(swatchPanel);

            return rootPanel;
        });

        RefreshHueList();

        Grid rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"),
            Margin = new Thickness(12)
        };

        rootGrid.Children.Add(applyHueCheckBox);
        Grid.SetRow(applyHueCheckBox, 0);

        rootGrid.Children.Add(searchBox);
        Grid.SetRow(searchBox, 1);

        rootGrid.Children.Add(hueListBox);
        Grid.SetRow(hueListBox, 2);

        rootGrid.Children.Add(descriptionText);
        Grid.SetRow(descriptionText, 3);

        StackPanel buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            Spacing = 10,
            Children =
        {
            cancelButton,
            okButton
        }
        };

        rootGrid.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 4);

        dialog.Content = rootGrid;

        await dialog.ShowDialog(owner);
        return result;
    }

    private async Task<VdEnhancementDialogResult?> ShowVdEnhancementDialogAsync(
Window owner,
VdHueDialogResult? hueDialogResult)
    {
        Window dialog = new Window
        {
            Title = "Export VD Enhancement",
            Width = 760,
            Height = 700,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        WriteableBitmap? previewSourceBitmap = BuildEnhancementPreviewSourceBitmap();

        CheckBox sharpenCheckBox = new CheckBox
        {
            Content = "Apply sharpen",
            IsChecked = false
        };

        NumericUpDown sharpenAmountBox = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 5,
            Increment = 0.1m,
            Value = 1.0m,
            Width = 120,
            IsEnabled = false,
            Margin = new Thickness(24, 6, 0, 0)
        };

        ComboBox sharpenModeComboBox = new ComboBox
        {
            ItemsSource = new[]
            {
                "Gaussian",
                "Pixel"
            },
            SelectedIndex = 0,
            Width = 140,
            IsEnabled = false,
            Margin = new Thickness(24, 6, 0, 0)
        };

        CheckBox contrastCheckBox = new CheckBox
        {
            Content = "Apply contrast boost",
            IsChecked = false,
            Margin = new Thickness(0, 12, 0, 0)
        };

        NumericUpDown contrastAmountBox = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 2,
            Increment = 0.05m,
            Value = 0.20m,
            Width = 120,
            IsEnabled = false,
            Margin = new Thickness(24, 6, 0, 0)
        };

        CheckBox outlineBoostCheckBox = new CheckBox
        {
            Content = "Apply outline boost",
            IsChecked = false,
            Margin = new Thickness(0, 12, 0, 0)
        };

        NumericUpDown outlineStrengthBox = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 1,
            Increment = 0.05m,
            Value = 0.35m,
            Width = 120,
            IsEnabled = false,
            Margin = new Thickness(24, 6, 0, 0)
        };

        TextBlock previewText = new TextBlock
        {
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        Avalonia.Controls.Image beforeImage = new Avalonia.Controls.Image
        {
            Width = 160,
            Height = 160,
            Stretch = Stretch.Uniform
        };

        Avalonia.Controls.Image afterImage = new Avalonia.Controls.Image
        {
            Width = 160,
            Height = 160,
            Stretch = Stretch.Uniform
        };

        Border beforeBorder = new Border
        {
            Width = 180,
            Height = 180,
            Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#20242B")),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = beforeImage
        };

        Border afterBorder = new Border
        {
            Width = 200,
            Height = 200,
            Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#20242B")),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = afterImage
        };

        Button okButton = new Button
        {
            Content = "OK",
            Width = 90
        };

        Button cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90
        };

        VdEnhancementDialogResult? result = null;

        void UpdatePreviewImages()
        {
            if (previewSourceBitmap == null)
            {
                beforeImage.Source = null;
                afterImage.Source = null;
                return;
            }

            WriteableBitmap beforeThumb = BuildPreviewThumbnailBitmap(previewSourceBitmap, 160, 160);
            beforeImage.Source = beforeThumb;

            VdEnhancementDialogResult previewEnhancement = new VdEnhancementDialogResult
            {
                Confirmed = true,
                ApplySharpen = sharpenCheckBox.IsChecked == true,
                ApplyContrast = contrastCheckBox.IsChecked == true,
                ApplyOutlineBoost = outlineBoostCheckBox.IsChecked == true,
                SharpenMode = sharpenModeComboBox.SelectedIndex == 1
                    ? SharpenMode.Pixel
                    : SharpenMode.Gaussian,
                SharpenAmount = (float)(sharpenAmountBox.Value ?? 0m),
                ContrastAmount = (float)(contrastAmountBox.Value ?? 0m),
                OutlineStrength = (float)(outlineStrengthBox.Value ?? 0.35m)
            };

            WriteableBitmap enhancedBitmap = ApplyEnhancementsToBitmap(previewSourceBitmap, previewEnhancement);
            WriteableBitmap finalPreviewBitmap = enhancedBitmap;

            if (hueDialogResult != null &&
                hueDialogResult.ApplyHue &&
                hueDialogResult.SelectedHue != null)
            {
                finalPreviewBitmap = ApplyHueToBitmap(enhancedBitmap, hueDialogResult.SelectedHue);
            }

            WriteableBitmap afterThumb = BuildPreviewThumbnailBitmap(finalPreviewBitmap, 160, 160);
            afterImage.Source = afterThumb;
        }

        void UpdatePreviewText()
        {
            string hueText =
                hueDialogResult != null &&
                hueDialogResult.ApplyHue &&
                hueDialogResult.SelectedHue != null
                    ? "Hue " + hueDialogResult.SelectedHue.HueId
                    : "Off";

            previewText.Text =
                "Selected enhancements:" + Environment.NewLine +
                "- Sharpen: " + ((sharpenCheckBox.IsChecked == true)
    ? ((sharpenModeComboBox.SelectedItem?.ToString() ?? "Gaussian") + " " + (sharpenAmountBox.Value?.ToString() ?? "0"))
    : "Off") + Environment.NewLine +
                "- Contrast: " + ((contrastCheckBox.IsChecked == true) ? contrastAmountBox.Value?.ToString() ?? "0" : "Off") + Environment.NewLine +
                "- Outline Boost: " + ((outlineBoostCheckBox.IsChecked == true) ? "On" : "Off") + Environment.NewLine +
                "- Hue: " + hueText + Environment.NewLine + Environment.NewLine +
                "Preview uses the current displayed frame thumbnail.";
        }

        void RefreshAllPreview()
        {
            UpdatePreviewText();
            UpdatePreviewImages();
        }

        sharpenCheckBox.IsCheckedChanged += (_, _) =>
        {
            bool enabled = sharpenCheckBox.IsChecked == true;
            sharpenAmountBox.IsEnabled = enabled;
            sharpenModeComboBox.IsEnabled = enabled;
            RefreshAllPreview();
        };

        sharpenModeComboBox.SelectionChanged += (_, _) =>
        {
            RefreshAllPreview();
        };

        contrastCheckBox.IsCheckedChanged += (_, _) =>
        {
            contrastAmountBox.IsEnabled = contrastCheckBox.IsChecked == true;
            RefreshAllPreview();
        };

        outlineBoostCheckBox.IsCheckedChanged += (_, _) =>
        {
            outlineStrengthBox.IsEnabled = outlineBoostCheckBox.IsChecked == true;
            RefreshAllPreview();
        };

        sharpenAmountBox.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(NumericUpDown.Value))
            {
                RefreshAllPreview();
            }
        };

        contrastAmountBox.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(NumericUpDown.Value))
            {
                RefreshAllPreview();
            }
        };

        outlineStrengthBox.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(NumericUpDown.Value))
            {
                RefreshAllPreview();
            }
        };

        okButton.Click += (_, _) =>
        {
            result = new VdEnhancementDialogResult
            {
                Confirmed = true,
                ApplySharpen = sharpenCheckBox.IsChecked == true,
                ApplyContrast = contrastCheckBox.IsChecked == true,
                ApplyOutlineBoost = outlineBoostCheckBox.IsChecked == true,
                SharpenMode = sharpenModeComboBox.SelectedIndex == 1 ? SharpenMode.Pixel : SharpenMode.Gaussian,
                SharpenAmount = (float)(sharpenAmountBox.Value ?? 0m),
                ContrastAmount = (float)(contrastAmountBox.Value ?? 0m),
                OutlineStrength = (float)(outlineStrengthBox.Value ?? 0.35m)
            };

            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            result = new VdEnhancementDialogResult
            {
                Confirmed = false
            };

            dialog.Close();
        };

        Grid controlsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto"),
            ColumnSpacing = 16,
            RowSpacing = 8
        };

        TextBlock titleText = new TextBlock
        {
            Text = "Choose enhancement filters for the exported animation:",
            Margin = new Thickness(0, 0, 0, 8)
        };

        Grid.SetRow(sharpenCheckBox, 0);
        Grid.SetColumn(sharpenCheckBox, 0);

        Grid.SetRow(sharpenAmountBox, 0);
        Grid.SetColumn(sharpenAmountBox, 1);

        Grid.SetRow(sharpenModeComboBox, 1);
        Grid.SetColumn(sharpenModeComboBox, 1);

        Grid.SetRow(contrastCheckBox, 2);
        Grid.SetColumn(contrastCheckBox, 0);

        Grid.SetRow(contrastAmountBox, 2);
        Grid.SetColumn(contrastAmountBox, 1);

        Grid.SetRow(outlineBoostCheckBox, 3);
        Grid.SetColumn(outlineBoostCheckBox, 0);

        Grid.SetRow(outlineStrengthBox, 3);
        Grid.SetColumn(outlineStrengthBox, 1);

        Grid.SetRow(previewText, 4);
        Grid.SetColumn(previewText, 0);
        Grid.SetColumnSpan(previewText, 2);

        controlsGrid.Children.Add(sharpenCheckBox);
        controlsGrid.Children.Add(sharpenAmountBox);
        controlsGrid.Children.Add(sharpenModeComboBox);
        controlsGrid.Children.Add(contrastCheckBox);
        controlsGrid.Children.Add(contrastAmountBox);
        controlsGrid.Children.Add(outlineBoostCheckBox);
        controlsGrid.Children.Add(outlineStrengthBox);
        controlsGrid.Children.Add(previewText);

        StackPanel controlsPanel = new StackPanel
        {
            Spacing = 4,
            Children =
    {
        titleText,
        controlsGrid
    }
        };

        StackPanel beforePanel = new StackPanel
        {
            Spacing = 6,
            Children =
        {
            new TextBlock
            {
                Text = "Before",
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            },
            beforeBorder
        }
        };

        StackPanel afterPanel = new StackPanel
        {
            Spacing = 6,
            Children =
        {
            new TextBlock
            {
                Text = "After",
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            },
            afterBorder
        }
        };

        StackPanel previewPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 16,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Children =
        {
            beforePanel,
            afterPanel
        }
        };

        Grid rootGrid = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        Grid.SetRow(controlsPanel, 0);
        Grid.SetRow(previewPanel, 1);

        rootGrid.Children.Add(controlsPanel);
        rootGrid.Children.Add(previewPanel);

        StackPanel buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Thickness(0, 12, 0, 0),
            Children =
        {
            cancelButton,
            okButton
        }
        };

        Grid.SetRow(buttonPanel, 2);
        rootGrid.Children.Add(buttonPanel);

        dialog.Content = rootGrid;

        RefreshAllPreview();

        await dialog.ShowDialog(owner);
        return result;
    }

    private async Task<CreateLegacyMulIdxDialogResult?> ShowCreateLegacyMulIdxDialogAsync(Window owner)
    {
        Window dialog = new Window
        {
            Title = "Create Empty Legacy MUL/IDX",
            Width = 500,
            Height = 500,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        TextBox fileNameTextBox = new TextBox
        {
            Text = "anim_custom",
            Watermark = "Base file name (without .mul / .idx)"
        };

        ComboBox typeComboBox = new ComboBox
        {
            ItemsSource = new[]
            {
            "H (22 actions)",
            "L (13 actions)",
            "P (35 actions)"
        },
            SelectedIndex = 0
        };

        NumericUpDown startBodyNumeric = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 65535,
            Value = 0
        };

        NumericUpDown endBodyNumeric = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 65535,
            Value = 500
        };

        TextBlock helpText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray
        };

        TextBlock modeText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Goldenrod
        };

        Button createButton = new Button
        {
            Content = "Create",
            Width = 90
        };

        Button cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90
        };

        CreateLegacyMulIdxDialogResult? result = null;

        void UpdateDialogState()
        {
            string baseFileName = (fileNameTextBox.Text ?? string.Empty).Trim();
            bool isAnimSeries = IsAnimSeriesFileName(baseFileName);

            typeComboBox.IsEnabled = !isAnimSeries;

            dialog.Title = isAnimSeries
    ? "Create Empty CUO Anim MUL/IDX"
    : "Create Empty Legacy MUL/IDX";

            helpText.Text = isAnimSeries
                ? "Creates an empty CUO-compatible anim series MUL/IDX pair. " +
                  "For anim, anim2, anim7, and other anim# files, slot layout is based on body range instead of a fixed H/L/P type."
                : "Creates an empty classic fixed-layout MUL/IDX pair using the selected animation type. " +
                  "All IDX entries are initialized as empty (-1, -1, -1).";

            modeText.Text = isAnimSeries
                ? "CUO anim-series layout:" + Environment.NewLine +
                  "Bodies 0-199 = 22 actions" + Environment.NewLine +
                  "Bodies 200-399 = 13 actions" + Environment.NewLine +
                  "Bodies 400+ = 35 actions" + Environment.NewLine +
                  "Animation type selection is ignored for anim# files."
                : "Fixed-layout mode:" + Environment.NewLine +
                  "The selected H / L / P type will be used for all bodies in the file.";
        }

        fileNameTextBox.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(TextBox.Text))
            {
                UpdateDialogState();
            }
        };

        createButton.Click += (_, _) =>
        {
            string baseFileName = (fileNameTextBox.Text ?? string.Empty).Trim();
            char typeLetter = typeComboBox.SelectedIndex switch
            {
                1 => 'L',
                2 => 'P',
                _ => 'H'
            };

            int startBody = Convert.ToInt32(startBodyNumeric.Value);
            int endBody = Convert.ToInt32(endBodyNumeric.Value);

            result = new CreateLegacyMulIdxDialogResult
            {
                Confirmed = true,
                BaseFileName = baseFileName,
                TypeLetter = typeLetter,
                StartBody = startBody,
                EndBody = endBody
            };

            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            result = null;
            dialog.Close();
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
        {
            helpText,
            modeText,
            new TextBlock { Text = "Base file name" },
            fileNameTextBox,
            new TextBlock { Text = "Animation type" },
            typeComboBox,
            new TextBlock { Text = "Start body ID" },
            startBodyNumeric,
            new TextBlock { Text = "End body ID" },
            endBodyNumeric,
            new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    cancelButton,
                    createButton
                }
            }
        }
        };

        UpdateDialogState();

        await dialog.ShowDialog(owner);
        return result;
    }

    private async Task<BodyAssignmentDialogResult?> ShowBodyAssignmentDialogAsync(
        Window owner,
        MulSlotEntry targetSlot,
        string bodyConvPath)
    {
        if (targetSlot.FileType < 2)
        {
            return new BodyAssignmentDialogResult
            {
                Confirmed = true,
                BodyId = targetSlot.TrueBodyId
            };
        }

        int suggestedBodyId = bodyConvAssignmentService.GetNextFreeBodyId(bodyConvPath);

        Window dialog = new Window
        {
            Title = "Assign Body ID",
            Width = 560,
            Height = 600,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        RadioButton autoRadio = new RadioButton
        {
            Content = "Auto assign next free body ID",
            IsChecked = true
        };

        RadioButton manualRadio = new RadioButton
        {
            Content = "Use this body ID:",
            Margin = new Thickness(0, 8, 0, 0)
        };

        NumericUpDown manualBodyIdBox = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 100000,
            Value = suggestedBodyId,
            IsEnabled = false,
            Width = 140,
            Margin = new Thickness(24, 8, 0, 0)
        };

        string defaultMobType = targetSlot.AnimLength switch
        {
            13 => "ANIMAL",
            22 => "MONSTER",
            35 => "HUMAN",
            _ => "MONSTER"
        };

        ComboBox mobTypeComboBox = new ComboBox
        {
            ItemsSource = new[]
            {
                "MONSTER",
                "SEA_MONSTER",
                "ANIMAL",
                "HUMAN",
                "EQUIPMENT"
            },
            SelectedItem = defaultMobType,
            Width = 180,
            Margin = new Thickness(0, 8, 0, 0)
        };

        TextBox commentTextBox = new TextBox
        {
            Watermark = "Optional comment for mobtypes.txt line",
            Text = "imported via viewer",
            Width = 320,
            Margin = new Thickness(0, 8, 0, 0)
        };

        TextBlock validationText = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = Brushes.OrangeRed,
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock previewText = new TextBlock
        {
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock targetInfoText = new TextBlock
        {
            Text =
                "Target file: " + targetSlot.FileName + Environment.NewLine +
                "Target slot body index: " + targetSlot.BodyIndex + Environment.NewLine +
                "Animation type: " + targetSlot.TypeLetter + " (" + targetSlot.AnimLength + " actions)"
        };

        Button okButton = new Button
        {
            Content = "OK",
            Width = 90
        };

        Button cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90
        };

        BodyAssignmentDialogResult? result = null;

        void UpdatePreview()
        {
            int selectedBodyId = autoRadio.IsChecked == true
                ? suggestedBodyId
                : (int)(manualBodyIdBox.Value ?? suggestedBodyId);

            bool invalid = false;
            string validationMessage = string.Empty;

            if (selectedBodyId < 1)
            {
                invalid = true;
                validationMessage = "Body ID must be 1 or greater.";
            }
            else if (manualRadio.IsChecked == true && bodyConvAssignmentService.BodyIdExists(bodyConvPath, selectedBodyId))
            {
                invalid = true;
                validationMessage = "That body ID is already used in bodyconv.def. Choose a different number.";
            }

            validationText.Text = validationMessage;
            okButton.IsEnabled = !invalid;

            if (invalid)
            {
                previewText.Text = "Preview unavailable until the selected body ID is valid.";
                return;
            }

            string selectedMobType = mobTypeComboBox.SelectedItem?.ToString() ?? defaultMobType;
            string selectedComment = commentTextBox.Text ?? string.Empty;

            BodyConvAssignmentService.PreviewResult preview =
    bodyConvAssignmentService.BuildPreview(
        bodyConvPath,
        selectedBodyId,
        targetSlot.FileType,
        targetSlot.BodyIndex,
        selectedComment);

            previewText.Text =
                "Preview bodyconv.def line:" + Environment.NewLine +
                preview.PreviewLine + Environment.NewLine + Environment.NewLine +
                "Preview mobtypes.txt line:" + Environment.NewLine +
                mobTypeAssignmentService.BuildPreviewLine(selectedBodyId, selectedMobType, selectedComment);
        }

        commentTextBox.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(TextBox.Text))
            {
                UpdatePreview();
            }
        };

        mobTypeComboBox.SelectionChanged += (_, _) =>
        {
            UpdatePreview();
        };

        autoRadio.IsCheckedChanged += (_, _) =>
        {
            manualBodyIdBox.IsEnabled = manualRadio.IsChecked == true;
            UpdatePreview();
        };

        manualRadio.IsCheckedChanged += (_, _) =>
        {
            manualBodyIdBox.IsEnabled = manualRadio.IsChecked == true;
            UpdatePreview();
        };

        manualBodyIdBox.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(NumericUpDown.Value))
            {
                UpdatePreview();
            }
        };

        okButton.Click += (_, _) =>
        {
            int selectedBodyId = autoRadio.IsChecked == true
                ? suggestedBodyId
                : (int)(manualBodyIdBox.Value ?? suggestedBodyId);

            if (selectedBodyId < 1)
            {
                return;
            }

            if (manualRadio.IsChecked == true && bodyConvAssignmentService.BodyIdExists(bodyConvPath, selectedBodyId))
            {
                return;
            }

            result = new BodyAssignmentDialogResult
            {
                Confirmed = true,
                BodyId = selectedBodyId,
                MobType = mobTypeComboBox.SelectedItem?.ToString() ?? defaultMobType,
                Comment = commentTextBox.Text ?? string.Empty
            };

            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            result = new BodyAssignmentDialogResult
            {
                Confirmed = false,
                BodyId = -1,
                MobType = defaultMobType,
                Comment = string.Empty
            };

            dialog.Close();
        };

        StackPanel buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Children =
        {
            cancelButton,
            okButton
        }
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 8,
            Children =
        {
            targetInfoText,
            autoRadio,
            manualRadio,
            manualBodyIdBox,
            new TextBlock { Text = "Mob type" },
            mobTypeComboBox,
            new TextBlock { Text = "Comment for mobtypes.txt and bodyconv.def" },
            commentTextBox,
            validationText,
            previewText,
            buttonPanel
        }
        };

        UpdatePreview();

        await dialog.ShowDialog(owner);
        return result;
    }

    private double ParsePercentFromPreset(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 100;
        }

        string cleaned = text.Replace("%", "", StringComparison.Ordinal).Trim();

        if (double.TryParse(cleaned, out double percent) && percent > 0)
        {
            return percent;
        }

        return 100;
    }

    private async Task<List<HueDataService.HueEntry>?> GetOrLoadHueEntriesAsync(Window owner)
    {
        if (!string.IsNullOrWhiteSpace(currentHueFilePath) &&
            cachedHueEntries != null &&
            cachedHueEntries.Count > 0 &&
            File.Exists(currentHueFilePath))
        {
            return cachedHueEntries;
        }

        string hueFilePath = await FindOrPromptForHueFileAsync(owner);
        if (string.IsNullOrWhiteSpace(hueFilePath) || !File.Exists(hueFilePath))
        {
            return null;
        }

        List<HueDataService.HueEntry> loadedEntries = hueDataService.LoadHueEntries(hueFilePath);
        if (loadedEntries.Count == 0)
        {
            StatusText = "Could not load any hues from hues.mul.";
            return null;
        }

        currentHueFilePath = hueFilePath;
        cachedHueEntries = loadedEntries;
        return cachedHueEntries;
    }


    private async Task<string> FindOrPromptForHueFileAsync(Window owner)
    {
        if (!string.IsNullOrWhiteSpace(currentHueFilePath) && File.Exists(currentHueFilePath))
        {
            return currentHueFilePath;
        }

        string currentFolderPath = GetCurrentFolderPath();

        if (!string.IsNullOrWhiteSpace(currentFolderPath) && Directory.Exists(currentFolderPath))
        {
            string candidatePath = Path.Combine(currentFolderPath, "hues.mul");
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }


        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select hues.mul",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                new FilePickerFileType("Hue file")
                {
                    Patterns = new[] { "hues.mul", "*.mul" }
                }
                }
            });

        if (files.Count == 0)
        {
            return string.Empty;
        }

        return files[0].TryGetLocalPath() ?? string.Empty;
    }

    private async Task ShowHelpAsync()
    {
        Window? owner = GetMainWindow();
        if (owner == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        Window dialog = new Window
        {
            Title = "Ultima Animation Forge Help",
            Width = 760,
            Height = 680,
            MinWidth = 640,
            MinHeight = 520,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        TextBlock helpText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text =
                "Ultima Animation Forge Help" + Environment.NewLine +
                Environment.NewLine +

                "Navigation" + Environment.NewLine +
                "- Select an animation from the left list." + Environment.NewLine +
                "- Use File, Type, Dir, and Action at the top to filter and change what is loaded." + Environment.NewLine +
                "- The frame strip under the preview shows thumbnails for the currently loaded frames." + Environment.NewLine +
                Environment.NewLine +

"Keyboard Shortcuts" + Environment.NewLine +
"- Ctrl+O = Open UO folder" + Environment.NewLine +
"- Ctrl+S = Save changes" + Environment.NewLine +
"- Ctrl+E = Export frames" + Environment.NewLine +
"- Ctrl+Shift+E = Export VD" + Environment.NewLine +
"- Ctrl+I = Import VD" + Environment.NewLine +
"- Ctrl+Shift+I = Import VD into UOP" + Environment.NewLine +
"- Ctrl+P = Play preview" + Environment.NewLine +
"- Ctrl+Shift+P = Pause preview" + Environment.NewLine +
"- Ctrl+Left = Previous frame" + Environment.NewLine +
"- Ctrl+Right = Next frame" + Environment.NewLine +
"- Ctrl+R = Replace selected frame" + Environment.NewLine +
"- Ctrl+Shift+R = Import PNG sequence" + Environment.NewLine +
"- Ctrl+Delete = Delete animation" + Environment.NewLine +
"- Ctrl+Z = Undo last frame removed or replaced" + Environment.NewLine +
"- Alt+1 = Direction North" + Environment.NewLine +
"- Alt+2 = Direction Northeast" + Environment.NewLine +
"- Alt+3 = Direction East" + Environment.NewLine +
"- Alt+4 = Direction Southeast" + Environment.NewLine +
"- Alt+5 = Direction South" + Environment.NewLine +
"- Alt+C = Toggle checker background" + Environment.NewLine +
"- Alt+L = Toggle loop playback" + Environment.NewLine +
Environment.NewLine +

"Preview and Playback" + Environment.NewLine +
"- Play and Pause control animation playback." + Environment.NewLine +
"- Prev Frame and Next Frame step through frames manually." + Environment.NewLine +
"- Zoom changes the preview size." + Environment.NewLine +
"- Speed changes playback rate." + Environment.NewLine +
"- Checker toggles the preview background and can be saved per profile." + Environment.NewLine +
"- Loop controls whether playback restarts automatically and can be saved per profile." + Environment.NewLine +
"- Dir can be changed with the dropdown, slider, or Alt+1 through Alt+5." + Environment.NewLine +
Environment.NewLine +

                "Drag and Drop" + Environment.NewLine +
                "- Drop a single PNG on the preview to replace the selected frame." + Environment.NewLine +
                "- Drop multiple PNG files on the preview to import a frame sequence." + Environment.NewLine +
                "- Drop a UO folder on the main window to load it." + Environment.NewLine +
                "- In slot view, drop a VD file on the main window to queue an import." + Environment.NewLine +
                Environment.NewLine +

                "Editing" + Environment.NewLine +
                "- Replace Frame replaces the currently selected frame thumbnail." + Environment.NewLine +
                "- Import PNG Sequence replaces the loaded direction using the selected PNG files." + Environment.NewLine +
                "- Save Changes writes queued MUL changes and frame edits to disk." + Environment.NewLine +
                "- Unsaved changes are shown in the top bar." + Environment.NewLine +
                Environment.NewLine +

                "Slot View" + Environment.NewLine +
                "- Enable MUL Slot View to work with free MUL body slots or UOP body targets." + Environment.NewLine +
                "- MUL targets are used for queued VD imports." + Environment.NewLine +
                "- UOP targets are used when importing VD into a UOP file." + Environment.NewLine +
                Environment.NewLine +

                "Export" + Environment.NewLine +
                "- Export Frames supports PNG, JPG, BMP, animated GIF, and sprite sheet PNG." + Environment.NewLine +
                "- Export VD writes a VD animation using the currently selected body." + Environment.NewLine +
                Environment.NewLine +

                "Tips" + Environment.NewLine +
                "- If a drag operation shows a blocked cursor, make sure you are dropping onto the preview area for PNG files." + Environment.NewLine +
                "- If imported art looks wrong, check palette limits and frame size alignment." + Environment.NewLine +
                "- A detachable preview window is planned for multi-monitor workflows."
        };

        Button closeButton = new Button
        {
            Content = "Close",
            Width = 100,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        closeButton.Click += (_, _) => dialog.Close();

        StackPanel contentPanel = new StackPanel
        {
            Spacing = 12,
            Children =
        {
            helpText
        }
        };

        ScrollViewer scrollViewer = new ScrollViewer
        {
            Content = contentPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        Grid rootGrid = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("*,Auto")
        };

        Grid.SetRow(scrollViewer, 0);
        Grid.SetRow(closeButton, 1);

        rootGrid.Children.Add(scrollViewer);
        rootGrid.Children.Add(closeButton);

        dialog.Content = rootGrid;

        await dialog.ShowDialog(owner);
    }

    private async Task ShowDetachedPreviewAsync()
    {
        Window? owner = GetMainWindow();
        if (owner == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        if (detachedPreviewWindow != null)
        {
            detachedPreviewWindow.Show();
            detachedPreviewWindow.Activate();
            return;
        }

        detachedPreviewViewModel ??= new DetachedPreviewViewModel(this);
        detachedPreviewViewModel.SyncFromMainIfFollowing();

        DetachedPreviewWindow window = new DetachedPreviewWindow
        {
            DataContext = detachedPreviewViewModel
        };

        detachedPreviewWindow = window;

        detachedPreviewWindow.Closed += (_, _) =>
        {
            detachedPreviewWindow = null;
        };

        detachedPreviewWindow.Show(owner);
        await Task.CompletedTask;
    }

    private async Task ShowDetachedDebugAsync()
    {
        Window? owner = GetMainWindow();
        if (owner == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        if (detachedDebugWindow != null)
        {
            detachedDebugWindow.Show();
            detachedDebugWindow.Activate();
            return;
        }

        TextBlock blockSizeLabel = new TextBlock
        {
            Text = "Block Size",
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White
        };

        TextBlock blockSizeValue = new TextBlock
        {
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        };
        blockSizeValue.Bind(TextBlock.TextProperty, new Binding(nameof(SelectedBlockSize)));

        TextBlock blockHeaderLabel = new TextBlock
        {
            Text = "Raw Block Debug",
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White
        };

        TextBox blockHeaderTextBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 360
        };
        blockHeaderTextBox.Bind(TextBox.TextProperty, new Binding(nameof(SelectedBlockHeader)));

        Button closeButton = new Button
        {
            Content = "Close",
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        Grid rootGrid = new Grid
        {
            Margin = new Thickness(14),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"),
            RowSpacing = 10
        };

        Grid.SetRow(blockSizeLabel, 0);
        Grid.SetRow(blockSizeValue, 1);
        Grid.SetRow(blockHeaderLabel, 2);
        Grid.SetRow(blockHeaderTextBox, 3);
        Grid.SetRow(closeButton, 4);

        rootGrid.Children.Add(blockSizeLabel);
        rootGrid.Children.Add(blockSizeValue);
        rootGrid.Children.Add(blockHeaderLabel);
        rootGrid.Children.Add(blockHeaderTextBox);
        rootGrid.Children.Add(closeButton);

        detachedDebugWindow = new Window
        {
            Title = "Debug",
            Width = 760,
            Height = 620,
            MinWidth = 420,
            MinHeight = 320,
            Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#1E2228")),
            Foreground = Brushes.White,
            Content = rootGrid,
            DataContext = this
        };

        closeButton.Click += (_, _) =>
        {
            detachedDebugWindow?.Close();
        };

        detachedDebugWindow.Closed += (_, _) =>
        {
            detachedDebugWindow = null;
        };

        detachedDebugWindow.Show(owner);

        await Task.CompletedTask;
    }

    private async Task<VdExportTargetDialogResult?> ShowVdExportTargetDialogAsync(Window owner)
    {
        Window dialog = new Window
        {
            Title = "VD Export Target Type",
            Width = 420,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        ComboBox targetComboBox = new ComboBox
        {
            ItemsSource = new[]
            {
                "Native UOP Creature (32 actions)",
                "MUL Animal (13 actions)",
                "MUL Monster (22 actions)",
                "MUL Human / Equipment (35 actions)"
            },
            SelectedIndex = 0,
            Margin = new Thickness(0, 8, 0, 0)
        };

        TextBlock infoText = new TextBlock
        {
            Text =
                "Choose the VD layout to export.\n" +
                "Use a MUL-compatible target if you want to import the VD back into anim.mul / anim#.mul later.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };

        Button okButton = new Button
        {
            Content = "OK",
            Width = 90
        };

        Button cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90
        };

        VdExportTargetDialogResult? result = null;

        okButton.Click += (_, _) =>
        {
            VdExportTargetType selectedTarget = targetComboBox.SelectedIndex switch
            {
                1 => VdExportTargetType.Animal13,
                2 => VdExportTargetType.Monster22,
                3 => VdExportTargetType.Human35,
                _ => VdExportTargetType.Native
            };

            result = new VdExportTargetDialogResult
            {
                Confirmed = true,
                TargetType = selectedTarget
            };

            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            result = new VdExportTargetDialogResult
            {
                Confirmed = false
            };

            dialog.Close();
        };

        StackPanel buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 10,
            Children =
            {
                cancelButton,
                okButton
            }
        };

        StackPanel rootPanel = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                infoText,
                targetComboBox,
                buttonPanel
            }
        };

        dialog.Content = rootPanel;

        await dialog.ShowDialog(owner);
        return result;
    }

    private async Task<VdExportRemapDialogResult?> ShowVdExportRemapDialogAsync(
    Window owner,
    VdExportTargetType targetType)
    {
        if (targetType == VdExportTargetType.Native)
        {
            return new VdExportRemapDialogResult
            {
                Confirmed = true,
                RemapProfile = VdExportRemapProfile.None
            };
        }

        Window dialog = new Window
        {
            Title = "VD Export Remap Profile",
            Width = 460,
            Height = 240,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        string[] options = targetType switch
        {
            VdExportTargetType.Animal13 => new[]
            {
                "None (trim only)",
                "Animal Basic"
            },
            VdExportTargetType.Monster22 => new[]
            {
                "None (trim only)",
                "Monster Basic"
            },
            VdExportTargetType.Human35 => new[]
            {
                "None (trim only)",
                "Human Basic"
            },
            _ => new[]
            {
                "None (trim only)"
            }
        };

        ComboBox remapComboBox = new ComboBox
        {
            ItemsSource = options,
            SelectedIndex = 0,
            Margin = new Thickness(0, 8, 0, 0)
        };

        TextBlock infoText = new TextBlock
        {
            Text =
                "Choose how UOP actions should be mapped into the MUL-style action layout.\n" +
                "Use 'None' to only trim by action count.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };

        Button okButton = new Button
        {
            Content = "OK",
            Width = 90
        };

        Button cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90
        };

        VdExportRemapDialogResult? result = null;

        okButton.Click += (_, _) =>
        {
            VdExportRemapProfile profile = targetType switch
            {
                VdExportTargetType.Animal13 => remapComboBox.SelectedIndex switch
                {
                    1 => VdExportRemapProfile.AnimalBasic,
                    _ => VdExportRemapProfile.None
                },
                VdExportTargetType.Monster22 => remapComboBox.SelectedIndex switch
                {
                    1 => VdExportRemapProfile.MonsterBasic,
                    _ => VdExportRemapProfile.None
                },
                VdExportTargetType.Human35 => remapComboBox.SelectedIndex switch
                {
                    1 => VdExportRemapProfile.HumanBasic,
                    _ => VdExportRemapProfile.None
                },
                _ => VdExportRemapProfile.None
            };

            result = new VdExportRemapDialogResult
            {
                Confirmed = true,
                RemapProfile = profile
            };

            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            result = new VdExportRemapDialogResult
            {
                Confirmed = false,
                RemapProfile = VdExportRemapProfile.None
            };

            dialog.Close();
        };

        StackPanel buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 10,
            Children =
            {
                cancelButton,
                okButton
            }
        };

        StackPanel rootPanel = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                infoText,
                remapComboBox,
                buttonPanel
            }
        };

        dialog.Content = rootPanel;

        await dialog.ShowDialog(owner);
        return result;
    }
}