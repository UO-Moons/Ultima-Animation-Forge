using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;
using ImageSharpImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Bgra32>;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    private sealed class PropCompositeResult
    {
        public required WriteableBitmap Bitmap { get; init; }
        public int LeftPadding { get; init; }
        public int TopPadding { get; init; }
    }

    private sealed class FrameEditSnapshot
    {
        public List<VdFrameData> Frames { get; init; } = new();
        public int CurrentFrameIndex { get; init; }
        public string Description { get; init; } = string.Empty;
    }

    private sealed class FramePixels
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; }

        public FramePixels(int width, int height, byte[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }
    }

    private sealed class NaturalFileNameComparer : IComparer<string>
    {
        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            int leftIndex = 0;
            int rightIndex = 0;

            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                char leftChar = left[leftIndex];
                char rightChar = right[rightIndex];

                bool leftIsDigit = char.IsDigit(leftChar);
                bool rightIsDigit = char.IsDigit(rightChar);

                if (leftIsDigit && rightIsDigit)
                {
                    long leftValue = 0;
                    while (leftIndex < left.Length && char.IsDigit(left[leftIndex]))
                    {
                        leftValue = (leftValue * 10) + (left[leftIndex] - '0');
                        leftIndex++;
                    }

                    long rightValue = 0;
                    while (rightIndex < right.Length && char.IsDigit(right[rightIndex]))
                    {
                        rightValue = (rightValue * 10) + (right[rightIndex] - '0');
                        rightIndex++;
                    }

                    int numberCompare = leftValue.CompareTo(rightValue);
                    if (numberCompare != 0)
                    {
                        return numberCompare;
                    }

                    continue;
                }

                int charCompare = char.ToUpperInvariant(leftChar).CompareTo(char.ToUpperInvariant(rightChar));
                if (charCompare != 0)
                {
                    return charCompare;
                }

                leftIndex++;
                rightIndex++;
            }

            return left.Length.CompareTo(right.Length);
        }
    }

    partial void OnSelectedFrameThumbnailChanged(AnimationFrameThumbnail? value)
    {
        if (suppressSelectedThumbnailChanged)
        {
            return;
        }

        if (value == null)
        {
            return;
        }

        if (value.FrameIndex < 0 || value.FrameIndex >= decodedFrames.Count)
        {
            return;
        }

        if (playbackTimer != null && playbackTimer.IsEnabled)
        {
            playbackTimer.Stop();
        }

        currentFrameIndex = value.FrameIndex;
        PreviewBitmap = decodedFrames[currentFrameIndex];
        CaptureLivePreviewSourceFromCurrentFrame();
        RefreshLivePreviewImage();

        OnPropertyChanged(nameof(CurrentFrameDisplayText));
        StatusText = "Showing frame " + (currentFrameIndex + 1) + " of " + decodedFrames.Count + ".";
    }

    private void RebuildFrameThumbnails()
    {
        suppressSelectedThumbnailChanged = true;

        try
        {
            FrameThumbnails.Clear();

            for (int index = 0; index < decodedFrames.Count; index++)
            {
                FrameThumbnails.Add(new AnimationFrameThumbnail
                {
                    FrameIndex = index,
                    Bitmap = decodedFrames[index]
                });
            }

            if (decodedFrames.Count > 0 &&
                currentFrameIndex >= 0 &&
                currentFrameIndex < FrameThumbnails.Count)
            {
                SelectedFrameThumbnail = FrameThumbnails[currentFrameIndex];
            }
            else
            {
                SelectedFrameThumbnail = null;
            }
        }
        finally
        {
            suppressSelectedThumbnailChanged = false;
        }

        OnPropertyChanged(nameof(HasFrameThumbnails));
        OnPropertyChanged(nameof(CurrentFrameDisplayText));
    }

    private void ClearDecodedFramesAndThumbnails()
    {
        decodedFrames.Clear();
        editableFrames.Clear();
        currentFrameIndex = 0;
        PreviewBitmap = null;
        previewSourceBitmapBeforeLiveEffects = null;
        hasFrameEdits = false;
        compareFramePoses.Clear();
        OnPropertyChanged(nameof(CurrentComparePoseText));
        OnPropertyChanged(nameof(HasComparePoseForCurrentFrame));

        undoFrameEditStack.Clear();
        OnPropertyChanged(nameof(CanUndoFrameEdit));

        if (UndoFrameEditCommand is RelayCommand relayCommand)
        {
            relayCommand.NotifyCanExecuteChanged();
        }

        suppressSelectedThumbnailChanged = true;
        try
        {
            FrameThumbnails.Clear();
            SelectedFrameThumbnail = null;
        }
        finally
        {
            suppressSelectedThumbnailChanged = false;
        }

        OnPropertyChanged(nameof(HasFrameThumbnails));
        OnPropertyChanged(nameof(CurrentFrameDisplayText));
        OnPropertyChanged(nameof(HasFrameEdits));
    }

    private void SyncSelectedThumbnailToCurrentFrame()
    {
        if (currentFrameIndex < 0 || currentFrameIndex >= FrameThumbnails.Count)
        {
            return;
        }

        suppressSelectedThumbnailChanged = true;
        try
        {
            SelectedFrameThumbnail = FrameThumbnails[currentFrameIndex];
        }
        finally
        {
            suppressSelectedThumbnailChanged = false;
        }

        OnPropertyChanged(nameof(CurrentFrameDisplayText));
    }

    private void PushUndoSnapshot(string description)
    {
        if (editableFrames.Count == 0)
        {
            return;
        }

        FrameEditSnapshot snapshot = new FrameEditSnapshot
        {
            Frames = CloneFrameList(editableFrames),
            CurrentFrameIndex = currentFrameIndex,
            Description = description
        };

        undoFrameEditStack.Push(snapshot);
        OnPropertyChanged(nameof(CanUndoFrameEdit));

        if (UndoFrameEditCommand is RelayCommand relayCommand)
        {
            relayCommand.NotifyCanExecuteChanged();
        }
    }

    private List<VdFrameData> CloneFrameList(List<VdFrameData> sourceFrames)
    {
        List<VdFrameData> result = new List<VdFrameData>(sourceFrames.Count);

        foreach (VdFrameData frame in sourceFrames)
        {
            WriteableBitmap clonedBitmap = CloneBitmap(frame.Bitmap);

            result.Add(new VdFrameData
            {
                Bitmap = clonedBitmap,
                Palette565 = frame.Palette565 != null ? new List<ushort>(frame.Palette565) : null,
                CenterX = frame.CenterX,
                CenterY = frame.CenterY,
                Width = frame.Width,
                Height = frame.Height,
                InitCoordsX = frame.InitCoordsX,
                InitCoordsY = frame.InitCoordsY,
                EndCoordsX = frame.EndCoordsX,
                EndCoordsY = frame.EndCoordsY,
                FrameId = frame.FrameId,
                FrameNumber = frame.FrameNumber,
                DataOffset = frame.DataOffset
            });
        }

        return result;
    }

    private WriteableBitmap CloneBitmap(WriteableBitmap sourceBitmap)
    {
        using ILockedFramebuffer sourceFramebuffer = sourceBitmap.Lock();

        int width = sourceFramebuffer.Size.Width;
        int height = sourceFramebuffer.Size.Height;
        int rowBytes = sourceFramebuffer.RowBytes;
        byte[] pixels = new byte[rowBytes * height];

        Marshal.Copy(sourceFramebuffer.Address, pixels, 0, pixels.Length);

        WriteableBitmap clonedBitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer targetFramebuffer = clonedBitmap.Lock();
        Marshal.Copy(pixels, 0, targetFramebuffer.Address, pixels.Length);

        return clonedBitmap;
    }

    private void RestoreFrameSnapshot(FrameEditSnapshot snapshot)
    {
        editableFrames.Clear();
        editableFrames.AddRange(CloneFrameList(snapshot.Frames));

        currentFrameIndex = snapshot.CurrentFrameIndex;

        if (currentFrameIndex < 0)
        {
            currentFrameIndex = 0;
        }

        if (editableFrames.Count == 0)
        {
            currentFrameIndex = 0;
        }
        else if (currentFrameIndex >= editableFrames.Count)
        {
            currentFrameIndex = editableFrames.Count - 1;
        }

        RefreshDecodedFramesFromEditableFrames();
        hasFrameEdits = true;
        OnPropertyChanged(nameof(HasFrameEdits));
        RefreshUnsavedChangesState();
    }

    private void UndoFrameEdit()
    {
        if (undoFrameEditStack.Count == 0)
        {
            StatusText = "Nothing to undo.";
            return;
        }

        FrameEditSnapshot snapshot = undoFrameEditStack.Pop();
        RestoreFrameSnapshot(snapshot);

        StatusText = "Undid frame edit: " + snapshot.Description + ".";
        OnPropertyChanged(nameof(CanUndoFrameEdit));

        if (UndoFrameEditCommand is RelayCommand relayCommand)
        {
            relayCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task ReplaceSelectedFrameAsync()
    {
        if (ShowMulSlotView)
        {
            StatusText = "Frame replacement is only available in animation view.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "No animation selected.";
            return;
        }

        if (decodedFrames.Count == 0 || editableFrames.Count == 0)
        {
            StatusText = "No editable frames are loaded.";
            return;
        }

        if (SelectedFrameThumbnail == null)
        {
            StatusText = "Select a frame thumbnail first.";
            return;
        }

        int targetFrameIndex = SelectedFrameThumbnail.FrameIndex;

        if (targetFrameIndex < 0 || targetFrameIndex >= decodedFrames.Count || targetFrameIndex >= editableFrames.Count)
        {
            StatusText = "Selected frame index is out of range.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Choose PNG Frame",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("PNG Image")
                    {
                        Patterns = new[] { "*.png" }
                    }
                }
            });

        if (files.Count == 0)
        {
            StatusText = "Replace frame cancelled.";
            return;
        }

        string? pngPath = files[0].TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(pngPath))
        {
            StatusText = "Selected PNG does not have a local path.";
            return;
        }

        PushUndoSnapshot("Replace frame " + (targetFrameIndex + 1));

        await ReplaceSelectedFrameFromPathAsync(pngPath);
    }

    private async Task<WriteableBitmap?> LoadBitmapFromStorageFileAsync(IStorageFile storageFile)
    {
        if (storageFile == null)
        {
            return null;
        }

        try
        {
            await using Stream readStream = await storageFile.OpenReadAsync();
            using ImageSharpImage image = await SixLabors.ImageSharp.Image.LoadAsync<Bgra32>(readStream);

            return ConvertImageSharpToWriteableBitmap(image);
        }
        catch
        {
            return null;
        }
    }

    private void RefreshDecodedFramesFromEditableFrames()
    {
        decodedFrames.Clear();

        foreach (VdFrameData frame in editableFrames)
        {
            decodedFrames.Add(frame.Bitmap);
        }

        if (decodedFrames.Count == 0)
        {
            currentFrameIndex = 0;
            PreviewBitmap = null;
        }
        else
        {
            if (currentFrameIndex < 0 || currentFrameIndex >= decodedFrames.Count)
            {
                currentFrameIndex = 0;
            }

            PreviewBitmap = decodedFrames[currentFrameIndex];
        }

        RebuildFrameThumbnails();
        SyncSelectedThumbnailToCurrentFrame();
        OnPropertyChanged(nameof(CurrentFrameDisplayText));
    }

    private VdFrameData BuildReplacementFrameFromBitmap(WriteableBitmap replacementBitmap, VdFrameData sourceFrame)
    {
        WriteableBitmap finalBitmap = replacementBitmap;

        if (sourceFrame.Palette565 != null && sourceFrame.Palette565.Count >= 256)
        {
            finalBitmap = RemapBitmapToPalette(replacementBitmap, sourceFrame.Palette565);
        }

        return new VdFrameData
        {
            Bitmap = finalBitmap,
            Palette565 = sourceFrame.Palette565 != null
                ? new List<ushort>(sourceFrame.Palette565)
                : null,
            CenterX = sourceFrame.CenterX,
            CenterY = sourceFrame.CenterY,
            Width = (ushort)finalBitmap.PixelSize.Width,
            Height = (ushort)finalBitmap.PixelSize.Height,
            InitCoordsX = sourceFrame.InitCoordsX,
            InitCoordsY = sourceFrame.InitCoordsY,
            EndCoordsX = sourceFrame.EndCoordsX,
            EndCoordsY = sourceFrame.EndCoordsY,
            FrameId = sourceFrame.FrameId,
            FrameNumber = sourceFrame.FrameNumber,
            DataOffset = sourceFrame.DataOffset
        };
    }

    private WriteableBitmap RemapBitmapToPalette(WriteableBitmap sourceBitmap, List<ushort> palette565)
    {
        FramePixels pixels = ReadWriteableBitmapPixels(sourceBitmap);
        byte[] outputPixels = new byte[pixels.Pixels.Length];

        for (int y = 0; y < pixels.Height; y++)
        {
            for (int x = 0; x < pixels.Width; x++)
            {
                int offset = ((y * pixels.Width) + x) * 4;

                byte blue = pixels.Pixels[offset + 0];
                byte green = pixels.Pixels[offset + 1];
                byte red = pixels.Pixels[offset + 2];
                byte alpha = pixels.Pixels[offset + 3];

                if (alpha == 0)
                {
                    outputPixels[offset + 0] = 0;
                    outputPixels[offset + 1] = 0;
                    outputPixels[offset + 2] = 0;
                    outputPixels[offset + 3] = 0;
                    continue;
                }

                ushort argb1555 = ConvertBgraPixelTo1555(red, green, blue, alpha);
                ushort nearest = FindNearestPaletteColor(argb1555, palette565);

                Avalonia.Media.Color mappedColor = Convert1555ToAvaloniaColor(nearest);

                outputPixels[offset + 0] = mappedColor.B;
                outputPixels[offset + 1] = mappedColor.G;
                outputPixels[offset + 2] = mappedColor.R;
                outputPixels[offset + 3] = mappedColor.A;
            }
        }

        return CreateWriteableBitmapFromPixels(pixels.Width, pixels.Height, outputPixels);
    }

    private ushort FindNearestPaletteColor(ushort target1555, List<ushort> palette565)
    {
        if (palette565 == null || palette565.Count == 0)
        {
            return 0x8000;
        }

        int targetR = (target1555 >> 10) & 0x1F;
        int targetG = (target1555 >> 5) & 0x1F;
        int targetB = target1555 & 0x1F;

        int bestDistance = int.MaxValue;
        ushort bestColor = 0x8000;

        for (int index = 1; index < palette565.Count; index++)
        {
            ushort color = palette565[index];

            if (color == 0x8000)
            {
                continue;
            }

            int r = (color >> 10) & 0x1F;
            int g = (color >> 5) & 0x1F;
            int b = color & 0x1F;

            int distance =
                Math.Abs(targetR - r) +
                Math.Abs(targetG - g) +
                Math.Abs(targetB - b);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestColor = color;

                if (distance == 0)
                {
                    break;
                }
            }
        }

        return bestColor;
    }

    private ushort ConvertBgraPixelTo1555(byte red, byte green, byte blue, byte alpha)
    {
        if (alpha == 0)
        {
            return 0x8000;
        }

        return (ushort)(
            0x8000 |
            ((red >> 3) << 10) |
            ((green >> 3) << 5) |
            (blue >> 3));
    }

    private Avalonia.Media.Color Convert1555ToAvaloniaColor(ushort color1555)
    {
        if (color1555 == 0x8000)
        {
            return Avalonia.Media.Color.FromArgb(0, 0, 0, 0);
        }

        byte red = (byte)(((color1555 >> 10) & 0x1F) << 3);
        byte green = (byte)(((color1555 >> 5) & 0x1F) << 3);
        byte blue = (byte)((color1555 & 0x1F) << 3);

        return Avalonia.Media.Color.FromArgb(255, red, green, blue);
    }

    private int CountUniqueVisibleColors1555(WriteableBitmap bitmap)
    {
        if (bitmap == null)
        {
            return 0;
        }

        FramePixels pixels = ReadWriteableBitmapPixels(bitmap);
        HashSet<ushort> uniqueColors = new HashSet<ushort>();

        for (int y = 0; y < pixels.Height; y++)
        {
            for (int x = 0; x < pixels.Width; x++)
            {
                int offset = ((y * pixels.Width) + x) * 4;

                byte blue = pixels.Pixels[offset + 0];
                byte green = pixels.Pixels[offset + 1];
                byte red = pixels.Pixels[offset + 2];
                byte alpha = pixels.Pixels[offset + 3];

                if (alpha == 0)
                {
                    continue;
                }

                ushort color1555 = ConvertBgraPixelTo1555(red, green, blue, alpha);

                if (color1555 == 0x8000)
                {
                    color1555 = 0x8001;
                }

                uniqueColors.Add(color1555);
            }
        }

        return uniqueColors.Count;
    }

    private int CountUsablePaletteColors(List<ushort>? palette565)
    {
        if (palette565 == null || palette565.Count == 0)
        {
            return 0;
        }

        HashSet<ushort> uniqueColors = new HashSet<ushort>();

        for (int index = 1; index < palette565.Count && index < 256; index++)
        {
            ushort color = palette565[index];

            if (color == 0 || color == 0x8000)
            {
                continue;
            }

            uniqueColors.Add(color);
        }

        return uniqueColors.Count;
    }

    private int CountPaletteMisses(WriteableBitmap bitmap, List<ushort>? palette565)
    {
        if (bitmap == null || palette565 == null || palette565.Count == 0)
        {
            return 0;
        }

        HashSet<ushort> paletteColors = new HashSet<ushort>();

        for (int index = 1; index < palette565.Count && index < 256; index++)
        {
            ushort color = palette565[index];

            if (color == 0 || color == 0x8000)
            {
                continue;
            }

            paletteColors.Add(color);
        }

        if (paletteColors.Count == 0)
        {
            return 0;
        }

        FramePixels pixels = ReadWriteableBitmapPixels(bitmap);
        HashSet<ushort> importedColors = new HashSet<ushort>();

        for (int y = 0; y < pixels.Height; y++)
        {
            for (int x = 0; x < pixels.Width; x++)
            {
                int offset = ((y * pixels.Width) + x) * 4;

                byte blue = pixels.Pixels[offset + 0];
                byte green = pixels.Pixels[offset + 1];
                byte red = pixels.Pixels[offset + 2];
                byte alpha = pixels.Pixels[offset + 3];

                if (alpha == 0)
                {
                    continue;
                }

                ushort color1555 = ConvertBgraPixelTo1555(red, green, blue, alpha);

                if (color1555 == 0x8000)
                {
                    color1555 = 0x8001;
                }

                importedColors.Add(color1555);
            }
        }

        int misses = 0;

        foreach (ushort color in importedColors)
        {
            if (!paletteColors.Contains(color))
            {
                misses++;
            }
        }

        return misses;
    }

    private async Task<bool> ConfirmPaletteReductionWarningAsync(
        Window owner,
        int importedColorCount,
        int availablePaletteColorCount,
        int paletteMissCount,
        string itemLabel)
    {
        Window dialog = new Window
        {
            Title = "Palette Reduction Warning",
            Width = 420,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        bool confirmed = false;

        TextBlock warningText = new TextBlock
        {
            Text =
                itemLabel + " uses about " + importedColorCount + " visible colors." +
                Environment.NewLine +
                "Current MUL palette usable colors: about " + availablePaletteColorCount + "." +
                Environment.NewLine +
                "Imported colors not found exactly in palette: about " + paletteMissCount + "." +
                Environment.NewLine + Environment.NewLine +
                "The image will be remapped to the existing palette, " +
                Environment.NewLine +
                "which can cause color loss, banding, or shading changes." +
                Environment.NewLine + Environment.NewLine +
                "Do you want to continue?",
            TextWrapping = TextWrapping.Wrap
        };

        Button continueButton = new Button
        {
            Content = "Continue",
            Width = 90
        };

        Button cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90
        };

        continueButton.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
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
                warningText,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children =
                    {
                        cancelButton,
                        continueButton
                    }
                }
            }
        };

        await dialog.ShowDialog(owner);
        return confirmed;
    }

    private bool NeedsPaletteReductionWarning(WriteableBitmap importedBitmap, VdFrameData sourceFrame)
    {
        if (importedBitmap == null)
        {
            return false;
        }

        if (sourceFrame.Palette565 == null || sourceFrame.Palette565.Count < 256)
        {
            return false;
        }

        int importedUniqueColors = CountUniqueVisibleColors1555(importedBitmap);
        if (importedUniqueColors <= 0)
        {
            return false;
        }

        int paletteMisses = CountPaletteMisses(importedBitmap, sourceFrame.Palette565);
        if (paletteMisses <= 0)
        {
            return false;
        }

        double missRatio = (double)paletteMisses / importedUniqueColors;

        return
            paletteMisses >= 8 ||
            missRatio >= 0.15;
    }

    private async Task<List<WriteableBitmap>> LoadBitmapsFromStorageFilesAsync(IEnumerable<IStorageFile> storageFiles)
    {
        List<WriteableBitmap> result = new List<WriteableBitmap>();

        foreach (IStorageFile storageFile in storageFiles)
        {
            WriteableBitmap? bitmap = await LoadBitmapFromStorageFileAsync(storageFile);
            if (bitmap != null)
            {
                result.Add(bitmap);
            }
        }

        return result;
    }

    private List<VdFrameData> BuildReplacementFramesFromBitmaps(
        List<WriteableBitmap> importedBitmaps,
        List<VdFrameData> sourceFrames)
    {
        List<VdFrameData> result = new List<VdFrameData>();

        if (importedBitmaps == null || sourceFrames == null)
        {
            return result;
        }

        if (importedBitmaps.Count == 0 || sourceFrames.Count == 0)
        {
            return result;
        }

        int count = Math.Min(importedBitmaps.Count, sourceFrames.Count);

        for (int index = 0; index < count; index++)
        {
            result.Add(BuildReplacementFrameFromBitmap(importedBitmaps[index], sourceFrames[index]));
        }

        return result;
    }

    private async Task ImportPngSequenceAsync()
    {
        if (ShowMulSlotView)
        {
            StatusText = "PNG sequence import is only available in animation view.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "No animation selected.";
            return;
        }

        if (editableFrames.Count == 0)
        {
            StatusText = "No editable frames are loaded.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Choose PNG Sequence",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("PNG Images")
                    {
                        Patterns = new[] { "*.png" }
                    }
                }
            });

        if (files.Count == 0)
        {
            StatusText = "PNG sequence import cancelled.";
            return;
        }

        List<string> pngPaths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToList();

        if (pngPaths.Count == 0)
        {
            StatusText = "Selected PNG files do not have local paths.";
            return;
        }

        PushUndoSnapshot("Import PNG sequence");

        await ImportPngSequenceFromPathsAsync(pngPaths);
    }

    private async Task<WriteableBitmap?> LoadBitmapFromFilePathAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            await using FileStream readStream = File.OpenRead(filePath);
            using ImageSharpImage image = await SixLabors.ImageSharp.Image.LoadAsync<Bgra32>(readStream);

            return ConvertImageSharpToWriteableBitmap(image);
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<WriteableBitmap>> LoadBitmapsFromFilePathsAsync(IEnumerable<string> filePaths)
    {
        List<WriteableBitmap> results = new();

        foreach (string filePath in filePaths)
        {
            WriteableBitmap? bitmap = await LoadBitmapFromFilePathAsync(filePath);
            if (bitmap != null)
            {
                results.Add(bitmap);
            }
        }

        return results;
    }

    private async Task ReplaceSelectedFrameFromPathAsync(string pngPath)
    {
        if (ShowMulSlotView)
        {
            StatusText = "Frame replacement is only available in animation view.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "No animation selected.";
            return;
        }

        if (decodedFrames.Count == 0 || editableFrames.Count == 0)
        {
            StatusText = "No editable frames are loaded.";
            return;
        }

        if (SelectedFrameThumbnail == null)
        {
            StatusText = "Select a frame thumbnail first.";
            return;
        }

        int targetFrameIndex = SelectedFrameThumbnail.FrameIndex;

        if (targetFrameIndex < 0 || targetFrameIndex >= decodedFrames.Count || targetFrameIndex >= editableFrames.Count)
        {
            StatusText = "Selected frame index is out of range.";
            return;
        }

        WriteableBitmap? replacementBitmap = await LoadBitmapFromFilePathAsync(pngPath);

        if (replacementBitmap == null)
        {
            StatusText = "Failed to load dropped PNG.";
            return;
        }

        VdFrameData existingFrame = editableFrames[targetFrameIndex];

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        if (NeedsPaletteReductionWarning(replacementBitmap, existingFrame))
        {
            int importedColorCount = CountUniqueVisibleColors1555(replacementBitmap);
            int availablePaletteColorCount = CountUsablePaletteColors(existingFrame.Palette565);
            int paletteMissCount = CountPaletteMisses(replacementBitmap, existingFrame.Palette565);

            bool confirmed = await ConfirmPaletteReductionWarningAsync(
                mainWindow,
                importedColorCount,
                availablePaletteColorCount,
                paletteMissCount,
                "The dropped PNG");

            if (!confirmed)
            {
                StatusText = "Replace frame cancelled after palette warning.";
                return;
            }
        }

        VdFrameData replacementFrame = BuildReplacementFrameFromBitmap(replacementBitmap, existingFrame);

        editableFrames[targetFrameIndex] = replacementFrame;
        currentFrameIndex = targetFrameIndex;
        hasFrameEdits = true;
        OnPropertyChanged(nameof(HasFrameEdits));
        RefreshUnsavedChangesState();

        RefreshDecodedFramesFromEditableFrames();

        StatusText =
            "Replaced frame " + (targetFrameIndex + 1) +
            " of " + editableFrames.Count +
            " using dropped PNG.";
    }

    private async Task ImportPngSequenceFromPathsAsync(IReadOnlyList<string> pngPaths)
    {
        if (ShowMulSlotView)
        {
            StatusText = "PNG sequence import is only available in animation view.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "No animation selected.";
            return;
        }

        if (editableFrames.Count == 0)
        {
            StatusText = "No editable frames are loaded.";
            return;
        }

        List<string> orderedPaths = pngPaths
            .OrderBy(path => Path.GetFileName(path), new NaturalFileNameComparer())
            .ToList();

        List<WriteableBitmap> importedBitmaps = await LoadBitmapsFromFilePathsAsync(orderedPaths);

        if (importedBitmaps.Count == 0)
        {
            StatusText = "No PNG frames could be loaded from the drop.";
            return;
        }

        if (importedBitmaps.Count != editableFrames.Count)
        {
            StatusText =
                "PNG sequence count (" + importedBitmaps.Count +
                ") does not match current direction frame count (" + editableFrames.Count + ").";
            return;
        }

        int worstImportedColorCount = 0;
        int worstAvailablePaletteColorCount = 0;
        int worstPaletteMissCount = 0;

        for (int index = 0; index < importedBitmaps.Count; index++)
        {
            VdFrameData existingFrame = editableFrames[index];
            WriteableBitmap importedBitmap = importedBitmaps[index];

            if (!NeedsPaletteReductionWarning(importedBitmap, existingFrame))
            {
                continue;
            }

            int importedColorCount = CountUniqueVisibleColors1555(importedBitmap);
            int availablePaletteColorCount = CountUsablePaletteColors(existingFrame.Palette565);
            int paletteMissCount = CountPaletteMisses(importedBitmap, existingFrame.Palette565);

            if (paletteMissCount > worstPaletteMissCount)
            {
                worstImportedColorCount = importedColorCount;
                worstAvailablePaletteColorCount = availablePaletteColorCount;
                worstPaletteMissCount = paletteMissCount;
            }
        }

        if (worstPaletteMissCount > 0)
        {
            Window? mainWindow = GetMainWindow();
            if (mainWindow == null)
            {
                StatusText = "Could not locate main window.";
                return;
            }

            bool confirmed = await ConfirmPaletteReductionWarningAsync(
                mainWindow,
                worstImportedColorCount,
                worstAvailablePaletteColorCount,
                worstPaletteMissCount,
                "The dropped PNG sequence");

            if (!confirmed)
            {
                StatusText = "PNG sequence import cancelled after palette warning.";
                return;
            }
        }

        List<VdFrameData> replacementFrames = BuildReplacementFramesFromBitmaps(importedBitmaps, editableFrames);

        if (replacementFrames.Count != editableFrames.Count)
        {
            StatusText = "Failed to build replacement frames from dropped PNG sequence.";
            return;
        }

        editableFrames.Clear();
        editableFrames.AddRange(replacementFrames);

        currentFrameIndex = 0;
        hasFrameEdits = true;
        OnPropertyChanged(nameof(HasFrameEdits));
        RefreshUnsavedChangesState();

        RefreshDecodedFramesFromEditableFrames();

        StatusText =
            "Imported dropped PNG sequence (" + replacementFrames.Count +
            " frames) for " + SelectedAnimation.DisplayName + ".";
    }

    private async Task ReplaceFrameThumbnailAsync(object? parameter)
    {
        if (parameter is AnimationFrameThumbnail thumbnail)
        {
            if (thumbnail.FrameIndex >= 0 && thumbnail.FrameIndex < FrameThumbnails.Count)
            {
                suppressSelectedThumbnailChanged = true;
                try
                {
                    SelectedFrameThumbnail = thumbnail;
                }
                finally
                {
                    suppressSelectedThumbnailChanged = false;
                }

                if (thumbnail.FrameIndex >= 0 && thumbnail.FrameIndex < decodedFrames.Count)
                {
                    currentFrameIndex = thumbnail.FrameIndex;
                    PreviewBitmap = decodedFrames[currentFrameIndex];
                    OnPropertyChanged(nameof(CurrentFrameDisplayText));
                }
            }
        }

        await ReplaceSelectedFrameAsync();
    }

    private async Task RemoveFrameThumbnailAsync(object? parameter)
    {
        if (parameter is AnimationFrameThumbnail thumbnail)
        {
            if (thumbnail.FrameIndex >= 0 && thumbnail.FrameIndex < FrameThumbnails.Count)
            {
                suppressSelectedThumbnailChanged = true;
                try
                {
                    SelectedFrameThumbnail = thumbnail;
                }
                finally
                {
                    suppressSelectedThumbnailChanged = false;
                }

                if (thumbnail.FrameIndex >= 0 && thumbnail.FrameIndex < decodedFrames.Count)
                {
                    currentFrameIndex = thumbnail.FrameIndex;
                    PreviewBitmap = decodedFrames[currentFrameIndex];
                    OnPropertyChanged(nameof(CurrentFrameDisplayText));
                }
            }
        }

        await RemoveSelectedFrameAsync();
    }

    private async Task RemoveSelectedFrameAsync()
    {
        if (ShowMulSlotView)
        {
            StatusText = "Frame removal is only available in animation view.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "No animation selected.";
            return;
        }

        if (currentResolvedAnimationBlock == null)
        {
            StatusText = "No resolved animation block is selected.";
            return;
        }

        if (currentResolvedAnimationBlock.IsUop)
        {
            StatusText = "Frame removal is not implemented for UOP animations yet.";
            return;
        }

        if (decodedFrames.Count == 0 || editableFrames.Count == 0)
        {
            StatusText = "No editable frames are loaded.";
            return;
        }

        if (SelectedFrameThumbnail == null)
        {
            StatusText = "Select a frame thumbnail first.";
            return;
        }

        int targetFrameIndex = SelectedFrameThumbnail.FrameIndex;

        if (targetFrameIndex < 0 || targetFrameIndex >= editableFrames.Count)
        {
            StatusText = "Selected frame index is out of range.";
            return;
        }

        if (editableFrames.Count <= 1)
        {
            StatusText = "Cannot remove the last remaining frame.";
            return;
        }

        PushUndoSnapshot("Remove frame " + (targetFrameIndex + 1));

        editableFrames.RemoveAt(targetFrameIndex);

        if (currentFrameIndex >= editableFrames.Count)
        {
            currentFrameIndex = editableFrames.Count - 1;
        }

        if (currentFrameIndex < 0)
        {
            currentFrameIndex = 0;
        }

        hasFrameEdits = true;
        OnPropertyChanged(nameof(HasFrameEdits));
        RefreshUnsavedChangesState();

        RefreshDecodedFramesFromEditableFrames();

        StatusText =
            "Removed frame " + (targetFrameIndex + 1) +
            ". Click Save Changes to write the edited animation.";

        await Task.CompletedTask;
    }

    public void BeginPreviewDrag(Avalonia.Point pointerPosition, bool affectAllFrames)
    {
        if (!PreviewDragModeEnabled)
        {
            return;
        }

        if (editableFrames.Count == 0 || decodedFrames.Count == 0)
        {
            return;
        }

        previewDragActive = true;
        previewDragAffectsAllFrames = affectAllFrames;
        previewDragStartPoint = pointerPosition;
        previewDragLastAppliedDx = 0;
        previewDragLastAppliedDy = 0;
        previewDragUndoSnapshotTaken = false;

        StatusText = affectAllFrames
            ? "Dragging all frames in current direction."
            : "Dragging current frame.";
    }

    public void UpdatePreviewDrag(Avalonia.Point pointerPosition)
    {
        if (!previewDragActive || editableFrames.Count == 0 || decodedFrames.Count == 0)
        {
            return;
        }

        double scale = Math.Max(0.01, PreviewScale);

        int totalDx = (int)Math.Round((pointerPosition.X - previewDragStartPoint.X) / scale);
        int totalDy = (int)Math.Round((pointerPosition.Y - previewDragStartPoint.Y) / scale);

        int stepDx = totalDx - previewDragLastAppliedDx;
        int stepDy = totalDy - previewDragLastAppliedDy;

        if (stepDx == 0 && stepDy == 0)
        {
            return;
        }

        if (!previewDragUndoSnapshotTaken)
        {
            PushUndoSnapshot(previewDragAffectsAllFrames
                ? "Drag all frames"
                : "Drag current frame");

            previewDragUndoSnapshotTaken = true;
        }

        ApplyBitmapTranslation(stepDx, stepDy, previewDragAffectsAllFrames);

        previewDragLastAppliedDx = totalDx;
        previewDragLastAppliedDy = totalDy;

        StatusText =
            (previewDragAffectsAllFrames ? "Moved all frames by " : "Moved frame by ") +
            totalDx + ", " + totalDy + ".";
    }

    public void EndPreviewDrag()
    {
        if (!previewDragActive)
        {
            return;
        }

        previewDragActive = false;
        previewDragAffectsAllFrames = false;
        previewDragLastAppliedDx = 0;
        previewDragLastAppliedDy = 0;
        previewDragUndoSnapshotTaken = false;
    }

    private void ApplyBitmapTranslation(int dx, int dy, bool affectAllFrames)
    {
        if (dx == 0 && dy == 0)
        {
            return;
        }

        if (editableFrames.Count == 0)
        {
            return;
        }

        if (affectAllFrames)
        {
            List<VdFrameData> translated = ExpandAndTranslateFrames(editableFrames, dx, dy);
            editableFrames.Clear();
            editableFrames.AddRange(translated);
        }
        else
        {
            int frameIndex = currentFrameIndex;

            if (SelectedFrameThumbnail != null &&
                SelectedFrameThumbnail.FrameIndex >= 0 &&
                SelectedFrameThumbnail.FrameIndex < editableFrames.Count)
            {
                frameIndex = SelectedFrameThumbnail.FrameIndex;
            }

            if (frameIndex < 0 || frameIndex >= editableFrames.Count)
            {
                return;
            }

            List<VdFrameData> singleFrameList = new List<VdFrameData> { editableFrames[frameIndex] };
            List<VdFrameData> translatedSingle = ExpandAndTranslateFrames(singleFrameList, dx, dy);
            editableFrames[frameIndex] = translatedSingle[0];
            currentFrameIndex = frameIndex;
        }

        RefreshDecodedFramesFromEditableFrames();
        RebuildFrameThumbnails();
        SyncSelectedThumbnailToCurrentFrame();

        if (hasImportedSpriteSheetSession)
        {
            int directionIndex = GetSelectedDirectionIndex();
            importedSpriteSheetDirections[directionIndex] = CloneFrameList(editableFrames);
        }

        hasFrameEdits = true;
        OnPropertyChanged(nameof(HasFrameEdits));
        RefreshUnsavedChangesState();
    }

    private List<VdFrameData> ExpandAndTranslateFrames(List<VdFrameData> sourceFrames, int dx, int dy)
    {
        List<VdFrameData> result = new List<VdFrameData>();

        if (sourceFrames == null || sourceFrames.Count == 0)
        {
            return result;
        }

        int leftPad = Math.Max(0, -dx);
        int topPad = Math.Max(0, -dy);
        int rightPad = Math.Max(0, dx);
        int bottomPad = Math.Max(0, dy);

        int maxWidth = sourceFrames.Max(f => f.Bitmap.PixelSize.Width);
        int maxHeight = sourceFrames.Max(f => f.Bitmap.PixelSize.Height);

        int newWidth = maxWidth + leftPad + rightPad;
        int newHeight = maxHeight + topPad + bottomPad;

        foreach (VdFrameData frame in sourceFrames)
        {
            WriteableBitmap translatedBitmap = DrawBitmapOnExpandedCanvas(
                frame.Bitmap,
                newWidth,
                newHeight,
                leftPad + dx,
                topPad + dy);

            result.Add(new VdFrameData
            {
                Bitmap = translatedBitmap,
                Palette565 = frame.Palette565 != null ? new List<ushort>(frame.Palette565) : null,
                CenterX = (short)(frame.CenterX + leftPad),
                CenterY = (short)(frame.CenterY + topPad),
                Width = (ushort)newWidth,
                Height = (ushort)newHeight,
                InitCoordsX = frame.InitCoordsX,
                InitCoordsY = frame.InitCoordsY,
                EndCoordsX = frame.EndCoordsX,
                EndCoordsY = frame.EndCoordsY,
                FrameId = frame.FrameId,
                FrameNumber = frame.FrameNumber,
                DataOffset = frame.DataOffset
            });
        }

        return result;
    }

    private WriteableBitmap DrawBitmapOnExpandedCanvas(
        WriteableBitmap sourceBitmap,
        int canvasWidth,
        int canvasHeight,
        int drawX,
        int drawY)
    {
        using ILockedFramebuffer sourceFramebuffer = sourceBitmap.Lock();

        int sourceWidth = sourceFramebuffer.Size.Width;
        int sourceHeight = sourceFramebuffer.Size.Height;
        int sourceRowBytes = sourceFramebuffer.RowBytes;

        byte[] sourcePixels = new byte[sourceRowBytes * sourceHeight];
        Marshal.Copy(sourceFramebuffer.Address, sourcePixels, 0, sourcePixels.Length);

        int targetRowBytes = canvasWidth * 4;
        byte[] targetPixels = new byte[targetRowBytes * canvasHeight];

        for (int y = 0; y < sourceHeight; y++)
        {
            int targetY = drawY + y;
            if (targetY < 0 || targetY >= canvasHeight)
            {
                continue;
            }

            for (int x = 0; x < sourceWidth; x++)
            {
                int targetX = drawX + x;
                if (targetX < 0 || targetX >= canvasWidth)
                {
                    continue;
                }

                int srcOffset = (y * sourceRowBytes) + (x * 4);
                int dstOffset = (targetY * targetRowBytes) + (targetX * 4);

                targetPixels[dstOffset + 0] = sourcePixels[srcOffset + 0];
                targetPixels[dstOffset + 1] = sourcePixels[srcOffset + 1];
                targetPixels[dstOffset + 2] = sourcePixels[srcOffset + 2];
                targetPixels[dstOffset + 3] = sourcePixels[srcOffset + 3];
            }
        }

        WriteableBitmap result = new WriteableBitmap(
            new PixelSize(canvasWidth, canvasHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer targetFramebuffer = result.Lock();
        Marshal.Copy(targetPixels, 0, targetFramebuffer.Address, targetPixels.Length);

        return result;
    }

    private VdFrameData TranslateFrameBitmap(VdFrameData sourceFrame, int dx, int dy)
    {
        WriteableBitmap shiftedBitmap = ShiftBitmapPixels(sourceFrame.Bitmap, dx, dy);

        return new VdFrameData
        {
            Bitmap = shiftedBitmap,
            Palette565 = sourceFrame.Palette565 != null ? new List<ushort>(sourceFrame.Palette565) : null,
            CenterX = sourceFrame.CenterX,
            CenterY = sourceFrame.CenterY,
            Width = (ushort)shiftedBitmap.PixelSize.Width,
            Height = (ushort)shiftedBitmap.PixelSize.Height,
            InitCoordsX = sourceFrame.InitCoordsX,
            InitCoordsY = sourceFrame.InitCoordsY,
            EndCoordsX = sourceFrame.EndCoordsX,
            EndCoordsY = sourceFrame.EndCoordsY,
            FrameId = sourceFrame.FrameId,
            FrameNumber = sourceFrame.FrameNumber,
            DataOffset = sourceFrame.DataOffset
        };
    }

    private WriteableBitmap ShiftBitmapPixels(WriteableBitmap sourceBitmap, int dx, int dy)
    {
        using ILockedFramebuffer sourceFramebuffer = sourceBitmap.Lock();

        int width = sourceFramebuffer.Size.Width;
        int height = sourceFramebuffer.Size.Height;
        int rowBytes = sourceFramebuffer.RowBytes;

        byte[] sourcePixels = new byte[rowBytes * height];
        Marshal.Copy(sourceFramebuffer.Address, sourcePixels, 0, sourcePixels.Length);

        byte[] targetPixels = new byte[rowBytes * height];

        int copyWidth = width - Math.Abs(dx);
        int copyHeight = height - Math.Abs(dy);

        if (copyWidth > 0 && copyHeight > 0)
        {
            int srcStartX = dx < 0 ? -dx : 0;
            int srcStartY = dy < 0 ? -dy : 0;
            int dstStartX = dx > 0 ? dx : 0;
            int dstStartY = dy > 0 ? dy : 0;

            for (int y = 0; y < copyHeight; y++)
            {
                int srcOffset = ((srcStartY + y) * rowBytes) + (srcStartX * 4);
                int dstOffset = ((dstStartY + y) * rowBytes) + (dstStartX * 4);
                int bytesToCopy = copyWidth * 4;

                Buffer.BlockCopy(sourcePixels, srcOffset, targetPixels, dstOffset, bytesToCopy);
            }
        }

        WriteableBitmap shiftedBitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer targetFramebuffer = shiftedBitmap.Lock();
        Marshal.Copy(targetPixels, 0, targetFramebuffer.Address, targetPixels.Length);

        return shiftedBitmap;
    }

    private async Task ApplyCurrentDirectionEnhancementsAsync()
    {
        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        VdScaleDialogResult? scaleDialogResult = await ShowVdScaleDialogAsync(mainWindow);
        if (scaleDialogResult == null || !scaleDialogResult.Confirmed)
        {
            StatusText = "Enhancement cancelled.";
            return;
        }

        VdHueDialogResult? hueDialogResult = await ShowVdHueDialogAsync(mainWindow);
        if (hueDialogResult == null || !hueDialogResult.Confirmed)
        {
            StatusText = "Enhancement cancelled.";
            return;
        }

        VdEnhancementDialogResult? enhancementDialogResult =
            await ShowVdEnhancementDialogAsync(mainWindow, hueDialogResult);

        if (enhancementDialogResult == null || !enhancementDialogResult.Confirmed)
        {
            StatusText = "Enhancement cancelled.";
            return;
        }

        ExportRequest request = BuildEnhancementExportRequest(
            scaleDialogResult,
            hueDialogResult,
            enhancementDialogResult);

        await ApplyCurrentDirectionEnhancementsFromRequestAsync(request);
    }

    private async Task ApplyFullAnimationEnhancementsAsync()
    {
        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        VdScaleDialogResult? scaleDialogResult = await ShowVdScaleDialogAsync(mainWindow);
        if (scaleDialogResult == null || !scaleDialogResult.Confirmed)
        {
            StatusText = "Full animation enhancement cancelled.";
            return;
        }

        VdHueDialogResult? hueDialogResult = await ShowVdHueDialogAsync(mainWindow);
        if (hueDialogResult == null || !hueDialogResult.Confirmed)
        {
            StatusText = "Full animation enhancement cancelled.";
            return;
        }

        VdEnhancementDialogResult? enhancementDialogResult =
            await ShowVdEnhancementDialogAsync(mainWindow, hueDialogResult);

        if (enhancementDialogResult == null || !enhancementDialogResult.Confirmed)
        {
            StatusText = "Full animation enhancement cancelled.";
            return;
        }

        ExportRequest request = BuildEnhancementExportRequest(
            scaleDialogResult,
            hueDialogResult,
            enhancementDialogResult);

        await ApplyFullAnimationEnhancementsFromRequestAsync(request);
    }

    private ExportRequest BuildLivePreviewRequest()
    {
        return new ExportRequest
        {
            ApplySharpen = LivePreviewSharpenEnabled,
            ApplyContrast = LivePreviewContrastEnabled,
            ApplyOutlineBoost = LivePreviewOutlineEnabled,
            SharpenMode = LivePreviewSharpenModeIndex == 1 ? SharpenMode.Pixel : SharpenMode.Gaussian,
            SharpenAmount = (float)LivePreviewSharpenAmount,
            ContrastAmount = (float)LivePreviewContrastAmount,
            OutlineStrength = (float)LivePreviewOutlineStrength,

            ApplyHue = LivePreviewHueEnabled && LivePreviewSelectedHue != null,
            SelectedHue = LivePreviewSelectedHue,

            ResizeEnabled = Math.Abs(LivePreviewScalePercent - 100.0) > 0.001,
            ResizePercent = LivePreviewScalePercent,
            ResizeSampler = LivePreviewResizeSampler
        };
    }

    private async Task ConfigureLivePreviewHueAsync()
    {
        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        VdHueDialogResult? hueDialogResult = await ShowVdHueDialogAsync(mainWindow);
        if (hueDialogResult == null || !hueDialogResult.Confirmed)
        {
            return;
        }

        LivePreviewHueEnabled = hueDialogResult.ApplyHue;
        LivePreviewSelectedHue = hueDialogResult.SelectedHue;

        RefreshLivePreviewImage();
    }

    private async Task ConfigureLivePreviewScaleAsync()
    {
        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        VdScaleDialogResult? scaleDialogResult = await ShowVdScaleDialogAsync(mainWindow);
        if (scaleDialogResult == null || !scaleDialogResult.Confirmed)
        {
            return;
        }

        LivePreviewScalePercent = scaleDialogResult.ScaleFactor * 100.0;
        LivePreviewResizeSampler = scaleDialogResult.ResizeSampler;

        RefreshLivePreviewImage();
    }

    private void RefreshLivePreviewImage()
    {
        if (suppressLivePreviewRefresh)
        {
            return;
        }

        if (previewSourceBitmapBeforeLiveEffects == null)
        {
            PreviewBitmap = decodedFrames.Count > 0 && currentFrameIndex >= 0 && currentFrameIndex < decodedFrames.Count
                ? decodedFrames[currentFrameIndex]
                : null;
            return;
        }

        WriteableBitmap previewBase = CloneBitmap(previewSourceBitmapBeforeLiveEffects);

        if (editableFrames.Count > 0 &&
            currentFrameIndex >= 0 &&
            currentFrameIndex < editableFrames.Count)
        {
            previewBase = BuildPreviewWithActiveOverlays(editableFrames[currentFrameIndex]);
        }

        ExportRequest request = BuildLivePreviewRequest();
        PreviewBitmap = ApplyExportEffects(previewBase, request);
        RefreshCompareSidePreviewImage();
    }

    private void InvalidateCompareOverlayCache()
    {
        compareOverlayFrames.Clear();
        compareOverlayCacheKey = string.Empty;
    }

    private void ClearCompareOverlay()
    {
        compareOverlayEnabled = false;
        compareSelectedAnimation = null;
        compareOverlayActionIndex = 0;
        compareOverlayDirectionIndex = 0;
        compareOverlayFrameIndex = 0;
        compareOverlaySyncMode = "Same Frame";
        compareOverlayOffsetX = 0;
        compareOverlayOffsetY = 0;
        compareOverlayOpacityPercent = 50.0;
        compareFramePoses.Clear();
        InvalidateCompareOverlayCache();

        OnPropertyChanged(nameof(CompareOverlayEnabled));
        OnPropertyChanged(nameof(CompareSelectedAnimation));
        OnPropertyChanged(nameof(CompareOverlayActionIndex));
        OnPropertyChanged(nameof(CompareOverlayDirectionIndex));
        OnPropertyChanged(nameof(CompareOverlayFrameIndex));
        OnPropertyChanged(nameof(CompareOverlaySyncMode));
        OnPropertyChanged(nameof(IsCompareOverlayManualFrameMode));
        OnPropertyChanged(nameof(CompareOverlayOffsetX));
        OnPropertyChanged(nameof(CompareOverlayOffsetY));
        OnPropertyChanged(nameof(CompareOverlayOpacityPercent));
        OnPropertyChanged(nameof(HasCompareOverlayTarget));
        OnPropertyChanged(nameof(CompareOverlaySummaryText));
        OnPropertyChanged(nameof(CurrentComparePoseText));
        OnPropertyChanged(nameof(HasComparePoseForCurrentFrame));

        RefreshLivePreviewImage();
        StatusText = "Cleared compare overlay.";
    }

    private WriteableBitmap BuildPreviewWithActiveOverlays(VdFrameData sourceFrame)
    {
        WriteableBitmap previewBitmap = CloneBitmap(sourceFrame.Bitmap);

        if (PropOverlayEnabled &&
            loadedPropOverlayBitmap != null)
        {
            previewBitmap = BuildPreviewWithPropOverlay(sourceFrame);
        }

        if (CompareOverlayEnabled &&
            CompareSelectedAnimation != null)
        {
            previewBitmap = BuildPreviewWithCompareOverlay(previewBitmap);
        }

        return previewBitmap;
    }

    private string BuildCompareOverlayCacheKey()
    {
        if (CompareSelectedAnimation == null)
        {
            return string.Empty;
        }

        return
            NormalizeAnimationFileNameForCompare(CompareSelectedAnimation.SourceFile) + "|" +
            CompareSelectedAnimation.SourceMode + "|" +
            CompareSelectedAnimation.BodyId + "|" +
            Math.Max(0, CompareOverlayActionIndex) + "|" +
            Math.Clamp(CompareOverlayDirectionIndex, 0, 4);
    }

    private bool EnsureCompareOverlayFramesLoaded()
    {
        if (CompareSelectedAnimation == null)
        {
            return false;
        }

        string cacheKey = BuildCompareOverlayCacheKey();

        if (compareOverlayFrames.Count > 0 &&
            string.Equals(compareOverlayCacheKey, cacheKey, StringComparison.Ordinal))
        {
            return true;
        }

        compareOverlayFrames.Clear();
        compareOverlayCacheKey = string.Empty;

        DetachedPreviewLoadResult result = LoadDetachedPreview(
            CompareSelectedAnimation,
            Math.Max(0, CompareOverlayActionIndex),
            Math.Clamp(CompareOverlayDirectionIndex, 0, 4));

        if (!result.Success || result.Frames == null || result.Frames.Count == 0)
        {
            return false;
        }

        compareOverlayFrames.AddRange(result.Frames);
        compareOverlayCacheKey = cacheKey;
        return true;
    }

    private void SaveComparePoseForCurrentFrame()
    {
        if (currentFrameIndex < 0 || editableFrames.Count == 0)
        {
            StatusText = "No frame is loaded.";
            return;
        }

        compareFramePoses[currentFrameIndex] = new CompareFramePose
        {
            OffsetX = CompareOverlayOffsetX,
            OffsetY = CompareOverlayOffsetY
        };

        OnPropertyChanged(nameof(CurrentComparePoseText));
        OnPropertyChanged(nameof(HasComparePoseForCurrentFrame));

        RefreshLivePreviewImage();
        StatusText = "Saved compare pose for frame " + (currentFrameIndex + 1) + ".";
    }

    private void CopyComparePoseFromPreviousFrame()
    {
        if (currentFrameIndex <= 0)
        {
            StatusText = "There is no previous frame to copy from.";
            return;
        }

        if (!compareFramePoses.TryGetValue(currentFrameIndex - 1, out CompareFramePose? previousPose))
        {
            StatusText = "Previous frame does not have a saved compare pose.";
            return;
        }

        CompareOverlayOffsetX = previousPose.OffsetX;
        CompareOverlayOffsetY = previousPose.OffsetY;

        RefreshLivePreviewImage();
        StatusText = "Copied compare pose from frame " + currentFrameIndex + ".";
    }

    private void ClearComparePoseForCurrentFrame()
    {
        if (currentFrameIndex < 0)
        {
            StatusText = "No frame is selected.";
            return;
        }

        if (compareFramePoses.Remove(currentFrameIndex))
        {
            OnPropertyChanged(nameof(CurrentComparePoseText));
            OnPropertyChanged(nameof(HasComparePoseForCurrentFrame));
            RefreshLivePreviewImage();
            StatusText = "Cleared compare pose for frame " + (currentFrameIndex + 1) + ".";
            return;
        }

        StatusText = "Current frame does not have a saved compare pose.";
    }

    private CompareFramePose GetEffectiveComparePoseForFrame(int frameIndex)
    {
        if (compareFramePoses.TryGetValue(frameIndex, out CompareFramePose? pose))
        {
            return pose;
        }

        return new CompareFramePose
        {
            OffsetX = CompareOverlayOffsetX,
            OffsetY = CompareOverlayOffsetY
        };
    }

    private WriteableBitmap BuildPreviewWithCompareOverlay(WriteableBitmap baseBitmap)
    {
        if (!EnsureCompareOverlayFramesLoaded())
        {
            return CloneBitmap(baseBitmap);
        }

        int overlayFrameIndex = ResolveCompareOverlayFrameIndex(currentFrameIndex);

        overlayFrameIndex = Math.Clamp(
            overlayFrameIndex,
            0,
            Math.Max(0, compareOverlayFrames.Count - 1));

        if (overlayFrameIndex < 0 || overlayFrameIndex >= compareOverlayFrames.Count)
        {
            return CloneBitmap(baseBitmap);
        }

        WriteableBitmap overlayBitmap = compareOverlayFrames[overlayFrameIndex];
        CompareFramePose pose = GetEffectiveComparePoseForFrame(currentFrameIndex);

        return CompositeCompareOntoBase(
            baseBitmap,
            overlayBitmap,
            pose.OffsetX,
            pose.OffsetY,
            CompareOverlayOpacityPercent);
    }

    private void RefreshCompareSidePreviewImage()
    {
        if (!CompareSideBySideEnabled || CompareSelectedAnimation == null)
        {
            CompareSidePreviewBitmap = null;
            OnPropertyChanged(nameof(CompareSidePreviewInfoText));
            return;
        }

        if (!EnsureCompareOverlayFramesLoaded() || compareOverlayFrames.Count == 0)
        {
            CompareSidePreviewBitmap = null;
            OnPropertyChanged(nameof(CompareSidePreviewInfoText));
            return;
        }

        int overlayFrameIndex = ResolveCompareOverlayFrameIndex(currentFrameIndex);

        overlayFrameIndex = Math.Clamp(
            overlayFrameIndex,
            0,
            Math.Max(0, compareOverlayFrames.Count - 1));

        if (overlayFrameIndex < 0 || overlayFrameIndex >= compareOverlayFrames.Count)
        {
            CompareSidePreviewBitmap = null;
            OnPropertyChanged(nameof(CompareSidePreviewInfoText));
            return;
        }

        CompareSidePreviewBitmap = CloneBitmap(compareOverlayFrames[overlayFrameIndex]);
        OnPropertyChanged(nameof(CompareSidePreviewInfoText));
    }

    private WriteableBitmap CompositeCompareOntoBase(
        WriteableBitmap baseBitmap,
        WriteableBitmap overlayBitmap,
        int offsetX,
        int offsetY,
        double opacityPercent)
    {
        FramePixels basePixels = ReadWriteableBitmapPixels(baseBitmap);
        FramePixels overlayPixels = ReadWriteableBitmapPixels(overlayBitmap);

        int drawX = (basePixels.Width / 2) - (overlayPixels.Width / 2) + offsetX;
        int drawY = (basePixels.Height / 2) - (overlayPixels.Height / 2) + offsetY;

        int minX = Math.Min(0, drawX);
        int minY = Math.Min(0, drawY);
        int maxX = Math.Max(basePixels.Width, drawX + overlayPixels.Width);
        int maxY = Math.Max(basePixels.Height, drawY + overlayPixels.Height);

        int leftPadding = -minX;
        int topPadding = -minY;
        int outputWidth = maxX - minX;
        int outputHeight = maxY - minY;

        byte[] outputPixels = new byte[outputWidth * outputHeight * 4];

        DrawBitmapOntoBuffer(
            outputPixels,
            outputWidth,
            outputHeight,
            basePixels,
            leftPadding,
            topPadding);

        DrawBitmapOntoBufferWithOpacity(
            outputPixels,
            outputWidth,
            outputHeight,
            overlayPixels,
            drawX + leftPadding,
            drawY + topPadding,
            opacityPercent);

        return CreateWriteableBitmapFromPixels(outputWidth, outputHeight, outputPixels);
    }

    private void DrawBitmapOntoBufferWithOpacity(
        byte[] destinationPixels,
        int destinationWidth,
        int destinationHeight,
        FramePixels sourcePixels,
        int destinationX,
        int destinationY,
        double opacityPercent)
    {
        double opacity = Math.Clamp(opacityPercent / 100.0, 0.0, 1.0);

        for (int y = 0; y < sourcePixels.Height; y++)
        {
            int targetY = destinationY + y;
            if (targetY < 0 || targetY >= destinationHeight)
            {
                continue;
            }

            for (int x = 0; x < sourcePixels.Width; x++)
            {
                int targetX = destinationX + x;
                if (targetX < 0 || targetX >= destinationWidth)
                {
                    continue;
                }

                int sourceOffset = ((y * sourcePixels.Width) + x) * 4;

                byte srcB = sourcePixels.Pixels[sourceOffset + 0];
                byte srcG = sourcePixels.Pixels[sourceOffset + 1];
                byte srcR = sourcePixels.Pixels[sourceOffset + 2];
                byte srcA = sourcePixels.Pixels[sourceOffset + 3];

                if (srcA == 0)
                {
                    continue;
                }

                srcA = (byte)Math.Clamp(Math.Round(srcA * opacity), 0, 255);
                if (srcA == 0)
                {
                    continue;
                }

                int destinationOffset = ((targetY * destinationWidth) + targetX) * 4;

                byte dstB = destinationPixels[destinationOffset + 0];
                byte dstG = destinationPixels[destinationOffset + 1];
                byte dstR = destinationPixels[destinationOffset + 2];
                byte dstA = destinationPixels[destinationOffset + 3];

                BlendSourceOver(
                    srcB, srcG, srcR, srcA,
                    dstB, dstG, dstR, dstA,
                    out byte outB, out byte outG, out byte outR, out byte outA);

                destinationPixels[destinationOffset + 0] = outB;
                destinationPixels[destinationOffset + 1] = outG;
                destinationPixels[destinationOffset + 2] = outR;
                destinationPixels[destinationOffset + 3] = outA;
            }
        }
    }

    public void BeginCompareOverlayDrag(Avalonia.Point pointerPosition)
    {
        if (!CompareOverlayDragModeEnabled)
        {
            return;
        }

        if (!CompareOverlayEnabled || CompareSelectedAnimation == null)
        {
            StatusText = "Enable compare overlay and choose a compare animation first.";
            return;
        }

        if (!EnsureCompareOverlayFramesLoaded())
        {
            StatusText = "Could not load compare overlay frames.";
            return;
        }

        compareOverlayDragActive = true;
        compareOverlayDragStartPoint = pointerPosition;
        compareOverlayDragStartOffsetX = CompareOverlayOffsetX;
        compareOverlayDragStartOffsetY = CompareOverlayOffsetY;

        StatusText = "Dragging compare overlay.";
    }

    public void UpdateCompareOverlayDrag(Avalonia.Point pointerPosition)
    {
        if (!compareOverlayDragActive)
        {
            return;
        }

        double scale = Math.Max(0.01, PreviewScale);

        int totalDx = (int)Math.Round((pointerPosition.X - compareOverlayDragStartPoint.X) / scale);
        int totalDy = (int)Math.Round((pointerPosition.Y - compareOverlayDragStartPoint.Y) / scale);

        int newOffsetX = compareOverlayDragStartOffsetX + totalDx;
        int newOffsetY = compareOverlayDragStartOffsetY + totalDy;

        if (newOffsetX == CompareOverlayOffsetX &&
            newOffsetY == CompareOverlayOffsetY)
        {
            return;
        }

        CompareOverlayOffsetX = newOffsetX;
        CompareOverlayOffsetY = newOffsetY;

        StatusText =
            "Compare overlay offset: X " + CompareOverlayOffsetX +
            ", Y " + CompareOverlayOffsetY + ".";
    }

    public void EndCompareOverlayDrag()
    {
        if (!compareOverlayDragActive)
        {
            return;
        }

        compareOverlayDragActive = false;
    }

    public bool IsAnyPreviewDragActive()
    {
        return previewDragActive || compareOverlayDragActive;
    }

    private void ApplyCompareOverlayToCurrentFrame()
    {
        if (ShowMulSlotView)
        {
            StatusText = "Compare overlay is only available in animation view.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "No animation selected.";
            return;
        }

        if (!CompareOverlayEnabled || CompareSelectedAnimation == null)
        {
            StatusText = "Enable compare overlay and choose a compare animation first.";
            return;
        }

        if (editableFrames.Count == 0)
        {
            StatusText = "No editable frames are loaded.";
            return;
        }

        if (!EnsureCompareOverlayFramesLoaded() || compareOverlayFrames.Count == 0)
        {
            StatusText = "No compare overlay frames are loaded.";
            return;
        }

        int targetFrameIndex = currentFrameIndex;

        if (SelectedFrameThumbnail != null &&
            SelectedFrameThumbnail.FrameIndex >= 0 &&
            SelectedFrameThumbnail.FrameIndex < editableFrames.Count)
        {
            targetFrameIndex = SelectedFrameThumbnail.FrameIndex;
        }

        if (targetFrameIndex < 0 || targetFrameIndex >= editableFrames.Count)
        {
            StatusText = "Selected frame index is out of range.";
            return;
        }

        PushUndoSnapshot("Apply compare overlay to frame " + (targetFrameIndex + 1));

        editableFrames[targetFrameIndex] = ApplyCompareOverlayToFrame(
            editableFrames[targetFrameIndex],
            targetFrameIndex);

        hasFrameEdits = true;
        OnPropertyChanged(nameof(HasFrameEdits));
        RefreshUnsavedChangesState();

        RefreshDecodedFramesFromEditableFrames();

        currentFrameIndex = Math.Clamp(targetFrameIndex, 0, Math.Max(0, decodedFrames.Count - 1));
        CaptureLivePreviewSourceFromCurrentFrame();
        RefreshLivePreviewImage();

        StatusText =
            "Applied compare overlay to frame " + (targetFrameIndex + 1) +
            ". Click Save Changes to write the edit.";
    }

    private void ApplyCompareOverlayToCurrentDirection()
    {
        if (ShowMulSlotView)
        {
            StatusText = "Compare overlay is only available in animation view.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "No animation selected.";
            return;
        }

        if (!CompareOverlayEnabled || CompareSelectedAnimation == null)
        {
            StatusText = "Enable compare overlay and choose a compare animation first.";
            return;
        }

        if (editableFrames.Count == 0)
        {
            StatusText = "No editable frames are loaded.";
            return;
        }

        if (!EnsureCompareOverlayFramesLoaded() || compareOverlayFrames.Count == 0)
        {
            StatusText = "No compare overlay frames are loaded.";
            return;
        }

        PushUndoSnapshot("Apply compare overlay to current direction");

        for (int i = 0; i < editableFrames.Count; i++)
        {
            editableFrames[i] = ApplyCompareOverlayToFrame(editableFrames[i], i);
        }

        hasFrameEdits = true;
        OnPropertyChanged(nameof(HasFrameEdits));
        RefreshUnsavedChangesState();

        RefreshDecodedFramesFromEditableFrames();
        CaptureLivePreviewSourceFromCurrentFrame();
        RefreshLivePreviewImage();

        StatusText =
            "Applied compare overlay to all loaded frames in the current direction. " +
            "Click Save Changes to write the edit.";
    }

    private VdFrameData ApplyCompareOverlayToFrame(VdFrameData sourceFrame, int frameIndex)
    {
        if (!EnsureCompareOverlayFramesLoaded() || compareOverlayFrames.Count == 0)
        {
            return sourceFrame;
        }

        int overlayFrameIndex = ResolveCompareOverlayFrameIndex(frameIndex);

        overlayFrameIndex = Math.Clamp(
            overlayFrameIndex,
            0,
            Math.Max(0, compareOverlayFrames.Count - 1));

        if (overlayFrameIndex < 0 || overlayFrameIndex >= compareOverlayFrames.Count)
        {
            return sourceFrame;
        }

        WriteableBitmap overlayBitmap = compareOverlayFrames[overlayFrameIndex];
        CompareFramePose pose = GetEffectiveComparePoseForFrame(frameIndex);

        CompareCompositeResult composite = CompositeCompareOntoFrame(
            sourceFrame.Bitmap,
            overlayBitmap,
            pose.OffsetX,
            pose.OffsetY,
            CompareOverlayOpacityPercent);

        WriteableBitmap finalBitmap = composite.Bitmap;

        if (sourceFrame.Palette565 != null && sourceFrame.Palette565.Count >= 256)
        {
            finalBitmap = RemapBitmapToPalette(finalBitmap, sourceFrame.Palette565);
        }

        return new VdFrameData
        {
            Bitmap = finalBitmap,
            Palette565 = sourceFrame.Palette565 != null
                ? new List<ushort>(sourceFrame.Palette565)
                : null,
            CenterX = (short)(sourceFrame.CenterX + composite.LeftPadding),
            CenterY = (short)(sourceFrame.CenterY + composite.TopPadding),
            Width = (ushort)finalBitmap.PixelSize.Width,
            Height = (ushort)finalBitmap.PixelSize.Height,
            InitCoordsX = (short)(sourceFrame.InitCoordsX + composite.LeftPadding),
            InitCoordsY = (short)(sourceFrame.InitCoordsY + composite.TopPadding),
            EndCoordsX = (short)(sourceFrame.EndCoordsX + composite.LeftPadding),
            EndCoordsY = (short)(sourceFrame.EndCoordsY + composite.TopPadding),
            FrameId = sourceFrame.FrameId,
            FrameNumber = sourceFrame.FrameNumber,
            DataOffset = sourceFrame.DataOffset
        };
    }

    private sealed class CompareCompositeResult
    {
        public required WriteableBitmap Bitmap { get; init; }
        public int LeftPadding { get; init; }
        public int TopPadding { get; init; }
    }

    private CompareCompositeResult CompositeCompareOntoFrame(
    WriteableBitmap baseBitmap,
    WriteableBitmap overlayBitmap,
    int offsetX,
    int offsetY,
    double opacityPercent)
    {
        FramePixels basePixels = ReadWriteableBitmapPixels(baseBitmap);
        FramePixels overlayPixels = ReadWriteableBitmapPixels(overlayBitmap);

        int drawX = (basePixels.Width / 2) - (overlayPixels.Width / 2) + offsetX;
        int drawY = (basePixels.Height / 2) - (overlayPixels.Height / 2) + offsetY;

        int minX = Math.Min(0, drawX);
        int minY = Math.Min(0, drawY);
        int maxX = Math.Max(basePixels.Width, drawX + overlayPixels.Width);
        int maxY = Math.Max(basePixels.Height, drawY + overlayPixels.Height);

        int leftPadding = -minX;
        int topPadding = -minY;
        int outputWidth = maxX - minX;
        int outputHeight = maxY - minY;

        byte[] outputPixels = new byte[outputWidth * outputHeight * 4];

        DrawBitmapOntoBuffer(
            outputPixels,
            outputWidth,
            outputHeight,
            basePixels,
            leftPadding,
            topPadding);

        DrawBitmapOntoBufferWithOpacity(
            outputPixels,
            outputWidth,
            outputHeight,
            overlayPixels,
            drawX + leftPadding,
            drawY + topPadding,
            opacityPercent);

        return new CompareCompositeResult
        {
            Bitmap = CreateWriteableBitmapFromPixels(outputWidth, outputHeight, outputPixels),
            LeftPadding = leftPadding,
            TopPadding = topPadding
        };
    }

    private int ResolveCompareOverlayFrameIndex(int primaryFrameIndex)
    {
        if (compareOverlayFrames.Count == 0)
        {
            return 0;
        }

        int maxIndex = compareOverlayFrames.Count - 1;

        switch (CompareOverlaySyncMode)
        {
            case "Same Frame":
                return primaryFrameIndex;

            case "Loop Secondary":
                if (compareOverlayFrames.Count <= 0)
                {
                    return 0;
                }

                int wrapped = primaryFrameIndex % compareOverlayFrames.Count;
                if (wrapped < 0)
                {
                    wrapped += compareOverlayFrames.Count;
                }

                return wrapped;

            case "Clamp Secondary":
                return Math.Clamp(primaryFrameIndex, 0, maxIndex);

            case "Manual Frame":
                return Math.Clamp(CompareOverlayFrameIndex, 0, maxIndex);

            default:
                return primaryFrameIndex;
        }
    }

    private async Task LoadPropOverlayAsync()
    {
        if (ShowMulSlotView)
        {
            StatusText = "Prop overlay is only available in animation view.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Choose Prop PNG",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("PNG Image")
                    {
                        Patterns = new[] { "*.png" }
                    }
                }
            });

        if (files.Count == 0)
        {
            StatusText = "Load prop cancelled.";
            return;
        }

        string? pngPath = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(pngPath))
        {
            StatusText = "Selected PNG does not have a local path.";
            return;
        }

        WriteableBitmap? bitmap = await LoadBitmapFromStorageFileAsync(files[0]);
        if (bitmap == null)
        {
            StatusText = "Failed to load prop PNG.";
            return;
        }

        loadedPropOverlayBitmap = bitmap;
        loadedPropOverlayPath = pngPath;
        PropOverlayFileName = Path.GetFileName(pngPath);
        PropOverlayEnabled = true;

        OnPropertyChanged(nameof(HasPropOverlayLoaded));
        OnPropertyChanged(nameof(PropOverlaySummaryText));

        RefreshLivePreviewImage();
        StatusText = "Loaded prop overlay: " + PropOverlayFileName + ".";
    }

    private void ClearPropOverlay()
    {
        loadedPropOverlayBitmap = null;
        loadedPropOverlayPath = string.Empty;
        propFramePoses.Clear();
        PropOverlayPivotX = 0;
        PropOverlayPivotY = 0;
        PropOverlayFlipHorizontal = false;
        PropOverlayFlipVertical = false;

        PropOverlayEnabled = false;
        PropOverlayFileName = "None";
        PropOverlayOffsetX = 0;
        PropOverlayOffsetY = 0;
        PropOverlayScalePercent = 100.0;
        PropOverlayRotationDegrees = 0.0;
        PropOverlayDrawOrderIndex = 1;

        OnPropertyChanged(nameof(HasPropOverlayLoaded));
        OnPropertyChanged(nameof(PropOverlaySummaryText));
        OnPropertyChanged(nameof(CurrentPropPoseText));
        OnPropertyChanged(nameof(HasPropPoseForCurrentFrame));

        RefreshLivePreviewImage();
        StatusText = "Cleared prop overlay.";
    }

    private void SavePropPoseForCurrentFrame()
    {
        if (currentFrameIndex < 0 || editableFrames.Count == 0)
        {
            StatusText = "No frame is loaded.";
            return;
        }

        propFramePoses[currentFrameIndex] = new PropFramePose
        {
            OffsetX = PropOverlayOffsetX,
            OffsetY = PropOverlayOffsetY,
            RotationDegrees = PropOverlayRotationDegrees,
            FlipHorizontal = PropOverlayFlipHorizontal,
            FlipVertical = PropOverlayFlipVertical
        };

        OnPropertyChanged(nameof(CurrentPropPoseText));
        OnPropertyChanged(nameof(HasPropPoseForCurrentFrame));

        RefreshLivePreviewImage();
        StatusText = "Saved prop pose for frame " + (currentFrameIndex + 1) + ".";
    }

    private void CopyPropPoseFromPreviousFrame()
    {
        if (currentFrameIndex <= 0)
        {
            StatusText = "There is no previous frame to copy from.";
            return;
        }

        if (!propFramePoses.TryGetValue(currentFrameIndex - 1, out PropFramePose? previousPose))
        {
            StatusText = "Previous frame does not have a saved prop pose.";
            return;
        }

        PropOverlayOffsetX = previousPose.OffsetX;
        PropOverlayOffsetY = previousPose.OffsetY;
        PropOverlayRotationDegrees = previousPose.RotationDegrees;
        PropOverlayFlipHorizontal = previousPose.FlipHorizontal;
        PropOverlayFlipVertical = previousPose.FlipVertical;

        RefreshLivePreviewImage();
        StatusText = "Copied prop pose from frame " + currentFrameIndex + ".";
    }

    private void ClearPropPoseForCurrentFrame()
    {
        if (currentFrameIndex < 0)
        {
            StatusText = "No frame is selected.";
            return;
        }

        if (propFramePoses.Remove(currentFrameIndex))
        {
            OnPropertyChanged(nameof(CurrentPropPoseText));
            OnPropertyChanged(nameof(HasPropPoseForCurrentFrame));
            RefreshLivePreviewImage();
            StatusText = "Cleared prop pose for frame " + (currentFrameIndex + 1) + ".";
            return;
        }

        StatusText = "Current frame does not have a saved prop pose.";
    }

    private PropFramePose GetEffectivePropPoseForFrame(int frameIndex)
    {
        if (propFramePoses.TryGetValue(frameIndex, out PropFramePose? pose))
        {
            return pose;
        }

        return new PropFramePose
        {
            OffsetX = PropOverlayOffsetX,
            OffsetY = PropOverlayOffsetY,
            RotationDegrees = PropOverlayRotationDegrees,
            FlipHorizontal = PropOverlayFlipHorizontal,
            FlipVertical = PropOverlayFlipVertical
        };
    }

    private void ApplyPropOverlayToCurrentFrame()
    {
        if (ShowMulSlotView)
        {
            StatusText = "Prop overlay is only available in animation view.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "No animation selected.";
            return;
        }

        if (!PropOverlayEnabled || loadedPropOverlayBitmap == null)
        {
            StatusText = "Load a prop PNG first.";
            return;
        }

        if (editableFrames.Count == 0)
        {
            StatusText = "No editable frames are loaded.";
            return;
        }

        int targetFrameIndex = currentFrameIndex;

        if (SelectedFrameThumbnail != null &&
            SelectedFrameThumbnail.FrameIndex >= 0 &&
            SelectedFrameThumbnail.FrameIndex < editableFrames.Count)
        {
            targetFrameIndex = SelectedFrameThumbnail.FrameIndex;
        }

        if (targetFrameIndex < 0 || targetFrameIndex >= editableFrames.Count)
        {
            StatusText = "Selected frame index is out of range.";
            return;
        }

        PushUndoSnapshot("Apply prop to frame " + (targetFrameIndex + 1));

        editableFrames[targetFrameIndex] = ApplyPropOverlayToFrame(editableFrames[targetFrameIndex], targetFrameIndex);

        hasFrameEdits = true;
        OnPropertyChanged(nameof(HasFrameEdits));
        RefreshUnsavedChangesState();

        RefreshDecodedFramesFromEditableFrames();

        currentFrameIndex = Math.Clamp(targetFrameIndex, 0, Math.Max(0, decodedFrames.Count - 1));
        CaptureLivePreviewSourceFromCurrentFrame();
        RefreshLivePreviewImage();

        StatusText = "Applied prop to frame " + (targetFrameIndex + 1) + ". Click Save Changes to write the edit.";
    }

    private void ApplyPropOverlayToCurrentDirection()
    {
        if (ShowMulSlotView)
        {
            StatusText = "Prop overlay is only available in animation view.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "No animation selected.";
            return;
        }

        if (!PropOverlayEnabled || loadedPropOverlayBitmap == null)
        {
            StatusText = "Load a prop PNG first.";
            return;
        }

        if (editableFrames.Count == 0)
        {
            StatusText = "No editable frames are loaded.";
            return;
        }

        PushUndoSnapshot("Apply prop to current direction");

        for (int i = 0; i < editableFrames.Count; i++)
        {
            editableFrames[i] = ApplyPropOverlayToFrame(editableFrames[i], i);
        }

        hasFrameEdits = true;
        OnPropertyChanged(nameof(HasFrameEdits));
        RefreshUnsavedChangesState();

        RefreshDecodedFramesFromEditableFrames();
        CaptureLivePreviewSourceFromCurrentFrame();
        RefreshLivePreviewImage();

        StatusText = "Applied prop to all loaded frames in the current direction. Click Save Changes to write the edit.";
    }

    private WriteableBitmap BuildPreviewWithPropOverlay(VdFrameData sourceFrame)
    {
        VdFrameData previewFrame = ApplyPropOverlayToFrame(sourceFrame, currentFrameIndex);
        return previewFrame.Bitmap;
    }

    private VdFrameData ApplyPropOverlayToFrame(VdFrameData sourceFrame, int frameIndex)
    {
        if (loadedPropOverlayBitmap == null)
        {
            return sourceFrame;
        }

        PropFramePose pose = GetEffectivePropPoseForFrame(frameIndex);

        WriteableBitmap transformedProp = BuildTransformedPropBitmap(
            loadedPropOverlayBitmap,
            pose);

        PropCompositeResult composite = CompositePropOntoFrame(
            sourceFrame.Bitmap,
            transformedProp,
            pose.OffsetX,
            pose.OffsetY,
            PropOverlayDrawOrderIndex == 0);

        WriteableBitmap finalBitmap = composite.Bitmap;

        if (sourceFrame.Palette565 != null && sourceFrame.Palette565.Count >= 256)
        {
            finalBitmap = RemapBitmapToPalette(finalBitmap, sourceFrame.Palette565);
        }

        return new VdFrameData
        {
            Bitmap = finalBitmap,
            Palette565 = sourceFrame.Palette565 != null
                ? new List<ushort>(sourceFrame.Palette565)
                : null,
            CenterX = (short)(sourceFrame.CenterX + composite.LeftPadding),
            CenterY = (short)(sourceFrame.CenterY + composite.TopPadding),
            Width = (ushort)finalBitmap.PixelSize.Width,
            Height = (ushort)finalBitmap.PixelSize.Height,
            InitCoordsX = (short)(sourceFrame.InitCoordsX + composite.LeftPadding),
            InitCoordsY = (short)(sourceFrame.InitCoordsY + composite.TopPadding),
            EndCoordsX = (short)(sourceFrame.EndCoordsX + composite.LeftPadding),
            EndCoordsY = (short)(sourceFrame.EndCoordsY + composite.TopPadding),
            FrameId = sourceFrame.FrameId,
            FrameNumber = sourceFrame.FrameNumber,
            DataOffset = sourceFrame.DataOffset
        };
    }

    private WriteableBitmap BuildTransformedPropBitmap(WriteableBitmap sourceBitmap, PropFramePose pose)
    {
        using ImageSharpImage image = ConvertWriteableBitmapToImageSharp(sourceBitmap);

        int scaledWidth = Math.Max(1, (int)Math.Round(image.Width * (PropOverlayScalePercent / 100.0)));
        int scaledHeight = Math.Max(1, (int)Math.Round(image.Height * (PropOverlayScalePercent / 100.0)));

        image.Mutate(context =>
        {
            if (scaledWidth != image.Width || scaledHeight != image.Height)
            {
                context.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(scaledWidth, scaledHeight),
                    Sampler = KnownResamplers.Bicubic,
                    Mode = ResizeMode.Stretch
                });
            }

            if (pose.FlipHorizontal)
            {
                context.Flip(FlipMode.Horizontal);
            }

            if (pose.FlipVertical)
            {
                context.Flip(FlipMode.Vertical);
            }
        });

        int pivotX = (int)Math.Round(PropOverlayPivotX * (PropOverlayScalePercent / 100.0));
        int pivotY = (int)Math.Round(PropOverlayPivotY * (PropOverlayScalePercent / 100.0));

        if (Math.Abs(pose.RotationDegrees) <= 0.001)
        {
            return ConvertImageSharpToWriteableBitmap(image);
        }

        using MemoryStream inputStream = new MemoryStream();
        image.SaveAsPng(inputStream);
        inputStream.Position = 0;

        using ImageSharpImage sourceForRotate =
            SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Bgra32>(inputStream);

        double radians = pose.RotationDegrees * (Math.PI / 180.0);
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);

        SixLabors.ImageSharp.PointF[] corners =
        [
            RotatePoint(0, 0, pivotX, pivotY, cos, sin),
    RotatePoint(sourceForRotate.Width, 0, pivotX, pivotY, cos, sin),
    RotatePoint(0, sourceForRotate.Height, pivotX, pivotY, cos, sin),
    RotatePoint(sourceForRotate.Width, sourceForRotate.Height, pivotX, pivotY, cos, sin)
        ];

        float minX = corners.Min(p => p.X);
        float minY = corners.Min(p => p.Y);
        float maxX = corners.Max(p => p.X);
        float maxY = corners.Max(p => p.Y);

        int outWidth = Math.Max(1, (int)Math.Ceiling(maxX - minX));
        int outHeight = Math.Max(1, (int)Math.Ceiling(maxY - minY));

        float translatedPivotX = pivotX - minX;
        float translatedPivotY = pivotY - minY;

        sourceForRotate.Mutate(context =>
        {
            context.Transform(
                new SixLabors.ImageSharp.Processing.AffineTransformBuilder()
                    .AppendTranslation(new SixLabors.ImageSharp.PointF(-pivotX, -pivotY))
                    .AppendRotationDegrees((float)pose.RotationDegrees)
                    .AppendTranslation(new SixLabors.ImageSharp.PointF(translatedPivotX, translatedPivotY)));
        });

        using ImageSharpImage canvas = new(outWidth, outHeight);
        canvas.Mutate(context =>
        {
            context.DrawImage(sourceForRotate, new SixLabors.ImageSharp.Point(0, 0), 1f);
        });

        return ConvertImageSharpToWriteableBitmap(canvas);
    }

    private static SixLabors.ImageSharp.PointF RotatePoint(
        float x,
        float y,
        float pivotX,
        float pivotY,
        double cos,
        double sin)
    {
        double dx = x - pivotX;
        double dy = y - pivotY;

        float rx = (float)((dx * cos) - (dy * sin) + pivotX);
        float ry = (float)((dx * sin) + (dy * cos) + pivotY);

        return new SixLabors.ImageSharp.PointF(rx, ry);
    }

    private PropCompositeResult CompositePropOntoFrame(
        WriteableBitmap frameBitmap,
        WriteableBitmap propBitmap,
        int offsetX,
        int offsetY,
        bool drawPropBehindFrame)
    {
        FramePixels framePixels = ReadWriteableBitmapPixels(frameBitmap);
        FramePixels propPixels = ReadWriteableBitmapPixels(propBitmap);

        int drawX = (framePixels.Width / 2) - (propPixels.Width / 2) + offsetX;
        int drawY = (framePixels.Height / 2) - (propPixels.Height / 2) + offsetY;

        int minX = Math.Min(0, drawX);
        int minY = Math.Min(0, drawY);
        int maxX = Math.Max(framePixels.Width, drawX + propPixels.Width);
        int maxY = Math.Max(framePixels.Height, drawY + propPixels.Height);

        int leftPadding = -minX;
        int topPadding = -minY;
        int outputWidth = maxX - minX;
        int outputHeight = maxY - minY;

        byte[] outputPixels = new byte[outputWidth * outputHeight * 4];

        if (drawPropBehindFrame)
        {
            DrawBitmapOntoBuffer(outputPixels, outputWidth, outputHeight, propPixels, drawX + leftPadding, drawY + topPadding);
            DrawBitmapOntoBuffer(outputPixels, outputWidth, outputHeight, framePixels, leftPadding, topPadding);
        }
        else
        {
            DrawBitmapOntoBuffer(outputPixels, outputWidth, outputHeight, framePixels, leftPadding, topPadding);
            DrawBitmapOntoBuffer(outputPixels, outputWidth, outputHeight, propPixels, drawX + leftPadding, drawY + topPadding);
        }

        return new PropCompositeResult
        {
            Bitmap = CreateWriteableBitmapFromPixels(outputWidth, outputHeight, outputPixels),
            LeftPadding = leftPadding,
            TopPadding = topPadding
        };
    }

    private void DrawBitmapOntoBuffer(
        byte[] destinationPixels,
        int destinationWidth,
        int destinationHeight,
        FramePixels sourcePixels,
        int destinationX,
        int destinationY)
    {
        for (int y = 0; y < sourcePixels.Height; y++)
        {
            int targetY = destinationY + y;
            if (targetY < 0 || targetY >= destinationHeight)
            {
                continue;
            }

            for (int x = 0; x < sourcePixels.Width; x++)
            {
                int targetX = destinationX + x;
                if (targetX < 0 || targetX >= destinationWidth)
                {
                    continue;
                }

                int sourceOffset = ((y * sourcePixels.Width) + x) * 4;
                byte srcB = sourcePixels.Pixels[sourceOffset + 0];
                byte srcG = sourcePixels.Pixels[sourceOffset + 1];
                byte srcR = sourcePixels.Pixels[sourceOffset + 2];
                byte srcA = sourcePixels.Pixels[sourceOffset + 3];

                if (srcA == 0)
                {
                    continue;
                }

                int destinationOffset = ((targetY * destinationWidth) + targetX) * 4;

                byte dstB = destinationPixels[destinationOffset + 0];
                byte dstG = destinationPixels[destinationOffset + 1];
                byte dstR = destinationPixels[destinationOffset + 2];
                byte dstA = destinationPixels[destinationOffset + 3];

                BlendSourceOver(
                    srcB, srcG, srcR, srcA,
                    dstB, dstG, dstR, dstA,
                    out byte outB, out byte outG, out byte outR, out byte outA);

                destinationPixels[destinationOffset + 0] = outB;
                destinationPixels[destinationOffset + 1] = outG;
                destinationPixels[destinationOffset + 2] = outR;
                destinationPixels[destinationOffset + 3] = outA;
            }
        }
    }

    private void BlendSourceOver(
        byte srcB,
        byte srcG,
        byte srcR,
        byte srcA,
        byte dstB,
        byte dstG,
        byte dstR,
        byte dstA,
        out byte outB,
        out byte outG,
        out byte outR,
        out byte outA)
    {
        float srcAlpha = srcA / 255f;
        float dstAlpha = dstA / 255f;
        float finalAlpha = srcAlpha + (dstAlpha * (1f - srcAlpha));

        if (finalAlpha <= 0f)
        {
            outB = 0;
            outG = 0;
            outR = 0;
            outA = 0;
            return;
        }

        float srcWeight = srcAlpha / finalAlpha;
        float dstWeight = (dstAlpha * (1f - srcAlpha)) / finalAlpha;

        outB = (byte)Math.Clamp(Math.Round((srcB * srcWeight) + (dstB * dstWeight)), 0, 255);
        outG = (byte)Math.Clamp(Math.Round((srcG * srcWeight) + (dstG * dstWeight)), 0, 255);
        outR = (byte)Math.Clamp(Math.Round((srcR * srcWeight) + (dstR * dstWeight)), 0, 255);
        outA = (byte)Math.Clamp(Math.Round(finalAlpha * 255f), 0, 255);
    }

    private void CaptureLivePreviewSourceFromCurrentFrame()
    {
        if (decodedFrames.Count == 0 || currentFrameIndex < 0 || currentFrameIndex >= decodedFrames.Count)
        {
            previewSourceBitmapBeforeLiveEffects = null;
            return;
        }

        previewSourceBitmapBeforeLiveEffects = CloneBitmap(decodedFrames[currentFrameIndex]);
    }

    private void ResetLivePreviewSettings()
    {
        suppressLivePreviewRefresh = true;

        LivePreviewSharpenEnabled = false;
        LivePreviewContrastEnabled = false;
        LivePreviewOutlineEnabled = false;
        LivePreviewHueEnabled = false;
        LivePreviewSharpenAmount = 1.0;
        LivePreviewContrastAmount = 0.20;
        LivePreviewOutlineStrength = 0.35;
        LivePreviewSharpenModeIndex = 0;

        suppressLivePreviewRefresh = false;

        CaptureLivePreviewSourceFromCurrentFrame();
        RefreshLivePreviewImage();
        StatusText = "Live preview controls reset.";
    }

    private async Task ApplyLivePreviewToCurrentDirectionAsync()
    {
        await ApplyCurrentDirectionEnhancementsFromRequestAsync(BuildLivePreviewRequest());
        ClearLivePreviewAfterCommit();
    }

    private async Task ApplyLivePreviewToFullAnimationAsync()
    {
        await ApplyFullAnimationEnhancementsFromRequestAsync(BuildLivePreviewRequest());
        ClearLivePreviewAfterCommit();
    }

    private async Task ApplyCurrentDirectionEnhancementsFromRequestAsync(ExportRequest request)
    {
        if (ShowMulSlotView)
        {
            StatusText = "Animation enhancement is only available in animation view.";
            return;
        }

        if (SelectedAnimation == null || currentResolvedAnimationBlock == null)
        {
            StatusText = "Select an animation first.";
            return;
        }

        if (currentResolvedAnimationBlock.IsUop)
        {
            StatusText = "Direct in-place enhancement save is currently supported for MUL animations only.";
            return;
        }

        if (editableFrames.Count == 0)
        {
            StatusText = "No editable frames are loaded.";
            return;
        }

        PushUndoSnapshot("Enhance current direction");

        List<VdFrameData> processedFrames = ApplyEditEffectsToFrames(
            CloneFrameList(editableFrames),
            request);

        editableFrames.Clear();
        editableFrames.AddRange(processedFrames);

        RefreshDecodedFramesFromEditableFrames();
        CaptureLivePreviewSourceFromCurrentFrame();
        RefreshLivePreviewImage();

        hasFrameEdits = true;
        OnPropertyChanged(nameof(HasFrameEdits));
        RefreshUnsavedChangesState();

        StatusText =
            "Applied enhancements to body " + SelectedAnimation.BodyId +
            ", action " + GetSelectedActionIndex() +
            ", direction " + GetSelectedDirectionIndex() +
            ". Click Save Changes to write them.";

        await Task.CompletedTask;
    }

    private async Task ApplyFullAnimationEnhancementsFromRequestAsync(ExportRequest request)
    {
        if (ShowMulSlotView)
        {
            StatusText = "Full animation enhancement is only available in animation view.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "Select an animation first.";
            return;
        }

        IAnimationDataSource? dataSource = GetDataSourceForEntry(SelectedAnimation);
        if (dataSource == null)
        {
            StatusText = "No data source is available for the selected animation.";
            return;
        }

        if (string.Equals(SelectedAnimation.SourceMode, "UOP", StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "Full in-place enhancement save is currently MUL-only. UOP direct edit-save is not implemented yet.";
            return;
        }

        int bodyId = GetEffectiveSelectedBodyId(SelectedAnimation);
        if (bodyId < 0)
        {
            StatusText = "Could not determine body ID.";
            return;
        }

        List<int> actionIndices = dataSource.GetAvailableActionIndices(bodyId)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (actionIndices.Count == 0)
        {
            int fallbackActionCount = dataSource.GetGroupCountForBody(bodyId);
            for (int actionIndex = 0; actionIndex < fallbackActionCount; actionIndex++)
            {
                actionIndices.Add(actionIndex);
            }
        }

        int originalFrameIndex = currentFrameIndex;
        int originalActionIndex = GetSelectedActionIndex();
        int originalDirectionIndex = GetSelectedDirectionIndex();

        if (editableFrames.Count > 0)
        {
            PushUndoSnapshot("Enhance full animation");
        }

        int queuedCount = 0;

        foreach (int actionIndex in actionIndices)
        {
            for (int directionIndex = 0; directionIndex < 5; directionIndex++)
            {
                if (!dataSource.TryResolveAnimationBlock(bodyId, actionIndex, directionIndex, out ResolvedAnimationBlock resolvedBlock))
                {
                    continue;
                }

                if (resolvedBlock.IsUop)
                {
                    continue;
                }

                byte[] blockData = dataSource.ReadAnimationBlock(resolvedBlock);
                if (blockData.Length == 0)
                {
                    continue;
                }

                List<VdFrameData> frames = DecodeFramesForVdExport(blockData, resolvedBlock, directionIndex);
                if (frames.Count == 0)
                {
                    continue;
                }

                List<VdFrameData> processedFrames = ApplyEditEffectsToFrames(frames, request);

                if (QueueEditedMulFramesForResolvedBlock(resolvedBlock, processedFrames))
                {
                    queuedCount++;
                }

                if (actionIndex == originalActionIndex && directionIndex == originalDirectionIndex)
                {
                    editableFrames.Clear();
                    editableFrames.AddRange(CloneFrameList(processedFrames));
                }
            }
        }

        if (editableFrames.Count > 0)
        {
            currentFrameIndex = Math.Clamp(originalFrameIndex, 0, editableFrames.Count - 1);
            RefreshDecodedFramesFromEditableFrames();
            CaptureLivePreviewSourceFromCurrentFrame();
            RefreshLivePreviewImage();
        }

        hasFrameEdits = editableFrames.Count > 0;
        OnPropertyChanged(nameof(HasFrameEdits));
        RefreshUnsavedChangesState();

        StatusText =
            "Queued enhanced full animation for body " + bodyId +
            " across " + queuedCount + " direction block(s). Click Save Changes to write them.";

        await Task.CompletedTask;
    }

    private void ClearLivePreviewAfterCommit()
    {
        suppressLivePreviewRefresh = true;

        LivePreviewSharpenEnabled = false;
        LivePreviewContrastEnabled = false;
        LivePreviewOutlineEnabled = false;
        LivePreviewHueEnabled = false;
        LivePreviewSharpenAmount = 1.0;
        LivePreviewContrastAmount = 0.20;
        LivePreviewOutlineStrength = 0.35;
        LivePreviewSharpenModeIndex = 0;

        suppressLivePreviewRefresh = false;

        if (decodedFrames.Count > 0 && currentFrameIndex >= 0 && currentFrameIndex < decodedFrames.Count)
        {
            previewSourceBitmapBeforeLiveEffects = CloneBitmap(decodedFrames[currentFrameIndex]);
            PreviewBitmap = decodedFrames[currentFrameIndex];
        }
        else
        {
            previewSourceBitmapBeforeLiveEffects = null;
            PreviewBitmap = null;
        }
    }

    private void SetupMountRiderAlignment()
    {
        if (ShowMulSlotView)
        {
            StatusText = "Mount rider alignment is only available in animation view.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "Select a mount animation first.";
            return;
        }

        AnimationEntry? riderAnimation = FindDefaultMountedRiderAnimation();

        if (riderAnimation == null)
        {
            StatusText = "Could not find a human rider animation. Try selecting Body 400 manually in Compare.";
            return;
        }

        CompareOverlayEnabled = true;
        CompareSideBySideEnabled = false;
        CompareSelectedAnimation = riderAnimation;

        CompareOverlayActionIndex = GuessMountedRiderActionIndex();
        CompareOverlayDirectionIndex = GetSelectedDirectionIndex();
        CompareOverlaySyncMode = "Loop Secondary";
        CompareOverlayFrameIndex = 0;
        CompareOverlayOpacityPercent = 65.0;

        CompareOverlayOffsetX = 0;
        CompareOverlayOffsetY = -20;

        InvalidateCompareOverlayCache();
        RefreshLivePreviewImage();

        StatusText =
            "Mount rider alignment enabled. Rider frames will loop if the mount has more frames than the rider.";
    }

    private AnimationEntry? FindDefaultMountedRiderAnimation()
    {
        string selectedFile = NormalizeAnimationFileNameForCompare(SelectedAnimation?.SourceFile);

        AnimationEntry? match = allAnimationEntries.FirstOrDefault(x =>
            x.BodyId == 400 &&
            string.Equals(NormalizeAnimationFileNameForCompare(x.SourceFile), selectedFile, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            return CloneAnimationEntryForCompare(match);
        }

        match = allAnimationEntries.FirstOrDefault(x => x.BodyId == 400);
        if (match != null)
        {
            return CloneAnimationEntryForCompare(match);
        }

        match = allAnimationEntries.FirstOrDefault(x =>
            x.BodyId >= 400 &&
            x.BodyId <= 401 &&
            string.Equals(x.SourceMode, SelectedAnimation?.SourceMode, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            return CloneAnimationEntryForCompare(match);
        }

        return null;
    }

    private AnimationEntry CloneAnimationEntryForCompare(AnimationEntry source)
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

    private int GuessMountedRiderActionIndex()
    {
        string actionText = SelectedAction ?? string.Empty;
        int mountAction = GetSelectedActionIndex();

        if (actionText.Contains("Run", StringComparison.OrdinalIgnoreCase) || mountAction == 1)
        {
            return 24; // Horse_Run_01
        }

        if (actionText.Contains("Idle", StringComparison.OrdinalIgnoreCase))
        {
            return 25; // Horse_Idle_01
        }

        if (actionText.Contains("Bow", StringComparison.OrdinalIgnoreCase))
        {
            return 27; // Horse_AttackBow_01
        }

        if (actionText.Contains("Cross", StringComparison.OrdinalIgnoreCase))
        {
            return 28; // Horse_AttackCrossbow_01
        }

        if (actionText.Contains("Attack", StringComparison.OrdinalIgnoreCase))
        {
            return 26; // Horse_Attack1H_SlashRight_01
        }

        return 23; // Horse_Walk_01
    }
}