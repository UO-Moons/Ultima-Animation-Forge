using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
    private void LoadSelectedAnimationBlock()
    {
        SelectedBlockSizeText = "-";
        SelectedBlockHeaderText = "-";
        ClearDecodedFramesAndThumbnails();

        OnPropertyChanged(nameof(SelectedBlockSize));
        OnPropertyChanged(nameof(SelectedBlockHeader));
        OnPropertyChanged(nameof(PreviewInfoText));
        OnPropertyChanged(nameof(SelectedSequenceRequestedAction));
        OnPropertyChanged(nameof(SelectedSequenceResolvedGroup));
        OnPropertyChanged(nameof(SelectedSequenceFrameCount));
        OnPropertyChanged(nameof(SelectedSequenceRemap));
        OnPropertyChanged(nameof(SelectedSequenceBodyMapping));
        OnPropertyChanged(nameof(SelectedUopVirtualPathDisplay));

        if (SelectedAnimation == null)
        {
            return;
        }

        IAnimationDataSource? dataSource = GetDataSourceForEntry(SelectedAnimation);

        if (currentResolvedAnimationBlock == null)
        {
            StatusText = "No resolved animation block is selected.";
            return;
        }

        byte[] blockData = GetEffectiveAnimationBlockForCurrentSelection(dataSource);

        if (blockData.Length == 0)
        {
            StatusText = "Failed to read animation block.";
            return;
        }

        string bodyConvDebugText = currentResolvedAnimationBlock.DebugText;
        SelectedBlockSizeText = blockData.Length.ToString() + " bytes";

        if (currentResolvedAnimationBlock.IsUop)
        {
            DecodeUopAnimation(blockData, GetSelectedDirectionIndex(), currentResolvedAnimationBlock.DebugText);
            return;
        }

        if (blockData.Length < 516)
        {
            StatusText = "Animation block too small.";
            return;
        }

        DecodeMulAnimationFromOffset(blockData, 512, bodyConvDebugText);

        if (HasQueuedBlockForCurrentSelection())
        {
            StatusText += " Showing queued unsaved edit.";
        }
    }

    private bool HasQueuedBlockForCurrentSelection()
    {
        if (currentResolvedAnimationBlock == null || currentResolvedAnimationBlock.IsUop)
        {
            return false;
        }

        return pendingMulImportSession.TryGetFileEdit(
                   currentResolvedAnimationBlock.IdxPath,
                   out PendingMulImportSession.PendingMulFileEdit? fileEdit) &&
               fileEdit != null &&
               fileEdit.PendingBlocksBySlotIndex.ContainsKey(currentResolvedAnimationBlock.SlotIndex);
    }

    private void DecodeMulAnimationFromOffset(byte[] blockData, int dataStart, string bodyConvDebugText)
    {
        uint frameCount = BitConverter.ToUInt32(blockData, dataStart);

        if (frameCount == 0 || frameCount > 1000)
        {
            if (SelectedAnimation != null)
            {
                SelectedAnimation.FrameCount = 0;
                SelectedAnimation.FrameSize = "-";
                OnPropertyChanged(nameof(SelectedFrameCount));
                OnPropertyChanged(nameof(SelectedFrameSize));
            }

            SelectedBlockHeaderText = "Invalid frame count at byte " + dataStart + ": " + frameCount;
            OnPropertyChanged(nameof(PreviewInfoText));
            OnPropertyChanged(nameof(SelectedBlockHeader));
            return;
        }

        if (SelectedAnimation != null)
        {
            SelectedAnimation.FrameCount = (int)frameCount;
            OnPropertyChanged(nameof(SelectedFrameCount));
        }

        OnPropertyChanged(nameof(PreviewInfoText));

        int frameTableStart = dataStart + 4;
        uint[] frameOffsets = new uint[frameCount];

        for (int index = 0; index < frameCount; index++)
        {
            int offsetPosition = frameTableStart + (index * 4);

            if (offsetPosition + 4 > blockData.Length)
            {
                StatusText = "Frame offset table exceeds block size.";
                return;
            }

            frameOffsets[index] = BitConverter.ToUInt32(blockData, offsetPosition);
        }

        string frameOffsetListText = string.Empty;
        int previewCount = frameCount < 5 ? (int)frameCount : 5;

        for (int index = 0; index < previewCount; index++)
        {
            if (index > 0)
            {
                frameOffsetListText += ", ";
            }

            frameOffsetListText += frameOffsets[index].ToString();
        }

        List<ushort> palette565 = new List<ushort>(256);
        for (int index = 0; index < 256; index++)
        {
            ushort value = (ushort)(BitConverter.ToUInt16(blockData, index * 2) ^ 0x8000);

            if (index == 0 && value == 0)
            {
                value = 0x8000;
            }
            else if (index != 0 && value == 0x8000)
            {
                value = 0x8001;
            }

            palette565.Add(value);
        }

        for (ushort frameIndex = 0; frameIndex < frameCount; frameIndex++)
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

            short centerX = BitConverter.ToInt16(blockData, frameStart + 0);
            short centerY = BitConverter.ToInt16(blockData, frameStart + 2);
            ushort width = (ushort)BitConverter.ToInt16(blockData, frameStart + 4);
            ushort height = (ushort)BitConverter.ToInt16(blockData, frameStart + 6);

            WriteableBitmap? frameBitmap = DecodeFrameToBitmap(blockData, frameStart);

            if (frameBitmap != null)
            {
                VdFrameData frameData = new VdFrameData
                {
                    Bitmap = frameBitmap,
                    Palette565 = new List<ushort>(palette565),
                    CenterX = centerX,
                    CenterY = centerY,
                    Width = width,
                    Height = height,
                    InitCoordsX = 0,
                    InitCoordsY = 0,
                    EndCoordsX = 0,
                    EndCoordsY = 0,
                    FrameId = frameIndex,
                    FrameNumber = frameIndex,
                    DataOffset = frameOffsets[frameIndex]
                };

                editableFrames.Add(frameData);
                decodedFrames.Add(frameBitmap);
            }
        }

        if (decodedFrames.Count > 0)
        {
            currentFrameIndex = 0;
            PreviewBitmap = decodedFrames[0];
        }

        RebuildFrameThumbnails();
        CaptureLivePreviewSourceFromCurrentFrame();
        RefreshLivePreviewImage();

        hasFrameEdits = false;
        OnPropertyChanged(nameof(HasFrameEdits));

        int firstFrameStart = -1;
        string firstFrameBytesText = "-";

        if (frameOffsets.Length > 0 && frameOffsets[0] != 0)
        {
            firstFrameStart = dataStart + (int)frameOffsets[0];

            if (firstFrameStart >= 0 && firstFrameStart < blockData.Length)
            {
                int frameDebugLength = 24;

                if (firstFrameStart + frameDebugLength > blockData.Length)
                {
                    frameDebugLength = blockData.Length - firstFrameStart;
                }

                string[] frameHexValues = new string[frameDebugLength];

                for (int index = 0; index < frameDebugLength; index++)
                {
                    frameHexValues[index] = blockData[firstFrameStart + index].ToString("X2");
                }

                firstFrameBytesText = string.Join(" ", frameHexValues);
            }
        }

        short debugWidth = -1;
        short debugHeight = -1;

        if (firstFrameStart >= 0 && firstFrameStart + 8 <= blockData.Length)
        {
            debugWidth = BitConverter.ToInt16(blockData, firstFrameStart + 4);
            debugHeight = BitConverter.ToInt16(blockData, firstFrameStart + 6);
        }

        SelectedBlockHeaderText =
            "FrameCount: " + frameCount +
            " | DataStart: " + dataStart +
            " | FirstFrameSize: " + debugWidth + "x" + debugHeight +
            " | FrameOffsets: [" + frameOffsetListText + "]" +
            " | DecodedFrames: " + decodedFrames.Count +
            " | FirstFrameStart: " + firstFrameStart +
            " | FirstFrameBytes: " + firstFrameBytesText +
            (string.IsNullOrWhiteSpace(bodyConvDebugText) ? string.Empty : " | " + bodyConvDebugText);

        OnPropertyChanged(nameof(SelectedBlockSize));
        OnPropertyChanged(nameof(SelectedBlockHeader));

        if (SelectedAnimation != null && currentResolvedAnimationBlock != null)
        {
            StatusText = "Read animation block for index " + SelectedAnimation.IndexNumber + " from " + currentResolvedAnimationBlock.SourceFileName + ".";
        }
        else if (SelectedMulSlot != null)
        {
            StatusText = "Selected free MUL body slot " + SelectedMulSlot.BodyIndex + " from " + SelectedMulSlot.FileName + ".";
        }
        else
        {
            StatusText = "Read animation block.";
        }
    }

    private void DecodeUopAnimation(byte[] blockData, int directionIndex, string debugText)
    {
        if (blockData == null || blockData.Length < 40)
        {
            SelectedAnimation!.FrameCount = 0;
            SelectedAnimation.FrameSize = "-";
            SelectedBlockHeaderText = "UOP block too small.";
            OnPropertyChanged(nameof(SelectedFrameCount));
            OnPropertyChanged(nameof(SelectedFrameSize));
            OnPropertyChanged(nameof(PreviewInfoText));
            OnPropertyChanged(nameof(SelectedBlockHeader));
            return;
        }

        UopBinHeaderData header = ReadUopBinHeader(blockData);

        if (header.Magic != 1431260481U || header.FrameCount == 0)
        {
            SelectedAnimation!.FrameCount = 0;
            SelectedAnimation.FrameSize = "-";
            SelectedBlockHeaderText =
                "Invalid UOP header. Magic=" + header.Magic +
                " FrameCount=" + header.FrameCount;
            OnPropertyChanged(nameof(SelectedFrameCount));
            OnPropertyChanged(nameof(SelectedFrameSize));
            OnPropertyChanged(nameof(PreviewInfoText));
            OnPropertyChanged(nameof(SelectedBlockHeader));
            return;
        }

        List<UopFrameIndexData> frameIndexes = ReadUopFrameIndexes(blockData, header);

        if (frameIndexes.Count == 0)
        {
            SelectedAnimation!.FrameCount = 0;
            SelectedAnimation.FrameSize = "-";
            SelectedBlockHeaderText = "UOP frame index table is empty.";
            OnPropertyChanged(nameof(SelectedFrameCount));
            OnPropertyChanged(nameof(SelectedFrameSize));
            OnPropertyChanged(nameof(PreviewInfoText));
            OnPropertyChanged(nameof(SelectedBlockHeader));
            return;
        }

        List<UopFrameIndexData> directionFrames = GetUopDirectionFrames(frameIndexes, directionIndex);

        SelectedAnimation!.FrameCount = directionFrames.Count;
        OnPropertyChanged(nameof(SelectedFrameCount));
        OnPropertyChanged(nameof(PreviewInfoText));

        for (int frameListIndex = 0; frameListIndex < directionFrames.Count; frameListIndex++)
        {
            UopFrameIndexData frameIndex = directionFrames[frameListIndex];

            int frameDataAbsoluteOffset = (int)(frameIndex.StreamPosition + frameIndex.FrameDataOffset);
            if (frameDataAbsoluteOffset < 0 || frameDataAbsoluteOffset + 512 + 8 > blockData.Length)
            {
                continue;
            }

            List<AvaloniaColorEntry> palette = ReadUopPalette(blockData, frameDataAbsoluteOffset);
            List<ushort> palette565 = ConvertUopPaletteTo1555(palette);

            UopFrameHeaderData frameHeader = ReadUopFrameHeader(blockData, frameDataAbsoluteOffset + 512);
            WriteableBitmap? frameBitmap = DecodeUopFrame(blockData, frameIndexes, frameIndex);

            if (frameBitmap != null)
            {
                VdFrameData frameData = new VdFrameData
                {
                    Bitmap = frameBitmap,
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
                };

                editableFrames.Add(frameData);
                decodedFrames.Add(frameBitmap);
            }
        }

        if (decodedFrames.Count > 0)
        {
            currentFrameIndex = 0;
            PreviewBitmap = decodedFrames[0];
        }

        RebuildFrameThumbnails();
        CaptureLivePreviewSourceFromCurrentFrame();
        RefreshLivePreviewImage();

        hasFrameEdits = false;
        OnPropertyChanged(nameof(HasFrameEdits));

        string firstFrameDebug = "-";
        int firstWidth = -1;
        int firstHeight = -1;

        if (directionFrames.Count > 0)
        {
            UopFrameIndexData firstDirectionFrame = directionFrames[0];
            int firstFrameAbsoluteOffset = (int)(firstDirectionFrame.StreamPosition + firstDirectionFrame.FrameDataOffset);

            if (firstFrameAbsoluteOffset >= 0 && firstFrameAbsoluteOffset + 32 <= blockData.Length)
            {
                string[] debugBytes = new string[24];

                for (int index = 0; index < 24; index++)
                {
                    debugBytes[index] = blockData[firstFrameAbsoluteOffset + index].ToString("X2");
                }

                firstFrameDebug = string.Join(" ", debugBytes);

                if (firstFrameAbsoluteOffset + 8 <= blockData.Length)
                {
                    UopFrameHeaderData frameHeader = ReadUopFrameHeader(blockData, firstFrameAbsoluteOffset + 512);
                    firstWidth = frameHeader.Width;
                    firstHeight = frameHeader.Height;
                }
            }
        }

        if (SelectedAnimation.FrameSize == "-" && firstWidth > 0 && firstHeight > 0)
        {
            SelectedAnimation.FrameSize = firstWidth + " x " + firstHeight;
            OnPropertyChanged(nameof(SelectedFrameSize));
        }

        SelectedBlockHeaderText =
           "UOP Magic: " + header.Magic +
           " | AnimationId: " + header.AnimationId +
           " | FrameCount: " + header.FrameCount +
           " | IndexedFrames: " + frameIndexes.Count +
           " | SelectedDirection: " + directionIndex +
           " | DirectionFrames: " + directionFrames.Count +
           " | DecodedFrames: " + decodedFrames.Count +
           " | CellBounds: [" + header.BoundLeft + "," + header.BoundTop + "] to [" + header.BoundRight + "," + header.BoundBottom + "]" +
           " | RequestedAction: " + currentResolvedAnimationBlock!.RequestedActionIndex +
           " | ResolvedGroup: " + currentResolvedAnimationBlock.ResolvedUopGroupIndex +
           " | SequenceRemap: " +
               (currentResolvedAnimationBlock.UsedSequenceRemap
                   ? currentResolvedAnimationBlock.RequestedActionIndex + " -> " + currentResolvedAnimationBlock.ResolvedUopGroupIndex
                   : "No") +
           " | SequenceFrameCount: " + currentResolvedAnimationBlock.SequenceFrameCount +
           " | BodyMap: " +
               (currentResolvedAnimationBlock.BodyId == currentResolvedAnimationBlock.ResolvedBodyId
                   ? currentResolvedAnimationBlock.BodyId.ToString()
                   : currentResolvedAnimationBlock.BodyId + " -> " + currentResolvedAnimationBlock.ResolvedBodyId) +
           " | FirstFrameSize: " + firstWidth + "x" + firstHeight +
           " | FirstFrameBytes: " + firstFrameDebug +
           " | UopPath: " + currentResolvedAnimationBlock.UopVirtualPath;

        OnPropertyChanged(nameof(SelectedBlockSize));
        OnPropertyChanged(nameof(SelectedBlockHeader));

        StatusText =
            "Read UOP animation for body " + SelectedAnimation.BodyId +
            ", action " + SelectedAnimation.ActionId +
            ", direction " + directionIndex +
            " from " + currentResolvedAnimationBlock!.SourceFileName + ".";
    }

    private UopBinHeaderData ReadUopBinHeader(byte[] blockData)
    {
        using MemoryStream memoryStream = new MemoryStream(blockData, false);
        using BinaryReader reader = new BinaryReader(memoryStream);

        return new UopBinHeaderData
        {
            Magic = reader.ReadUInt32(),
            Version = reader.ReadUInt32(),
            TotalSize = reader.ReadUInt32(),
            AnimationId = reader.ReadUInt32(),
            BoundLeft = reader.ReadInt16(),
            BoundTop = reader.ReadInt16(),
            BoundRight = reader.ReadInt16(),
            BoundBottom = reader.ReadInt16(),
            Unknown1 = reader.ReadUInt32(),
            Unknown2 = reader.ReadUInt32(),
            FrameCount = reader.ReadUInt32(),
            FrameIndexOffset = reader.ReadUInt32()
        };
    }

    private List<UopFrameIndexData> ReadUopFrameIndexes(byte[] blockData, UopBinHeaderData header)
    {
        List<UopFrameIndexData> frameIndexes = new List<UopFrameIndexData>();

        if (header.FrameIndexOffset <= 0 || header.FrameIndexOffset >= blockData.Length)
        {
            return frameIndexes;
        }

        using MemoryStream memoryStream = new MemoryStream(blockData, false);
        using BinaryReader reader = new BinaryReader(memoryStream);

        reader.BaseStream.Seek(header.FrameIndexOffset, SeekOrigin.Begin);

        for (int index = 0; index < header.FrameCount; index++)
        {
            long streamPosition = reader.BaseStream.Position;

            if (streamPosition + 16 > blockData.Length)
            {
                break;
            }

            UopFrameIndexData frameIndex = new UopFrameIndexData
            {
                Direction = reader.ReadUInt16(),
                FrameNumber = reader.ReadUInt16(),
                Left = reader.ReadInt16(),
                Top = reader.ReadInt16(),
                Right = reader.ReadInt16(),
                Bottom = reader.ReadInt16(),
                FrameDataOffset = reader.ReadUInt32(),
                StreamPosition = streamPosition
            };

            frameIndexes.Add(frameIndex);
        }

        return frameIndexes;
    }

    private List<UopFrameIndexData> GetUopDirectionFrames(
        List<UopFrameIndexData> frameIndexes,
        int directionIndex)
    {
        if (frameIndexes == null || frameIndexes.Count == 0)
        {
            return new List<UopFrameIndexData>();
        }

        List<UopFrameIndexData> directionFrames = frameIndexes
            .Where(frame => frame.Direction == directionIndex)
            .OrderBy(frame => frame.FrameNumber)
            .ThenBy(frame => frame.StreamPosition)
            .ToList();

        if (directionFrames.Count == 0)
        {
            List<UopFrameIndexData> orderedFrameIndexes = new List<UopFrameIndexData>(frameIndexes);
            orderedFrameIndexes.Sort((left, right) =>
                left.StreamPosition.CompareTo(right.StreamPosition));

            uint framesPerDirection = (uint)Math.Ceiling(orderedFrameIndexes.Count / 5.0);
            if (framesPerDirection == 0 && orderedFrameIndexes.Count > 0)
            {
                framesPerDirection = 1;
            }

            int startIndex = directionIndex * (int)framesPerDirection;
            int endIndex = Math.Min(startIndex + (int)framesPerDirection, orderedFrameIndexes.Count);

            for (int index = startIndex; index < endIndex; index++)
            {
                directionFrames.Add(orderedFrameIndexes[index]);
            }
        }

        return directionFrames;
    }

    private WriteableBitmap? DecodeUopFrame(byte[] blockData, List<UopFrameIndexData> allFrameIndexes, UopFrameIndexData targetFrame)
    {
        int frameDataAbsoluteOffset = (int)(targetFrame.StreamPosition + targetFrame.FrameDataOffset);

        if (frameDataAbsoluteOffset < 0 || frameDataAbsoluteOffset + 512 + 8 > blockData.Length)
        {
            return null;
        }

        List<AvaloniaColorEntry> palette = ReadUopPalette(blockData, frameDataAbsoluteOffset);
        UopFrameHeaderData frameHeader = ReadUopFrameHeader(blockData, frameDataAbsoluteOffset + 512);

        if (frameHeader.Width <= 0 || frameHeader.Height <= 0)
        {
            return null;
        }

        if (SelectedAnimation != null && SelectedAnimation.FrameSize == "-")
        {
            SelectedAnimation.FrameSize = frameHeader.Width + " x " + frameHeader.Height;
            OnPropertyChanged(nameof(SelectedFrameSize));
            OnPropertyChanged(nameof(PreviewInfoText));
        }

        List<UopFrameIndexData> sortedFrames = new List<UopFrameIndexData>(allFrameIndexes);
        sortedFrames.Sort((left, right) =>
            (left.StreamPosition + left.FrameDataOffset).CompareTo(right.StreamPosition + right.FrameDataOffset));

        int sortedIndex = sortedFrames.FindIndex(frame =>
            frame.StreamPosition == targetFrame.StreamPosition &&
            frame.FrameDataOffset == targetFrame.FrameDataOffset);

        long streamEnd = blockData.Length;

        if (sortedIndex >= 0 && sortedIndex + 1 < sortedFrames.Count)
        {
            streamEnd = sortedFrames[sortedIndex + 1].StreamPosition + sortedFrames[sortedIndex + 1].FrameDataOffset;
        }

        return DecodeUopRleFrame(blockData, frameDataAbsoluteOffset + 512 + 8, frameHeader, palette, streamEnd);
    }

    private List<AvaloniaColorEntry> ReadUopPalette(byte[] blockData, int paletteOffset)
    {
        List<AvaloniaColorEntry> palette = new List<AvaloniaColorEntry>(256);

        using MemoryStream memoryStream = new MemoryStream(blockData, false);
        using BinaryReader reader = new BinaryReader(memoryStream);

        reader.BaseStream.Seek(paletteOffset, SeekOrigin.Begin);

        for (int index = 0; index < 256; index++)
        {
            ushort colorValue = (ushort)(reader.ReadUInt16() ^ 0x8000);

            if ((colorValue & 0x8000) == 0 || index == 0)
            {
                palette.Add(new AvaloniaColorEntry(0, 0, 0, 0));
            }
            else
            {
                byte red = (byte)(((colorValue >> 10) & 0x1F) << 3);
                byte green = (byte)(((colorValue >> 5) & 0x1F) << 3);
                byte blue = (byte)((colorValue & 0x1F) << 3);

                palette.Add(new AvaloniaColorEntry(red, green, blue, 255));
            }
        }

        return palette;
    }

    private UopFrameHeaderData ReadUopFrameHeader(byte[] blockData, int headerOffset)
    {
        using MemoryStream memoryStream = new MemoryStream(blockData, false);
        using BinaryReader reader = new BinaryReader(memoryStream);

        reader.BaseStream.Seek(headerOffset, SeekOrigin.Begin);

        return new UopFrameHeaderData
        {
            CenterX = reader.ReadInt16(),
            CenterY = reader.ReadInt16(),
            Width = reader.ReadUInt16(),
            Height = reader.ReadUInt16()
        };
    }

    private WriteableBitmap? DecodeUopRleFrame(
        byte[] blockData,
        long pixelDataOffset,
        UopFrameHeaderData frameHeader,
        List<AvaloniaColorEntry> palette,
        long streamEnd)
    {
        if (frameHeader.Width <= 0 || frameHeader.Height <= 0)
        {
            return null;
        }

        int width = frameHeader.Width;
        int height = frameHeader.Height;

        if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
        {
            return null;
        }

        long strideLong = (long)width * 4L;
        long pixelBufferLength = strideLong * height;

        if (strideLong > int.MaxValue || pixelBufferLength > int.MaxValue)
        {
            return null;
        }

        int stride = (int)strideLong;
        byte[] pixels = new byte[(int)pixelBufferLength];

        using MemoryStream memoryStream = new MemoryStream(blockData, false);
        using BinaryReader reader = new BinaryReader(memoryStream);

        reader.BaseStream.Seek(pixelDataOffset, SeekOrigin.Begin);

        try
        {
            while (reader.BaseStream.Position < streamEnd && reader.BaseStream.Position + 4 <= streamEnd)
            {
                uint header = reader.ReadUInt32();

                if (header == 0x7FFF7FFF)
                {
                    break;
                }

                int runLength = (int)(header & 0x0FFF);
                int y = (int)((header >> 12) & 0x03FF);
                int x = (int)((header >> 22) & 0x03FF);

                if ((x & 0x0200) != 0)
                {
                    x = -(1024 - x);
                }

                if ((y & 0x0200) != 0)
                {
                    y = -(1024 - y);
                }

                int pixelY = height - 1 - (-y - frameHeader.CenterY);

                if (runLength == 0)
                {
                    continue;
                }

                if (reader.BaseStream.Position + runLength > streamEnd)
                {
                    break;
                }

                for (int index = 0; index < runLength; index++)
                {
                    byte paletteIndex = reader.ReadByte();
                    AvaloniaColorEntry color = palette[paletteIndex];
                    int pixelX = frameHeader.CenterX + x + index;

                    if (pixelX < 0 || pixelX >= width || pixelY < 0 || pixelY >= height)
                    {
                        continue;
                    }

                    int pixelOffset = (pixelY * stride) + (pixelX * 4);
                    pixels[pixelOffset + 0] = color.Blue;
                    pixels[pixelOffset + 1] = color.Green;
                    pixels[pixelOffset + 2] = color.Red;
                    pixels[pixelOffset + 3] = color.Alpha;
                }
            }
        }
        catch (EndOfStreamException)
        {
            return null;
        }

        WriteableBitmap bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var framebuffer = bitmap.Lock())
        {
            Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
        }

        return bitmap;
    }

    private WriteableBitmap? DecodeFrameToBitmap(byte[] blockData, int frameStart)
    {
        if (frameStart < 0 || frameStart + 8 > blockData.Length)
        {
            return null;
        }

        List<ushort> palette565 = new List<ushort>(256);
        for (int index = 0; index < 256; index++)
        {
            ushort value = (ushort)(BitConverter.ToUInt16(blockData, index * 2) ^ 0x8000);

            if (index == 0 && value == 0)
            {
                value = 0x8000;
            }
            else if (index != 0 && value == 0x8000)
            {
                value = 0x8001;
            }

            palette565.Add(value);
        }

        short centerX = BitConverter.ToInt16(blockData, frameStart + 0);
        short centerY = BitConverter.ToInt16(blockData, frameStart + 2);
        short width = BitConverter.ToInt16(blockData, frameStart + 4);
        short height = BitConverter.ToInt16(blockData, frameStart + 6);

        if (width <= 0 || height <= 0 || width > 500 || height > 500)
        {
            return null;
        }

        if (SelectedAnimation != null && SelectedAnimation.FrameSize == "-")
        {
            SelectedAnimation.FrameSize = width + " x " + height;
            OnPropertyChanged(nameof(SelectedFrameSize));
            OnPropertyChanged(nameof(PreviewInfoText));
        }

        int stride = width * 4;
        byte[] pixels = new byte[height * stride];

        int position = frameStart + 8;

        while (position + 4 <= blockData.Length)
        {
            uint header = BitConverter.ToUInt32(blockData, position);
            position += 4;

            if (header == 0x7FFF7FFF)
            {
                break;
            }

            int runLength = (int)(header & 0x0FFF);

            int x = (int)((header >> 22) & 0x03FF);
            if ((x & 0x0200) != 0)
            {
                x = -(1024 - x);
            }

            int y = (int)((header >> 12) & 0x03FF);
            if ((y & 0x0200) != 0)
            {
                y = -(1024 - y);
            }

            int pixelY = height - 1 - (-y - centerY);

            for (int index = 0; index < runLength; index++)
            {
                if (position >= blockData.Length)
                {
                    break;
                }

                byte paletteIndex = blockData[position++];
                ushort color = palette565[paletteIndex];

                int red = ((color >> 10) & 0x1F) * 8;
                int green = ((color >> 5) & 0x1F) * 8;
                int blue = (color & 0x1F) * 8;

                int pixelX = centerX + x + index;

                if (pixelX < 0 || pixelY < 0 || pixelX >= width || pixelY >= height)
                {
                    continue;
                }

                int pixelOffset = (pixelY * stride) + (pixelX * 4);

                pixels[pixelOffset + 0] = (byte)blue;
                pixels[pixelOffset + 1] = (byte)green;
                pixels[pixelOffset + 2] = (byte)red;
                pixels[pixelOffset + 3] = 255;
            }
        }

        WriteableBitmap bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var framebuffer = bitmap.Lock())
        {
            Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
        }

        return bitmap;
    }

    private byte[] GetEffectiveAnimationBlockForCurrentSelection(IAnimationDataSource? dataSource)
    {
        if (currentResolvedAnimationBlock == null || dataSource == null)
        {
            return Array.Empty<byte>();
        }

        if (!currentResolvedAnimationBlock.IsUop &&
            pendingMulImportSession.TryGetFileEdit(
                currentResolvedAnimationBlock.IdxPath,
                out PendingMulImportSession.PendingMulFileEdit? fileEdit) &&
            fileEdit != null &&
            fileEdit.PendingBlocksBySlotIndex.TryGetValue(
                currentResolvedAnimationBlock.SlotIndex,
                out PendingMulImportSession.PendingMulBlock? pendingBlock) &&
            pendingBlock != null &&
            pendingBlock.BlockData != null &&
            pendingBlock.BlockData.Length > 0)
        {
            return pendingBlock.BlockData;
        }

        return dataSource.ReadAnimationBlock(currentResolvedAnimationBlock);
    }

    internal DetachedPreviewLoadResult LoadDetachedPreview(
    AnimationEntry animation,
    int actionIndex,
    int directionIndex)
    {
        DetachedPreviewLoadResult result = new DetachedPreviewLoadResult();

        if (animation == null)
        {
            result.Message = "No animation selected.";
            return result;
        }

        IAnimationDataSource? dataSource = GetDataSourceForEntry(animation);
        if (dataSource == null)
        {
            result.Message = "No animation data source is available.";
            return result;
        }

        if (!dataSource.TryResolveAnimationBlock(animation.BodyId, actionIndex, directionIndex, out ResolvedAnimationBlock resolvedBlock))
        {
            result.Message = "No animation data for body " + animation.BodyId + ", action " + actionIndex + ", direction " + directionIndex + ".";
            return result;
        }

        byte[] blockData = dataSource.ReadAnimationBlock(resolvedBlock);
        if (blockData.Length == 0)
        {
            result.Message = "Failed to read animation block.";
            return result;
        }

        List<WriteableBitmap> frames;

        if (resolvedBlock.IsUop)
        {
            frames = DecodeUopFramesToList(blockData, directionIndex);
        }
        else
        {
            if (blockData.Length < 516)
            {
                result.Message = "Animation block too small.";
                return result;
            }

            frames = DecodeMulFramesToList(blockData, 512);
        }

        if (frames.Count == 0)
        {
            result.Message = "No frames decoded.";
            return result;
        }

        result.Success = true;
        result.Frames = frames;
        result.FrameCount = frames.Count;
        result.FrameSizeText = frames[0].PixelSize.Width + " x " + frames[0].PixelSize.Height;
        result.PreviewInfoText =
            "Body " + animation.BodyId +
            " | Action " + actionIndex +
            " | Direction " + directionIndex +
            " | " + resolvedBlock.SourceFileName +
            " | Frames: " + frames.Count;

        return result;
    }

    private List<WriteableBitmap> DecodeMulFramesToList(byte[] blockData, int dataStart)
    {
        List<WriteableBitmap> frames = new();

        uint frameCount = BitConverter.ToUInt32(blockData, dataStart);
        if (frameCount == 0 || frameCount > 1000)
        {
            return frames;
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

        for (ushort frameIndex = 0; frameIndex < frameCount; frameIndex++)
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

            WriteableBitmap? frameBitmap = DecodeFrameToBitmapDetached(blockData, frameStart);
            if (frameBitmap != null)
            {
                frames.Add(frameBitmap);
            }
        }

        return frames;
    }

    private List<WriteableBitmap> DecodeUopFramesToList(byte[] blockData, int directionIndex)
    {
        List<WriteableBitmap> decoded = new();

        if (blockData == null || blockData.Length < 40)
        {
            return decoded;
        }

        UopBinHeaderData header = ReadUopBinHeader(blockData);

        if (header.Magic != 1431260481U || header.FrameCount == 0)
        {
            return decoded;
        }

        List<UopFrameIndexData> frameIndexes = ReadUopFrameIndexes(blockData, header);
        if (frameIndexes.Count == 0)
        {
            return decoded;
        }

        List<UopFrameIndexData> directionFrames = frameIndexes
            .Where(x => x.Direction == directionIndex)
            .OrderBy(x => x.FrameNumber)
            .ToList();

        foreach (UopFrameIndexData frameIndex in directionFrames)
        {
            WriteableBitmap? frameBitmap = DecodeUopFrame(blockData, frameIndexes, frameIndex);
            if (frameBitmap != null)
            {
                decoded.Add(frameBitmap);
            }
        }

        return decoded;
    }

    private WriteableBitmap? DecodeFrameToBitmapDetached(byte[] blockData, int frameStart)
    {
        if (frameStart < 0 || frameStart + 8 > blockData.Length)
        {
            return null;
        }

        List<ushort> palette565 = new List<ushort>(256);
        for (int index = 0; index < 256; index++)
        {
            ushort value = (ushort)(BitConverter.ToUInt16(blockData, index * 2) ^ 0x8000);

            if (index == 0 && value == 0)
            {
                value = 0x8000;
            }
            else if (index != 0 && value == 0x8000)
            {
                value = 0x8001;
            }

            palette565.Add(value);
        }

        short centerX = BitConverter.ToInt16(blockData, frameStart + 0);
        short centerY = BitConverter.ToInt16(blockData, frameStart + 2);
        short width = BitConverter.ToInt16(blockData, frameStart + 4);
        short height = BitConverter.ToInt16(blockData, frameStart + 6);

        if (width <= 0 || height <= 0 || width > 500 || height > 500)
        {
            return null;
        }

        int stride = width * 4;
        byte[] pixels = new byte[height * stride];

        int position = frameStart + 8;

        while (position + 4 <= blockData.Length)
        {
            uint header = BitConverter.ToUInt32(blockData, position);
            position += 4;

            if (header == 0x7FFF7FFF)
            {
                break;
            }

            int runLength = (int)(header & 0x0FFF);

            int x = (int)((header >> 22) & 0x03FF);
            if ((x & 0x0200) != 0)
            {
                x = -(1024 - x);
            }

            int y = (int)((header >> 12) & 0x03FF);
            if ((y & 0x0200) != 0)
            {
                y = -(1024 - y);
            }

            int pixelY = height - 1 - (-y - centerY);

            for (int index = 0; index < runLength; index++)
            {
                if (position >= blockData.Length)
                {
                    break;
                }

                byte paletteIndex = blockData[position++];
                ushort color = palette565[paletteIndex];

                int red = ((color >> 10) & 0x1F) * 8;
                int green = ((color >> 5) & 0x1F) * 8;
                int blue = (color & 0x1F) * 8;

                int pixelX = centerX + x + index;

                if (pixelX < 0 || pixelY < 0 || pixelX >= width || pixelY >= height)
                {
                    continue;
                }

                int pixelOffset = (pixelY * stride) + (pixelX * 4);

                pixels[pixelOffset + 0] = (byte)blue;
                pixels[pixelOffset + 1] = (byte)green;
                pixels[pixelOffset + 2] = (byte)red;
                pixels[pixelOffset + 3] = 255;
            }
        }

        WriteableBitmap bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var framebuffer = bitmap.Lock())
        {
            Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
        }

        return bitmap;
    }
}