using System;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    private void PlayPreview()
    {
        if (decodedFrames.Count == 0)
        {
            StatusText = "No decoded frames to play.";
            return;
        }

        if (playbackTimer == null)
        {
            playbackTimer = new DispatcherTimer();
            playbackTimer.Tick += PlaybackTimer_Tick;
        }

        double framesPerSecond = PlaybackSpeed;
        if (framesPerSecond < 1)
        {
            framesPerSecond = 1;
        }

        playbackTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / framesPerSecond);
        playbackTimer.Start();

        StatusText = "Playback started.";
    }

    private void PausePreview()
    {
        if (playbackTimer != null)
        {
            playbackTimer.Stop();
        }

        StatusText = "Playback paused.";
    }

    private void PreviousFrame()
    {
        if (decodedFrames.Count == 0)
        {
            StatusText = "No decoded frames loaded.";
            return;
        }

        currentFrameIndex--;

        if (currentFrameIndex < 0)
        {
            currentFrameIndex = decodedFrames.Count - 1;
        }

        CaptureLivePreviewSourceFromCurrentFrame();
        RefreshLivePreviewImage();
        SyncSelectedThumbnailToCurrentFrame();
        StatusText = "Showing frame " + (currentFrameIndex + 1) + " of " + decodedFrames.Count + ".";
    }

    private void NextFrame()
    {
        if (decodedFrames.Count == 0)
        {
            StatusText = "No decoded frames loaded.";
            return;
        }

        currentFrameIndex++;

        if (currentFrameIndex >= decodedFrames.Count)
        {
            currentFrameIndex = 0;
        }

        CaptureLivePreviewSourceFromCurrentFrame();
        RefreshLivePreviewImage();
        SyncSelectedThumbnailToCurrentFrame();
        StatusText = "Showing frame " + (currentFrameIndex + 1) + " of " + decodedFrames.Count + ".";
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs eventArgs)
    {
        if (decodedFrames.Count == 0)
        {
            return;
        }

        currentFrameIndex++;

        if (currentFrameIndex >= decodedFrames.Count)
        {
            if (LoopPlayback)
            {
                currentFrameIndex = 0;
            }
            else
            {
                currentFrameIndex = decodedFrames.Count - 1;

                if (playbackTimer != null)
                {
                    playbackTimer.Stop();
                }

                CaptureLivePreviewSourceFromCurrentFrame();
                RefreshLivePreviewImage();
                SyncSelectedThumbnailToCurrentFrame();
                StatusText = "Playback finished.";
                return;
            }
        }

        CaptureLivePreviewSourceFromCurrentFrame();
        RefreshLivePreviewImage();
        SyncSelectedThumbnailToCurrentFrame();
    }

    private bool WasPlaybackRunning()
    {
        return playbackTimer != null && playbackTimer.IsEnabled;
    }

    private void ResumePlaybackIfNeeded(bool shouldResume)
    {
        if (shouldResume && decodedFrames.Count > 0)
        {
            PlayPreview();
        }
    }
}