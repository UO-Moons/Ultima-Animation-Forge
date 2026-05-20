using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
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
    private sealed class VdFileNameAssignment
    {
        public int BodyId { get; set; }
        public string MobType { get; set; } = "MONSTER";
        public string Comment { get; set; } = string.Empty;
    }

    private enum ExportMode
    {
        CurrentFrame,
        CurrentDirection,
        AllDirections,
        AllActionsAndDirections
    }

    private enum ExportImageFormat
    {
        Png,
        Jpg,
        Bmp,
        Gif,
        SpriteSheetPng
    }

    private enum SpriteSheetMetadataFormat
    {
        None,
        Csv,
        Json
    }

    private enum VdExportTargetType
    {
        Native,
        Animal13,
        Monster22,
        Human35
    }

    private enum SharpenMode
    {
        Gaussian,
        Pixel
    }

    private enum VdExportRemapProfile
    {
        None,
        AnimalBasic,
        MonsterBasic,
        HumanBasic
    }

    private sealed class UopBinHeaderData
    {
        public uint Magic { get; set; }
        public uint Version { get; set; }
        public uint TotalSize { get; set; }
        public uint AnimationId { get; set; }
        public short BoundLeft { get; set; }
        public short BoundTop { get; set; }
        public short BoundRight { get; set; }
        public short BoundBottom { get; set; }
        public uint Unknown1 { get; set; }
        public uint Unknown2 { get; set; }
        public uint FrameCount { get; set; }
        public uint FrameIndexOffset { get; set; }
    }

    private sealed class UopFrameIndexData
    {
        public ushort Direction { get; set; }
        public ushort FrameNumber { get; set; }
        public short Left { get; set; }
        public short Top { get; set; }
        public short Right { get; set; }
        public short Bottom { get; set; }
        public uint FrameDataOffset { get; set; }
        public long StreamPosition { get; set; }
    }

    private sealed class UopFrameHeaderData
    {
        public short CenterX { get; set; }
        public short CenterY { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
    }

    private sealed class AvaloniaColorEntry
    {
        public byte Red { get; }
        public byte Green { get; }
        public byte Blue { get; }
        public byte Alpha { get; }

        public AvaloniaColorEntry(byte red, byte green, byte blue, byte alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }
    }

    private sealed class VdExportRemapDialogResult
    {
        public bool Confirmed { get; set; }
        public VdExportRemapProfile RemapProfile { get; set; } = VdExportRemapProfile.None;
    }

    private sealed class VdExportTargetDialogResult
    {
        public bool Confirmed { get; set; }
        public VdExportTargetType TargetType { get; set; } = VdExportTargetType.Native;
    }

    private sealed class ExportRequest
    {
        public ExportMode Mode { get; set; }
        public ExportImageFormat Format { get; set; }
        public bool GifLoop { get; set; }

        public bool ResizeEnabled { get; set; }
        public double ResizePercent { get; set; } = 100.0;
        public ResizeSamplerMode ResizeSampler { get; set; } = ResizeSamplerMode.Auto;

        public int SpriteSheetColumns { get; set; } = 8;
        public int SpriteSheetPadding { get; set; } = 2;
        public SpriteSheetMetadataFormat SpriteSheetMetadata { get; set; } = SpriteSheetMetadataFormat.None;

        public bool ApplyHue { get; set; }
        public HueDataService.HueEntry? SelectedHue { get; set; }

        public bool ApplySharpen { get; set; }
        public bool ApplyContrast { get; set; }
        public bool ApplyOutlineBoost { get; set; }

        public SharpenMode SharpenMode { get; set; } = SharpenMode.Gaussian;
        public float SharpenAmount { get; set; } = 0f;
        public float ContrastAmount { get; set; } = 0f;
        public float OutlineStrength { get; set; } = 0.35f;
    }

    public enum ResizeSamplerMode
    {
        Auto,
        NearestNeighbor,
        Lanczos3,
        Spline,
        BicubicSharper
    }

    private sealed class VdScaleDialogResult
    {
        public bool Confirmed { get; set; }
        public double ScaleFactor { get; set; } = 1.0;
        public ResizeSamplerMode ResizeSampler { get; set; } = ResizeSamplerMode.Auto;
    }

    private sealed class GifCanvasInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int DrawCenterX { get; set; }
        public int DrawCenterY { get; set; }
    }

    private sealed class GifFrameData
    {
        public required WriteableBitmap Bitmap { get; init; }
        public short CenterX { get; init; }
        public short CenterY { get; init; }
    }

    private sealed class SpriteSheetCellInfo
    {
        public int CellWidth { get; set; }
        public int CellHeight { get; set; }
        public int DrawCenterX { get; set; }
        public int DrawCenterY { get; set; }
    }

    private sealed class SpriteSheetFrameData
    {
        public required WriteableBitmap Bitmap { get; init; }
        public short CenterX { get; init; }
        public short CenterY { get; init; }
    }

    private sealed class SpriteSheetFrameMetadata
    {
        public int FrameIndex { get; set; }
        public int Action { get; set; }
        public int Direction { get; set; }

        public int CellX { get; set; }
        public int CellY { get; set; }
        public int CellWidth { get; set; }
        public int CellHeight { get; set; }

        public int DrawX { get; set; }
        public int DrawY { get; set; }

        public int SourceWidth { get; set; }
        public int SourceHeight { get; set; }

        public int CenterX { get; set; }
        public int CenterY { get; set; }
    }

    private sealed class SpriteSheetBuildResult
    {
        public required ImageSharpImage SheetImage { get; init; }
        public List<SpriteSheetFrameMetadata> Frames { get; init; } = new();
        public int SheetWidth { get; init; }
        public int SheetHeight { get; init; }
        public int Columns { get; init; }
        public int Rows { get; init; }
        public int Padding { get; init; }
        public int CellWidth { get; init; }
        public int CellHeight { get; init; }
    }

    private sealed class VdHueDialogResult
    {
        public bool Confirmed { get; set; }
        public bool ApplyHue { get; set; }
        public HueDataService.HueEntry? SelectedHue { get; set; }
    }

    private sealed class VdEnhancementDialogResult
    {
        public bool Confirmed { get; set; }
        public bool ApplySharpen { get; set; }
        public bool ApplyContrast { get; set; }
        public bool ApplyOutlineBoost { get; set; }

        public SharpenMode SharpenMode { get; set; } = SharpenMode.Gaussian;
        public float SharpenAmount { get; set; } = 0f;
        public float ContrastAmount { get; set; } = 0f;
        public float OutlineStrength { get; set; } = 0.35f;
    }

    private short GetAnimTypeForVdExportTarget(VdExportTargetType targetType, int discoveredActionCount)
    {
        return targetType switch
        {
            VdExportTargetType.Animal13 => 1,
            VdExportTargetType.Monster22 => 0,
            VdExportTargetType.Human35 => 2,
            _ => 4
        };
    }

    private short GetMulAnimTypeForBodyExport(int bodyId)
    {
        int groupCount = GetGroupCountForBody(bodyId);

        return groupCount switch
        {
            13 => 1,
            22 => 0,
            35 => 2,
            _ => 0
        };
    }

    private int GetMaxActionCountForVdExportTarget(VdExportTargetType targetType)
    {
        return targetType switch
        {
            VdExportTargetType.Animal13 => 13,
            VdExportTargetType.Monster22 => 22,
            VdExportTargetType.Human35 => 35,
            _ => 32
        };
    }

    private Dictionary<int, Dictionary<int, List<VdFrameData>>> BuildVdExportDataForTarget(Dictionary<int, Dictionary<int, List<VdFrameData>>> sourceData, VdExportTargetType targetType)
    {
        Dictionary<int, Dictionary<int, List<VdFrameData>>> result =
            new Dictionary<int, Dictionary<int, List<VdFrameData>>>();

        int maxActions = GetMaxActionCountForVdExportTarget(targetType);

        foreach (KeyValuePair<int, Dictionary<int, List<VdFrameData>>> actionPair in sourceData.OrderBy(x => x.Key))
        {
            if (actionPair.Key < 0 || actionPair.Key >= maxActions)
            {
                continue;
            }

            Dictionary<int, List<VdFrameData>> directionCopy = new Dictionary<int, List<VdFrameData>>();

            foreach (KeyValuePair<int, List<VdFrameData>> directionPair in actionPair.Value)
            {
                if (directionPair.Value == null || directionPair.Value.Count == 0)
                {
                    continue;
                }

                directionCopy[directionPair.Key] = new List<VdFrameData>(directionPair.Value);
            }

            if (directionCopy.Count > 0)
            {
                result[actionPair.Key] = directionCopy;
            }
        }

        return result;
    }

    private async Task ExportCurrentFrameAsync()
    {
        if (PreviewBitmap == null)
        {
            StatusText = "No current frame to export.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow is null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        ExportImageFormat format = ExportImageFormat.Png;

        string extension = GetFileExtension(format);

        string suggestedFileName =
            BuildExportBaseName(GetSelectedDirectionIndex()) +
            "-" + currentFrameIndex.ToString("D3") + "." + extension;

        FilePickerSaveOptions saveOptions = new FilePickerSaveOptions
        {
            Title = "Export Current Frame",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = extension,
            FileTypeChoices = new[]
            {
            new FilePickerFileType("Image File")
            {
                Patterns = new[] { "*.png", "*.jpg", "*.bmp", "*.gif" }
            }
        }
        };

        IStorageFile? file = await mainWindow.StorageProvider.SaveFilePickerAsync(saveOptions);

        if (file == null)
        {
            StatusText = "Export current frame cancelled.";
            return;
        }

        string? localPath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            StatusText = "Selected export file does not have a local path.";
            return;
        }

        format = GetFormatFromExtension(Path.GetExtension(localPath));

        ExportRequest? exportRequest = await ShowExportModeDialogAsync(mainWindow);

        if (exportRequest == null)
        {
            StatusText = "Export current frame cancelled.";
            return;
        }

        if (exportRequest.Format == ExportImageFormat.SpriteSheetPng)
        {
            StatusText = "Sprite sheet export is only available from Export Frames.";
            return;
        }

        VdHueDialogResult? hueDialogResult = await ShowVdHueDialogAsync(mainWindow);
        if (hueDialogResult == null || !hueDialogResult.Confirmed)
        {
            StatusText = "Export current frame cancelled.";
            return;
        }

        VdEnhancementDialogResult? enhancementDialogResult = await ShowVdEnhancementDialogAsync(mainWindow, hueDialogResult);
        if (enhancementDialogResult == null || !enhancementDialogResult.Confirmed)
        {
            StatusText = "Export current frame cancelled.";
            return;
        }

        exportRequest.ApplyHue = hueDialogResult.ApplyHue;
        exportRequest.SelectedHue = hueDialogResult.SelectedHue;
        exportRequest.ApplySharpen = enhancementDialogResult.ApplySharpen;
        exportRequest.ApplyContrast = enhancementDialogResult.ApplyContrast;
        exportRequest.ApplyOutlineBoost = enhancementDialogResult.ApplyOutlineBoost;
        exportRequest.SharpenMode = enhancementDialogResult.SharpenMode;
        exportRequest.SharpenAmount = enhancementDialogResult.SharpenAmount;
        exportRequest.ContrastAmount = enhancementDialogResult.ContrastAmount;
        exportRequest.OutlineStrength = enhancementDialogResult.OutlineStrength;

        try
        {
            if (format == ExportImageFormat.Gif)
            {
                List<VdFrameData> singleGifFrame = new List<VdFrameData>
            {
                new VdFrameData
                {
                    Bitmap = ApplyExportEffects(PreviewBitmap, exportRequest),
                    CenterX = 0,
                    CenterY = 0,
                    Width = (ushort)PreviewBitmap.PixelSize.Width,
                    Height = (ushort)PreviewBitmap.PixelSize.Height,
                    InitCoordsX = 0,
                    InitCoordsY = 0,
                    EndCoordsX = 0,
                    EndCoordsY = 0,
                    FrameId = 0,
                    FrameNumber = 0,
                    DataOffset = 0
                }
            };

                await SaveAnimatedGifAsync(
                    singleGifFrame,
                    localPath,
                    exportRequest.GifLoop,
                    exportRequest.ResizeEnabled,
                    exportRequest.ResizePercent,
                    exportRequest.ResizeSampler);
            }
            else
            {
                await SaveBitmapToFileAsync(
                    PreviewBitmap,
                    localPath,
                    format,
                    exportRequest);
            }
        }
        catch (UnauthorizedAccessException)
        {
            StatusText = "Access denied writing the selected file.";
            return;
        }
        catch (Exception exception)
        {
            StatusText = "Failed to export current frame: " + exception.Message;
            return;
        }

        StatusText = "Exported current frame to " + Path.GetFileName(localPath) + ".";
    }

    private async Task ExportFramesAsync()
    {
        if (ShowMulSlotView)
        {
            StatusText = "Frame export is only available for loaded animations, not free MUL body slots.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "No animation selected.";
            return;
        }

        int bodyId = GetEffectiveSelectedBodyId(SelectedAnimation);
        if (bodyId < 0)
        {
            StatusText = "Could not determine selected body ID.";
            return;
        }

        IAnimationDataSource? dataSource = GetDataSourceForEntry(SelectedAnimation);
        if (dataSource == null)
        {
            StatusText = "No animation data source is available for the selected entry.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        ExportRequest? exportRequest = await ShowExportModeDialogAsync(mainWindow);

        if (exportRequest == null)
        {
            StatusText = "Export cancelled.";
            return;
        }

        VdHueDialogResult? hueDialogResult = await ShowVdHueDialogAsync(mainWindow);
        if (hueDialogResult == null || !hueDialogResult.Confirmed)
        {
            StatusText = "Export cancelled.";
            return;
        }

        VdEnhancementDialogResult? enhancementDialogResult = await ShowVdEnhancementDialogAsync(mainWindow, hueDialogResult);
        if (enhancementDialogResult == null || !enhancementDialogResult.Confirmed)
        {
            StatusText = "Export cancelled.";
            return;
        }

        exportRequest.ApplyHue = hueDialogResult.ApplyHue;
        exportRequest.SelectedHue = hueDialogResult.SelectedHue;
        exportRequest.ApplySharpen = enhancementDialogResult.ApplySharpen;
        exportRequest.ApplyContrast = enhancementDialogResult.ApplyContrast;
        exportRequest.ApplyOutlineBoost = enhancementDialogResult.ApplyOutlineBoost;
        exportRequest.SharpenMode = enhancementDialogResult.SharpenMode;
        exportRequest.SharpenAmount = enhancementDialogResult.SharpenAmount;
        exportRequest.ContrastAmount = enhancementDialogResult.ContrastAmount;
        exportRequest.OutlineStrength = enhancementDialogResult.OutlineStrength;

        if (exportRequest.Mode == ExportMode.CurrentFrame)
        {
            await ExportCurrentFrameAsync();
            return;
        }

        FolderPickerOpenOptions folderOptions = new FolderPickerOpenOptions
        {
            Title = "Select Folder For Exported Frames",
            AllowMultiple = false
        };

        IReadOnlyList<IStorageFolder> folders =
            await mainWindow.StorageProvider.OpenFolderPickerAsync(folderOptions);

        if (folders.Count == 0)
        {
            StatusText = "Export frames cancelled.";
            return;
        }

        string? folderPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            StatusText = "Selected export folder does not have a local path.";
            return;
        }

        int totalExportCount = 0;
        int exportedDirections = 0;
        int exportedActions = 0;

        List<int> actionIndices = GetExportActionIndices(bodyId, exportRequest.Mode);
        List<int> directionIndices = GetExportDirectionIndices(exportRequest.Mode);

        foreach (int actionIndex in actionIndices)
        {
            bool actionExportedAnything = false;

            foreach (int directionIndex in directionIndices)
            {
                if (!dataSource.TryResolveAnimationBlock(
                        bodyId,
                        actionIndex,
                        directionIndex,
                        out ResolvedAnimationBlock resolvedBlock))
                {
                    continue;
                }

                byte[] blockData = dataSource.ReadAnimationBlock(resolvedBlock);
                if (blockData.Length == 0)
                {
                    continue;
                }

                string extension = GetFileExtension(exportRequest.Format);
                string baseName = BuildExportBaseNameForExport(
                    bodyId,
                    actionIndex,
                    directionIndex,
                    resolvedBlock.SourceFileName);

                if (exportRequest.Format == ExportImageFormat.SpriteSheetPng)
                {
                    List<VdFrameData> spriteFrames =
                        DecodeFramesForVdExport(blockData, resolvedBlock, directionIndex);

                    if (spriteFrames.Count == 0)
                    {
                        continue;
                    }

                    spriteFrames = ApplyExportEffectsToFrames(spriteFrames, exportRequest);

                    string spriteSheetPath = Path.Combine(folderPath, baseName + "-sheet.png");

                    await SaveSpriteSheetAsync(
                        spriteFrames,
                        spriteSheetPath,
                        actionIndex,
                        directionIndex,
                        exportRequest.SpriteSheetColumns,
                        exportRequest.SpriteSheetPadding,
                        exportRequest.ResizeEnabled,
                        exportRequest.ResizePercent,
                        exportRequest.ResizeSampler,
                        exportRequest.SpriteSheetMetadata);

                    totalExportCount++;
                }
                else if (exportRequest.Format == ExportImageFormat.Gif)
                {
                    List<VdFrameData> directionGifFrames =
                        DecodeFramesForVdExport(blockData, resolvedBlock, directionIndex);

                    if (directionGifFrames.Count == 0)
                    {
                        continue;
                    }

                    directionGifFrames = ApplyExportEffectsToFrames(directionGifFrames, exportRequest);

                    string gifPath = Path.Combine(folderPath, baseName + ".gif");

                    await SaveAnimatedGifAsync(
                        directionGifFrames,
                        gifPath,
                        exportRequest.GifLoop,
                        exportRequest.ResizeEnabled,
                        exportRequest.ResizePercent,
                        exportRequest.ResizeSampler);

                    totalExportCount++;
                }
                else
                {
                    List<WriteableBitmap> directionFrames =
                        DecodeFramesForExport(blockData, resolvedBlock, directionIndex);

                    if (directionFrames.Count == 0)
                    {
                        continue;
                    }

                    try
                    {
                        for (int frameIndex = 0; frameIndex < directionFrames.Count; frameIndex++)
                        {
                            string filePath = Path.Combine(
                                folderPath,
                                baseName + "-" + frameIndex.ToString("D3") + "." + extension);

                            await SaveBitmapToFileAsync(
                                directionFrames[frameIndex],
                                filePath,
                                exportRequest.Format,
                                exportRequest);

                            totalExportCount++;
                        }
                    }
                    finally
                    {
                        foreach (WriteableBitmap bitmap in directionFrames)
                        {
                            bitmap.Dispose();
                        }
                    }
                }

                exportedDirections++;
                actionExportedAnything = true;
            }

            if (actionExportedAnything)
            {
                exportedActions++;
            }
        }

        if (totalExportCount == 0)
        {
            StatusText = "No frames were exported for the selected animation.";
            return;
        }

        string exportUnit =
            exportRequest.Format == ExportImageFormat.Gif ||
            exportRequest.Format == ExportImageFormat.SpriteSheetPng
                ? "file(s)"
                : "frame(s)";

        StatusText =
            "Exported " + totalExportCount +
            " " + exportUnit +
            " across " + exportedDirections +
            " direction(s) and " + exportedActions +
            " action(s) to " + folderPath + ".";
    }

    private List<int> GetExportActionIndices(int bodyId, ExportMode exportMode)
    {
        List<int> actions = new List<int>();

        switch (exportMode)
        {
            case ExportMode.CurrentDirection:
            case ExportMode.AllDirections:
                actions.Add(GetSelectedActionIndex());
                break;

            case ExportMode.AllActionsAndDirections:
                IAnimationDataSource? dataSource = GetDataSourceForEntry(SelectedAnimation);

                if (dataSource != null)
                {
                    actions.AddRange(dataSource.GetAvailableActionIndices(bodyId));
                }

                if (actions.Count == 0)
                {
                    int fallbackCount = GetGroupCountForBody(bodyId);

                    for (int actionIndex = 0; actionIndex < fallbackCount; actionIndex++)
                    {
                        actions.Add(actionIndex);
                    }
                }
                break;

            default:
                actions.Add(GetSelectedActionIndex());
                break;
        }

        actions = actions.Distinct().OrderBy(value => value).ToList();
        return actions;
    }

    private List<int> GetExportDirectionIndices(ExportMode exportMode)
    {
        switch (exportMode)
        {
            case ExportMode.CurrentDirection:
                return new List<int> { GetSelectedDirectionIndex() };

            case ExportMode.AllDirections:
            case ExportMode.AllActionsAndDirections:
                return new List<int> { 0, 1, 2, 3, 4 };

            default:
                return new List<int> { GetSelectedDirectionIndex() };
        }
    }

    private string BuildExportBaseNameForExport(int bodyId, int actionIndex, int directionIndex, string sourceFileName)
    {
        string sourceName =
            sourceFileName.Replace(".mul", "", StringComparison.OrdinalIgnoreCase)
                          .Replace(".uop", "", StringComparison.OrdinalIgnoreCase);

        return SanitizeFileName(
            "body-" + bodyId.ToString() +
            "-action-" + actionIndex.ToString() +
            "-dir-" + directionIndex.ToString() +
            "-" + sourceName);
    }

    private WriteableBitmap ResizeBitmapForExport(WriteableBitmap sourceBitmap, bool resizeEnabled, double resizePercent, ResizeSamplerMode resizeSampler)
    {
        if (!resizeEnabled)
        {
            return sourceBitmap;
        }

        double scaleFactor = resizePercent / 100.0;

        if (Math.Abs(scaleFactor - 1.0) < 0.0001)
        {
            return sourceBitmap;
        }

        return ScaleBitmapWithSampler(sourceBitmap, scaleFactor, resizeSampler);
    }

    private List<WriteableBitmap> DecodeFramesForExport(byte[] blockData, ResolvedAnimationBlock resolvedBlock, int directionIndex)
    {
        List<WriteableBitmap> exportFrames = new List<WriteableBitmap>();

        if (blockData == null || blockData.Length == 0)
        {
            return exportFrames;
        }

        if (resolvedBlock.IsUop)
        {
            if (blockData.Length < 40)
            {
                return exportFrames;
            }

            UopBinHeaderData header = ReadUopBinHeader(blockData);

            if (header.Magic != 1431260481U || header.FrameCount == 0)
            {
                return exportFrames;
            }

            List<UopFrameIndexData> frameIndexes = ReadUopFrameIndexes(blockData, header);

            if (frameIndexes.Count == 0)
            {
                return exportFrames;
            }

            List<UopFrameIndexData> directionFrames = GetUopDirectionFrames(frameIndexes, directionIndex);

            for (int index = 0; index < directionFrames.Count; index++)
            {
                WriteableBitmap? frameBitmap = DecodeUopFrame(blockData, frameIndexes, directionFrames[index]);

                if (frameBitmap != null)
                {
                    exportFrames.Add(frameBitmap);
                }
            }

            return exportFrames;
        }

        if (blockData.Length < 516)
        {
            return exportFrames;
        }

        int dataStart = 512;
        uint frameCount = BitConverter.ToUInt32(blockData, dataStart);

        if (frameCount == 0 || frameCount > 1000)
        {
            return exportFrames;
        }

        int frameTableStart = dataStart + 4;
        uint[] frameOffsets = new uint[frameCount];

        for (int index = 0; index < frameCount; index++)
        {
            int offsetPosition = frameTableStart + (index * 4);

            if (offsetPosition + 4 > blockData.Length)
            {
                return exportFrames;
            }

            frameOffsets[index] = BitConverter.ToUInt32(blockData, offsetPosition);
        }

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            if (frameOffsets[frameIndex] == 0)
            {
                continue;
            }

            int frameStart = dataStart + (int)frameOffsets[frameIndex];

            if (frameStart < 0 || frameStart + 8 > blockData.Length)
            {
                continue;
            }

            WriteableBitmap? frameBitmap = DecodeFrameToBitmap(blockData, frameStart);

            if (frameBitmap != null)
            {
                exportFrames.Add(frameBitmap);
            }
        }

        return exportFrames;
    }

    private string BuildExportBaseName(int? directionIndex = null)
    {
        if (ShowMulSlotView && SelectedMulSlot != null)
        {
            string slotBaseName =
                SelectedMulSlot.FileName.Replace(".idx", "", StringComparison.OrdinalIgnoreCase) +
                "-body-" + SelectedMulSlot.BodyIndex.ToString();

            if (directionIndex.HasValue)
            {
                slotBaseName += "-dir-" + directionIndex.Value.ToString();
            }

            return SanitizeFileName(slotBaseName);
        }

        if (SelectedAnimation != null)
        {
            string sourceName =
                SelectedAnimation.SourceFile.Replace(".mul", "", StringComparison.OrdinalIgnoreCase)
                                            .Replace(".uop", "", StringComparison.OrdinalIgnoreCase);

            string animationBaseName =
                "body-" + SelectedAnimation.BodyId.ToString() +
                "-action-" + SelectedAnimation.ActionId.ToString();

            if (directionIndex.HasValue)
            {
                animationBaseName += "-dir-" + directionIndex.Value.ToString();
            }

            animationBaseName += "-" + sourceName;

            return SanitizeFileName(animationBaseName);
        }

        return "animation-export";
    }

    private string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "animation-export";
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '_');
        }

        return value;
    }

    private ExportImageFormat GetFormatFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" => ExportImageFormat.Jpg,
            ".jpeg" => ExportImageFormat.Jpg,
            ".bmp" => ExportImageFormat.Bmp,
            ".gif" => ExportImageFormat.Gif,
            _ => ExportImageFormat.Png
        };
    }

    private string GetFileExtension(ExportImageFormat format)
    {
        return format switch
        {
            ExportImageFormat.Png => "png",
            ExportImageFormat.Jpg => "jpg",
            ExportImageFormat.Bmp => "bmp",
            ExportImageFormat.Gif => "gif",
            ExportImageFormat.SpriteSheetPng => "png",
            _ => "png"
        };
    }

    private IImageEncoder GetEncoder(ExportImageFormat format)
    {
        return format switch
        {
            ExportImageFormat.Jpg => new JpegEncoder(),
            ExportImageFormat.Bmp => new BmpEncoder(),
            ExportImageFormat.Gif => new GifEncoder(),
            _ => new PngEncoder()
        };
    }

    private ImageSharpImage ConvertWriteableBitmapToImageSharp(WriteableBitmap bitmap)
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
                sourceBytes, y * sourceRowBytes,
                packedBytes, y * targetRowBytes,
                targetRowBytes);
        }

        return SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(packedBytes, width, height);
    }

    private async Task SaveBitmapToFileAsync(WriteableBitmap bitmap, string filePath, ExportImageFormat format)
    {
        await SaveBitmapToFileAsync(bitmap, filePath, format, false, 100.0, ResizeSamplerMode.Auto);
    }

    private async Task SaveBitmapToFileAsync(WriteableBitmap bitmap, string filePath, ExportImageFormat format, bool resizeEnabled, double resizePercent, ResizeSamplerMode resizeSampler)
    {
        WriteableBitmap exportBitmap = ResizeBitmapForExport(bitmap, resizeEnabled, resizePercent, resizeSampler);

        using ImageSharpImage image = ConvertWriteableBitmapToImageSharp(exportBitmap);
        IImageEncoder encoder = GetEncoder(format);
        await image.SaveAsync(filePath, encoder);
    }

    private async Task SaveBitmapToFileAsync(WriteableBitmap bitmap, string filePath, ExportImageFormat format, ExportRequest exportRequest)
    {
        WriteableBitmap processedBitmap = ApplyExportEffects(bitmap, exportRequest);
        WriteableBitmap exportBitmap = ResizeBitmapForExport(
            processedBitmap,
            exportRequest.ResizeEnabled,
            exportRequest.ResizePercent,
            exportRequest.ResizeSampler);

        using ImageSharpImage image = ConvertWriteableBitmapToImageSharp(exportBitmap);
        IImageEncoder encoder = GetEncoder(format);
        await image.SaveAsync(filePath, encoder);
    }

    private async Task SaveAnimatedGifAsync(List<VdFrameData> vdFrames, string filePath, bool loop, bool resizeEnabled = false, double resizePercent = 100.0, ResizeSamplerMode resizeSampler = ResizeSamplerMode.Auto)
    {
        if (vdFrames == null || vdFrames.Count == 0)
        {
            return;
        }

        int frameCountToUse = Math.Min(vdFrames.Count, MaxGifFrames);
        List<VdFrameData> framesToUse = vdFrames.Take(frameCountToUse).ToList();

        int gifDelay = Math.Max(1, (int)Math.Round(100.0 / Math.Max(1.0, PlaybackSpeed)));

        List<GifFrameData> preparedFrames = BuildGifFramesFromVdFrames(
            framesToUse,
            resizeEnabled,
            resizePercent,
            resizeSampler);

        if (preparedFrames.Count == 0)
        {
            return;
        }

        GifCanvasInfo canvasInfo = CalculateGifCanvas(preparedFrames);

        using ImageSharpImage gifImage = BuildGifCanvasFrame(preparedFrames[0], canvasInfo, resizeSampler);

        gifImage.Metadata.GetGifMetadata().RepeatCount = (ushort)(loop ? 0 : 1);
        ConfigureGifFrameMetadata(gifImage.Frames.RootFrame, gifDelay);

        for (int index = 1; index < preparedFrames.Count; index++)
        {
            using ImageSharpImage nextFrameImage =
                BuildGifCanvasFrame(preparedFrames[index], canvasInfo, resizeSampler);

            ConfigureGifFrameMetadata(nextFrameImage.Frames.RootFrame, gifDelay);
            gifImage.Frames.AddFrame(nextFrameImage.Frames.RootFrame);
        }

        await gifImage.SaveAsync(filePath, new GifEncoder());
    }

    private ImageSharpImage PrepareFrameForGif(WriteableBitmap bitmap, ResizeSamplerMode resizeSampler = ResizeSamplerMode.Auto)
    {
        ImageSharpImage image = ConvertWriteableBitmapToImageSharp(bitmap);

        if (image.Width > MaxGifWidth || image.Height > MaxGifHeight)
        {
            double widthScale = (double)MaxGifWidth / image.Width;
            double heightScale = (double)MaxGifHeight / image.Height;
            double scaleFactor = Math.Min(widthScale, heightScale);

            IResampler sampler = GetImageSharpResampler(resizeSampler, scaleFactor);

            image.Mutate(context =>
            {
                context.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new SixLabors.ImageSharp.Size(MaxGifWidth, MaxGifHeight),
                    Sampler = sampler
                });
            });
        }

        return image;
    }

    private async Task ExportVdAsync()
    {
        if (ShowMulSlotView)
        {
            StatusText = "VD export is only available for loaded animations.";
            return;
        }

        if (SelectedAnimation == null)
        {
            StatusText = "No animation selected.";
            return;
        }

        int bodyId = GetEffectiveSelectedBodyId(SelectedAnimation);
        if (bodyId < 0)
        {
            StatusText = "Could not determine selected body ID.";
            return;
        }

        IAnimationDataSource? dataSource = GetDataSourceForEntry(SelectedAnimation);
        if (dataSource == null)
        {
            StatusText = "No animation data source is available for the selected entry.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        VdScaleDialogResult? scaleDialogResult = await ShowVdScaleDialogAsync(mainWindow);

        if (scaleDialogResult == null || !scaleDialogResult.Confirmed)
        {
            StatusText = "VD export cancelled.";
            return;
        }

        double scaleFactor = scaleDialogResult.ScaleFactor;

        ResizeSamplerMode resizeSampler = scaleDialogResult.ResizeSampler;

        VdHueDialogResult? hueDialogResult = await ShowVdHueDialogAsync(mainWindow);

        if (hueDialogResult == null || !hueDialogResult.Confirmed)
        {
            StatusText = "VD export cancelled.";
            return;
        }

        VdEnhancementDialogResult? enhancementDialogResult = await ShowVdEnhancementDialogAsync(mainWindow, hueDialogResult);

        if (enhancementDialogResult == null || !enhancementDialogResult.Confirmed)
        {
            StatusText = "VD export cancelled.";
            return;
        }

        bool isUopExport = string.Equals(SelectedAnimation.SourceMode, "UOP", StringComparison.OrdinalIgnoreCase);

        VdExportTargetType vdExportTargetType = VdExportTargetType.Native;
        VdExportRemapProfile vdExportRemapProfile = VdExportRemapProfile.None;

        if (isUopExport)
        {
            VdExportTargetDialogResult? vdTargetDialogResult = await ShowVdExportTargetDialogAsync(mainWindow);

            if (vdTargetDialogResult == null || !vdTargetDialogResult.Confirmed)
            {
                StatusText = "VD export cancelled.";
                return;
            }

            vdExportTargetType = vdTargetDialogResult.TargetType;

            VdExportRemapDialogResult? vdRemapDialogResult =
                await ShowVdExportRemapDialogAsync(mainWindow, vdExportTargetType);

            if (vdRemapDialogResult == null || !vdRemapDialogResult.Confirmed)
            {
                StatusText = "VD export cancelled.";
                return;
            }

            vdExportRemapProfile = vdRemapDialogResult.RemapProfile;
        }

        string suggestedFileName = "body-" + bodyId.ToString() + ".vd";

        FilePickerSaveOptions saveOptions = new FilePickerSaveOptions
        {
            Title = "Export VD Animation",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "vd",
            FileTypeChoices = new[]
            {
            new FilePickerFileType("VD Animation")
            {
                Patterns = new[] { "*.vd" }
            }
        }
        };

        IStorageFile? file = await mainWindow.StorageProvider.SaveFilePickerAsync(saveOptions);
        if (file == null)
        {
            StatusText = "VD export cancelled.";
            return;
        }

        string? filePath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            StatusText = "Selected VD export file does not have a local path.";
            return;
        }

        int vdActionCount;

        if (isUopExport)
        {
            vdActionCount = vdExportTargetType switch
            {
                VdExportTargetType.Animal13 => 13,
                VdExportTargetType.Monster22 => 22,
                VdExportTargetType.Human35 => 35,
                _ => 32
            };
        }
        else
        {
            vdActionCount = dataSource.GetGroupCountForBody(bodyId);
        }

        if (vdActionCount != 13 && vdActionCount != 22 && vdActionCount != 32 && vdActionCount != 35)
        {
            StatusText = "Unsupported VD action count: " + vdActionCount + ".";
            return;
        }

        Dictionary<int, Dictionary<int, List<VdFrameData>>> exportData = new();

        for (int actionIndex = 0; actionIndex < vdActionCount; actionIndex++)
        {
            Dictionary<int, List<VdFrameData>> directionMap = new Dictionary<int, List<VdFrameData>>();

            for (int directionIndex = 0; directionIndex < 5; directionIndex++)
            {
                if (!dataSource.TryResolveAnimationBlock(bodyId, actionIndex, directionIndex, out ResolvedAnimationBlock resolvedBlock))
                {
                    continue;
                }

                byte[] blockData = dataSource.ReadAnimationBlock(resolvedBlock);
                if (blockData.Length == 0)
                {
                    continue;
                }

                List<VdFrameData> vdFrames = DecodeFramesForVdExport(blockData, resolvedBlock, directionIndex);

                if (vdFrames.Count > 0)
                {
                    List<VdFrameData> scaledFrames = ScaleVdFrames(vdFrames, scaleFactor, resizeSampler);
                    List<VdFrameData> enhancedFrames = ApplyEnhancementsToVdFrames(scaledFrames, enhancementDialogResult);
                    List<VdFrameData> finalFrames = enhancedFrames;

                    if (hueDialogResult.ApplyHue && hueDialogResult.SelectedHue != null)
                    {
                        finalFrames = ApplyHueToVdFrames(enhancedFrames, hueDialogResult.SelectedHue);
                    }

                    if (!directionMap.TryGetValue(directionIndex, out var existingFrames))
                    {
                        directionMap[directionIndex] = new List<VdFrameData>(finalFrames);
                    }
                    else
                    {
                        // Keep the better animation (usually more frames = better)
                        if (finalFrames.Count > existingFrames.Count)
                        {
                            directionMap[directionIndex] = new List<VdFrameData>(finalFrames);
                        }
                    }
                }
            }

            if (directionMap.Count > 0)
            {
                exportData[actionIndex] = directionMap;
            }
        }

        if (exportData.Count == 0)
        {
            StatusText = "No animation data available to export as VD.";
            return;
        }

        Dictionary<int, Dictionary<int, List<VdFrameData>>> finalExportData;
        short animType;

        int discoveredActionCount = vdActionCount;

        if (isUopExport)
        {
            finalExportData = BuildVdExportDataForTargetWithRemap(
                exportData,
                vdExportTargetType,
                vdExportRemapProfile);

            if (finalExportData.Count == 0)
            {
                StatusText = "No animation data was available for the selected VD export target type.";
                return;
            }

            animType = GetAnimTypeForVdExportTarget(vdExportTargetType, discoveredActionCount);
        }
        else
        {
            finalExportData = exportData;

            if (finalExportData.Count == 0)
            {
                StatusText = "No animation data was available to export as VD.";
                return;
            }

            animType = GetMulAnimTypeForBodyExport(bodyId);
        }

        try
        {
            VdExportService.ExportBodyAnimation(filePath, animType, finalExportData);

            string hueText =
                hueDialogResult.ApplyHue && hueDialogResult.SelectedHue != null
                    ? " with hue " + hueDialogResult.SelectedHue.HueId
                    : string.Empty;

            List<string> enhancementNames = new List<string>();

            if (enhancementDialogResult.ApplySharpen)
            {
                enhancementNames.Add("sharpen");
            }

            if (enhancementDialogResult.ApplyContrast)
            {
                enhancementNames.Add("contrast");
            }

            if (enhancementDialogResult.ApplyOutlineBoost)
            {
                enhancementNames.Add("outline " + enhancementDialogResult.OutlineStrength.ToString("0.00"));
            }

            string enhancementText = enhancementNames.Count > 0
                ? " with " + string.Join(", ", enhancementNames)
                : string.Empty;

            string exportModeText;

            if (isUopExport)
            {
                string targetLabel = vdExportTargetType switch
                {
                    VdExportTargetType.Animal13 => "MUL Animal (13 actions)",
                    VdExportTargetType.Monster22 => "MUL Monster (22 actions)",
                    VdExportTargetType.Human35 => "MUL Human / Equipment (35 actions)",
                    _ => "Native UOP Creature (32 actions)"
                };

                string remapLabel = GetVdExportRemapLabel(vdExportRemapProfile);
                exportModeText = " as " + targetLabel + " using remap " + remapLabel;
            }
            else
            {
                exportModeText = " as native MUL-compatible VD";
            }

            StatusText =
                "Exported VD animation to " + Path.GetFileName(filePath) +
                exportModeText +
                " at " + (scaleFactor * 100.0).ToString("0") + "% scale" +
                enhancementText +
                hueText + ".";
        }
        catch (Exception exception)
        {
            StatusText = "Failed to export VD: " + exception.Message;
        }
    }

    private List<VdFrameData> DecodeFramesForVdExport(byte[] blockData, ResolvedAnimationBlock resolvedBlock, int directionIndex)
    {
        List<VdFrameData> frames = new List<VdFrameData>();

        if (blockData == null || blockData.Length == 0)
        {
            return frames;
        }

        if (resolvedBlock.IsUop)
        {
            if (blockData.Length < 40)
            {
                return frames;
            }

            UopBinHeaderData header = ReadUopBinHeader(blockData);
            if (header.Magic != 1431260481U || header.FrameCount == 0)
            {
                return frames;
            }

            List<UopFrameIndexData> frameIndexes = ReadUopFrameIndexes(blockData, header);
            if (frameIndexes.Count == 0)
            {
                return frames;
            }

            List<UopFrameIndexData> directionFrames = GetUopDirectionFrames(frameIndexes, directionIndex);

            if (directionFrames.Count == 0)
            {
                return frames;
            }

            foreach (UopFrameIndexData frameIndex in directionFrames)
            {
                int frameDataAbsoluteOffset = (int)(frameIndex.StreamPosition + frameIndex.FrameDataOffset);
                if (frameDataAbsoluteOffset < 0 || frameDataAbsoluteOffset + 512 + 8 > blockData.Length)
                {
                    continue;
                }

                List<AvaloniaColorEntry> palette = ReadUopPalette(blockData, frameDataAbsoluteOffset);
                List<ushort> palette565 = ConvertUopPaletteTo1555(palette);

                UopFrameHeaderData frameHeader = ReadUopFrameHeader(blockData, frameDataAbsoluteOffset + 512);
                WriteableBitmap? bitmap = DecodeUopFrame(blockData, frameIndexes, frameIndex);

                if (bitmap == null)
                {
                    continue;
                }

                frames.Add(new VdFrameData
                {
                    Bitmap = bitmap,
                    Palette565 = palette565,
                    CenterX = frameHeader.CenterX,
                    CenterY = frameHeader.CenterY,
                    Width = frameHeader.Width,
                    Height = frameHeader.Height,
                    InitCoordsX = frameIndex.Left,
                    InitCoordsY = frameIndex.Top,
                    EndCoordsX = frameIndex.Right,
                    EndCoordsY = frameIndex.Bottom,
                    FrameId = frameIndex.FrameNumber,
                    FrameNumber = frameIndex.FrameNumber,
                    DataOffset = frameIndex.FrameDataOffset
                });
            }

            return frames;
        }

        if (blockData.Length < 516)
        {
            return frames;
        }

        int dataStart = 512;
        uint frameCount = BitConverter.ToUInt32(blockData, dataStart);

        if (frameCount == 0 || frameCount > 1000)
        {
            return frames;
        }

        ushort[] paletteRaw = new ushort[256];
        for (int index = 0; index < 256; index++)
        {
            paletteRaw[index] = (ushort)(BitConverter.ToUInt16(blockData, index * 2) ^ 0x8000);

            if (paletteRaw[index] == 0x8000 && index != 0)
            {
                paletteRaw[index] = 0x8001;
            }
        }

        int frameTableStart = dataStart + 4;
        uint[] frameOffsets = new uint[frameCount];

        for (int index = 0; index < frameCount; index++)
        {
            int offsetPosition = frameTableStart + (index * 4);
            if (offsetPosition + 4 > blockData.Length)
            {
                return frames;
            }

            frameOffsets[index] = BitConverter.ToUInt32(blockData, offsetPosition);
        }

        for (ushort frameNumber = 0; frameNumber < frameCount; frameNumber++)
        {
            if (frameOffsets[frameNumber] == 0)
            {
                continue;
            }

            int frameStart = dataStart + (int)frameOffsets[frameNumber];
            if (frameStart < 0 || frameStart + 8 > blockData.Length)
            {
                continue;
            }

            short centerX = BitConverter.ToInt16(blockData, frameStart + 0);
            short centerY = BitConverter.ToInt16(blockData, frameStart + 2);
            ushort width = (ushort)BitConverter.ToInt16(blockData, frameStart + 4);
            ushort height = (ushort)BitConverter.ToInt16(blockData, frameStart + 6);

            WriteableBitmap? bitmap = DecodeFrameToBitmap(blockData, frameStart);
            if (bitmap != null)
            {
                frames.Add(new VdFrameData
                {
                    Bitmap = bitmap,
                    Palette565 = new List<ushort>(paletteRaw),
                    CenterX = centerX,
                    CenterY = centerY,
                    Width = width,
                    Height = height,
                    InitCoordsX = 0,
                    InitCoordsY = 0,
                    EndCoordsX = 0,
                    EndCoordsY = 0,
                    FrameId = frameNumber,
                    FrameNumber = frameNumber,
                    DataOffset = frameOffsets[frameNumber]
                });
            }
        }

        return frames;
    }

    private List<VdFrameData> ScaleVdFrames(List<VdFrameData> sourceFrames, double scaleFactor, ResizeSamplerMode resizeSampler)
    {
        List<VdFrameData> result = new List<VdFrameData>();

        if (sourceFrames == null || sourceFrames.Count == 0)
        {
            return result;
        }

        if (scaleFactor <= 0)
        {
            scaleFactor = 1.0;
        }

        if (Math.Abs(scaleFactor - 1.0) < 0.0001)
        {
            result.AddRange(sourceFrames);
            return result;
        }

        foreach (VdFrameData frame in sourceFrames)
        {
            WriteableBitmap scaledBitmap = ScaleBitmapWithSampler(frame.Bitmap, scaleFactor, resizeSampler);

            result.Add(new VdFrameData
            {
                Bitmap = scaledBitmap,

                // Rebuild palette after scale so UO gets a clean 256-color palette.
                Palette565 = null,

                CenterX = ClampToShort((int)Math.Round(frame.CenterX * scaleFactor)),
                CenterY = ClampToShort((int)Math.Round(frame.CenterY * scaleFactor)),

                Width = (ushort)scaledBitmap.PixelSize.Width,
                Height = (ushort)scaledBitmap.PixelSize.Height,

                InitCoordsX = ClampToShort((int)Math.Round(frame.InitCoordsX * scaleFactor)),
                InitCoordsY = ClampToShort((int)Math.Round(frame.InitCoordsY * scaleFactor)),
                EndCoordsX = ClampToShort((int)Math.Round(frame.EndCoordsX * scaleFactor)),
                EndCoordsY = ClampToShort((int)Math.Round(frame.EndCoordsY * scaleFactor)),

                FrameId = frame.FrameId,
                FrameNumber = frame.FrameNumber,
                DataOffset = frame.DataOffset
            });
        }

        return result;
    }

    private WriteableBitmap ScaleBitmapWithSampler(WriteableBitmap sourceBitmap, double scaleFactor, ResizeSamplerMode resizeSampler)
    {
        using ImageSharpImage sourceImage = ConvertWriteableBitmapToImageSharp(sourceBitmap);

        int newWidth = Math.Max(1, (int)Math.Round(sourceImage.Width * scaleFactor));
        int newHeight = Math.Max(1, (int)Math.Round(sourceImage.Height * scaleFactor));

        IResampler sampler = GetImageSharpResampler(resizeSampler, scaleFactor);

        sourceImage.Mutate(context =>
        {
            context.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(newWidth, newHeight),
                Sampler = sampler,
                Mode = ResizeMode.Stretch
            });
        });

        if (resizeSampler == ResizeSamplerMode.BicubicSharper && scaleFactor < 1.0)
        {
            sourceImage.Mutate(context =>
            {
                context.GaussianSharpen(0.35f);
            });
        }

        return ConvertImageSharpToWriteableBitmap(sourceImage);
    }

    private IResampler GetImageSharpResampler(ResizeSamplerMode mode, double scaleFactor)
    {
        return mode switch
        {
            ResizeSamplerMode.NearestNeighbor => KnownResamplers.NearestNeighbor,
            ResizeSamplerMode.Lanczos3 => KnownResamplers.Lanczos3,
            ResizeSamplerMode.Spline => KnownResamplers.Spline,
            ResizeSamplerMode.BicubicSharper => KnownResamplers.Bicubic,

            _ => scaleFactor < 1.0
                ? KnownResamplers.Bicubic
                : KnownResamplers.Spline
        };
    }

    private WriteableBitmap ConvertImageSharpToWriteableBitmap(ImageSharpImage image)
    {
        byte[] pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);

        // Avalonia bitmap is created as Premul, so premultiply BGRA bytes first.
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte b = pixels[i + 0];
            byte g = pixels[i + 1];
            byte r = pixels[i + 2];
            byte a = pixels[i + 3];

            if (a == 0)
            {
                pixels[i + 0] = 0;
                pixels[i + 1] = 0;
                pixels[i + 2] = 0;
                pixels[i + 3] = 0;
                continue;
            }

            if (a < 255)
            {
                pixels[i + 0] = (byte)((b * a + 127) / 255);
                pixels[i + 1] = (byte)((g * a + 127) / 255);
                pixels[i + 2] = (byte)((r * a + 127) / 255);
                pixels[i + 3] = a;
            }
        }

        WriteableBitmap bitmap = new WriteableBitmap(
            new Avalonia.PixelSize(image.Width, image.Height),
            new Avalonia.Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            Avalonia.Platform.AlphaFormat.Premul);

        using (ILockedFramebuffer framebuffer = bitmap.Lock())
        {
            Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
        }

        return bitmap;
    }

    private short ClampToShort(int value)
    {
        if (value < short.MinValue)
        {
            return short.MinValue;
        }

        if (value > short.MaxValue)
        {
            return short.MaxValue;
        }

        return (short)value;
    }

    private List<VdFrameData> ApplyHueToVdFrames(List<VdFrameData> sourceFrames, HueDataService.HueEntry hueEntry)
    {
        List<VdFrameData> result = new List<VdFrameData>();

        if (sourceFrames == null || sourceFrames.Count == 0 || hueEntry == null || hueEntry.Colors.Count == 0)
        {
            return result;
        }

        foreach (VdFrameData frame in sourceFrames)
        {
            WriteableBitmap recoloredBitmap = ApplyHueToBitmap(frame.Bitmap, hueEntry);

            result.Add(new VdFrameData
            {
                Bitmap = recoloredBitmap,
                Palette565 = null,
                CenterX = frame.CenterX,
                CenterY = frame.CenterY,
                Width = (ushort)recoloredBitmap.PixelSize.Width,
                Height = (ushort)recoloredBitmap.PixelSize.Height,
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

    private WriteableBitmap ApplyHueToBitmap(WriteableBitmap sourceBitmap, HueDataService.HueEntry hueEntry)
    {
        FramePixels pixels = ReadWriteableBitmapPixels(sourceBitmap);
        byte[] outputPixels = new byte[pixels.Pixels.Length];

        Buffer.BlockCopy(pixels.Pixels, 0, outputPixels, 0, pixels.Pixels.Length);

        for (int y = 0; y < pixels.Height; y++)
        {
            for (int x = 0; x < pixels.Width; x++)
            {
                int offset = ((y * pixels.Width) + x) * 4;

                byte b = outputPixels[offset + 0];
                byte g = outputPixels[offset + 1];
                byte r = outputPixels[offset + 2];
                byte a = outputPixels[offset + 3];

                if (a == 0)
                {
                    continue;
                }

                int brightness = (r + g + b) / 3;
                int hueIndex = (int)Math.Round((brightness / 255.0) * 31.0);

                if (hueIndex < 0)
                {
                    hueIndex = 0;
                }
                else if (hueIndex > 31)
                {
                    hueIndex = 31;
                }

                Avalonia.Media.Color hueColor = hueEntry.Colors[hueIndex];

                outputPixels[offset + 0] = hueColor.B;
                outputPixels[offset + 1] = hueColor.G;
                outputPixels[offset + 2] = hueColor.R;
                outputPixels[offset + 3] = a;
            }
        }

        return CreateWriteableBitmapFromPixels(pixels.Width, pixels.Height, outputPixels);
    }

    private FramePixels ReadWriteableBitmapPixels(WriteableBitmap bitmap)
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

        return new FramePixels(width, height, packedBytes);
    }

    private WriteableBitmap CreateWriteableBitmapFromPixels(int width, int height, byte[] pixels)
    {
        WriteableBitmap bitmap = new WriteableBitmap(
            new Avalonia.PixelSize(width, height),
            new Avalonia.Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            Avalonia.Platform.AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);

        return bitmap;
    }

    private List<VdFrameData> ApplyEnhancementsToVdFrames(List<VdFrameData> sourceFrames, VdEnhancementDialogResult enhancement)
    {
        List<VdFrameData> result = new List<VdFrameData>();

        if (sourceFrames == null || sourceFrames.Count == 0)
        {
            return result;
        }

        bool doAnything =
            enhancement.ApplySharpen ||
            enhancement.ApplyContrast ||
            enhancement.ApplyOutlineBoost;

        if (!doAnything)
        {
            result.AddRange(sourceFrames);
            return result;
        }

        foreach (VdFrameData frame in sourceFrames)
        {
            WriteableBitmap enhancedBitmap = ApplyEnhancementsToBitmap(frame.Bitmap, enhancement);

            result.Add(new VdFrameData
            {
                Bitmap = enhancedBitmap,
                Palette565 = null,
                CenterX = frame.CenterX,
                CenterY = frame.CenterY,
                Width = (ushort)enhancedBitmap.PixelSize.Width,
                Height = (ushort)enhancedBitmap.PixelSize.Height,
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

    private WriteableBitmap ApplyEnhancementsToBitmap(WriteableBitmap sourceBitmap, VdEnhancementDialogResult enhancement)
    {
        using ImageSharpImage image = ConvertWriteableBitmapToImageSharp(sourceBitmap);

        image.Mutate(context =>
        {
            if (enhancement.ApplyContrast && enhancement.ContrastAmount > 0f)
            {
                context.Contrast(1f + enhancement.ContrastAmount);
            }

            if (enhancement.ApplySharpen &&
                enhancement.SharpenAmount > 0f &&
                enhancement.SharpenMode == SharpenMode.Gaussian)
            {
                context.GaussianSharpen(enhancement.SharpenAmount);
            }
        });

        WriteableBitmap outputBitmap = ConvertImageSharpToWriteableBitmap(image);

        if (enhancement.ApplySharpen &&
            enhancement.SharpenAmount > 0f &&
            enhancement.SharpenMode == SharpenMode.Pixel)
        {
            outputBitmap = ApplyPixelSharpenToBitmap(outputBitmap, enhancement.SharpenAmount);
        }

        if (enhancement.ApplyOutlineBoost)
        {
            outputBitmap = ApplyOutlineBoostToBitmap(outputBitmap, enhancement.OutlineStrength);
        }

        return outputBitmap;
    }

    private WriteableBitmap ApplyOutlineBoostToBitmap(WriteableBitmap sourceBitmap, float outlineStrength)
    {
        FramePixels pixels = ReadWriteableBitmapPixels(sourceBitmap);
        byte[] outputPixels = new byte[pixels.Pixels.Length];

        Buffer.BlockCopy(pixels.Pixels, 0, outputPixels, 0, pixels.Pixels.Length);

        for (int y = 0; y < pixels.Height; y++)
        {
            for (int x = 0; x < pixels.Width; x++)
            {
                int offset = ((y * pixels.Width) + x) * 4;

                byte a = pixels.Pixels[offset + 3];
                if (a == 0)
                {
                    continue;
                }

                bool touchesTransparent =
                    IsTransparentNeighbor(pixels, x - 1, y) ||
                    IsTransparentNeighbor(pixels, x + 1, y) ||
                    IsTransparentNeighbor(pixels, x, y - 1) ||
                    IsTransparentNeighbor(pixels, x, y + 1);

                if (!touchesTransparent)
                {
                    continue;
                }

                byte b = pixels.Pixels[offset + 0];
                byte g = pixels.Pixels[offset + 1];
                byte r = pixels.Pixels[offset + 2];

                outputPixels[offset + 0] = DarkenByte(b, outlineStrength);
                outputPixels[offset + 1] = DarkenByte(g, outlineStrength);
                outputPixels[offset + 2] = DarkenByte(r, outlineStrength);
                outputPixels[offset + 3] = a;
            }
        }

        return CreateWriteableBitmapFromPixels(pixels.Width, pixels.Height, outputPixels);
    }

    private bool IsTransparentNeighbor(FramePixels pixels, int x, int y)
    {
        if (x < 0 || y < 0 || x >= pixels.Width || y >= pixels.Height)
        {
            return true;
        }

        int offset = ((y * pixels.Width) + x) * 4;
        return pixels.Pixels[offset + 3] == 0;
    }

    private byte DarkenByte(byte value, float amount)
    {
        int darkened = (int)Math.Round(value * (1.0f - amount));

        if (darkened < 0)
        {
            return 0;
        }

        if (darkened > 255)
        {
            return 255;
        }

        return (byte)darkened;
    }

    private WriteableBitmap? BuildEnhancementPreviewSourceBitmap()
    {
        if (PreviewBitmap != null)
        {
            return PreviewBitmap;
        }

        if (decodedFrames.Count > 0)
        {
            return decodedFrames[0];
        }

        return null;
    }

    private WriteableBitmap BuildPreviewThumbnailBitmap(WriteableBitmap sourceBitmap, int maxWidth, int maxHeight, ResizeSamplerMode resizeSampler = ResizeSamplerMode.Auto)
    {
        using ImageSharpImage image = ConvertWriteableBitmapToImageSharp(sourceBitmap);

        if (image.Width > maxWidth || image.Height > maxHeight)
        {
            double widthScale = (double)maxWidth / image.Width;
            double heightScale = (double)maxHeight / image.Height;
            double scaleFactor = Math.Min(widthScale, heightScale);

            IResampler sampler = GetImageSharpResampler(resizeSampler, scaleFactor);

            image.Mutate(context =>
            {
                context.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new SixLabors.ImageSharp.Size(maxWidth, maxHeight),
                    Sampler = sampler
                });
            });
        }

        return ConvertImageSharpToWriteableBitmap(image);
    }

    private WriteableBitmap ApplyPixelSharpenToBitmap(WriteableBitmap sourceBitmap, float amount)
    {
        FramePixels pixels = ReadWriteableBitmapPixels(sourceBitmap);
        byte[] outputPixels = new byte[pixels.Pixels.Length];

        Buffer.BlockCopy(pixels.Pixels, 0, outputPixels, 0, pixels.Pixels.Length);

        float centerWeight = 1.0f + (amount * 4.0f);
        float edgeWeight = -amount;

        for (int y = 0; y < pixels.Height; y++)
        {
            for (int x = 0; x < pixels.Width; x++)
            {
                int offset = ((y * pixels.Width) + x) * 4;

                byte alpha = pixels.Pixels[offset + 3];
                if (alpha == 0)
                {
                    continue;
                }

                float blue =
                    (GetPixelChannel(pixels, x, y, 0) * centerWeight) +
                    (GetPixelChannel(pixels, x - 1, y, 0) * edgeWeight) +
                    (GetPixelChannel(pixels, x + 1, y, 0) * edgeWeight) +
                    (GetPixelChannel(pixels, x, y - 1, 0) * edgeWeight) +
                    (GetPixelChannel(pixels, x, y + 1, 0) * edgeWeight);

                float green =
                    (GetPixelChannel(pixels, x, y, 1) * centerWeight) +
                    (GetPixelChannel(pixels, x - 1, y, 1) * edgeWeight) +
                    (GetPixelChannel(pixels, x + 1, y, 1) * edgeWeight) +
                    (GetPixelChannel(pixels, x, y - 1, 1) * edgeWeight) +
                    (GetPixelChannel(pixels, x, y + 1, 1) * edgeWeight);

                float red =
                    (GetPixelChannel(pixels, x, y, 2) * centerWeight) +
                    (GetPixelChannel(pixels, x - 1, y, 2) * edgeWeight) +
                    (GetPixelChannel(pixels, x + 1, y, 2) * edgeWeight) +
                    (GetPixelChannel(pixels, x, y - 1, 2) * edgeWeight) +
                    (GetPixelChannel(pixels, x, y + 1, 2) * edgeWeight);

                outputPixels[offset + 0] = ClampToByte(blue);
                outputPixels[offset + 1] = ClampToByte(green);
                outputPixels[offset + 2] = ClampToByte(red);
                outputPixels[offset + 3] = alpha;
            }
        }

        return CreateWriteableBitmapFromPixels(pixels.Width, pixels.Height, outputPixels);
    }

    private byte GetPixelChannel(FramePixels pixels, int x, int y, int channel)
    {
        if (x < 0 || y < 0 || x >= pixels.Width || y >= pixels.Height)
        {
            return 0;
        }

        int offset = ((y * pixels.Width) + x) * 4;
        return pixels.Pixels[offset + channel];
    }

    private byte ClampToByte(float value)
    {
        int rounded = (int)Math.Round(value);

        if (rounded < 0)
        {
            return 0;
        }

        if (rounded > 255)
        {
            return 255;
        }

        return (byte)rounded;
    }

    private List<ushort> ConvertUopPaletteTo1555(List<AvaloniaColorEntry> palette)
    {
        List<ushort> result = new List<ushort>(256);

        for (int i = 0; i < 256; i++)
        {
            if (i >= palette.Count)
            {
                result.Add(i == 0 ? (ushort)0x8000 : (ushort)0x0000);
                continue;
            }

            AvaloniaColorEntry color = palette[i];

            if (color.Alpha == 0)
            {
                result.Add(i == 0 ? (ushort)0x8000 : (ushort)0x0000);
                continue;
            }

            ushort value =
                (ushort)(0x8000 |
                ((color.Red >> 3) << 10) |
                ((color.Green >> 3) << 5) |
                (color.Blue >> 3));

            if (i == 0 && value == 0)
            {
                value = 0x8000;
            }

            result.Add(value);
        }

        while (result.Count < 256)
        {
            result.Add(0);
        }

        return result;
    }

    private Dictionary<int, Dictionary<int, List<VdFrameData>>> BuildVdExportDataForTargetWithRemap(Dictionary<int, Dictionary<int, List<VdFrameData>>> sourceData, VdExportTargetType targetType, VdExportRemapProfile remapProfile)
    {
        if (remapProfile == VdExportRemapProfile.None)
        {
            return BuildVdExportDataForTarget(sourceData, targetType);
        }

        Dictionary<int, int> remapTable = GetVdExportRemapTable(remapProfile);
        Dictionary<int, Dictionary<int, List<VdFrameData>>> result =
            new Dictionary<int, Dictionary<int, List<VdFrameData>>>();

        int maxActions = GetMaxActionCountForVdExportTarget(targetType);

        foreach (KeyValuePair<int, Dictionary<int, List<VdFrameData>>> actionPair in sourceData.OrderBy(x => x.Key))
        {
            int sourceAction = actionPair.Key;

            if (!remapTable.TryGetValue(sourceAction, out int targetAction))
            {
                if (sourceAction < 0 || sourceAction >= maxActions)
                {
                    continue;
                }

                targetAction = sourceAction;
            }

            if (targetAction < 0 || targetAction >= maxActions)
            {
                continue;
            }

            if (!result.TryGetValue(targetAction, out Dictionary<int, List<VdFrameData>>? directionMap))
            {
                directionMap = new Dictionary<int, List<VdFrameData>>();
                result[targetAction] = directionMap;
            }

            foreach (KeyValuePair<int, List<VdFrameData>> directionPair in actionPair.Value)
            {
                if (directionPair.Value == null || directionPair.Value.Count == 0)
                {
                    continue;
                }

                if (!directionMap.TryGetValue(directionPair.Key, out List<VdFrameData>? existingFrames))
                {
                    directionMap[directionPair.Key] = new List<VdFrameData>(directionPair.Value);
                }
                else
                {
                    if (directionPair.Value.Count > existingFrames.Count)
                    {
                        directionMap[directionPair.Key] = new List<VdFrameData>(directionPair.Value);
                    }
                }
            }
        }

        return result;
    }

    private Dictionary<int, int> GetVdExportRemapTable(VdExportRemapProfile remapProfile)
    {
        Dictionary<int, int> map = new Dictionary<int, int>();

        switch (remapProfile)
        {
            case VdExportRemapProfile.AnimalBasic:
                // UOP source action -> MUL animal target action
                map[0] = 0;   // Walk
                map[1] = 1;   // Run
                map[2] = 2;   // Idle
                map[3] = 3;   // Eat
                map[4] = 4;   // Alert
                map[5] = 5;   // Attack1
                map[6] = 6;   // Attack2
                map[7] = 7;   // GetHit
                map[8] = 8;   // Die1
                map[9] = 9;   // Idle alt
                map[10] = 10; // Fidget
                map[11] = 11; // LieDown
                map[12] = 12; // Die2
                break;

            case VdExportRemapProfile.MonsterBasic:

                // Movement
                map[0] = 0;   // Walk → Walk
                map[1] = 1;   // Run → Walk alt (MUL doesn't separate cleanly)

                // Idle
                map[3] = 2;   // Idle → Idle
                map[4] = 2;   // Idle alt → Idle

                // Death
                map[8] = 3;   // Die1
                map[9] = 4;   // Die2

                // Attacks (collapse MANY → FEW)
                map[7] = 5;  // Attack1
                map[8] = 5;
                map[9] = 5;

                map[10] = 6;  // Attack2
                map[11] = 6;
                map[12] = 6;

                map[13] = 7;  // Attack3
                map[14] = 7;

                // Ranged / throw / spell
                map[15] = 8;  // AttackBow
                map[16] = 9;  // AttackCrossbow
                map[17] = 10; // Throw / special

                // Hit reaction
                map[18] = 11; // GetHit

                // Misc (best-fit mapping)
                map[5] = 12; // Fidget → Pillage
                map[6] = 13; // Alert → Stomp

                // Casting
                map[16] = 14; // Cast1
                map[17] = 15; // Cast2

                // Block / defensive
                map[19] = 16; // Block

                // Optional fillers
                map[20] = 17;
                map[21] = 18;
                map[22] = 19;
                map[23] = 20;
                map[24] = 21;

                break;

            case VdExportRemapProfile.HumanBasic:
                // Preserve first 35 human-like actions
                for (int i = 0; i < 35; i++)
                {
                    map[i] = i;
                }
                break;
        }

        return map;
    }

    private string GetVdExportRemapLabel(VdExportRemapProfile remapProfile)
    {
        return remapProfile switch
        {
            VdExportRemapProfile.AnimalBasic => "Animal Basic",
            VdExportRemapProfile.MonsterBasic => "Monster Basic",
            VdExportRemapProfile.HumanBasic => "Human Basic",
            _ => "None"
        };
    }

    private GifCanvasInfo CalculateGifCanvas(IReadOnlyList<GifFrameData> frames)
    {
        GifCanvasInfo info = new GifCanvasInfo();

        if (frames == null || frames.Count == 0)
        {
            info.Width = 1;
            info.Height = 1;
            info.DrawCenterX = 0;
            info.DrawCenterY = 0;
            return info;
        }

        int maxLeft = 0;
        int maxTop = 0;
        int maxRight = 1;
        int maxBottom = 1;

        foreach (GifFrameData frame in frames)
        {
            if (frame?.Bitmap == null)
            {
                continue;
            }

            int width = frame.Bitmap.PixelSize.Width;
            int height = frame.Bitmap.PixelSize.Height;

            maxLeft = Math.Max(maxLeft, frame.CenterX);
            maxTop = Math.Max(maxTop, frame.CenterY + height);
            maxRight = Math.Max(maxRight, width - frame.CenterX);
            maxBottom = Math.Max(maxBottom, -frame.CenterY);
        }

        info.DrawCenterX = maxLeft;
        info.DrawCenterY = maxTop;
        info.Width = Math.Max(1, maxLeft + maxRight);
        info.Height = Math.Max(1, maxTop + maxBottom);

        return info;
    }

    private ImageSharpImage BuildGifCanvasFrame(GifFrameData frame, GifCanvasInfo canvasInfo, ResizeSamplerMode resizeSampler)
    {
        ImageSharpImage sourceImage = PrepareFrameForGif(frame.Bitmap, resizeSampler);
        ImageSharpImage targetImage = new ImageSharpImage(canvasInfo.Width, canvasInfo.Height);

        int drawX = canvasInfo.DrawCenterX - frame.CenterX;
        int drawY = canvasInfo.DrawCenterY - frame.CenterY - sourceImage.Height;

        targetImage.Mutate(context =>
        {
            context.DrawImage(sourceImage, new SixLabors.ImageSharp.Point(drawX, drawY), 1f);
        });

        sourceImage.Dispose();
        return targetImage;
    }

    private List<GifFrameData> BuildGifFramesFromVdFrames(List<VdFrameData> vdFrames, bool resizeEnabled, double resizePercent, ResizeSamplerMode resizeSampler)
    {
        List<GifFrameData> result = new List<GifFrameData>();

        if (vdFrames == null || vdFrames.Count == 0)
        {
            return result;
        }

        double scaleFactor = resizeEnabled ? (resizePercent / 100.0) : 1.0;

        foreach (VdFrameData vdFrame in vdFrames)
        {
            if (vdFrame.Bitmap == null)
            {
                continue;
            }

            WriteableBitmap exportBitmap = ResizeBitmapForExport(
                vdFrame.Bitmap,
                resizeEnabled,
                resizePercent,
                resizeSampler);

            short centerX = vdFrame.CenterX;
            short centerY = vdFrame.CenterY;

            if (Math.Abs(scaleFactor - 1.0) > 0.0001)
            {
                centerX = ClampToShort((int)Math.Round(vdFrame.CenterX * scaleFactor));
                centerY = ClampToShort((int)Math.Round(vdFrame.CenterY * scaleFactor));
            }

            result.Add(new GifFrameData
            {
                Bitmap = exportBitmap,
                CenterX = centerX,
                CenterY = centerY
            });
        }

        return result;
    }

    private void ConfigureGifFrameMetadata(ImageFrame<Bgra32> frame, int gifDelay)
    {
        var metadata = frame.Metadata.GetGifMetadata();
        metadata.FrameDelay = gifDelay;

        // Clear previous frame before drawing the next one.
        metadata.DisposalMethod = GifDisposalMethod.RestoreToBackground;
    }

    private List<SpriteSheetFrameData> BuildSpriteSheetFramesFromVdFrames(List<VdFrameData> vdFrames, bool resizeEnabled, double resizePercent, ResizeSamplerMode resizeSampler)
    {
        List<SpriteSheetFrameData> result = new List<SpriteSheetFrameData>();

        if (vdFrames == null || vdFrames.Count == 0)
        {
            return result;
        }

        double scaleFactor = resizeEnabled ? (resizePercent / 100.0) : 1.0;

        foreach (VdFrameData vdFrame in vdFrames)
        {
            WriteableBitmap exportBitmap = ResizeBitmapForExport(
                vdFrame.Bitmap,
                resizeEnabled,
                resizePercent,
                resizeSampler);

            short centerX = vdFrame.CenterX;
            short centerY = vdFrame.CenterY;

            if (Math.Abs(scaleFactor - 1.0) > 0.0001)
            {
                centerX = ClampToShort((int)Math.Round(vdFrame.CenterX * scaleFactor));
                centerY = ClampToShort((int)Math.Round(vdFrame.CenterY * scaleFactor));
            }

            result.Add(new SpriteSheetFrameData
            {
                Bitmap = exportBitmap,
                CenterX = centerX,
                CenterY = centerY
            });
        }

        return result;
    }

    private SpriteSheetCellInfo CalculateSpriteSheetCellInfo(IReadOnlyList<SpriteSheetFrameData> frames)
    {
        SpriteSheetCellInfo info = new SpriteSheetCellInfo();

        if (frames == null || frames.Count == 0)
        {
            info.CellWidth = 1;
            info.CellHeight = 1;
            info.DrawCenterX = 0;
            info.DrawCenterY = 0;
            return info;
        }

        int maxLeft = 0;
        int maxTop = 0;
        int maxRight = 1;
        int maxBottom = 1;

        foreach (SpriteSheetFrameData frame in frames)
        {
            if (frame?.Bitmap == null)
            {
                continue;
            }

            int width = frame.Bitmap.PixelSize.Width;
            int height = frame.Bitmap.PixelSize.Height;

            maxLeft = Math.Max(maxLeft, frame.CenterX);
            maxTop = Math.Max(maxTop, frame.CenterY + height);
            maxRight = Math.Max(maxRight, width - frame.CenterX);
            maxBottom = Math.Max(maxBottom, -frame.CenterY);
        }

        info.DrawCenterX = maxLeft;
        info.DrawCenterY = maxTop;
        info.CellWidth = Math.Max(1, maxLeft + maxRight);
        info.CellHeight = Math.Max(1, maxTop + maxBottom);

        return info;
    }

    private SpriteSheetBuildResult BuildSpriteSheetImage(List<VdFrameData> vdFrames, int actionIndex, int directionIndex, int columns, int padding, ResizeSamplerMode resizeSampler, bool resizeEnabled, double resizePercent)
    {
        List<SpriteSheetFrameData> preparedFrames = BuildSpriteSheetFramesFromVdFrames(
            vdFrames,
            resizeEnabled,
            resizePercent,
            resizeSampler);

        if (preparedFrames.Count == 0)
        {
            return new SpriteSheetBuildResult
            {
                SheetImage = new ImageSharpImage(1, 1),
                Frames = new List<SpriteSheetFrameMetadata>(),
                SheetWidth = 1,
                SheetHeight = 1,
                Columns = 1,
                Rows = 1,
                Padding = padding,
                CellWidth = 1,
                CellHeight = 1
            };
        }

        if (columns < 1)
        {
            columns = 1;
        }

        if (padding < 0)
        {
            padding = 0;
        }

        SpriteSheetCellInfo cellInfo = CalculateSpriteSheetCellInfo(preparedFrames);

        int frameCount = preparedFrames.Count;
        int rows = (int)Math.Ceiling(frameCount / (double)columns);

        int sheetWidth = (columns * cellInfo.CellWidth) + ((columns + 1) * padding);
        int sheetHeight = (rows * cellInfo.CellHeight) + ((rows + 1) * padding);

        ImageSharpImage sheet = new ImageSharpImage(sheetWidth, sheetHeight);
        List<SpriteSheetFrameMetadata> metadata = new List<SpriteSheetFrameMetadata>(preparedFrames.Count);

        for (int index = 0; index < preparedFrames.Count; index++)
        {
            SpriteSheetFrameData frame = preparedFrames[index];

            using ImageSharpImage frameImage = ConvertWriteableBitmapToImageSharp(frame.Bitmap);

            int column = index % columns;
            int row = index / columns;

            int cellX = padding + (column * (cellInfo.CellWidth + padding));
            int cellY = padding + (row * (cellInfo.CellHeight + padding));

            int drawX = cellX + cellInfo.DrawCenterX - frame.CenterX;
            int drawY = cellY + cellInfo.DrawCenterY - frame.CenterY - frameImage.Height;

            sheet.Mutate(context =>
            {
                context.DrawImage(frameImage, new SixLabors.ImageSharp.Point(drawX, drawY), 1f);
            });

            metadata.Add(new SpriteSheetFrameMetadata
            {
                FrameIndex = index,
                Action = actionIndex,
                Direction = directionIndex,
                CellX = cellX,
                CellY = cellY,
                CellWidth = cellInfo.CellWidth,
                CellHeight = cellInfo.CellHeight,
                DrawX = drawX,
                DrawY = drawY,
                SourceWidth = frame.Bitmap.PixelSize.Width,
                SourceHeight = frame.Bitmap.PixelSize.Height,
                CenterX = frame.CenterX,
                CenterY = frame.CenterY
            });
        }

        return new SpriteSheetBuildResult
        {
            SheetImage = sheet,
            Frames = metadata,
            SheetWidth = sheetWidth,
            SheetHeight = sheetHeight,
            Columns = columns,
            Rows = rows,
            Padding = padding,
            CellWidth = cellInfo.CellWidth,
            CellHeight = cellInfo.CellHeight
        };
    }

    private async Task SaveSpriteSheetAsync(List<VdFrameData> vdFrames, string filePath, int actionIndex, int directionIndex, int columns, int padding, bool resizeEnabled, double resizePercent, ResizeSamplerMode resizeSampler, SpriteSheetMetadataFormat metadataFormat)
    {
        SpriteSheetBuildResult buildResult = BuildSpriteSheetImage(
            vdFrames,
            actionIndex,
            directionIndex,
            columns,
            padding,
            resizeSampler,
            resizeEnabled,
            resizePercent);

        using (buildResult.SheetImage)
        {
            await buildResult.SheetImage.SaveAsync(filePath, new PngEncoder());
        }

        if (metadataFormat == SpriteSheetMetadataFormat.None)
        {
            return;
        }

        string metadataPath = metadataFormat switch
        {
            SpriteSheetMetadataFormat.Csv => Path.ChangeExtension(filePath, ".csv"),
            SpriteSheetMetadataFormat.Json => Path.ChangeExtension(filePath, ".json"),
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(metadataPath))
        {
            return;
        }

        if (metadataFormat == SpriteSheetMetadataFormat.Csv)
        {
            await SaveSpriteSheetMetadataCsvAsync(metadataPath, buildResult);
        }
        else if (metadataFormat == SpriteSheetMetadataFormat.Json)
        {
            await SaveSpriteSheetMetadataJsonAsync(metadataPath, buildResult);
        }
    }

    private async Task SaveSpriteSheetMetadataCsvAsync(string filePath, SpriteSheetBuildResult buildResult)
    {
        List<string> lines = new List<string>
    {
        "frame_index,action,direction,cell_x,cell_y,cell_width,cell_height,draw_x,draw_y,source_width,source_height,center_x,center_y"
    };

        foreach (SpriteSheetFrameMetadata frame in buildResult.Frames)
        {
            lines.Add(
                frame.FrameIndex + "," +
                frame.Action + "," +
                frame.Direction + "," +
                frame.CellX + "," +
                frame.CellY + "," +
                frame.CellWidth + "," +
                frame.CellHeight + "," +
                frame.DrawX + "," +
                frame.DrawY + "," +
                frame.SourceWidth + "," +
                frame.SourceHeight + "," +
                frame.CenterX + "," +
                frame.CenterY);
        }

        await File.WriteAllLinesAsync(filePath, lines);
    }

    private async Task SaveSpriteSheetMetadataJsonAsync(string filePath, SpriteSheetBuildResult buildResult)
    {
        var payload = new
        {
            sheetWidth = buildResult.SheetWidth,
            sheetHeight = buildResult.SheetHeight,
            columns = buildResult.Columns,
            rows = buildResult.Rows,
            padding = buildResult.Padding,
            cellWidth = buildResult.CellWidth,
            cellHeight = buildResult.CellHeight,
            frames = buildResult.Frames
        };

        string json = System.Text.Json.JsonSerializer.Serialize(
            payload,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

        await File.WriteAllTextAsync(filePath, json);
    }

    private WriteableBitmap ApplyExportEffects(WriteableBitmap sourceBitmap, ExportRequest exportRequest)
    {
        WriteableBitmap outputBitmap = sourceBitmap;

        bool applyEnhancement =
            exportRequest.ApplySharpen ||
            exportRequest.ApplyContrast ||
            exportRequest.ApplyOutlineBoost;

        if (applyEnhancement)
        {
            VdEnhancementDialogResult enhancement = new VdEnhancementDialogResult
            {
                Confirmed = true,
                ApplySharpen = exportRequest.ApplySharpen,
                ApplyContrast = exportRequest.ApplyContrast,
                ApplyOutlineBoost = exportRequest.ApplyOutlineBoost,
                SharpenMode = exportRequest.SharpenMode,
                SharpenAmount = exportRequest.SharpenAmount,
                ContrastAmount = exportRequest.ContrastAmount,
                OutlineStrength = exportRequest.OutlineStrength
            };

            outputBitmap = ApplyEnhancementsToBitmap(outputBitmap, enhancement);
        }

        if (exportRequest.ApplyHue && exportRequest.SelectedHue != null)
        {
            outputBitmap = ApplyHueToBitmap(outputBitmap, exportRequest.SelectedHue);
        }

        return outputBitmap;
    }

    private List<VdFrameData> ApplyExportEffectsToFrames(List<VdFrameData> sourceFrames, ExportRequest exportRequest)
    {
        List<VdFrameData> result = new List<VdFrameData>();

        if (sourceFrames == null || sourceFrames.Count == 0)
        {
            return result;
        }

        foreach (VdFrameData frame in sourceFrames)
        {
            WriteableBitmap processedBitmap = ApplyExportEffects(frame.Bitmap, exportRequest);

            result.Add(new VdFrameData
            {
                Bitmap = processedBitmap,
                Palette565 = null,
                CenterX = frame.CenterX,
                CenterY = frame.CenterY,
                Width = (ushort)processedBitmap.PixelSize.Width,
                Height = (ushort)processedBitmap.PixelSize.Height,
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

    private ExportRequest BuildEnhancementExportRequest(
    VdScaleDialogResult? scaleDialogResult,
    VdHueDialogResult hueDialogResult,
    VdEnhancementDialogResult enhancementDialogResult)
    {
        return new ExportRequest
        {
            ResizeEnabled = scaleDialogResult != null &&
                            scaleDialogResult.Confirmed &&
                            Math.Abs(scaleDialogResult.ScaleFactor - 1.0) > 0.0001,
            ResizePercent = scaleDialogResult != null
                ? scaleDialogResult.ScaleFactor * 100.0
                : 100.0,
            ResizeSampler = scaleDialogResult?.ResizeSampler ?? ResizeSamplerMode.Auto,

            ApplyHue = hueDialogResult.ApplyHue,
            SelectedHue = hueDialogResult.SelectedHue,

            ApplySharpen = enhancementDialogResult.ApplySharpen,
            ApplyContrast = enhancementDialogResult.ApplyContrast,
            ApplyOutlineBoost = enhancementDialogResult.ApplyOutlineBoost,
            SharpenMode = enhancementDialogResult.SharpenMode,
            SharpenAmount = enhancementDialogResult.SharpenAmount,
            ContrastAmount = enhancementDialogResult.ContrastAmount,
            OutlineStrength = enhancementDialogResult.OutlineStrength
        };
    }

    private List<VdFrameData> ApplyEditEffectsToFrames(List<VdFrameData> sourceFrames, ExportRequest request)
    {
        List<VdFrameData> processedFrames = ApplyExportEffectsToFrames(sourceFrames, request);

        if (!request.ResizeEnabled)
        {
            return processedFrames;
        }

        double scaleFactor = request.ResizePercent / 100.0;
        return ScaleVdFrames(processedFrames, scaleFactor, request.ResizeSampler);
    }
}