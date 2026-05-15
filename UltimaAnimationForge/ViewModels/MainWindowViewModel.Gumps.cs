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
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    private WriteableBitmap? gumpOverlayBitmap;

    [ObservableProperty]
    private string gumpOverlayImageName = "No overlay loaded";

    [ObservableProperty]
    private double gumpOverlayOpacityPercent = 35.0;

    public ObservableCollection<GumpOverlayBlendMode> GumpOverlayBlendModes { get; } = new()
{
    GumpOverlayBlendMode.NormalFade,
    GumpOverlayBlendMode.Multiply,
    GumpOverlayBlendMode.Overlay,
    GumpOverlayBlendMode.SoftLight,
    GumpOverlayBlendMode.Screen
};

    [ObservableProperty]
    private GumpOverlayBlendMode selectedGumpOverlayBlendMode = GumpOverlayBlendMode.Overlay;

    private void ResetGumpsForProfileChange()
    {
        GumpEntries.Clear();
        SelectedGump = null;
        SelectedGumpBitmap = null;
        PairedGump = null;
        PairedGumpBitmap = null;
        GumpInfoText = "No gump loaded.";
    }

    [ObservableProperty]
    private bool showPaperdollTemplate;

    [ObservableProperty]
    private string selectedPaperdollTemplate = "Male";

    public ObservableCollection<string> PaperdollTemplateOptions { get; } = new()
{
    "Male",
    "Female"
};

    [ObservableProperty]
    private int gumpWearableOffsetX;

    [ObservableProperty]
    private int gumpWearableOffsetY;

    [ObservableProperty]
    private double paperdollTemplateOpacity = 0.70;

    [ObservableProperty]
    private int paperdollTemplateGumpId = 12;

    [ObservableProperty]
    private int paperdollTemplateOffsetX;

    [ObservableProperty]
    private int paperdollTemplateOffsetY;

    [ObservableProperty]
    private WriteableBitmap? paperdollTemplateBitmap;

    [ObservableProperty]
    private int gumpWearableScalePercent = 100;

    partial void OnGumpInfoTextChanged(string value)
    {
        OnPropertyChanged(nameof(HeaderStatusText));
    }
    partial void OnShowPaperdollTemplateChanged(bool value)
    {
        if (value)
        {
            LoadPaperdollTemplateGump();
        }
    }

    partial void OnPaperdollTemplateGumpIdChanged(int value)
    {
        if (ShowPaperdollTemplate)
        {
            LoadPaperdollTemplateGump();
        }
    }

    private void LoadPaperdollTemplateGump()
    {
        GumpEntry? templateEntry = gumpDataService.Entries
            .FirstOrDefault(entry => entry.GumpId == PaperdollTemplateGumpId && entry.IsValid);

        if (templateEntry == null)
        {
            PaperdollTemplateBitmap = null;
            GumpInfoText = "Paperdoll template gump not found.";
            return;
        }

        GumpLoadResult result = gumpDataService.LoadGump(templateEntry);

        if (!result.Success || result.Bitmap == null)
        {
            PaperdollTemplateBitmap = null;
            GumpInfoText = result.Message;
            return;
        }

        PaperdollTemplateBitmap = result.Bitmap;
    }

    [RelayCommand]
    private void NudgePaperdollTemplateLeft()
    {
        PaperdollTemplateOffsetX--;
    }

    [RelayCommand]
    private void NudgePaperdollTemplateRight()
    {
        PaperdollTemplateOffsetX++;
    }

    [RelayCommand]
    private void NudgePaperdollTemplateUp()
    {
        PaperdollTemplateOffsetY--;
    }

    [RelayCommand]
    private void NudgePaperdollTemplateDown()
    {
        PaperdollTemplateOffsetY++;
    }

    [RelayCommand]
    private void ResetPaperdollTemplateOffset()
    {
        PaperdollTemplateOffsetX = 0;
        PaperdollTemplateOffsetY = 0;
    }


    private WriteableBitmap? originalGumpEditBitmap;

    [ObservableProperty]
    private bool hasUnsavedGumpEdit;

    public ObservableCollection<string> GumpSharpenModes { get; } = new()
{
    "Mild Sharpen",
    "Strong Sharpen",
    "Edge Enhance",
    "Unsharp Mask"
};

    [ObservableProperty]
    private string selectedGumpSharpenMode = "Mild Sharpen";

    private readonly GumpDataService gumpDataService = new();

    public ObservableCollection<GumpEntry> GumpEntries { get; } = new();

    [ObservableProperty]
    private GumpEntry? selectedGump;

    [ObservableProperty]
    private WriteableBitmap? selectedGumpBitmap;

    [ObservableProperty]
    private string gumpSearchText = string.Empty;

    [ObservableProperty]
    private bool showFreeGumpSlots;

    [ObservableProperty]
    private string gumpInfoText = "No gump loaded.";

    [ObservableProperty]
    private int totalFreeGumpSlots;

    [ObservableProperty]
    private int totalUsedGumpSlots;

    public string GumpSlotSummaryText =>
        "Used: " + TotalUsedGumpSlots + "\nFree: " + TotalFreeGumpSlots;

    [ObservableProperty]
    private GumpFreeSlotMode selectedGumpFreeSlotMode = GumpFreeSlotMode.All;

    [ObservableProperty]
    private int totalFreeMaleWearableGumpSlots;

    [ObservableProperty]
    private int totalFreeFemaleWearableGumpSlots;

    public string GumpWearableSlotSummaryText =>
        "Male 50000-59999 Free: " + TotalFreeMaleWearableGumpSlots +
        "\nFemale 60000-65534 Free: " + TotalFreeFemaleWearableGumpSlots;

    [ObservableProperty]
    private bool showGumpParallelView;

    [ObservableProperty]
    private WriteableBitmap? pairedGumpBitmap;

    [ObservableProperty]
    private GumpEntry? pairedGump;

    public string GumpParallelPairText
    {
        get
        {
            if (SelectedGump == null)
            {
                return "Pair: -";
            }

            int pairId = GetWearablePairGumpId(SelectedGump.GumpId);
            if (pairId < 0)
            {
                return "Pair: none";
            }

            return "Pair: " + pairId + " [0x" + pairId.ToString("X") + "]";
        }
    }

    [RelayCommand]
    private void NudgeGumpLeft()
    {
        GumpWearableOffsetX--;
    }

    [RelayCommand]
    private void NudgeGumpRight()
    {
        GumpWearableOffsetX++;
    }

    [RelayCommand]
    private void NudgeGumpUp()
    {
        GumpWearableOffsetY--;
    }

    [RelayCommand]
    private void NudgeGumpDown()
    {
        GumpWearableOffsetY++;
    }

    [RelayCommand]
    private void ResetGumpWearableOffset()
    {
        GumpWearableOffsetX = 0;
        GumpWearableOffsetY = 0;
    }

    [RelayCommand]
    private void SharpenSelectedGump()
    {
        switch (SelectedGumpSharpenMode)
        {
            case "Strong Sharpen":
                ApplySelectedGumpEdit(ApplyStrongSharpen, "Strong Sharpen");
                break;

            case "Edge Enhance":
                ApplySelectedGumpEdit(ApplyEdgeEnhance, "Edge Enhance");
                break;

            case "Unsharp Mask":
                ApplySelectedGumpEdit(ApplyUnsharpMask, "Unsharp Mask");
                break;

            default:
                ApplySelectedGumpEdit(ApplySharpen, "Mild Sharpen");
                break;
        }
    }

    [RelayCommand]
    private void IncreaseGumpSaturation()
    {
        ApplySelectedGumpEdit(pixels => ApplySaturation(pixels, 1.20), "Saturation +");
    }

    [RelayCommand]
    private void DecreaseGumpSaturation()
    {
        ApplySelectedGumpEdit(pixels => ApplySaturation(pixels, 0.85), "Saturation -");
    }

    [RelayCommand]
    private void IncreaseGumpContrast()
    {
        ApplySelectedGumpEdit(pixels => ApplyContrast(pixels, 1.20), "Contrast +");
    }

    [RelayCommand]
    private void DecreaseGumpContrast()
    {
        ApplySelectedGumpEdit(pixels => ApplyContrast(pixels, 0.85), "Contrast -");
    }

    [RelayCommand]
    private void BrightenGump()
    {
        ApplySelectedGumpEdit(pixels => ApplyBrightness(pixels, 15), "Brighten");
    }

    [RelayCommand]
    private void DarkenGump()
    {
        ApplySelectedGumpEdit(pixels => ApplyBrightness(pixels, -15), "Darken");
    }

    [RelayCommand]
    private void OutlineGump()
    {
        ApplySelectedGumpEdit(ApplyBlackOutline, "Outline");
    }

    private void ApplySelectedGumpEdit(Func<GumpFramePixels, GumpFramePixels> editFunction, string editName)
    {
        if (SelectedGump == null || !SelectedGump.IsValid)
        {
            GumpInfoText = "Select a valid gump first.";
            return;
        }

        if (SelectedGumpBitmap == null)
        {
            GumpInfoText = "Load a gump image first.";
            return;
        }

        GumpFramePixels sourcePixels = ReadGumpPixels(SelectedGumpBitmap);
        GumpFramePixels editedPixels = editFunction(sourcePixels);
        WriteableBitmap editedBitmap = BuildBitmapFromGumpPixels(editedPixels);

        SelectedGumpBitmap = editedBitmap;
        HasUnsavedGumpEdit = true;
        GumpInfoText = editName + " preview applied. Click Save Gump Edit to write it.";
    }

    private static GumpFramePixels ApplyStrongSharpen(GumpFramePixels source)
    {
        GumpFramePixels once = ApplySharpen(source);
        return ApplySharpen(once);
    }

    private static GumpFramePixels ApplyEdgeEnhance(GumpFramePixels source)
    {
        byte[] output = (byte[])source.Pixels.Clone();

        int[] kernel =
        {
        -1, -1, -1,
        -1,  9, -1,
        -1, -1, -1
    };

        ApplyKernel(source, output, kernel);

        return new GumpFramePixels(source.Width, source.Height, output);
    }

    private static GumpFramePixels ApplyUnsharpMask(GumpFramePixels source)
    {
        GumpFramePixels blurred = ApplyBoxBlur(source);
        byte[] output = (byte[])source.Pixels.Clone();

        for (int i = 0; i + 3 < output.Length; i += 4)
        {
            if (source.Pixels[i + 3] == 0)
            {
                continue;
            }

            output[i + 0] = ClampToByte(source.Pixels[i + 0] + ((source.Pixels[i + 0] - blurred.Pixels[i + 0]) * 0.75));
            output[i + 1] = ClampToByte(source.Pixels[i + 1] + ((source.Pixels[i + 1] - blurred.Pixels[i + 1]) * 0.75));
            output[i + 2] = ClampToByte(source.Pixels[i + 2] + ((source.Pixels[i + 2] - blurred.Pixels[i + 2]) * 0.75));
        }

        return new GumpFramePixels(source.Width, source.Height, output);
    }

    private static GumpFramePixels ApplySaturation(GumpFramePixels source, double factor)
    {
        byte[] output = (byte[])source.Pixels.Clone();

        for (int i = 0; i + 3 < output.Length; i += 4)
        {
            if (output[i + 3] == 0)
            {
                continue;
            }

            double blue = output[i + 0];
            double green = output[i + 1];
            double red = output[i + 2];

            double gray = (red * 0.299) + (green * 0.587) + (blue * 0.114);

            output[i + 0] = ClampToByte(gray + ((blue - gray) * factor));
            output[i + 1] = ClampToByte(gray + ((green - gray) * factor));
            output[i + 2] = ClampToByte(gray + ((red - gray) * factor));
        }

        return new GumpFramePixels(source.Width, source.Height, output);
    }

    private static GumpFramePixels ApplyBoxBlur(GumpFramePixels source)
    {
        byte[] output = (byte[])source.Pixels.Clone();

        int[] kernel =
        {
        1, 1, 1,
        1, 1, 1,
        1, 1, 1
    };

        ApplyKernel(source, output, kernel, 9);

        return new GumpFramePixels(source.Width, source.Height, output);
    }

    private static void ApplyKernel(GumpFramePixels source, byte[] output, int[] kernel, int divisor = 1)
    {
        for (int y = 1; y < source.Height - 1; y++)
        {
            for (int x = 1; x < source.Width - 1; x++)
            {
                int centerOffset = ((y * source.Width) + x) * 4;

                if (source.Pixels[centerOffset + 3] == 0)
                {
                    continue;
                }

                int blue = 0;
                int green = 0;
                int red = 0;
                int kernelIndex = 0;

                for (int ky = -1; ky <= 1; ky++)
                {
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        int sourceOffset = (((y + ky) * source.Width) + (x + kx)) * 4;
                        int weight = kernel[kernelIndex++];

                        blue += source.Pixels[sourceOffset + 0] * weight;
                        green += source.Pixels[sourceOffset + 1] * weight;
                        red += source.Pixels[sourceOffset + 2] * weight;
                    }
                }

                output[centerOffset + 0] = ClampToByte(blue / divisor);
                output[centerOffset + 1] = ClampToByte(green / divisor);
                output[centerOffset + 2] = ClampToByte(red / divisor);
            }
        }
    }

    [RelayCommand]
    private void SaveGumpEdit()
    {
        if (SelectedGump == null || !SelectedGump.IsValid || SelectedGumpBitmap == null)
        {
            GumpInfoText = "Select a valid edited gump first.";
            return;
        }

        GumpSaveResult result = gumpDataService.ReplaceGumpWithBitmap(
            SelectedGumpBitmap,
            SelectedGump.GumpId,
            "Gump edit");

        GumpInfoText = result.Message;

        if (result.Success)
        {
            originalGumpEditBitmap = CloneGumpBitmap(SelectedGumpBitmap);
            HasUnsavedGumpEdit = false;
            RebuildGumpList();
        }
    }

    [RelayCommand]
    private void ResetGumpEditPreview()
    {
        if (originalGumpEditBitmap == null)
        {
            GumpInfoText = "No original gump preview to restore.";
            return;
        }

        SelectedGumpBitmap = CloneGumpBitmap(originalGumpEditBitmap);
        HasUnsavedGumpEdit = false;
        GumpInfoText = "Gump edit preview reset.";
    }

    private static WriteableBitmap CloneGumpBitmap(WriteableBitmap source)
    {
        GumpFramePixels pixels = ReadGumpPixels(source);
        byte[] copiedPixels = (byte[])pixels.Pixels.Clone();
        return BuildBitmapFromGumpPixels(new GumpFramePixels(pixels.Width, pixels.Height, copiedPixels));
    }

    private sealed class GumpFramePixels
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; }

        public GumpFramePixels(int width, int height, byte[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }
    }

    private static GumpFramePixels ReadGumpPixels(WriteableBitmap bitmap)
    {
        using ILockedFramebuffer framebuffer = bitmap.Lock();

        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int srcRowBytes = framebuffer.RowBytes;
        int dstRowBytes = width * 4;

        byte[] source = new byte[srcRowBytes * height];
        Marshal.Copy(framebuffer.Address, source, 0, source.Length);

        byte[] packed = new byte[dstRowBytes * height];

        for (int y = 0; y < height; y++)
        {
            Buffer.BlockCopy(source, y * srcRowBytes, packed, y * dstRowBytes, dstRowBytes);
        }

        return new GumpFramePixels(width, height, packed);
    }

    private static WriteableBitmap BuildBitmapFromGumpPixels(GumpFramePixels pixels)
    {
        WriteableBitmap bitmap = new WriteableBitmap(
            new PixelSize(pixels.Width, pixels.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        Marshal.Copy(pixels.Pixels, 0, framebuffer.Address, pixels.Pixels.Length);

        return bitmap;
    }

    private static GumpFramePixels ApplyBrightness(GumpFramePixels source, int amount)
    {
        byte[] output = (byte[])source.Pixels.Clone();

        for (int i = 0; i + 3 < output.Length; i += 4)
        {
            if (output[i + 3] == 0)
            {
                continue;
            }

            output[i + 0] = ClampToByte(output[i + 0] + amount);
            output[i + 1] = ClampToByte(output[i + 1] + amount);
            output[i + 2] = ClampToByte(output[i + 2] + amount);
        }

        return new GumpFramePixels(source.Width, source.Height, output);
    }

    private static GumpFramePixels ApplyContrast(GumpFramePixels source, double factor)
    {
        byte[] output = (byte[])source.Pixels.Clone();

        for (int i = 0; i + 3 < output.Length; i += 4)
        {
            if (output[i + 3] == 0)
            {
                continue;
            }

            output[i + 0] = ClampToByte(((output[i + 0] - 128) * factor) + 128);
            output[i + 1] = ClampToByte(((output[i + 1] - 128) * factor) + 128);
            output[i + 2] = ClampToByte(((output[i + 2] - 128) * factor) + 128);
        }

        return new GumpFramePixels(source.Width, source.Height, output);
    }

    private static GumpFramePixels ApplySharpen(GumpFramePixels source)
    {
        byte[] output = (byte[])source.Pixels.Clone();

        int[] kernel =
        {
         0, -1,  0,
        -1,  5, -1,
         0, -1,  0
    };

        for (int y = 1; y < source.Height - 1; y++)
        {
            for (int x = 1; x < source.Width - 1; x++)
            {
                int centerOffset = ((y * source.Width) + x) * 4;

                if (source.Pixels[centerOffset + 3] == 0)
                {
                    continue;
                }

                int blue = 0;
                int green = 0;
                int red = 0;
                int kernelIndex = 0;

                for (int ky = -1; ky <= 1; ky++)
                {
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        int sourceOffset = (((y + ky) * source.Width) + (x + kx)) * 4;
                        int weight = kernel[kernelIndex++];

                        blue += source.Pixels[sourceOffset + 0] * weight;
                        green += source.Pixels[sourceOffset + 1] * weight;
                        red += source.Pixels[sourceOffset + 2] * weight;
                    }
                }

                output[centerOffset + 0] = ClampToByte(blue);
                output[centerOffset + 1] = ClampToByte(green);
                output[centerOffset + 2] = ClampToByte(red);
            }
        }

        return new GumpFramePixels(source.Width, source.Height, output);
    }

    private static GumpFramePixels ApplyBlackOutline(GumpFramePixels source)
    {
        byte[] output = (byte[])source.Pixels.Clone();

        for (int y = 1; y < source.Height - 1; y++)
        {
            for (int x = 1; x < source.Width - 1; x++)
            {
                int offset = ((y * source.Width) + x) * 4;

                if (source.Pixels[offset + 3] != 0)
                {
                    continue;
                }

                bool touchesVisiblePixel = false;

                for (int oy = -1; oy <= 1 && !touchesVisiblePixel; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0)
                        {
                            continue;
                        }

                        int neighborOffset = (((y + oy) * source.Width) + (x + ox)) * 4;
                        if (source.Pixels[neighborOffset + 3] > 0)
                        {
                            touchesVisiblePixel = true;
                            break;
                        }
                    }
                }

                if (touchesVisiblePixel)
                {
                    output[offset + 0] = 0;
                    output[offset + 1] = 0;
                    output[offset + 2] = 0;
                    output[offset + 3] = 255;
                }
            }
        }

        return new GumpFramePixels(source.Width, source.Height, output);
    }

    private static byte ClampToByte(double value)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value > 255)
        {
            return 255;
        }

        return (byte)value;
    }

    partial void OnShowGumpParallelViewChanged(bool value)
    {
        LoadPairedGump();
    }

    partial void OnSelectedGumpFreeSlotModeChanged(GumpFreeSlotMode value)
    {
        RebuildGumpList();
    }

    [RelayCommand]
    private void ShowGumpEditor()
    {
        ActiveToolTab = MainToolTab.Gumps;
    }

    partial void OnSelectedGumpChanged(GumpEntry? value)
    {
        LoadSelectedGump();
        LoadPairedGump();
        OnPropertyChanged(nameof(GumpParallelPairText));
    }

    partial void OnGumpSearchTextChanged(string value)
    {
        RebuildGumpList();
    }

    partial void OnShowFreeGumpSlotsChanged(bool value)
    {
        RebuildGumpList();
    }

    private void InitializeGumpsForCurrentFolder()
    {
        GumpEntries.Clear();
        SelectedGump = null;
        SelectedGumpBitmap = null;

        if (activeProfile == null)
        {
            activeProfile = GetActiveProfile();
        }

        string uoFolderPath = activeProfile?.UoFolderPath ?? string.Empty;

        if (string.IsNullOrWhiteSpace(uoFolderPath))
        {
            GumpInfoText = "Open a UO folder first.";
            return;
        }

        if (!gumpDataService.Initialize(uoFolderPath))
        {
            GumpInfoText = "Could not load gumpartLegacyMUL.uop or fallback Gumpidx.mul / Gumpart.mul.";
            return;
        }

        RebuildGumpList();
        GumpInfoText = "Loaded gump index.";
    }

    private void RebuildGumpList()
    {
        GumpEntries.Clear();

        string search = (GumpSearchText ?? string.Empty).Trim();

        HashSet<int> usedGumpIds = gumpDataService.Entries
            .Where(entry => entry.IsValid)
            .Select(entry => entry.GumpId)
            .ToHashSet();

        TotalUsedGumpSlots = usedGumpIds.Count;
        TotalFreeGumpSlots = 0xFFFF - usedGumpIds.Count;

        TotalFreeMaleWearableGumpSlots = CountFreeGumpSlots(usedGumpIds, 50000, 59999);
        TotalFreeFemaleWearableGumpSlots = CountFreeGumpSlots(usedGumpIds, 60000, 0xFFFE);

        OnPropertyChanged(nameof(GumpSlotSummaryText));
        OnPropertyChanged(nameof(GumpWearableSlotSummaryText));

        if (ShowFreeGumpSlots)
        {
            int startGumpId = 0;
            int endGumpId = 0xFFFE;

            if (SelectedGumpFreeSlotMode == GumpFreeSlotMode.MaleWearables)
            {
                startGumpId = 50000;
                endGumpId = 59999;
            }
            else if (SelectedGumpFreeSlotMode == GumpFreeSlotMode.FemaleWearables)
            {
                startGumpId = 60000;
                endGumpId = 0xFFFE;
            }

            for (int gumpId = startGumpId; gumpId <= endGumpId; gumpId++)
            {
                if (usedGumpIds.Contains(gumpId))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(search) &&
                    !gumpId.ToString().Contains(search) &&
                    !("0x" + gumpId.ToString("X")).Contains(search, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GumpEntries.Add(new GumpEntry
                {
                    GumpId = gumpId,
                    IsValid = false,
                    SourceFile = "Free Slot"
                });
            }

            SelectedGumpBitmap = null;

            GumpInfoText = SelectedGumpFreeSlotMode switch
            {
                GumpFreeSlotMode.MaleWearables => "Showing free male wearable gump slots.",
                GumpFreeSlotMode.FemaleWearables => "Showing free female wearable gump slots.",
                _ => "Showing free gump slots."
            };
        }
        else
        {
            foreach (GumpEntry entry in gumpDataService.Entries)
            {
                if (!entry.IsValid)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(search) &&
                    !entry.GumpId.ToString().Contains(search) &&
                    !entry.HexId.Contains(search, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GumpEntries.Add(entry);
            }
        }

        SelectedGump = GumpEntries.Count > 0 ? GumpEntries[0] : null;
    }

    private static int CountFreeGumpSlots(HashSet<int> usedGumpIds, int startGumpId, int endGumpId)
    {
        int freeCount = 0;

        for (int gumpId = startGumpId; gumpId <= endGumpId; gumpId++)
        {
            if (!usedGumpIds.Contains(gumpId))
            {
                freeCount++;
            }
        }

        return freeCount;
    }

    private void LoadSelectedGump()
    {
        SelectedGumpBitmap = null;
        originalGumpEditBitmap = null;
        HasUnsavedGumpEdit = false;

        GumpLoadResult result = gumpDataService.LoadGump(SelectedGump);

        GumpInfoText = result.Message;

        if (!result.Success || result.Bitmap == null)
        {
            return;
        }

        SelectedGumpBitmap = result.Bitmap;
        originalGumpEditBitmap = CloneGumpBitmap(result.Bitmap);
        HasUnsavedGumpEdit = false;
    }

    [RelayCommand]
    private async Task ImportPngToGumpAsync()
    {
        if (SelectedGump == null)
        {
            GumpInfoText = "Select a gump slot first.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            GumpInfoText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = SelectedGump.IsValid ? "Replace Gump From PNG" : "Import PNG Into Free Gump Slot",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image files")
                    {
                        Patterns = new[] { "*.png", "*.bmp" }
                    }
                }
            });

        if (files.Count == 0)
        {
            GumpInfoText = "PNG import cancelled.";
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            GumpInfoText = "Selected PNG does not have a local path.";
            return;
        }

        GumpSaveResult result = gumpDataService.ImportPngToSelectedGump(path, SelectedGump.GumpId);
        GumpInfoText = result.Message;

        RebuildGumpList();

        GumpEntry? imported = GumpEntries.FirstOrDefault(x => x.GumpId == SelectedGump.GumpId);
        if (imported != null)
        {
            SelectedGump = imported;
        }
    }

    [RelayCommand]
    private void RemoveSelectedGump()
    {
        if (SelectedGump == null)
        {
            GumpInfoText = "Select a gump first.";
            return;
        }

        if (!SelectedGump.IsValid)
        {
            GumpInfoText = "Selected gump slot is already free.";
            return;
        }

        int removedGumpId = SelectedGump.GumpId;

        GumpSaveResult result = gumpDataService.RemoveGump(removedGumpId);
        GumpInfoText = result.Message;

        RebuildGumpList();

        GumpEntry? next = GumpEntries.FirstOrDefault(x => x.GumpId >= removedGumpId);
        if (next != null)
        {
            SelectedGump = next;
        }
    }

    [RelayCommand]
    private async Task ExportSelectedGumpAsync()
    {
        if (SelectedGump == null)
        {
            GumpInfoText = "Select a gump first.";
            return;
        }

        if (!SelectedGump.IsValid)
        {
            GumpInfoText = "Selected gump slot is empty.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            GumpInfoText = "Could not locate main window.";
            return;
        }

        string fileName =
            "gump_" +
            SelectedGump.GumpId +
            "_0x" +
            SelectedGump.GumpId.ToString("X") +
            ".png";

        IStorageFile? file = await mainWindow.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export Gump Image",
                SuggestedFileName = fileName,
                DefaultExtension = "png",
                FileTypeChoices = new[]
                {
                new FilePickerFileType("PNG image")
                {
                    Patterns = new[] { "*.png" }
                }
                }
            });

        if (file == null)
        {
            GumpInfoText = "Export cancelled.";
            return;
        }

        if (activeProfile == null)
        {
            activeProfile = GetActiveProfile();
        }

        string folderPath = activeProfile?.OutputFolderPath ?? string.Empty;

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            GumpInfoText = "Set an Output Folder in Manage Profiles first.";
            return;
        }
    }

    [RelayCommand]
    private async Task ExportCheckedGumpsAsync()
    {
        List<GumpEntry> exportEntries = GumpEntries
            .Where(entry => entry.IsValid && entry.IsSelectedForExport)
            .ToList();

        if (exportEntries.Count == 0)
        {
            GumpInfoText = "No checked gumps to export.";
            return;
        }

        string? folderPath = await GetProfileOutputFolderAsync("Choose Export Folder");

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            GumpInfoText = "Mass export cancelled.";
            return;
        }

        int exported = 0;
        int failed = 0;

        foreach (GumpEntry entry in exportEntries)
        {
            string outputPath = Path.Combine(
                folderPath,
                "gump_" + entry.GumpId + "_0x" + entry.GumpId.ToString("X") + ".png");

            GumpSaveResult result = gumpDataService.ExportGumpToPng(entry, outputPath);

            if (result.Success)
            {
                exported++;
            }
            else
            {
                failed++;
            }
        }

        GumpInfoText = "Export checked complete. Exported: " + exported + ", Failed: " + failed + ".";
    }

    [RelayCommand]
    private void CheckAllVisibleGumps()
    {
        foreach (GumpEntry entry in GumpEntries.Where(x => x.IsValid))
        {
            entry.IsSelectedForExport = true;
        }
    }

    [RelayCommand]
    private void ClearCheckedGumps()
    {
        foreach (GumpEntry entry in GumpEntries)
        {
            entry.IsSelectedForExport = false;
        }
    }

    [RelayCommand]
    private async Task ImportCheckedGumpsAsync()
    {
        List<GumpEntry> targetEntries = GumpEntries
            .Where(entry => entry.IsSelectedForExport)
            .OrderBy(entry => entry.GumpId)
            .ToList();

        if (targetEntries.Count == 0)
        {
            GumpInfoText = "Check one or more gump slots first.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            GumpInfoText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Choose Images To Import",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                new FilePickerFileType("Image files")
                {
                    Patterns = new[] { "*.png", "*.bmp" }
                }
                }
            });

        if (files.Count == 0)
        {
            GumpInfoText = "Mass import cancelled.";
            return;
        }

        if (files.Count > targetEntries.Count)
        {
            GumpInfoText =
                "Selected " + files.Count +
                " images but only checked " + targetEntries.Count +
                " gump slots.";
            return;
        }

        int imported = 0;
        int failed = 0;

        for (int i = 0; i < files.Count; i++)
        {
            string? path = files[i].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                failed++;
                continue;
            }

            GumpEntry targetEntry = targetEntries[i];

            GumpSaveResult result =
                gumpDataService.ImportPngToSelectedGump(path, targetEntry.GumpId);

            if (result.Success)
            {
                imported++;
            }
            else
            {
                failed++;
            }
        }

        GumpInfoText =
            "Mass import complete. Imported: " + imported +
            ", Failed: " + failed + ".";

        RebuildGumpList();
    }

    private static int GetWearablePairGumpId(int gumpId)
    {
        if (gumpId >= 50000 && gumpId <= 59999)
        {
            return gumpId + 10000;
        }

        if (gumpId >= 60000 && gumpId <= 65534)
        {
            return gumpId - 10000;
        }

        return -1;
    }

    private void LoadPairedGump()
    {
        PairedGump = null;
        PairedGumpBitmap = null;

        if (!ShowGumpParallelView || SelectedGump == null)
        {
            return;
        }

        int pairId = GetWearablePairGumpId(SelectedGump.GumpId);
        if (pairId < 0)
        {
            return;
        }

        PairedGump = gumpDataService.Entries.FirstOrDefault(x => x.GumpId == pairId && x.IsValid);

        if (PairedGump == null)
        {
            PairedGump = new GumpEntry
            {
                GumpId = pairId,
                IsValid = false,
                SourceFile = "Missing Pair"
            };
            return;
        }

        GumpLoadResult result = gumpDataService.LoadGump(PairedGump);
        if (result.Success)
        {
            PairedGumpBitmap = result.Bitmap;
        }
    }

    private async Task<string?> GetProfileOutputFolderAsync(string pickerTitle)
    {
        if (activeProfile == null)
        {
            activeProfile = GetActiveProfile();
        }

        string folderPath = activeProfile?.OutputFolderPath ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
        {
            return folderPath;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            GumpInfoText = "Could not locate main window.";
            return null;
        }

        IReadOnlyList<IStorageFolder> folders = await mainWindow.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = pickerTitle,
                AllowMultiple = false
            });

        if (folders.Count == 0)
        {
            return null;
        }

        folderPath = folders[0].TryGetLocalPath() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            GumpInfoText = "Selected export folder is invalid.";
            return null;
        }

        if (activeProfile != null)
        {
            activeProfile.OutputFolderPath = folderPath;
            settingsService.Save(appSettings);
        }

        return folderPath;
    }

    [RelayCommand]
    private void SaveGumpOffset()
    {
        if (SelectedGump == null || !SelectedGump.IsValid || SelectedGumpBitmap == null)
        {
            GumpInfoText = "Select a valid gump first.";
            return;
        }

        if (GumpWearableOffsetX == 0 && GumpWearableOffsetY == 0)
        {
            GumpInfoText = "No wearable offset to save.";
            return;
        }

        GumpFramePixels sourcePixels = ReadGumpPixels(SelectedGumpBitmap);
        GumpFramePixels shiftedPixels = ApplyOffsetToGumpPixels(
            sourcePixels,
            GumpWearableOffsetX,
            GumpWearableOffsetY);

        WriteableBitmap shiftedBitmap = BuildBitmapFromGumpPixels(shiftedPixels);

        GumpSaveResult result = gumpDataService.ReplaceGumpWithBitmap(
            shiftedBitmap,
            SelectedGump.GumpId,
            "Offset");

        GumpInfoText = result.Message;

        if (result.Success)
        {
            SelectedGumpBitmap = shiftedBitmap;
            originalGumpEditBitmap = CloneGumpBitmap(shiftedBitmap);
            HasUnsavedGumpEdit = false;

            GumpWearableOffsetX = 0;
            GumpWearableOffsetY = 0;

            RebuildGumpList();

            GumpEntry? reselected = GumpEntries.FirstOrDefault(entry => entry.GumpId == SelectedGump.GumpId);
            if (reselected != null)
            {
                SelectedGump = reselected;
            }
        }
    }

    private static GumpFramePixels ApplyOffsetToGumpPixels(GumpFramePixels source, int offsetX, int offsetY)
    {
        byte[] output = new byte[source.Pixels.Length];

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                int targetX = x + offsetX;
                int targetY = y + offsetY;

                if (targetX < 0 || targetX >= source.Width || targetY < 0 || targetY >= source.Height)
                {
                    continue;
                }

                int sourceOffset = ((y * source.Width) + x) * 4;
                int targetOffset = ((targetY * source.Width) + targetX) * 4;

                output[targetOffset + 0] = source.Pixels[sourceOffset + 0];
                output[targetOffset + 1] = source.Pixels[sourceOffset + 1];
                output[targetOffset + 2] = source.Pixels[sourceOffset + 2];
                output[targetOffset + 3] = source.Pixels[sourceOffset + 3];
            }
        }

        return new GumpFramePixels(source.Width, source.Height, output);
    }

    [RelayCommand]
    private async Task LoadSourceImageForGumpAsync()
    {
        if (SelectedGump == null)
        {
            GumpInfoText = "Select a gump slot first.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            GumpInfoText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Choose Source Image / Screenshot",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                new FilePickerFileType("Image files")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" }
                }
                }
            });

        if (files.Count == 0)
        {
            GumpInfoText = "Source image load cancelled.";
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            GumpInfoText = "Selected source image does not have a local path.";
            return;
        }

        SelectedGumpBitmap = LoadBitmapForGumpPreview(path, 260, 237);
        originalGumpEditBitmap = CloneGumpBitmap(SelectedGumpBitmap);
        HasUnsavedGumpEdit = true;

        GumpWearableOffsetX = 0;
        GumpWearableOffsetY = 0;

        GumpInfoText = "Source image loaded. Use background cleanup / trim, then Save Edit.";
    }

    [RelayCommand]
    private void RemoveWhiteBackgroundFromGump()
    {
        if (SelectedGumpBitmap == null)
        {
            GumpInfoText = "Load a source image first.";
            return;
        }

        GumpFramePixels sourcePixels = ReadGumpPixels(SelectedGumpBitmap);
        GumpFramePixels cleanedPixels = RemoveNearWhiteBackground(sourcePixels, 235);
        cleanedPixels = RemoveThinArtifacts(cleanedPixels);
        SelectedGumpBitmap = BuildBitmapFromGumpPixels(cleanedPixels);

        HasUnsavedGumpEdit = true;
        GumpInfoText = "White background removed from preview.";
    }

    [RelayCommand]
    private void AutoTrimGumpPreview()
    {
        if (SelectedGumpBitmap == null)
        {
            GumpInfoText = "Load a source image first.";
            return;
        }

        GumpFramePixels sourcePixels = ReadGumpPixels(SelectedGumpBitmap);
        GumpFramePixels? trimmedPixels = TrimTransparentBorder(sourcePixels);

        if (trimmedPixels == null)
        {
            GumpInfoText = "Could not trim image. No visible pixels found.";
            return;
        }

        SelectedGumpBitmap = BuildBitmapFromGumpPixels(trimmedPixels);
        originalGumpEditBitmap = CloneGumpBitmap(SelectedGumpBitmap);

        GumpWearableOffsetX = 0;
        GumpWearableOffsetY = 0;
        HasUnsavedGumpEdit = true;

        GumpInfoText = "Transparent border trimmed from preview.";
    }

    private static WriteableBitmap LoadBitmapForGumpPreview(string path, int maxWidth, int maxHeight)
    {
        using FileStream stream = File.OpenRead(path);
        Bitmap source = new Bitmap(stream);

        int sourceWidth = source.PixelSize.Width;
        int sourceHeight = source.PixelSize.Height;

        double scale = Math.Min(
            maxWidth / (double)sourceWidth,
            maxHeight / (double)sourceHeight);

        if (scale > 1.0)
        {
            scale = 1.0;
        }

        int targetWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        int targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));

        Bitmap scaledSource = source.CreateScaledBitmap(
            new PixelSize(targetWidth, targetHeight),
            BitmapInterpolationMode.HighQuality);

        WriteableBitmap bitmap = new WriteableBitmap(
            new PixelSize(targetWidth, targetHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        scaledSource.CopyPixels(
            new PixelRect(0, 0, targetWidth, targetHeight),
            framebuffer.Address,
            framebuffer.RowBytes * framebuffer.Size.Height,
            framebuffer.RowBytes);

        return bitmap;
    }

    private static GumpFramePixels RemoveNearWhiteBackground(GumpFramePixels source, byte threshold)
    {
        byte[] output = (byte[])source.Pixels.Clone();
        bool[] visited = new bool[source.Width * source.Height];
        Queue<(int X, int Y)> queue = new();

        bool IsBackgroundPixel(int offset)
        {
            byte b = output[offset + 0];
            byte g = output[offset + 1];
            byte r = output[offset + 2];
            byte a = output[offset + 3];

            if (a == 0)
            {
                return true;
            }

            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            int brightness = (r + g + b) / 3;

            return
                brightness >= threshold ||
                (max >= 205 && max - min <= 35);
        }

        void EnqueueIfBackground(int x, int y)
        {
            if (x < 0 || y < 0 || x >= source.Width || y >= source.Height)
            {
                return;
            }

            int index = (y * source.Width) + x;
            if (visited[index])
            {
                return;
            }

            int offset = index * 4;
            if (!IsBackgroundPixel(offset))
            {
                return;
            }

            visited[index] = true;
            queue.Enqueue((x, y));
        }

        for (int x = 0; x < source.Width; x++)
        {
            EnqueueIfBackground(x, 0);
            EnqueueIfBackground(x, source.Height - 1);
        }

        for (int y = 0; y < source.Height; y++)
        {
            EnqueueIfBackground(0, y);
            EnqueueIfBackground(source.Width - 1, y);
        }

        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            int offset = ((y * source.Width) + x) * 4;

            output[offset + 0] = 0;
            output[offset + 1] = 0;
            output[offset + 2] = 0;
            output[offset + 3] = 0;

            EnqueueIfBackground(x - 1, y);
            EnqueueIfBackground(x + 1, y);
            EnqueueIfBackground(x, y - 1);
            EnqueueIfBackground(x, y + 1);
        }

        // Clean up white/gray fringe pixels left around the object.
        for (int i = 0; i + 3 < output.Length; i += 4)
        {
            byte b = output[i + 0];
            byte g = output[i + 1];
            byte r = output[i + 2];
            byte a = output[i + 3];

            if (a == 0)
            {
                continue;
            }

            int brightness = (r + g + b) / 3;
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));

            bool isWhiteFringe =
                brightness >= 225 ||
                (max >= 200 && max - min <= 28);

            if (!isWhiteFringe)
            {
                continue;
            }

            int newAlpha = 255 - ((brightness - 190) * 5);
            newAlpha = Math.Clamp(newAlpha, 0, 255);

            if (newAlpha <= 24)
            {
                output[i + 0] = 0;
                output[i + 1] = 0;
                output[i + 2] = 0;
                output[i + 3] = 0;
            }
            else if (newAlpha < a)
            {
                output[i + 3] = (byte)newAlpha;
            }
        }

        return new GumpFramePixels(source.Width, source.Height, output);
    }

    private static GumpFramePixels RemoveThinArtifacts(GumpFramePixels source)
    {
        byte[] output = (byte[])source.Pixels.Clone();

        for (int y = 0; y < source.Height; y++)
        {
            int runStart = -1;
            int runLength = 0;

            for (int x = 0; x <= source.Width; x++)
            {
                bool visible = false;

                if (x < source.Width)
                {
                    int offset = ((y * source.Width) + x) * 4;
                    visible = output[offset + 3] > 0;
                }

                if (visible)
                {
                    if (runStart < 0)
                    {
                        runStart = x;
                        runLength = 0;
                    }

                    runLength++;
                }
                else if (runStart >= 0)
                {
                    if (runLength <= 2)
                    {
                        for (int clearX = runStart; clearX < runStart + runLength; clearX++)
                        {
                            int clearOffset = ((y * source.Width) + clearX) * 4;
                            output[clearOffset + 0] = 0;
                            output[clearOffset + 1] = 0;
                            output[clearOffset + 2] = 0;
                            output[clearOffset + 3] = 0;
                        }
                    }

                    runStart = -1;
                    runLength = 0;
                }
            }
        }

        return new GumpFramePixels(source.Width, source.Height, output);
    }

    private static GumpFramePixels? TrimTransparentBorder(GumpFramePixels source)
    {
        int minX = source.Width;
        int minY = source.Height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                int offset = ((y * source.Width) + x) * 4;

                if (source.Pixels[offset + 3] == 0)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return null;
        }

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        byte[] output = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sourceOffset = (((minY + y) * source.Width) + (minX + x)) * 4;
                int targetOffset = ((y * width) + x) * 4;

                output[targetOffset + 0] = source.Pixels[sourceOffset + 0];
                output[targetOffset + 1] = source.Pixels[sourceOffset + 1];
                output[targetOffset + 2] = source.Pixels[sourceOffset + 2];
                output[targetOffset + 3] = source.Pixels[sourceOffset + 3];
            }
        }

        return new GumpFramePixels(width, height, output);
    }

    [RelayCommand]
    private void ApplyGumpPreviewScale()
    {
        if (SelectedGumpBitmap == null)
        {
            GumpInfoText = "Load a source image first.";
            return;
        }

        if (GumpWearableScalePercent <= 0)
        {
            GumpInfoText = "Scale must be greater than 0.";
            return;
        }

        GumpFramePixels sourcePixels = ReadGumpPixels(SelectedGumpBitmap);
        GumpFramePixels scaledPixels = ScaleGumpPixels(sourcePixels, GumpWearableScalePercent / 100.0);

        SelectedGumpBitmap = BuildBitmapFromGumpPixels(scaledPixels);
        originalGumpEditBitmap = CloneGumpBitmap(SelectedGumpBitmap);
        HasUnsavedGumpEdit = true;

        GumpWearableOffsetX = 0;
        GumpWearableOffsetY = 0;

        GumpInfoText = "Preview scaled to " + GumpWearableScalePercent + "%.";
    }

    private static GumpFramePixels ScaleGumpPixels(GumpFramePixels source, double scale)
    {
        int targetWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
        int targetHeight = Math.Max(1, (int)Math.Round(source.Height * scale));

        byte[] output = new byte[targetWidth * targetHeight * 4];

        double xRatio = source.Width / (double)targetWidth;
        double yRatio = source.Height / (double)targetHeight;

        for (int y = 0; y < targetHeight; y++)
        {
            double sourceY = (y + 0.5) * yRatio - 0.5;
            int y0 = Math.Clamp((int)Math.Floor(sourceY), 0, source.Height - 1);
            int y1 = Math.Clamp(y0 + 1, 0, source.Height - 1);
            double yLerp = sourceY - Math.Floor(sourceY);

            for (int x = 0; x < targetWidth; x++)
            {
                double sourceX = (x + 0.5) * xRatio - 0.5;
                int x0 = Math.Clamp((int)Math.Floor(sourceX), 0, source.Width - 1);
                int x1 = Math.Clamp(x0 + 1, 0, source.Width - 1);
                double xLerp = sourceX - Math.Floor(sourceX);

                int targetOffset = ((y * targetWidth) + x) * 4;

                for (int channel = 0; channel < 4; channel++)
                {
                    double c00 = source.Pixels[((y0 * source.Width) + x0) * 4 + channel];
                    double c10 = source.Pixels[((y0 * source.Width) + x1) * 4 + channel];
                    double c01 = source.Pixels[((y1 * source.Width) + x0) * 4 + channel];
                    double c11 = source.Pixels[((y1 * source.Width) + x1) * 4 + channel];

                    double top = c00 + ((c10 - c00) * xLerp);
                    double bottom = c01 + ((c11 - c01) * xLerp);
                    double value = top + ((bottom - top) * yLerp);

                    output[targetOffset + channel] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
                }
            }
        }

        return new GumpFramePixels(targetWidth, targetHeight, output);
    }

    [RelayCommand]
    private async Task LoadGumpOverlayImageAsync()
    {
        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            GumpInfoText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Load Gump Overlay Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                new FilePickerFileType("Image files")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" }
                }
                }
            });

        if (files.Count == 0)
        {
            GumpInfoText = "Overlay image load cancelled.";
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            GumpInfoText = "Selected overlay image does not have a local path.";
            return;
        }

        gumpOverlayBitmap = LoadBitmapAsWriteableBitmap(path);
        GumpOverlayImageName = Path.GetFileName(path);
        GumpInfoText = "Loaded overlay image: " + GumpOverlayImageName + ".";
    }

    [RelayCommand]
    private void ApplyOverlayToGump()
    {
        if (SelectedGump == null || !SelectedGump.IsValid)
        {
            GumpInfoText = "Select a valid gump first.";
            return;
        }

        if (SelectedGumpBitmap == null)
        {
            GumpInfoText = "Load a gump image first.";
            return;
        }

        if (gumpOverlayBitmap == null)
        {
            GumpInfoText = "Load an overlay image first.";
            return;
        }

        double opacity = Math.Clamp(GumpOverlayOpacityPercent / 100.0, 0.0, 1.0);

        GumpFramePixels basePixels = ReadGumpPixels(SelectedGumpBitmap);
        GumpFramePixels overlayPixels = ReadGumpPixels(gumpOverlayBitmap);

        GumpFramePixels blended = ApplyGumpOverlay(
            basePixels,
            overlayPixels,
            opacity,
            SelectedGumpOverlayBlendMode);

        SelectedGumpBitmap = BuildBitmapFromGumpPixels(blended);
        HasUnsavedGumpEdit = true;

        GumpInfoText =
            "Overlay applied using " +
            SelectedGumpOverlayBlendMode +
            " at " +
            GumpOverlayOpacityPercent.ToString("0") +
            "%. Click Save Edit to write it.";
    }

    private static WriteableBitmap LoadBitmapAsWriteableBitmap(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Bitmap source = new Bitmap(stream);

        WriteableBitmap bitmap = new WriteableBitmap(
            source.PixelSize,
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        source.CopyPixels(
            new PixelRect(0, 0, source.PixelSize.Width, source.PixelSize.Height),
            framebuffer.Address,
            framebuffer.RowBytes * framebuffer.Size.Height,
            framebuffer.RowBytes);

        return bitmap;
    }

    private static GumpFramePixels ApplyGumpOverlay(
        GumpFramePixels basePixels,
        GumpFramePixels overlayPixels,
        double opacity,
        GumpOverlayBlendMode blendMode)
    {
        byte[] output = (byte[])basePixels.Pixels.Clone();

        double scaleX = (double)basePixels.Width / overlayPixels.Width;
        double scaleY = (double)basePixels.Height / overlayPixels.Height;
        double scale = Math.Max(scaleX, scaleY);

        int scaledOverlayWidth = Math.Max(1, (int)Math.Round(overlayPixels.Width * scale));
        int scaledOverlayHeight = Math.Max(1, (int)Math.Round(overlayPixels.Height * scale));

        int cropX = (scaledOverlayWidth - basePixels.Width) / 2;
        int cropY = (scaledOverlayHeight - basePixels.Height) / 2;

        for (int y = 0; y < basePixels.Height; y++)
        {
            int scaledY = y + cropY;
            int overlayY = (int)(scaledY / scale);

            if (overlayY < 0)
            {
                overlayY = 0;
            }
            else if (overlayY >= overlayPixels.Height)
            {
                overlayY = overlayPixels.Height - 1;
            }

            for (int x = 0; x < basePixels.Width; x++)
            {
                int baseOffset = ((y * basePixels.Width) + x) * 4;

                byte baseAlpha = basePixels.Pixels[baseOffset + 3];
                if (baseAlpha == 0)
                {
                    continue;
                }

                int scaledX = x + cropX;
                int overlayX = (int)(scaledX / scale);

                if (overlayX < 0)
                {
                    overlayX = 0;
                }
                else if (overlayX >= overlayPixels.Width)
                {
                    overlayX = overlayPixels.Width - 1;
                }

                int overlayOffset = ((overlayY * overlayPixels.Width) + overlayX) * 4;

                byte overlayAlpha = overlayPixels.Pixels[overlayOffset + 3];
                if (overlayAlpha == 0)
                {
                    continue;
                }

                double blendAmount = opacity * (overlayAlpha / 255.0);

                byte baseBlue = basePixels.Pixels[baseOffset + 0];
                byte baseGreen = basePixels.Pixels[baseOffset + 1];
                byte baseRed = basePixels.Pixels[baseOffset + 2];

                byte overlayBlue = overlayPixels.Pixels[overlayOffset + 0];
                byte overlayGreen = overlayPixels.Pixels[overlayOffset + 1];
                byte overlayRed = overlayPixels.Pixels[overlayOffset + 2];

                output[baseOffset + 0] = BlendChannel(baseBlue, overlayBlue, blendAmount, blendMode);
                output[baseOffset + 1] = BlendChannel(baseGreen, overlayGreen, blendAmount, blendMode);
                output[baseOffset + 2] = BlendChannel(baseRed, overlayRed, blendAmount, blendMode);
                output[baseOffset + 3] = baseAlpha;
            }
        }

        return new GumpFramePixels(basePixels.Width, basePixels.Height, output);
    }

    private static byte BlendChannel(byte baseValue, byte overlayValue, double amount, GumpOverlayBlendMode blendMode)
    {
        double b = baseValue / 255.0;
        double o = overlayValue / 255.0;

        double blended = blendMode switch
        {
            GumpOverlayBlendMode.Multiply => b * o,

            GumpOverlayBlendMode.Screen => 1.0 - ((1.0 - b) * (1.0 - o)),

            GumpOverlayBlendMode.Overlay => b < 0.5
                ? 2.0 * b * o
                : 1.0 - (2.0 * (1.0 - b) * (1.0 - o)),

            GumpOverlayBlendMode.SoftLight => (1.0 - (2.0 * o)) * b * b + (2.0 * o * b),

            _ => o
        };

        double result = b + ((blended - b) * amount);
        return ClampToByte(result * 255.0);
    }
}