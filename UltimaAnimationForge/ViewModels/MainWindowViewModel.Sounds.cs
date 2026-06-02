using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    [ObservableProperty]
    private WriteableBitmap? soundWaveformBitmap;

    public ObservableCollection<WaveformPeak> SoundWaveformPeaks { get; } = new();

    [ObservableProperty]
    private string selectedMusicInfoText = "No music selected.";

    [ObservableProperty]
    private int editableMusicId;

    [ObservableProperty]
    private string editableMusicFileName = string.Empty;

    [ObservableProperty]
    private bool editableMusicLoop;

    [ObservableProperty]
    private bool deleteMusicFileOnRemove;

    [ObservableProperty]
    private string musicSearchText = string.Empty;

    [ObservableProperty]
    private bool importedMusicShouldLoop = true;

    private readonly MusicDataService musicDataService = new();
    private WaveOutEvent? musicOutput;
    private AudioFileReader? musicReader;

    public ObservableCollection<MusicEntry> MusicEntries { get; } = new();

    [ObservableProperty]
    private MusicEntry? selectedMusicEntry;

    [ObservableProperty]
    private string musicStatusText = "Music not loaded.";

    private readonly SoundDataService soundDataService = new();
    private SoundPlayer? soundPlayer;

    public ObservableCollection<SoundEntry> SoundEntries { get; } = new();

    [ObservableProperty]
    private SoundEntry? selectedSoundEntry;

    [ObservableProperty]
    private string soundSearchText = string.Empty;

    [ObservableProperty]
    private bool showFreeSoundSlots;

    [ObservableProperty]
    private bool sortSoundsByName;

    [ObservableProperty]
    private string soundStatusText = "Sound tab not loaded.";

    [ObservableProperty]
    private string selectedSoundInfoText = "No sound selected.";

    [RelayCommand]
    private void LoadSounds()
    {
        string folderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            SoundStatusText = "Open a UO folder first.";
            return;
        }

        if (!soundDataService.Load(folderPath))
        {
            SoundStatusText = "Could not load sound.mul / soundidx.mul.";
            return;
        }

        RebuildSoundEntries();
        RebuildSelectedSoundWaveform();
        SoundStatusText = "Loaded sounds.";
    }

    public ObservableCollection<WaveformBar> SoundWaveformBars { get; } = new();

    private void RebuildSelectedSoundWaveform()
    {
        SoundWaveformBars.Clear();
        SoundWaveformBitmap = null;

        if (SelectedSoundEntry == null || !SelectedSoundEntry.IsValid)
        {
            return;
        }

        List<WaveformPeak> peaks = soundDataService.BuildSoundWaveform(SelectedSoundEntry.Id, 260);

        SoundStatusText = "Waveform peaks: " + peaks.Count;

        if (peaks.Count == 0)
        {
            return;
        }

        SoundWaveformBitmap = BuildWaveformBitmap(peaks, 520, 96);
    }

    private static WriteableBitmap BuildWaveformBitmap(List<WaveformPeak> peaks, int width, int height)
    {
        WriteableBitmap bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        byte[] pixels = new byte[width * height * 4];

        // background
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = 0x1D;
            pixels[i + 1] = 0x16;
            pixels[i + 2] = 0x11;
            pixels[i + 3] = 0xFF;
        }

        int centerY = height / 2;

        // center line
        for (int x = 0; x < width; x++)
        {
            SetPixel(pixels, width, height, x, centerY, 0x6B, 0x60, 0x58);
        }

        if (peaks.Count == 0)
        {
            return bitmap;
        }

        double xScale = width / (double)peaks.Count;
        double maxHalfHeight = (height / 2.0) - 6.0;

        for (int i = 0; i < peaks.Count; i++)
        {
            WaveformPeak peak = peaks[i];

            double amount = Math.Max(peak.Positive, peak.Negative);
            int barHeight = Math.Max(1, (int)(amount * maxHalfHeight));

            int x = Math.Clamp((int)(i * xScale), 0, width - 1);

            int top = Math.Max(0, centerY - barHeight);
            int bottom = Math.Min(height - 1, centerY + barHeight);

            for (int y = top; y <= bottom; y++)
            {
                SetPixel(pixels, width, height, x, y, 0xFF, 0xB4, 0x7F);

                if (x + 1 < width)
                {
                    SetPixel(pixels, width, height, x + 1, y, 0xFF, 0xB4, 0x7F);
                }
            }
        }

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);

        return bitmap;
    }

    private static void SetPixel(byte[] pixels, int width, int height, int x, int y, byte blue, byte green, byte red)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return;
        }

        int offset = ((y * width) + x) * 4;

        pixels[offset + 0] = blue;
        pixels[offset + 1] = green;
        pixels[offset + 2] = red;
        pixels[offset + 3] = 0xFF;
    }

    partial void OnMusicSearchTextChanged(string value)
    {
        LoadMusic();
    }

    partial void OnSelectedMusicEntryChanged(MusicEntry? value)
    {
        if (value == null)
        {
            EditableMusicId = 0;
            EditableMusicFileName = string.Empty;
            EditableMusicLoop = false;
            SelectedMusicInfoText = "No music selected.";
            return;
        }

        EditableMusicId = value.Id;
        EditableMusicFileName = value.FileName;
        EditableMusicLoop = value.Loop;
        SelectedMusicInfoText = value.InfoText;
    }

    [RelayCommand]
    private void RebuildSoundEntries()
    {
        SoundEntries.Clear();

        foreach (SoundEntry entry in soundDataService.BuildEntries(ShowFreeSoundSlots, SoundSearchText, SortSoundsByName))
        {
            SoundEntries.Add(entry);
        }
    }

    partial void OnSoundSearchTextChanged(string value) => RebuildSoundEntries();
    partial void OnShowFreeSoundSlotsChanged(bool value) => RebuildSoundEntries();
    partial void OnSortSoundsByNameChanged(bool value) => RebuildSoundEntries();

    partial void OnSelectedSoundEntryChanged(SoundEntry? value)
    {
        SelectedSoundInfoText = value == null
            ? "No sound selected."
            : value.DisplayText + " | " + value.LengthText;

        RebuildSelectedSoundWaveform();
    }

    [RelayCommand]
    private void PlaySelectedSound()
    {
        if (SelectedSoundEntry == null || !SelectedSoundEntry.IsValid)
        {
            return;
        }

        byte[]? wavBytes = soundDataService.GetWavBytes(SelectedSoundEntry.Id);
        if (wavBytes == null)
        {
            return;
        }

        StopSound();

        MemoryStream stream = new MemoryStream(wavBytes);
        soundPlayer = new SoundPlayer(stream);
        soundPlayer.Play();

        RebuildSelectedSoundWaveform();
        SoundStatusText = "Playing " + SelectedSoundEntry.DisplayText;
    }

    [RelayCommand]
    private void StopSound()
    {
        soundPlayer?.Stop();
        soundPlayer?.Dispose();
        soundPlayer = null;
    }

    [RelayCommand]
    private void RemoveSelectedSound()
    {
        if (SelectedSoundEntry == null)
        {
            return;
        }

        soundDataService.Remove(SelectedSoundEntry.Id);
        SoundStatusText = "Removed " + SelectedSoundEntry.IdHex + ". Click Save Sounds to write changes.";
        SelectedSoundEntry = null;
        RebuildSoundEntries();
        RebuildSelectedSoundWaveform();
    }

    [RelayCommand]
    private void SaveSounds()
    {
        soundDataService.Save();
        SoundStatusText = "Saved sound.mul and soundidx.mul.";
    }

    [RelayCommand]
    private async Task ImportSelectedSoundAsync()
    {
        if (SelectedSoundEntry == null)
        {
            SoundStatusText = "Select a sound slot first.";
            return;
        }

        Window? owner = GetMainWindow();
        if (owner == null)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Import Audio As UO Sound",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
new FilePickerFileType("Audio File")
{
    Patterns = new[] { "*.wav", "*.mp3" }
}
                }
            });

        if (files.Count == 0)
        {
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        soundDataService.AddOrReplace(SelectedSoundEntry.Id, path);
        SoundStatusText = "Imported sound. Click Save Sounds to write changes.";
        RebuildSoundEntries();
        RebuildSelectedSoundWaveform();
    }

    [RelayCommand]
    private async Task ExportSelectedSoundAsync()
    {
        if (SelectedSoundEntry == null || !SelectedSoundEntry.IsValid)
        {
            return;
        }

        Window? owner = GetMainWindow();
        if (owner == null)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Export Selected Sound",
                AllowMultiple = false
            });

        if (folders.Count == 0)
        {
            return;
        }

        string? path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        soundDataService.ExportSound(SelectedSoundEntry.Id, path);
        SoundStatusText = "Exported selected sound.";
    }

    [RelayCommand]
    private async Task ExportAllSoundsAsync()
    {
        Window? owner = GetMainWindow();
        if (owner == null)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Export All Sounds",
                AllowMultiple = false
            });

        if (folders.Count == 0)
        {
            return;
        }

        string? path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        soundDataService.ExportAll(path, true);
        SoundStatusText = "Exported all sounds.";
    }

    [RelayCommand]
    private void LoadMusic()
    {
        string folderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            MusicStatusText = "Open a UO folder first.";
            return;
        }

        MusicEntries.Clear();

        foreach (MusicEntry entry in musicDataService.Load(folderPath))
        {
            if (!MatchesMusicSearch(entry))
            {
                continue;
            }

            MusicEntries.Add(entry);
        }

        MusicStatusText = "Loaded " + MusicEntries.Count + " music entries.";
    }

    [RelayCommand]
    private void PlaySelectedMusic()
    {
        if (SelectedMusicEntry == null)
        {
            return;
        }

        if (!File.Exists(SelectedMusicEntry.FullPath))
        {
            MusicStatusText = "MP3 file missing: " + SelectedMusicEntry.FileName;
            return;
        }

        StopMusic();

        musicReader = new AudioFileReader(SelectedMusicEntry.FullPath);
        musicOutput = new WaveOutEvent();
        musicOutput.Init(musicReader);
        musicOutput.Play();

        MusicStatusText = "Playing " + SelectedMusicEntry.DisplayText;
    }

    [RelayCommand]
    private void StopMusic()
    {
        musicOutput?.Stop();
        musicOutput?.Dispose();
        musicOutput = null;

        musicReader?.Dispose();
        musicReader = null;
    }

    [RelayCommand]
    private async Task ImportMusicAsync()
    {
        string folderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            MusicStatusText = "Open a UO folder first.";
            return;
        }

        Window? owner = GetMainWindow();
        if (owner == null)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Import MP3 Music",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                new FilePickerFileType("MP3 Music")
                {
                    Patterns = new[] { "*.mp3" }
                }
                }
            });

        if (files.Count == 0)
        {
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            MusicEntry imported = musicDataService.ImportMusic(folderPath, path, ImportedMusicShouldLoop);

            LoadMusic();

            SelectedMusicEntry = MusicEntries.FirstOrDefault(x => x.Id == imported.Id);

            MusicStatusText = "Imported music " + imported.FileName + " as ID " + imported.Id + ".";
        }
        catch (Exception exception)
        {
            MusicStatusText = "Import music failed: " + exception.Message;
        }
    }

    private bool MatchesMusicSearch(MusicEntry entry)
    {
        if (string.IsNullOrWhiteSpace(MusicSearchText))
        {
            return true;
        }

        string search = MusicSearchText.Trim();

        if (int.TryParse(search, out int id))
        {
            return entry.Id == id;
        }

        return entry.FileName.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void RemoveSelectedMusic()
    {
        if (SelectedMusicEntry == null)
        {
            MusicStatusText = "Select a music entry first.";
            return;
        }

        string folderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            MusicStatusText = "Open a UO folder first.";
            return;
        }

        string removedName = SelectedMusicEntry.FileName;

        try
        {
            musicDataService.RemoveMusic(folderPath, SelectedMusicEntry, DeleteMusicFileOnRemove);

            StopMusic();
            SelectedMusicEntry = null;
            LoadMusic();

            MusicStatusText = DeleteMusicFileOnRemove
                ? "Removed config entry and deleted MP3: " + removedName
                : "Removed config entry only: " + removedName;
        }
        catch (Exception exception)
        {
            MusicStatusText = "Remove music failed: " + exception.Message;
        }
    }

    [RelayCommand]
    private void SaveSelectedMusicEntry()
    {
        if (SelectedMusicEntry == null)
        {
            MusicStatusText = "Select a music entry first.";
            return;
        }

        string folderPath = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            MusicStatusText = "Open a UO folder first.";
            return;
        }

        try
        {
            musicDataService.UpdateMusicEntry(
                folderPath,
                SelectedMusicEntry,
                EditableMusicId,
                EditableMusicFileName,
                EditableMusicLoop);

            int savedId = EditableMusicId;

            LoadMusic();

            SelectedMusicEntry = MusicEntries.FirstOrDefault(x => x.Id == savedId);

            MusicStatusText = "Updated music config entry.";
        }
        catch (Exception exception)
        {
            MusicStatusText = "Update music failed: " + exception.Message;
        }
    }

    [RelayCommand]
    private void ConvertSelectedMusicToSound()
    {
        if (SelectedMusicEntry == null)
        {
            SoundStatusText = "Select a music entry first.";
            return;
        }

        if (!File.Exists(SelectedMusicEntry.FullPath))
        {
            SoundStatusText = "Music file is missing.";
            return;
        }

        if (SelectedSoundEntry == null)
        {
            SoundStatusText = "Select a target sound slot first.";
            return;
        }

        try
        {
            soundDataService.AddOrReplace(SelectedSoundEntry.Id, SelectedMusicEntry.FullPath);

            SoundStatusText =
                "Converted " + SelectedMusicEntry.FileName +
                " into sound slot " + SelectedSoundEntry.IdHex +
                ". Click Save Sounds to write sound.mul/soundidx.mul.";

            RebuildSoundEntries();
            RebuildSelectedSoundWaveform();
        }
        catch (Exception exception)
        {
            SoundStatusText = "MP3 to sound conversion failed: " + exception.Message;
        }
    }

    [RelayCommand]
    private void FindFirstFreeSoundSlot()
    {
        SoundEntry? freeSlot = soundDataService.FindFirstFreeSlot();

        if (freeSlot == null)
        {
            SoundStatusText = "No free sound slots found.";
            return;
        }

        if (!ShowFreeSoundSlots)
        {
            ShowFreeSoundSlots = true;
        }

        RebuildSoundEntries();
        RebuildSelectedSoundWaveform();

        SelectedSoundEntry = SoundEntries.FirstOrDefault(x => x.Id == freeSlot.Id);

        SoundStatusText = "Selected first free sound slot: " + freeSlot.IdHex;
    }
}