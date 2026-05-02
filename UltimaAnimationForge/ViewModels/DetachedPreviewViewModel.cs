using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.ViewModels;

public partial class DetachedPreviewViewModel : ViewModelBase
{
    private readonly MainWindowViewModel host;
    private readonly List<WriteableBitmap> decodedFrames = new();
    private readonly Dictionary<string, int> actionNameToIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> directionNameToIndex = new(StringComparer.Ordinal);

    private int currentFrameIndex = 0;
    private DispatcherTimer? playbackTimer;

    public ObservableCollection<AnimationEntry> AnimationEntries { get; } = new();
    public ObservableCollection<string> ActionOptions { get; } = new();
    public ObservableCollection<string> DirectionOptions { get; } = new();

    [ObservableProperty]
    private bool followMainSelection = true;

    [ObservableProperty]
    private AnimationEntry? selectedAnimation;

    [ObservableProperty]
    private string? selectedAction;

    [ObservableProperty]
    private string? selectedDirection;

    [ObservableProperty]
    private WriteableBitmap? previewBitmap;

    [ObservableProperty]
    private double zoomLevel = 1.0;

    [ObservableProperty]
    private double playbackSpeed = 8.0;

    [ObservableProperty]
    private bool showCheckerBackground = true;

    [ObservableProperty]
    private string previewInfoText = "No animation loaded.";

    public DetachedPreviewViewModel(MainWindowViewModel host)
    {
        this.host = host;

        PlayCommand = new RelayCommand(PlayPreview);
        PauseCommand = new RelayCommand(PausePreview);
        PreviousFrameCommand = new RelayCommand(ShowPreviousFrame);
        NextFrameCommand = new RelayCommand(ShowNextFrame);
        CopyMainSelectionCommand = new RelayCommand(CopyMainSelectionFromHost);

        ReloadAnimationEntries();

        if (AnimationEntries.Count > 0)
        {
            SelectedAnimation = AnimationEntries[0];
        }
    }

    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand PreviousFrameCommand { get; }
    public ICommand NextFrameCommand { get; }
    public ICommand CopyMainSelectionCommand { get; }

    public string ZoomText => ZoomLevel.ToString("0.0") + "x";

    public string PlaybackSpeedText => PlaybackSpeed.ToString("0.0") + "x";

    public string CurrentFrameDisplayText =>
        decodedFrames.Count == 0
            ? "Frame: - / -"
            : "Frame: " + (currentFrameIndex + 1) + " / " + decodedFrames.Count;

    public double PreviewImageWidth =>
        PreviewBitmap != null ? PreviewBitmap.PixelSize.Width * ZoomLevel : 0;

    public double PreviewImageHeight =>
        PreviewBitmap != null ? PreviewBitmap.PixelSize.Height * ZoomLevel : 0;

    public IBrush PreviewBackgroundBrush =>
        ShowCheckerBackground
            ? new SolidColorBrush(Avalonia.Media.Color.Parse("#20242B"))
            : new SolidColorBrush(Avalonia.Media.Color.Parse("#111317"));

    public void SyncFromMainIfFollowing()
    {
        if (!FollowMainSelection)
        {
            return;
        }

        CopyMainSelectionFromHost();
    }

    public void ReloadAnimationEntries()
    {
        string? selectedKey = GetAnimationKey(SelectedAnimation);

        AnimationEntries.Clear();

        foreach (AnimationEntry entry in host.GetAnimationEntriesSnapshot())
        {
            AnimationEntries.Add(CloneAnimationEntry(entry));
        }

        if (!string.IsNullOrWhiteSpace(selectedKey))
        {
            AnimationEntry? match = AnimationEntries.FirstOrDefault(x => GetAnimationKey(x) == selectedKey);
            if (match != null)
            {
                SelectedAnimation = match;
                return;
            }
        }

        if (AnimationEntries.Count > 0 && SelectedAnimation == null)
        {
            SelectedAnimation = AnimationEntries[0];
        }
    }

    private void CopyMainSelectionFromHost()
    {
        ReloadAnimationEntries();

        AnimationEntry? hostSelected = host.SelectedAnimation;
        if (hostSelected == null)
        {
            return;
        }

        string key = GetAnimationKey(hostSelected);

        AnimationEntry? localMatch = AnimationEntries.FirstOrDefault(x => GetAnimationKey(x) == key);
        if (localMatch != null)
        {
            SelectedAnimation = localMatch;
        }

        RebuildActionList();

        if (!string.IsNullOrWhiteSpace(host.SelectedAction) && ActionOptions.Contains(host.SelectedAction))
        {
            SelectedAction = host.SelectedAction;
        }
        else if (ActionOptions.Count > 0)
        {
            SelectedAction = ActionOptions[0];
        }

        RebuildDirectionList();

        if (!string.IsNullOrWhiteSpace(host.SelectedDirection) && DirectionOptions.Contains(host.SelectedDirection))
        {
            SelectedDirection = host.SelectedDirection;
        }
        else if (DirectionOptions.Count > 0)
        {
            SelectedDirection = DirectionOptions[0];
        }

        LoadCurrentSelection();
    }

    partial void OnFollowMainSelectionChanged(bool value)
    {
        if (value)
        {
            CopyMainSelectionFromHost();
        }
    }

    partial void OnSelectedAnimationChanged(AnimationEntry? value)
    {
        if (value == null)
        {
            return;
        }

        RebuildActionList();

        if (ActionOptions.Count > 0 && (SelectedAction == null || !ActionOptions.Contains(SelectedAction)))
        {
            SelectedAction = ActionOptions[0];
        }

        RebuildDirectionList();
        LoadCurrentSelection();
    }

    partial void OnSelectedActionChanged(string? value)
    {
        RebuildDirectionList();
        LoadCurrentSelection();
    }

    partial void OnSelectedDirectionChanged(string? value)
    {
        LoadCurrentSelection();
    }

    partial void OnZoomLevelChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomText));
        OnPropertyChanged(nameof(PreviewImageWidth));
        OnPropertyChanged(nameof(PreviewImageHeight));
    }

    partial void OnPlaybackSpeedChanged(double value)
    {
        OnPropertyChanged(nameof(PlaybackSpeedText));

        if (playbackTimer != null && playbackTimer.IsEnabled)
        {
            double fps = value < 1 ? 1 : value;
            playbackTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
        }
    }

    partial void OnShowCheckerBackgroundChanged(bool value)
    {
        OnPropertyChanged(nameof(PreviewBackgroundBrush));
    }

    partial void OnPreviewBitmapChanged(WriteableBitmap? value)
    {
        OnPropertyChanged(nameof(PreviewImageWidth));
        OnPropertyChanged(nameof(PreviewImageHeight));
        OnPropertyChanged(nameof(CurrentFrameDisplayText));
    }

    private void RebuildActionList()
    {
        ActionOptions.Clear();
        actionNameToIndex.Clear();

        if (SelectedAnimation == null)
        {
            return;
        }

        List<string> options = host.BuildActionOptionsForAnimation(SelectedAnimation);

        foreach (string option in options)
        {
            ActionOptions.Add(option);

            int open = option.LastIndexOf('(');
            int close = option.LastIndexOf(')');
            if (open >= 0 && close > open)
            {
                string numberText = option.Substring(open + 1, close - open - 1);
                if (int.TryParse(numberText, out int actionIndex))
                {
                    actionNameToIndex[option] = actionIndex;
                }
            }
        }
    }

    private void RebuildDirectionList()
    {
        DirectionOptions.Clear();
        directionNameToIndex.Clear();

        if (SelectedAnimation == null)
        {
            return;
        }

        int actionIndex = GetSelectedActionIndex();
        List<string> options = host.BuildDirectionOptionsForAnimation(SelectedAnimation, actionIndex);

        foreach (string option in options)
        {
            DirectionOptions.Add(option);

            int open = option.LastIndexOf('(');
            int close = option.LastIndexOf(')');
            if (open >= 0 && close > open)
            {
                string numberText = option.Substring(open + 1, close - open - 1);
                if (int.TryParse(numberText, out int directionIndex))
                {
                    directionNameToIndex[option] = directionIndex;
                }
            }
        }

        if (DirectionOptions.Count > 0 && (SelectedDirection == null || !DirectionOptions.Contains(SelectedDirection)))
        {
            SelectedDirection = DirectionOptions[0];
        }
    }

    private int GetSelectedActionIndex()
    {
        if (SelectedAction != null && actionNameToIndex.TryGetValue(SelectedAction, out int actionIndex))
        {
            return actionIndex;
        }

        return 0;
    }

    private int GetSelectedDirectionIndex()
    {
        if (SelectedDirection != null && directionNameToIndex.TryGetValue(SelectedDirection, out int directionIndex))
        {
            return directionIndex;
        }

        return 0;
    }

    private void LoadCurrentSelection()
    {
        if (SelectedAnimation == null)
        {
            decodedFrames.Clear();
            PreviewBitmap = null;
            PreviewInfoText = "No animation selected.";
            OnPropertyChanged(nameof(CurrentFrameDisplayText));
            return;
        }

        int actionIndex = GetSelectedActionIndex();
        int directionIndex = GetSelectedDirectionIndex();

        DetachedPreviewLoadResult result = host.LoadDetachedPreview(
            SelectedAnimation,
            actionIndex,
            directionIndex);

        if (!result.Success)
        {
            decodedFrames.Clear();
            PreviewBitmap = null;
            PreviewInfoText = result.Message;
            OnPropertyChanged(nameof(CurrentFrameDisplayText));
            return;
        }

        decodedFrames.Clear();
        decodedFrames.AddRange(result.Frames);

        currentFrameIndex = 0;
        PreviewBitmap = decodedFrames.Count > 0 ? decodedFrames[0] : null;
        PreviewInfoText = result.PreviewInfoText;

        OnPropertyChanged(nameof(CurrentFrameDisplayText));
    }

    private void PlayPreview()
    {
        if (decodedFrames.Count == 0)
        {
            return;
        }

        if (playbackTimer == null)
        {
            playbackTimer = new DispatcherTimer();
            playbackTimer.Tick += PlaybackTimer_Tick;
        }

        double fps = PlaybackSpeed < 1 ? 1 : PlaybackSpeed;
        playbackTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
        playbackTimer.Start();
    }

    private void PausePreview()
    {
        playbackTimer?.Stop();
    }

    private void ShowPreviousFrame()
    {
        if (decodedFrames.Count == 0)
        {
            return;
        }

        currentFrameIndex--;
        if (currentFrameIndex < 0)
        {
            currentFrameIndex = decodedFrames.Count - 1;
        }

        PreviewBitmap = decodedFrames[currentFrameIndex];
        OnPropertyChanged(nameof(CurrentFrameDisplayText));
    }

    private void ShowNextFrame()
    {
        if (decodedFrames.Count == 0)
        {
            return;
        }

        currentFrameIndex++;
        if (currentFrameIndex >= decodedFrames.Count)
        {
            currentFrameIndex = 0;
        }

        PreviewBitmap = decodedFrames[currentFrameIndex];
        OnPropertyChanged(nameof(CurrentFrameDisplayText));
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        if (decodedFrames.Count == 0)
        {
            return;
        }

        currentFrameIndex++;
        if (currentFrameIndex >= decodedFrames.Count)
        {
            currentFrameIndex = 0;
        }

        PreviewBitmap = decodedFrames[currentFrameIndex];
        OnPropertyChanged(nameof(CurrentFrameDisplayText));
    }

    private static string GetAnimationKey(AnimationEntry? entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        return entry.BodyId + "|" + entry.SourceFile + "|" + entry.SourceMode + "|" + entry.DisplayName;
    }

    private static AnimationEntry CloneAnimationEntry(AnimationEntry source)
    {
        return new AnimationEntry
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
    }
}