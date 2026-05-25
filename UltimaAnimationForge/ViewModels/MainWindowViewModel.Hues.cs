using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{

    public ObservableCollection<HueEditorColorSlot> HueOriginalColorSlots { get; } = new();

    public ObservableCollection<string> HuePresetOptions { get; } = new()
{
    "Cloth",
    "Metal",
    "Glow",
    "Fire",
    "Ice",
    "Poison",
    "Shadow",
    "Gold",
    "Bone",
    "Leather"
};

    [ObservableProperty]
    private string selectedHuePreset = "Cloth";

    public ICommand ApplyHuePresetCommand { get; private set; } = null!;

    [ObservableProperty]
    private WriteableBitmap? huePreviewBitmap;

    [ObservableProperty]
    private int huePreviewArtId = 0x0EED;

    [ObservableProperty]
    private int hueEditorReplaceSourceHueId;

    [ObservableProperty]
    private bool hueEditorShowFreeOnly;

    public string HueEditorFreeSlotText => GetFreeHueSlots().Count + " free slots";

    private string loadedHueEditorFilePath = string.Empty;
    private List<HueEditorEntry> allHueEditorEntries = new();

    public ObservableCollection<HueEditorEntry> HueEditorEntries { get; } = new();

    [ObservableProperty]
    private HueEditorEntry? selectedHueEditorEntry;

    [ObservableProperty]
    private HueEditorColorSlot? selectedHueEditorColor;

    [ObservableProperty]
    private string hueEditorSearchText = string.Empty;

    [ObservableProperty]
    private string hueEditorStatusText = "Open a UO folder or load hues.mul.";

    [ObservableProperty]
    private byte hueEditorRed;

    [ObservableProperty]
    private byte hueEditorGreen;

    [ObservableProperty]
    private byte hueEditorBlue;

    public ICommand CreateGuidedHueCommand { get; private set; } = null!;
    public ICommand ShowHueEditorCommand { get; private set; } = null!;
    public ICommand LoadHueEditorCommand { get; private set; } = null!;
    public ICommand SaveHueEditorCommand { get; private set; } = null!;
    public ICommand RevertHueEditorCommand { get; private set; } = null!;
    public ICommand ApplyHueEditorColorCommand { get; private set; } = null!;
    public ICommand MakeHueEditorGradientCommand { get; private set; } = null!;
    public ICommand CopyHueEditorNameCommand { get; private set; } = null!;
    public ICommand SelectNextFreeHueSlotCommand { get; private set; } = null!;
    public ICommand ExportSelectedHueCommand { get; private set; } = null!;
    public ICommand ImportReplaceSelectedHueCommand { get; private set; } = null!;
    public ICommand ReplaceHueFromIndexCommand { get; private set; } = null!;
    public ICommand RefreshHuePreviewCommand { get; private set; } = null!;

    public string HueEditorLoadedFileText =>
        string.IsNullOrWhiteSpace(loadedHueEditorFilePath)
            ? "No hues.mul loaded"
            : Path.GetFileName(loadedHueEditorFilePath);

    public string HueEditorCountText => HueEditorEntries.Count + " / " + allHueEditorEntries.Count + " hues";

    public IBrush HueEditorSelectedColorBrush =>
        SelectedHueEditorColor == null
            ? new SolidColorBrush(Color.Parse("#20242B"))
            : new SolidColorBrush(SelectedHueEditorColor.Color);

    private void InitializeHueEditorCommands()
    {
        ShowHueEditorCommand = new RelayCommand(() => ActiveToolTab = MainToolTab.Hues);
        LoadHueEditorCommand = new AsyncRelayCommand(LoadHueEditorAsync);
        SaveHueEditorCommand = new RelayCommand(SaveHueEditor);
        RevertHueEditorCommand = new RelayCommand(RevertHueEditor);
        ApplyHueEditorColorCommand = new RelayCommand(ApplyHueEditorColor);
        MakeHueEditorGradientCommand = new RelayCommand(MakeHueEditorGradient);
        CopyHueEditorNameCommand = new RelayCommand(CopyHueEditorNameToSearch);
        CreateGuidedHueCommand = new RelayCommand(CreateGuidedHue);
        SelectNextFreeHueSlotCommand = new RelayCommand(SelectNextFreeHueSlot);
        ExportSelectedHueCommand = new AsyncRelayCommand(ExportSelectedHueAsync);
        ImportReplaceSelectedHueCommand = new AsyncRelayCommand(ImportReplaceSelectedHueAsync);
        ReplaceHueFromIndexCommand = new RelayCommand(ReplaceHueFromIndex);
        RefreshHuePreviewCommand = new RelayCommand(UpdateHueArtPreview);
        ApplyHuePresetCommand = new RelayCommand(ApplyHuePreset);
    }

    private void CaptureOriginalHuePreview()
    {
        HueOriginalColorSlots.Clear();

        if (SelectedHueEditorEntry == null)
        {
            return;
        }

        foreach (HueEditorColorSlot slot in SelectedHueEditorEntry.Colors)
        {
            HueOriginalColorSlots.Add(new HueEditorColorSlot
            {
                Index = slot.Index,
                RawValue = slot.RawValue,
                Color = slot.Color
            });
        }
    }

    partial void OnHueEditorShowFreeOnlyChanged(bool value) => RefreshHueEditorFilter();

    private async Task LoadHueEditorAsync()
    {
        Window? owner = GetMainWindow();
        if (owner == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        string hueFilePath = await FindOrPromptForHueFileAsync(owner);
        if (string.IsNullOrWhiteSpace(hueFilePath) || !File.Exists(hueFilePath))
        {
            HueEditorStatusText = "Hue load cancelled.";
            return;
        }

        LoadHueEditorFromPath(hueFilePath);
    }

    private void LoadHueEditorFromPath(string hueFilePath)
    {
        try
        {
            List<HueEditorEntry> loaded = hueDataService.LoadHueEditorEntries(hueFilePath);
            if (loaded.Count == 0)
            {
                HueEditorStatusText = "No hues were found in hues.mul.";
                return;
            }

            loadedHueEditorFilePath = hueFilePath;
            currentHueFilePath = hueFilePath;
            cachedHueEntries = hueDataService.LoadHueEntries(hueFilePath);
            allHueEditorEntries = loaded;

            RefreshHueEditorFilter();

            SelectedHueEditorEntry = HueEditorEntries.FirstOrDefault();
            SelectedHueEditorColor = SelectedHueEditorEntry?.Colors.FirstOrDefault();

            HueEditorStatusText = "Loaded " + allHueEditorEntries.Count + " hues from " + Path.GetFileName(hueFilePath) + ".";
            OnPropertyChanged(nameof(HueEditorLoadedFileText));
            OnPropertyChanged(nameof(HueEditorCountText));
        }
        catch (Exception exception)
        {
            HueEditorStatusText = "Failed to load hues.mul: " + exception.Message;
        }
    }

    private void RefreshHueEditorFilter()
    {
        string search = (HueEditorSearchText ?? string.Empty).Trim();

        IEnumerable<HueEditorEntry> filtered = allHueEditorEntries;

        if (HueEditorShowFreeOnly)
        {
            filtered = filtered.Where(IsFreeHueSlot);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(entry =>
                entry.HueId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                entry.HexText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(entry.Name) && entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        HueEditorEntries.Clear();
        foreach (HueEditorEntry entry in filtered)
        {
            HueEditorEntries.Add(entry);
        }

        if (SelectedHueEditorEntry == null || !HueEditorEntries.Contains(SelectedHueEditorEntry))
        {
            SelectedHueEditorEntry = HueEditorEntries.FirstOrDefault();
        }

        OnPropertyChanged(nameof(HueEditorFreeSlotText));
        OnPropertyChanged(nameof(HueEditorCountText));
    }

    private void SaveHueEditor()
    {
        if (string.IsNullOrWhiteSpace(loadedHueEditorFilePath))
        {
            HueEditorStatusText = "No hues.mul is loaded.";
            return;
        }

        try
        {
            hueDataService.SaveHueEditorEntries(loadedHueEditorFilePath, allHueEditorEntries);
            cachedHueEntries = hueDataService.LoadHueEntries(loadedHueEditorFilePath);
            HueEditorStatusText = "Saved hues.mul. Backup created as hues.mul.bak if one did not already exist.";
        }
        catch (Exception exception)
        {
            HueEditorStatusText = "Failed to save hues.mul: " + exception.Message;
        }
    }

    private void RevertHueEditor()
    {
        if (string.IsNullOrWhiteSpace(loadedHueEditorFilePath) || !File.Exists(loadedHueEditorFilePath))
        {
            HueEditorStatusText = "No hues.mul is loaded.";
            return;
        }

        LoadHueEditorFromPath(loadedHueEditorFilePath);
    }

    private void ApplyHueEditorColor()
    {
        if (SelectedHueEditorColor == null)
        {
            return;
        }

        Color color = Color.FromRgb(HueEditorRed, HueEditorGreen, HueEditorBlue);
        SelectedHueEditorColor.Color = color;
        SelectedHueEditorColor.RawValue = HueDataService.ConvertAvaloniaColorToUoColor(color);
        OnPropertyChanged(nameof(HueEditorSelectedColorBrush));
        HueEditorStatusText = "Updated color " + SelectedHueEditorColor.IndexText + ".";
    }

    private void MakeHueEditorGradient()
    {
        if (SelectedHueEditorEntry == null)
        {
            return;
        }

        int count = SelectedHueEditorEntry.Colors.Count;
        if (count == 0)
        {
            return;
        }

        Color end = Color.FromRgb(HueEditorRed, HueEditorGreen, HueEditorBlue);

        for (int i = 0; i < count; i++)
        {
            double amount = count <= 1 ? 1.0 : i / (double)(count - 1);
            byte r = (byte)Math.Round(end.R * amount);
            byte g = (byte)Math.Round(end.G * amount);
            byte b = (byte)Math.Round(end.B * amount);
            Color color = Color.FromRgb(r, g, b);

            SelectedHueEditorEntry.Colors[i].Color = color;
            SelectedHueEditorEntry.Colors[i].RawValue = HueDataService.ConvertAvaloniaColorToUoColor(color);
        }

        HueEditorStatusText = "Built gradient for hue " + SelectedHueEditorEntry.HueId + ".";
    }

    private void CopyHueEditorNameToSearch()
    {
        if (SelectedHueEditorEntry != null)
        {
            HueEditorSearchText = SelectedHueEditorEntry.Name;
        }
    }

    partial void OnHueEditorSearchTextChanged(string value) => RefreshHueEditorFilter();

    partial void OnSelectedHueEditorEntryChanged(HueEditorEntry? value)
    {
        SelectedHueEditorColor = value?.Colors.FirstOrDefault();
        CaptureOriginalHuePreview();
        UpdateHueArtPreview();
    }

    partial void OnSelectedHueEditorColorChanged(HueEditorColorSlot? value)
    {
        if (value == null)
        {
            return;
        }

        HueEditorRed = value.Color.R;
        HueEditorGreen = value.Color.G;
        HueEditorBlue = value.Color.B;
        OnPropertyChanged(nameof(HueEditorSelectedColorBrush));
    }

    partial void OnHuePreviewArtIdChanged(int value)
    {
        UpdateHueArtPreview();
    }

    private void CreateGuidedHue()
    {
        if (SelectedHueEditorEntry == null)
        {
            HueEditorStatusText = "Select an empty/custom hue slot first.";
            return;
        }

        Color baseColor = Color.FromRgb(HueEditorRed, HueEditorGreen, HueEditorBlue);

        SelectedHueEditorEntry.Name = string.IsNullOrWhiteSpace(SelectedHueEditorEntry.Name)
            ? "Custom Hue " + SelectedHueEditorEntry.HueId
            : SelectedHueEditorEntry.Name;

        SelectedHueEditorEntry.TableStart = 1;
        SelectedHueEditorEntry.TableEnd = 31;

        for (int i = 0; i < SelectedHueEditorEntry.Colors.Count; i++)
        {
            double t = i / 31.0;

            // Better in-game ramp:
            // starts dark, builds body color, then slightly softens highlights
            double brightness = 0.10 + (t * 0.90);
            double highlightSoftener = t > 0.75 ? 1.0 - ((t - 0.75) * 0.20) : 1.0;

            byte r = ClampByte(baseColor.R * brightness * highlightSoftener);
            byte g = ClampByte(baseColor.G * brightness * highlightSoftener);
            byte b = ClampByte(baseColor.B * brightness * highlightSoftener);

            // Keep the first few slots from becoming dead black in-game.
            if (i > 0)
            {
                r = (byte)Math.Max((int)r, 8);
                g = (byte)Math.Max((int)g, 8);
                b = (byte)Math.Max((int)b, 8);
            }

            Color color = Color.FromRgb(r, g, b);

            SelectedHueEditorEntry.Colors[i].Color = color;
            SelectedHueEditorEntry.Colors[i].RawValue = HueDataService.ConvertAvaloniaColorToUoColor(color);
        }

        SelectedHueEditorColor = SelectedHueEditorEntry.Colors.FirstOrDefault();
        HueEditorStatusText = "Created guided hue ramp for hue " + SelectedHueEditorEntry.HueId + ". Save hues.mul when ready.";
    }

    private static byte ClampByte(double value)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value > 255)
        {
            return 255;
        }

        return (byte)Math.Round(value);
    }

    private List<HueEditorEntry> GetFreeHueSlots()
    {
        return allHueEditorEntries
            .Where(IsFreeHueSlot)
            .ToList();
    }

    private bool IsFreeHueSlot(HueEditorEntry entry)
    {
        if (entry == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entry.Name))
        {
            return false;
        }

        return entry.Colors.All(slot =>
            slot.RawValue == 0 ||
            slot.RawValue == 1 ||
            slot.RawValue == 0x0001 ||
            slot.RawValue == 0x8000);
    }

    private void SelectNextFreeHueSlot()
    {
        HueEditorEntry? freeSlot = GetFreeHueSlots().FirstOrDefault();

        if (freeSlot == null)
        {
            HueEditorStatusText = "No free hue slots were found.";
            return;
        }

        HueEditorShowFreeOnly = false;
        HueEditorSearchText = freeSlot.HueId.ToString();

        RefreshHueEditorFilter();

        SelectedHueEditorEntry = freeSlot;
        SelectedHueEditorColor = freeSlot.Colors.FirstOrDefault();

        HueEditorStatusText = "Selected free hue slot " + freeSlot.HueId + ".";
    }

    private async Task ExportSelectedHueAsync()
    {
        if (SelectedHueEditorEntry == null)
        {
            HueEditorStatusText = "Select a hue to export.";
            return;
        }

        Window? owner = GetMainWindow();
        if (owner == null)
        {
            HueEditorStatusText = "Could not locate main window.";
            return;
        }

        IStorageFile? file = await owner.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export Hue",
                SuggestedFileName = "Hue " + SelectedHueEditorEntry.HueId + ".txt",
                FileTypeChoices = new[]
                {
                new FilePickerFileType("Hue text file")
                {
                    Patterns = new[] { "*.txt" }
                }
                }
            });

        string? path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            HueEditorStatusText = "Hue export cancelled.";
            return;
        }

        hueDataService.ExportHueText(path, SelectedHueEditorEntry);
        HueEditorStatusText = "Exported hue " + SelectedHueEditorEntry.HueId + " to " + Path.GetFileName(path) + ".";
    }

    private async Task ImportReplaceSelectedHueAsync()
    {
        if (SelectedHueEditorEntry == null)
        {
            HueEditorStatusText = "Select the hue slot to replace first.";
            return;
        }

        Window? owner = GetMainWindow();
        if (owner == null)
        {
            HueEditorStatusText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Import Hue Text",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                new FilePickerFileType("Hue text file")
                {
                    Patterns = new[] { "*.txt" }
                }
                }
            });

        if (files.Count == 0)
        {
            HueEditorStatusText = "Hue import cancelled.";
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            HueEditorStatusText = "Selected hue file is invalid.";
            return;
        }

        HueEditorEntry imported = hueDataService.ImportHueText(path);
        CopyHueData(imported, SelectedHueEditorEntry, keepTargetHueId: true);

        SelectedHueEditorColor = SelectedHueEditorEntry.Colors.FirstOrDefault();
        HueEditorStatusText = "Replaced hue " + SelectedHueEditorEntry.HueId + " from " + Path.GetFileName(path) + ". Save hues.mul when ready.";
    }

    private void ReplaceHueFromIndex()
    {
        if (SelectedHueEditorEntry == null)
        {
            HueEditorStatusText = "Select the hue slot to replace first.";
            return;
        }

        HueEditorEntry? source = allHueEditorEntries.FirstOrDefault(x => x.HueId == HueEditorReplaceSourceHueId);
        if (source == null)
        {
            HueEditorStatusText = "Source hue " + HueEditorReplaceSourceHueId + " was not found.";
            return;
        }

        if (ReferenceEquals(source, SelectedHueEditorEntry))
        {
            HueEditorStatusText = "Source and target hue are the same.";
            return;
        }

        CopyHueData(source, SelectedHueEditorEntry, keepTargetHueId: true);

        SelectedHueEditorColor = SelectedHueEditorEntry.Colors.FirstOrDefault();
        HueEditorStatusText = "Copied hue " + source.HueId + " into hue " + SelectedHueEditorEntry.HueId + ". Save hues.mul when ready.";
    }

    private static void CopyHueData(HueEditorEntry source, HueEditorEntry target, bool keepTargetHueId)
    {
        int targetHueId = target.HueId;

        target.Name = source.Name;
        target.TableStart = source.TableStart;
        target.TableEnd = source.TableEnd;

        target.Colors.Clear();

        foreach (HueEditorColorSlot slot in source.Colors)
        {
            target.Colors.Add(new HueEditorColorSlot
            {
                Index = slot.Index,
                RawValue = slot.RawValue,
                Color = slot.Color
            });
        }

        if (keepTargetHueId)
        {
            target.HueId = targetHueId;
        }
    }

    private void UpdateHueArtPreview()
    {
        try
        {
            if (SelectedHueEditorEntry == null)
            {
                return;
            }

            if (!artDataService.Initialize(GetCurrentFolderPath()))
            {
                HueEditorStatusText = "Art files are not loaded. Open a UO folder first.";
                return;
            }

            ArtEntry? artEntry = artDataService.GetStaticEntryById(HuePreviewArtId);
            if (artEntry == null)
            {
                HueEditorStatusText = "Could not find static art 0x" + HuePreviewArtId.ToString("X4") + ".";
                return;
            }

            WriteableBitmap? source = artDataService.LoadBitmap(artEntry);
            if (source == null)
            {
                HueEditorStatusText = "Could not load static art 0x" + HuePreviewArtId.ToString("X4") + ".";
                return;
            }

            HuePreviewBitmap = ApplyHueEditorEntryToBitmap(source, SelectedHueEditorEntry);
            HueEditorStatusText = "Updated art preview using static 0x" + HuePreviewArtId.ToString("X4") + ".";
        }
        catch (Exception exception)
        {
            HueEditorStatusText = "Preview failed: " + exception.Message;
        }
    }

    private static WriteableBitmap BuildHuePreviewTestBitmap(int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = ((y * width) + x) * 4;

                byte value = (byte)Math.Clamp((x + y) * 255 / Math.Max(1, width + height - 2), 0, 255);

                pixels[offset + 0] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
                pixels[offset + 3] = 255;
            }
        }

        return CreateHuePreviewBitmap(width, height, pixels);
    }

    private static WriteableBitmap ApplyHueEditorEntryToBitmap(WriteableBitmap sourceBitmap, HueEditorEntry hueEntry)
    {
        HuePreviewPixels pixels = ReadHuePreviewPixels(sourceBitmap);
        byte[] outputPixels = new byte[pixels.Pixels.Length];

        Buffer.BlockCopy(pixels.Pixels, 0, outputPixels, 0, pixels.Pixels.Length);

        for (int y = 0; y < pixels.Height; y++)
        {
            for (int x = 0; x < pixels.Width; x++)
            {
                int offset = ((y * pixels.Width) + x) * 4;

                byte blue = outputPixels[offset + 0];
                byte green = outputPixels[offset + 1];
                byte red = outputPixels[offset + 2];
                byte alpha = outputPixels[offset + 3];

                if (alpha == 0)
                {
                    continue;
                }

                int brightness = (red + green + blue) / 3;
                int hueIndex = (int)Math.Round((brightness / 255.0) * 31.0);
                hueIndex = Math.Clamp(hueIndex, 0, 31);

                if (hueIndex >= hueEntry.Colors.Count)
                {
                    continue;
                }

                Color hueColor = hueEntry.Colors[hueIndex].Color;

                outputPixels[offset + 0] = hueColor.B;
                outputPixels[offset + 1] = hueColor.G;
                outputPixels[offset + 2] = hueColor.R;
                outputPixels[offset + 3] = alpha;
            }
        }

        return CreateHuePreviewBitmap(pixels.Width, pixels.Height, outputPixels);
    }

    private static HuePreviewPixels ReadHuePreviewPixels(WriteableBitmap bitmap)
    {
        using ILockedFramebuffer framebuffer = bitmap.Lock();

        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int sourceRowBytes = framebuffer.RowBytes;
        int targetRowBytes = width * 4;

        byte[] sourceBytes = new byte[sourceRowBytes * height];
        Marshal.Copy(framebuffer.Address, sourceBytes, 0, sourceBytes.Length);

        byte[] packedBytes = new byte[targetRowBytes * height];

        for (int y = 0; y < height; y++)
        {
            Buffer.BlockCopy(
                sourceBytes,
                y * sourceRowBytes,
                packedBytes,
                y * targetRowBytes,
                targetRowBytes);
        }

        return new HuePreviewPixels(width, height, packedBytes);
    }

    private static WriteableBitmap CreateHuePreviewBitmap(int width, int height, byte[] pixels)
    {
        WriteableBitmap bitmap = new(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);

        return bitmap;
    }

    private sealed class HuePreviewPixels
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; }

        public HuePreviewPixels(int width, int height, byte[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }
    }

    private void ApplyHuePreset()
    {
        if (SelectedHueEditorEntry == null)
        {
            HueEditorStatusText = "Select a hue slot first.";
            return;
        }

        Color baseColor = GetPresetBaseColor(SelectedHuePreset);
        if (SelectedHuePreset == "Cloth" || SelectedHuePreset == "Metal" || SelectedHuePreset == "Glow")
        {
            baseColor = Color.FromRgb(HueEditorRed, HueEditorGreen, HueEditorBlue);
        }

        SelectedHueEditorEntry.Name = string.IsNullOrWhiteSpace(SelectedHueEditorEntry.Name)
            ? SelectedHuePreset + " Hue " + SelectedHueEditorEntry.HueId
            : SelectedHueEditorEntry.Name;

        SelectedHueEditorEntry.TableStart = 1;
        SelectedHueEditorEntry.TableEnd = 31;

        for (int i = 0; i < SelectedHueEditorEntry.Colors.Count; i++)
        {
            double t = i / 31.0;
            Color color = BuildPresetColor(baseColor, SelectedHuePreset, t);

            SelectedHueEditorEntry.Colors[i].Color = color;
            SelectedHueEditorEntry.Colors[i].RawValue = HueDataService.ConvertAvaloniaColorToUoColor(color);
        }

        SelectedHueEditorColor = SelectedHueEditorEntry.Colors.FirstOrDefault();
        UpdateHueArtPreview();

        HueEditorStatusText = "Applied " + SelectedHuePreset + " preset to hue " + SelectedHueEditorEntry.HueId + ".";
    }

    private static Color GetPresetBaseColor(string preset)
    {
        return preset switch
        {
            "Fire" => Color.FromRgb(235, 72, 18),
            "Ice" => Color.FromRgb(90, 190, 255),
            "Poison" => Color.FromRgb(70, 210, 65),
            "Shadow" => Color.FromRgb(95, 45, 135),
            "Gold" => Color.FromRgb(235, 175, 45),
            "Bone" => Color.FromRgb(210, 195, 150),
            "Leather" => Color.FromRgb(130, 75, 35),
            _ => Color.FromRgb(160, 160, 160)
        };
    }

    private static Color BuildPresetColor(Color baseColor, string preset, double t)
    {
        double curve = preset switch
        {
            "Metal" => Math.Pow(t, 1.35),
            "Glow" => Math.Pow(t, 0.55),
            "Shadow" => Math.Pow(t, 1.8),
            "Bone" => Math.Pow(t, 0.85),
            _ => t
        };

        double min = preset switch
        {
            "Glow" => 0.18,
            "Shadow" => 0.05,
            "Metal" => 0.08,
            _ => 0.10
        };

        double brightness = min + (curve * (1.0 - min));

        double desaturate = preset switch
        {
            "Metal" => 0.25 + (t * 0.35),
            "Bone" => 0.35,
            "Leather" => 0.10,
            _ => 0.0
        };

        double r = baseColor.R * brightness;
        double g = baseColor.G * brightness;
        double b = baseColor.B * brightness;

        if (desaturate > 0)
        {
            double gray = (r + g + b) / 3.0;
            r = Lerp(r, gray, desaturate);
            g = Lerp(g, gray, desaturate);
            b = Lerp(b, gray, desaturate);
        }

        if (preset == "Fire" && t > 0.72)
        {
            r = Lerp(r, 255, (t - 0.72) * 1.2);
            g = Lerp(g, 220, (t - 0.72) * 0.8);
        }

        if (preset == "Glow" && t > 0.65)
        {
            r = Lerp(r, 255, (t - 0.65) * 0.8);
            g = Lerp(g, 255, (t - 0.65) * 0.8);
            b = Lerp(b, 255, (t - 0.65) * 0.8);
        }

        return Color.FromRgb(ClampByte(r), ClampByte(g), ClampByte(b));
    }

    private static double Lerp(double a, double b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return a + ((b - a) * t);
    }
}
