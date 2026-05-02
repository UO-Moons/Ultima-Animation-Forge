using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MythicPackageViewModel : ViewModelBase
{
    private readonly MythicPackageReaderService packageReader = new();

    public ObservableCollection<MythicPackageEntry> Entries { get; } = new();

    private readonly EcAmouPreviewDecoderService amouDecoder = new();

    private readonly EcAnimationCollectionService animationCollection = new();

    private bool suppressEntrySelectorSync = false;

    private List<EcAnimationCollectionEntry> currentXmlEntries = new();
    private readonly Dictionary<(int block, int file), MythicPackageEntry> entryLookup = new();

    private readonly List<WriteableBitmap> decodedAmouFrames = new();
    private int currentAmouFrameIndex = 0;

    public string CurrentFrameText =>
    decodedAmouFrames.Count == 0
        ? "Frame: - / -"
        : "Frame: " + (currentAmouFrameIndex + 1) + " / " + decodedAmouFrames.Count;

    public ObservableCollection<EcAmouBodyOption> EcBodyOptions { get; } = new();
    public ObservableCollection<EcAmouActionOption> EcActionOptions { get; } = new();

    private readonly Dictionary<ulong, EcAmouPreviewDecoderService.AmouMetadata> amouMetadataByHash = new();
    private bool syncingEcSelectors = false;

    [ObservableProperty]
    private EcAmouBodyOption? selectedEcBody;

    [ObservableProperty]
    private EcAmouActionOption? selectedEcAction;

    [ObservableProperty]
    private MythicPackageEntry? selectedEntry;

    [ObservableProperty]
    private string hexPreviewText = string.Empty;

    [ObservableProperty]
    private string packagePath = "No package loaded.";

    [ObservableProperty]
    private string statusText = "Open an EC/KR .uop package.";

    [ObservableProperty]
    private string selectedInfoText = "No entry selected.";

    [ObservableProperty]
    private WriteableBitmap? previewBitmap;

    public MythicPackageViewModel()
    {
        OpenPackageCommand = new AsyncRelayCommand(OpenPackageAsync);
        ExportSelectedCommand = new AsyncRelayCommand(ExportSelectedAsync);
        ExportAllCommand = new AsyncRelayCommand(ExportAllAsync);
        OpenDictionaryCommand = new AsyncRelayCommand(OpenDictionaryAsync);
        PreviousFrameCommand = new RelayCommand(ShowPreviousFrame);
        NextFrameCommand = new RelayCommand(ShowNextFrame);
        ExportAmouFramesCommand = new AsyncRelayCommand(ExportAmouFramesAsync);
        OpenAnimationCollectionCommand = new AsyncRelayCommand(OpenAnimationCollectionAsync);
        ExportFullAmouVdCommand = new AsyncRelayCommand(ExportFullAmouVdAsync);
    }

    public ICommand OpenPackageCommand { get; }
    public ICommand ExportSelectedCommand { get; }
    public ICommand ExportAllCommand { get; }
    public ICommand OpenDictionaryCommand { get; }
    public ICommand PreviousFrameCommand { get; }
    public ICommand NextFrameCommand { get; }
    public ICommand ExportAmouFramesCommand { get; }
    public ICommand OpenAnimationCollectionCommand { get; }
    public ICommand ExportFullAmouVdCommand { get; }

    private async Task ExportFullAmouVdAsync()
    {
        if (SelectedEcBody == null)
        {
            StatusText = "Select an AMOU body first.";
            return;
        }

        Window? window = GetActiveWindow();
        if (window == null)
        {
            StatusText = "Could not locate package window.";
            return;
        }

        FilePickerSaveOptions options = new FilePickerSaveOptions
        {
            Title = "Export Full AMOU Body as VD",
            SuggestedFileName = "body_" + SelectedEcBody.BodyId + "_EC.vd",
            FileTypeChoices =
            [
                new FilePickerFileType("VD file")
            {
                Patterns = [ "*.vd" ]
            }
            ]
        };

        IStorageFile? file = await window.StorageProvider.SaveFilePickerAsync(options);
        if (file == null)
        {
            StatusText = "VD export cancelled.";
            return;
        }

        string? outputPath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            StatusText = "VD export path was invalid.";
            return;
        }

        Dictionary<int, Dictionary<int, List<VdFrameData>>> actionDirectionFrames = new();

        List<MythicPackageEntry> bodyEntries = Entries
            .Where(entry =>
            {
                if (!string.Equals(entry.PreviewType, "AMOU Animation", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!amouMetadataByHash.TryGetValue(entry.Hash, out EcAmouPreviewDecoderService.AmouMetadata metadata))
                {
                    byte[] data = packageReader.ReadEntryBytes(entry.Hash);
                    if (!amouDecoder.TryReadMetadata(data, out metadata))
                    {
                        return false;
                    }

                    amouMetadataByHash[entry.Hash] = metadata;
                }

                return amouMetadataByHash[entry.Hash].AnimId == SelectedEcBody.BodyId;
            })
            .OrderBy(entry => amouMetadataByHash[entry.Hash].ActionId)
            .ThenBy(entry => entry.Hash)
            .ToList();

        foreach (MythicPackageEntry entry in bodyEntries)
        {
            byte[] data = packageReader.ReadEntryBytes(entry.Hash);

            if (!amouDecoder.TryReadMetadata(data, out EcAmouPreviewDecoderService.AmouMetadata metadata))
            {
                continue;
            }

            Dictionary<int, List<VdFrameData>> directions = amouDecoder.DecodeVdFramesByDirection(data);
            if (directions.Count == 0)
            {
                continue;
            }

            int action = metadata.ActionId;
            if (action < 0 || action >= 32)
            {
                continue;
            }

            actionDirectionFrames[action] = directions;
        }

        if (actionDirectionFrames.Count == 0)
        {
            StatusText = "No AMOU actions could be decoded for VD export.";
            return;
        }

        VdExportService.ExportBodyAnimation(outputPath, 4, actionDirectionFrames);

        StatusText =
            "Exported full AMOU body " +
            SelectedEcBody.BodyId +
            " to VD with " +
            actionDirectionFrames.Count +
            " action(s).";
    }

    private void SelectPackageEntryFromEcAction(MythicPackageEntry entry)
    {
        suppressEntrySelectorSync = true;
        SelectedEntry = entry;
        suppressEntrySelectorSync = false;

        LoadSelectedPreview();
    }

    private async Task OpenAnimationCollectionAsync()
    {
        Window? window = GetActiveWindow();
        if (window == null)
        {
            StatusText = "Could not locate package window.";
            return;
        }

        FilePickerOpenOptions options = new FilePickerOpenOptions
        {
            Title = "Open EC/KR Animation Collection XML",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Animation Collection XML")
            {
                Patterns = [ "*.xml" ]
            },
            FilePickerFileTypes.All
            ]
        };

        IReadOnlyList<IStorageFile> files =
            await window.StorageProvider.OpenFilePickerAsync(options);

        if (files.Count == 0)
        {
            StatusText = "Open animation collection cancelled.";
            return;
        }

        string? localPath = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
        {
            StatusText = "Selected XML does not have a local path.";
            return;
        }

        if (!animationCollection.Load(localPath, out string message))
        {
            StatusText = message;
            return;
        }

        StatusText = message;

        if (Entries.Count > 0)
        {
            BuildEcAnimationBrowser();
        }
    }

    private async Task ExportAmouFramesAsync()
    {
        if (decodedAmouFrames.Count == 0)
        {
            StatusText = "No decoded AMOU frames to export.";
            return;
        }

        Window? window = GetActiveWindow();
        if (window == null)
        {
            StatusText = "Could not locate package window.";
            return;
        }

        FolderPickerOpenOptions options = new FolderPickerOpenOptions
        {
            Title = "Select AMOU Frame Export Folder",
            AllowMultiple = false
        };

        IReadOnlyList<IStorageFolder> folders =
            await window.StorageProvider.OpenFolderPickerAsync(options);

        if (folders.Count == 0)
        {
            StatusText = "AMOU frame export cancelled.";
            return;
        }

        string? folderPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            StatusText = "Export folder was invalid.";
            return;
        }

        string baseName = SelectedEntry != null && !string.IsNullOrWhiteSpace(SelectedEntry.FileName)
            ? Path.GetFileNameWithoutExtension(SelectedEntry.FileName)
            : SelectedEntry?.HashText.Replace("0x", "") ?? "amou";

        for (int i = 0; i < decodedAmouFrames.Count; i++)
        {
            string outputPath = Path.Combine(folderPath, baseName + "_frame_" + i.ToString("D3") + ".png");

            using FileStream stream = File.Create(outputPath);
            decodedAmouFrames[i].Save(stream);
        }

        StatusText = "Exported " + decodedAmouFrames.Count + " AMOU frames as PNG.";
    }

    private async Task OpenDictionaryAsync()
    {
        Window? window = GetActiveWindow();
        if (window == null)
        {
            StatusText = "Could not locate package window.";
            return;
        }

        FilePickerOpenOptions options = new FilePickerOpenOptions
        {
            Title = "Open EC/KR Dictionary",
            AllowMultiple = false,
            FileTypeFilter =
[
    new FilePickerFileType("Dictionary / String Dictionary")
    {
        Patterns = [ "*.dic", "*string_dictionary*.uop" ]
    },
    FilePickerFileTypes.All
]
        };

        IReadOnlyList<IStorageFile> files =
            await window.StorageProvider.OpenFilePickerAsync(options);

        if (files.Count == 0)
        {
            StatusText = "Open dictionary cancelled.";
            return;
        }

        string? localPath = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
        {
            StatusText = "Selected dictionary does not have a local path.";
            return;
        }

        if (!packageReader.LoadDictionary(localPath, out string message))
        {
            StatusText = message;
            return;
        }

        StatusText = message;

        if (!string.IsNullOrWhiteSpace(packageReader.PackagePath))
        {
            ReloadEntriesFromCurrentPackage();
        }
    }

    private void ReloadEntriesFromCurrentPackage()
    {
        MythicPackageEntry? previousSelected = SelectedEntry;
        ulong previousHash = previousSelected?.Hash ?? 0;

        Entries.Clear();

        foreach (MythicPackageEntry entry in packageReader.GetEntries())
        {
            Entries.Add(entry);
        }

        SelectedEntry = Entries.FirstOrDefault(x => x.Hash == previousHash);

        if (SelectedEntry == null && Entries.Count > 0)
        {
            SelectedEntry = Entries[0];
        }

        LoadSelectedPreview();
        BuildEcAnimationBrowser();
    }

    partial void OnSelectedEntryChanged(MythicPackageEntry? value)
    {
        if (!suppressEntrySelectorSync)
        {
            SyncEcSelectorsToSelectedEntry(value);
        }

        LoadSelectedPreview();
    }

    partial void OnSelectedEcBodyChanged(EcAmouBodyOption? value)
    {
        if (syncingEcSelectors)
        {
            return;
        }

        RebuildEcActionOptions(value?.BodyId ?? -1);

        if (SelectedEcAction != null)
        {
            SelectPackageEntryFromEcAction(SelectedEcAction.Entry);
        }
    }

    partial void OnSelectedEcActionChanged(EcAmouActionOption? value)
    {
        if (syncingEcSelectors || value == null)
        {
            return;
        }

        SelectPackageEntryFromEcAction(value.Entry);
    }

    private void SyncEcSelectorsToSelectedEntry(MythicPackageEntry? entry)
    {
        if (syncingEcSelectors || entry == null || currentXmlEntries.Count == 0)
        {
            return;
        }

        EcAnimationCollectionEntry? xmlMatch = currentXmlEntries.FirstOrDefault(x =>
            x.BlockIndex == entry.BlockIndex &&
            x.FileIndex == entry.FileIndex);

        if (xmlMatch == null)
        {
            return;
        }

        syncingEcSelectors = true;

        EcAmouBodyOption? bodyMatch = EcBodyOptions.FirstOrDefault(x => x.BodyId == xmlMatch.BodyId);
        if (bodyMatch != null)
        {
            SelectedEcBody = bodyMatch;
        }

        RebuildEcActionOptions(xmlMatch.BodyId);

        EcAmouActionOption? actionMatch = EcActionOptions.FirstOrDefault(x =>
            x.Entry.BlockIndex == entry.BlockIndex &&
            x.Entry.FileIndex == entry.FileIndex);

        if (actionMatch != null)
        {
            SelectedEcAction = actionMatch;
        }

        syncingEcSelectors = false;
    }

    private async Task OpenPackageAsync()
    {
        Window? window = GetActiveWindow();
        if (window == null)
        {
            StatusText = "Could not locate package window.";
            return;
        }

        FilePickerOpenOptions options = new FilePickerOpenOptions
        {
            Title = "Open EC/KR UOP Package",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("UOP Package")
                {
                    Patterns = [ "*.uop" ]
                },
                FilePickerFileTypes.All
            ]
        };

        IReadOnlyList<IStorageFile> files =
            await window.StorageProvider.OpenFilePickerAsync(options);

        if (files.Count == 0)
        {
            StatusText = "Open package cancelled.";
            return;
        }

        string? localPath = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
        {
            StatusText = "Selected package does not have a local path.";
            return;
        }

        if (!packageReader.Load(localPath))
        {
            StatusText = "Failed to load package.";
            return;
        }

        PackagePath = localPath;
        Entries.Clear();

        foreach (MythicPackageEntry entry in packageReader.GetEntries())
        {
            Entries.Add(entry);
        }

        SelectedEntry = Entries.Count > 0 ? Entries[0] : null;
        StatusText = "Loaded " + Entries.Count + " package entries.";

        BuildEcAnimationBrowser();
    }

    private async Task ExportSelectedAsync()
    {
        if (SelectedEntry == null)
        {
            StatusText = "Select an entry first.";
            return;
        }

        Window? window = GetActiveWindow();
        if (window == null)
        {
            StatusText = "Could not locate package window.";
            return;
        }

        byte[] data = packageReader.ReadEntryBytes(SelectedEntry.Hash);
        if (data.Length == 0)
        {
            StatusText = "Selected entry has no readable data.";
            return;
        }

        string extension = MythicPackageReaderService.GetExtensionFromBytes(data);
        string suggestedName = SelectedEntry.HashText.Replace("0x", "") + extension;

        FilePickerSaveOptions options = new FilePickerSaveOptions
        {
            Title = "Export Package Entry",
            SuggestedFileName = suggestedName
        };

        IStorageFile? outputFile = await window.StorageProvider.SaveFilePickerAsync(options);
        if (outputFile == null)
        {
            StatusText = "Export cancelled.";
            return;
        }

        string? outputPath = outputFile.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            StatusText = "Export path was invalid.";
            return;
        }

        packageReader.ExportEntry(SelectedEntry.Hash, outputPath);
        StatusText = "Exported " + Path.GetFileName(outputPath) + ".";
    }

    private async Task ExportAllAsync()
    {
        if (Entries.Count == 0)
        {
            StatusText = "Open a package first.";
            return;
        }

        Window? window = GetActiveWindow();
        if (window == null)
        {
            StatusText = "Could not locate package window.";
            return;
        }

        FolderPickerOpenOptions options = new FolderPickerOpenOptions
        {
            Title = "Select Export Folder",
            AllowMultiple = false
        };

        IReadOnlyList<IStorageFolder> folders =
            await window.StorageProvider.OpenFolderPickerAsync(options);

        if (folders.Count == 0)
        {
            StatusText = "Export all cancelled.";
            return;
        }

        string? folderPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            StatusText = "Export folder was invalid.";
            return;
        }

        int count = packageReader.ExportAll(folderPath, Entries);
        StatusText = "Exported " + count + " entries.";
    }

    private void LoadSelectedPreview()
    {
        PreviewBitmap = null;
        decodedAmouFrames.Clear();
        currentAmouFrameIndex = 0;
        OnPropertyChanged(nameof(CurrentFrameText));

        if (SelectedEntry == null)
        {
            SelectedInfoText = "No entry selected.";
            return;
        }

        byte[] data = packageReader.ReadEntryBytes(SelectedEntry.Hash);
        HexPreviewText = BuildHexPreview(data, 512);

        SelectedInfoText =
            "Hash: " + SelectedEntry.HashText + Environment.NewLine +
            "Type: " + MythicPackageReaderService.GuessType(data) + Environment.NewLine +
            "Compressed: " + SelectedEntry.CompressedSize + " bytes" + Environment.NewLine +
            "Decompressed: " + SelectedEntry.DecompressedSize + " bytes" + Environment.NewLine +
            "Compression Flag: " + SelectedEntry.CompressionFlag + Environment.NewLine +
            "File Name: " + (string.IsNullOrWhiteSpace(SelectedEntry.FileName) ? "(unknown)" : SelectedEntry.FileName) + Environment.NewLine +
            "Offset: " + SelectedEntry.Offset;

        string type = MythicPackageReaderService.GuessType(data);

        if (type == "AMOU Animation")
        {
            SelectedInfoText += Environment.NewLine + Environment.NewLine +
                amouDecoder.BuildInfoText(data);

            decodedAmouFrames.Clear();
            decodedAmouFrames.AddRange(amouDecoder.DecodePreviewFrames(data, 0, 256));
            currentAmouFrameIndex = 0;

            if (decodedAmouFrames.Count > 0)
            {
                PreviewBitmap = decodedAmouFrames[0];

                SelectedInfoText += Environment.NewLine +
                    "Preview Frames Decoded: " + decodedAmouFrames.Count + Environment.NewLine +
                    "Preview Size: " +
                    decodedAmouFrames[0].PixelSize.Width + " x " +
                    decodedAmouFrames[0].PixelSize.Height;
            }
            else
            {
                SelectedInfoText += Environment.NewLine +
                    "Preview Frames Decoded: 0";
            }

            OnPropertyChanged(nameof(CurrentFrameText));
        }

        if (type == "PNG" || type == "JPG" || type == "BMP")
        {
            try
            {
                using MemoryStream stream = new MemoryStream(data);
                PreviewBitmap = WriteableBitmap.Decode(stream);
            }
            catch
            {
                PreviewBitmap = null;
                SelectedInfoText += Environment.NewLine + "Preview failed.";
            }
        }
    }

    private static string BuildHexPreview(byte[] data, int maxBytes)
    {
        if (data == null || data.Length == 0)
        {
            return "No data.";
        }

        int count = Math.Min(data.Length, maxBytes);
        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        for (int offset = 0; offset < count; offset += 16)
        {
            builder.Append(offset.ToString("X8"));
            builder.Append("  ");

            int rowCount = Math.Min(16, count - offset);

            for (int i = 0; i < 16; i++)
            {
                if (i < rowCount)
                {
                    builder.Append(data[offset + i].ToString("X2"));
                    builder.Append(' ');
                }
                else
                {
                    builder.Append("   ");
                }

                if (i == 7)
                {
                    builder.Append(' ');
                }
            }

            builder.Append(" ");

            for (int i = 0; i < rowCount; i++)
            {
                byte value = data[offset + i];

                if (value >= 32 && value <= 126)
                {
                    builder.Append((char)value);
                }
                else
                {
                    builder.Append('.');
                }
            }

            builder.AppendLine();
        }

        if (data.Length > count)
        {
            builder.AppendLine();
            builder.AppendLine("Showing first " + count + " of " + data.Length + " bytes.");
        }

        return builder.ToString();
    }

    private void ShowPreviousFrame()
    {
        if (decodedAmouFrames.Count == 0)
        {
            return;
        }

        currentAmouFrameIndex--;

        if (currentAmouFrameIndex < 0)
        {
            currentAmouFrameIndex = decodedAmouFrames.Count - 1;
        }

        PreviewBitmap = decodedAmouFrames[currentAmouFrameIndex];
        OnPropertyChanged(nameof(CurrentFrameText));
    }

    private void ShowNextFrame()
    {
        if (decodedAmouFrames.Count == 0)
        {
            return;
        }

        currentAmouFrameIndex++;

        if (currentAmouFrameIndex >= decodedAmouFrames.Count)
        {
            currentAmouFrameIndex = 0;
        }

        PreviewBitmap = decodedAmouFrames[currentAmouFrameIndex];
        OnPropertyChanged(nameof(CurrentFrameText));
    }

    private Window? GetActiveWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.Windows.Count > 0
                ? desktop.Windows[desktop.Windows.Count - 1]
                : desktop.MainWindow;
        }

        return null;
    }

    private void BuildEcAnimationBrowser()
    {
        syncingEcSelectors = true;

        EcBodyOptions.Clear();
        EcActionOptions.Clear();
        amouMetadataByHash.Clear();
        currentXmlEntries.Clear();
        entryLookup.Clear();

        string currentUopName = Path.GetFileName(packageReader.PackagePath);

        foreach (MythicPackageEntry entry in Entries)
        {
            entryLookup[(entry.BlockIndex, entry.FileIndex)] = entry;
        }

        if (animationCollection.IsLoaded)
        {
            currentXmlEntries = animationCollection.GetEntriesForUop(currentUopName);
        }

        foreach (MythicPackageEntry entry in Entries)
        {
            if (!string.Equals(entry.PreviewType, "AMOU Animation", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            byte[] data = packageReader.ReadEntryBytes(entry.Hash);
            if (!amouDecoder.TryReadMetadata(data, out EcAmouPreviewDecoderService.AmouMetadata metadata))
            {
                continue;
            }

            amouMetadataByHash[entry.Hash] = metadata;

            if (!EcBodyOptions.Any(x => x.BodyId == metadata.AnimId))
            {
                EcBodyOptions.Add(new EcAmouBodyOption
                {
                    BodyId = metadata.AnimId
                });
            }
        }

        syncingEcSelectors = false;

        if (EcBodyOptions.Count > 0)
        {
            SelectedEcBody = EcBodyOptions[0];
            RebuildEcActionOptions(SelectedEcBody.BodyId);
        }

        StatusText =
            "Loaded " + Entries.Count + " package entries. " +
            "Found " + EcBodyOptions.Count + " real AMOU animation bodies" +
            (currentXmlEntries.Count > 0 ? " with " + currentXmlEntries.Count + " XML mapping records loaded." : ".");
    }

    private void RebuildEcActionOptions(int bodyId)
    {
        syncingEcSelectors = true;

        EcActionOptions.Clear();

        if (bodyId < 0)
        {
            SelectedEcAction = null;
            syncingEcSelectors = false;
            return;
        }

        List<EcAnimationCollectionEntry> xmlEntries = currentXmlEntries
            .Where(x => x.BodyId == bodyId)
            .OrderBy(x => x.ActionId)
            .ToList();

        if (xmlEntries.Count > 0)
        {
            foreach (EcAnimationCollectionEntry xmlEntry in xmlEntries)
            {
                MythicPackageEntry? packageEntry = null;

                entryLookup.TryGetValue((xmlEntry.BlockIndex, xmlEntry.FileIndex), out packageEntry);

                if (packageEntry == null)
                {
                    string expectedPath =
                        "build/animationlegacyframe/" +
                        xmlEntry.BodyId.ToString("D6") +
                        "/" +
                        xmlEntry.ActionId.ToString("D2") +
                        ".bin";

                    packageEntry = Entries.FirstOrDefault(x =>
                        string.Equals(x.FileName, expectedPath, StringComparison.OrdinalIgnoreCase));
                }

                if (packageEntry == null)
                {
                    continue;
                }

                EcActionOptions.Add(new EcAmouActionOption
                {
                    BodyId = bodyId,
                    ActionIndex = xmlEntry.ActionId,
                    Entry = packageEntry
                });
            }
        }
        else
        {
            List<MythicPackageEntry> bodyEntries = Entries
                .Where(entry =>
                {
                    if (!amouMetadataByHash.TryGetValue(entry.Hash, out EcAmouPreviewDecoderService.AmouMetadata metadata))
                    {
                        byte[] data = packageReader.ReadEntryBytes(entry.Hash);
                        if (!amouDecoder.TryReadMetadata(data, out metadata))
                        {
                            return false;
                        }

                        amouMetadataByHash[entry.Hash] = metadata;
                    }

                    return metadata.AnimId == bodyId;
                })
                .OrderBy(entry => amouMetadataByHash[entry.Hash].ActionId)
                .ThenBy(entry => entry.Hash)
                .ToList();

            foreach (MythicPackageEntry entry in bodyEntries)
            {
                int actionIndex = amouMetadataByHash[entry.Hash].ActionId;

                EcActionOptions.Add(new EcAmouActionOption
                {
                    BodyId = bodyId,
                    ActionIndex = actionIndex,
                    Entry = entry
                });
            }
        }

        SelectedEcAction = EcActionOptions.Count > 0 ? EcActionOptions[0] : null;

        syncingEcSelectors = false;
    }
}