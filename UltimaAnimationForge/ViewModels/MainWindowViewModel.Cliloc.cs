using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public sealed class DictionaryFileOption
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;

    public override string ToString()
    {
        return FileName;
    }
}

public partial class MainWindowViewModel
{
    private readonly ClilocDataService clilocDataService = new();
    private string loadedClilocPath = string.Empty;

    public ObservableCollection<string> UoxDictionaryCategoryOptions { get; } = new();

    [ObservableProperty]
    private string selectedUoxDictionaryCategory = "All";

    [ObservableProperty]
    private bool showOnlyDuplicateClilocs;

    [ObservableProperty]
    private bool showOnlyEmptyClilocs;

    [ObservableProperty]
    private bool useUoxDictionaryMode;

    [ObservableProperty]
    private string uoxDictionaryFolderPath = string.Empty;

    public ObservableCollection<ClilocEntry> ClilocEntries { get; } = new();

    public ObservableCollection<int> FreeClilocNumbers { get; } = new();

    [ObservableProperty]
    private int? selectedFreeClilocNumber;

    [ObservableProperty]
    private ClilocEntry? selectedClilocEntry;

    [ObservableProperty]
    private string clilocSearchText = string.Empty;

    [ObservableProperty]
    private string clilocStatusText = "No cliloc loaded.";

    public string ClilocEditorTitle =>
    UseUoxDictionaryMode ? "UOX3 Dictionary Editor" : "Cliloc Editor";

    public string EntryNumberLabel =>
        UseUoxDictionaryMode ? "Dictionary ID" : "Cliloc ID";

    public string FreeNumberTitle =>
    UseUoxDictionaryMode ? "Free Dictionary Numbers" : "Free Cliloc Numbers";

    public string FindFreeNumbersButtonText =>
        UseUoxDictionaryMode ? "Find Free Dictionary Numbers" : "Find Free Cliloc Numbers";

    public string UseFreeNumberButtonText =>
        UseUoxDictionaryMode ? "Use Selected Dictionary Number" : "Use Selected Cliloc Number";

    [ObservableProperty]
    private string jumpToClilocText = string.Empty;

    public ObservableCollection<string> ClilocFileOptions { get; } = new();

    [ObservableProperty]
    private string? selectedClilocFile;

    public ObservableCollection<ClilocEntry> FilteredClilocEntries
    {
        get
        {
            IEnumerable<ClilocEntry> query = ClilocEntries;

            if (!string.IsNullOrWhiteSpace(loadedDictionaryPath) &&
                !string.IsNullOrWhiteSpace(SelectedUoxDictionaryCategory) &&
                !string.Equals(SelectedUoxDictionaryCategory, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    string.Equals(x.Category, SelectedUoxDictionaryCategory, StringComparison.OrdinalIgnoreCase));
            }

            if (ShowOnlyDuplicateClilocs)
            {
                query = query.Where(x => x.IsDuplicate);
            }

            if (ShowOnlyEmptyClilocs)
            {
                query = query.Where(x => string.IsNullOrWhiteSpace(x.Text));
            }

            if (!string.IsNullOrWhiteSpace(ClilocSearchText))
            {
                string search = ClilocSearchText.Trim();

                query = query.Where(x =>
                    x.Number.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    x.NumberHex.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    x.Text.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    x.Category.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            return new ObservableCollection<ClilocEntry>(
                query.OrderBy(x => x.Category).ThenBy(x => x.Number));
        }
    }

    partial void OnUseUoxDictionaryModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowClilocFileControls));
        OnPropertyChanged(nameof(ShowUoxDictionaryControls));
        OnPropertyChanged(nameof(ClilocEditorTitle));
        OnPropertyChanged(nameof(EntryNumberLabel));
        OnPropertyChanged(nameof(FreeNumberTitle));
        OnPropertyChanged(nameof(FindFreeNumbersButtonText));
        OnPropertyChanged(nameof(UseFreeNumberButtonText));
    }

    public bool ShowClilocFileControls => !UseUoxDictionaryMode;

    public bool ShowUoxDictionaryControls => UseUoxDictionaryMode;

    partial void OnSelectedUoxDictionaryCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredClilocEntries));
    }

    partial void OnShowOnlyDuplicateClilocsChanged(bool value)
    {
        OnPropertyChanged(nameof(FilteredClilocEntries));
    }

    partial void OnShowOnlyEmptyClilocsChanged(bool value)
    {
        OnPropertyChanged(nameof(FilteredClilocEntries));
    }

    private void RefreshUoxDictionaryCategories()
    {
        UoxDictionaryCategoryOptions.Clear();
        UoxDictionaryCategoryOptions.Add("All");

        foreach (string category in ClilocEntries
                     .Select(x => string.IsNullOrWhiteSpace(x.Category) ? "DICTIONARY" : x.Category)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x))
        {
            UoxDictionaryCategoryOptions.Add(category);
        }

        if (!UoxDictionaryCategoryOptions.Contains(SelectedUoxDictionaryCategory))
        {
            SelectedUoxDictionaryCategory = "All";
        }
    }

    private void RefreshClilocDuplicateFlags()
    {
        foreach (ClilocEntry entry in ClilocEntries)
        {
            entry.IsDuplicate = false;
        }

        foreach (IGrouping<int, ClilocEntry> group in ClilocEntries.GroupBy(x => x.Number))
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            foreach (ClilocEntry entry in group)
            {
                entry.IsDuplicate = true;
            }
        }

        int duplicateCount = ClilocEntries.Count(x => x.IsDuplicate);
        int emptyCount = ClilocEntries.Count(x => string.IsNullOrWhiteSpace(x.Text));

        StatusText = "Duplicate IDs: " + duplicateCount + " | Empty text: " + emptyCount + ".";

        OnPropertyChanged(nameof(FilteredClilocEntries));
    }

    private readonly UoxDictionaryDataService uoxDictionaryDataService = new();
    private string loadedDictionaryPath = string.Empty;

    public ObservableCollection<DictionaryFileOption> DictionaryFileOptions { get; } = new();

    [ObservableProperty]
    private DictionaryFileOption? selectedDictionaryFile;

    private void FindFreeClilocNumbers()
    {
        FreeClilocNumbers.Clear();

        if (ClilocEntries.Count == 0)
        {
            StatusText = "Load cliloc.enu first.";
            return;
        }

        HashSet<int> used = ClilocEntries
            .Select(x => x.Number)
            .ToHashSet();

        int min = ClilocEntries.Min(x => x.Number);
        int max = ClilocEntries.Max(x => x.Number);

        for (int number = min; number <= max; number++)
        {
            if (!used.Contains(number))
            {
                FreeClilocNumbers.Add(number);
            }
        }

        SelectedFreeClilocNumber = FreeClilocNumbers.FirstOrDefault();

        StatusText = "Found " + FreeClilocNumbers.Count + " free cliloc numbers.";
    }

    private void AddClilocEntryFromFreeNumber()
    {
        if (SelectedFreeClilocNumber == null || SelectedFreeClilocNumber <= 0)
        {
            StatusText = "Select a free cliloc number first.";
            return;
        }

        int number = SelectedFreeClilocNumber.Value;

        if (ClilocEntries.Any(x => x.Number == number))
        {
            StatusText = "That cliloc number is already used.";
            return;
        }

        ClilocEntry entry = new()
        {
            Number = number,
            Flag = 0,
            Text = "New cliloc entry",
            Category = string.IsNullOrWhiteSpace(SelectedUoxDictionaryCategory) ||
                       SelectedUoxDictionaryCategory == "All"
                ? "DICTIONARY"
                : SelectedUoxDictionaryCategory,
            IsDirty = true
        };

        ClilocEntries.Add(entry);
        SelectedClilocEntry = entry;

        FreeClilocNumbers.Remove(number);

        StatusText = "Added new cliloc entry " + number + ".";

        RefreshUoxDictionaryCategories();
        RefreshClilocDuplicateFlags();
        OnPropertyChanged(nameof(FilteredClilocEntries));
    }

    partial void OnClilocSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredClilocEntries));
    }

    private void LoadCliloc()
    {
        string folder = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            StatusText = "Open a UO folder first.";
            return;
        }

        RefreshClilocFileOptions();

        string fileName = string.IsNullOrWhiteSpace(SelectedClilocFile) ? "cliloc.enu" : SelectedClilocFile;

        string path = Path.Combine(folder, fileName);

        if (!File.Exists(path))
        {
            StatusText = fileName + " was not found in the selected UO folder.";
            return;
        }

        ClilocEntries.Clear();

        foreach (ClilocEntry entry in clilocDataService.Load(path))
        {
            ClilocEntries.Add(entry);
        }

        loadedClilocPath = path;
        loadedDictionaryPath = string.Empty;

        SelectedClilocEntry = ClilocEntries.FirstOrDefault();

        StatusText = "Loaded " + ClilocEntries.Count + " cliloc entries from " + Path.GetFileName(path) + ".";
        OnPropertyChanged(nameof(FilteredClilocEntries));
    }

    private void SaveCliloc()
    {
        if (!string.IsNullOrWhiteSpace(loadedDictionaryPath))
        {
            SaveDictionary();
            return;
        }

        if (string.IsNullOrWhiteSpace(loadedClilocPath))
        {
            StatusText = "Load cliloc.enu before saving.";
            return;
        }

        try
        {
            clilocDataService.Save(loadedClilocPath, ClilocEntries);

            StatusText = "Saved " + ClilocEntries.Count + " cliloc entries to " + Path.GetFileName(loadedClilocPath) + ".";
            OnPropertyChanged(nameof(FilteredClilocEntries));
        }
        catch (Exception exception)
        {
            StatusText = "Save failed: " + exception.Message;
        }
    }

    private void AddClilocEntry()
    {
        int nextNumber = 1000000;

        if (ClilocEntries.Count > 0)
        {
            nextNumber = ClilocEntries.Max(x => x.Number) + 1;
        }

        ClilocEntry entry = new()
        {
            Number = nextNumber,
            Flag = 0,
            Text = "New cliloc entry",
            IsDirty = true
        };

        ClilocEntries.Add(entry);
        SelectedClilocEntry = entry;

        StatusText = "Added new cliloc entry " + entry.Number + ".";
        OnPropertyChanged(nameof(FilteredClilocEntries));
    }

    private void DeleteClilocEntry()
    {
        if (SelectedClilocEntry == null)
        {
            StatusText = "Select a cliloc entry to delete.";
            return;
        }

        int number = SelectedClilocEntry.Number;

        ClilocEntries.Remove(SelectedClilocEntry);
        SelectedClilocEntry = ClilocEntries.FirstOrDefault();

        StatusText = "Deleted cliloc entry " + number + ". Save to write changes.";
        OnPropertyChanged(nameof(FilteredClilocEntries));
    }

    private void RefreshClilocFileOptions()
    {
        string? previousSelection = SelectedClilocFile;

        ClilocFileOptions.Clear();

        string folder = GetCurrentFolderPath();

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        foreach (string filePath in Directory.GetFiles(folder, "cliloc.*", SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName))
        {
            ClilocFileOptions.Add(Path.GetFileName(filePath));
        }

        if (!string.IsNullOrWhiteSpace(previousSelection) &&
            ClilocFileOptions.Contains(previousSelection))
        {
            SelectedClilocFile = previousSelection;
            return;
        }

        if (ClilocFileOptions.Count > 0)
        {
            SelectedClilocFile = ClilocFileOptions.FirstOrDefault(x =>
                string.Equals(x, "cliloc.enu", StringComparison.OrdinalIgnoreCase))
                ?? ClilocFileOptions.First();
        }
    }

    private void RefreshDictionaryFileOptions()
    {
        DictionaryFileOption? previousSelection = SelectedDictionaryFile;

        DictionaryFileOptions.Clear();

        string folder = string.IsNullOrWhiteSpace(UoxDictionaryFolderPath)
    ? GetCurrentFolderPath()
    : UoxDictionaryFolderPath;

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        foreach (string filePath in Directory.GetFiles(folder, "dictionary.*", SearchOption.AllDirectories)
                     .OrderBy(Path.GetFileName))
        {
            DictionaryFileOptions.Add(new DictionaryFileOption
            {
                FileName = Path.GetFileName(filePath),
                FullPath = filePath
            });
        }

        if (previousSelection != null)
        {
            DictionaryFileOption? match = DictionaryFileOptions.FirstOrDefault(x =>
                string.Equals(x.FullPath, previousSelection.FullPath, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                SelectedDictionaryFile = match;
                return;
            }
        }

        SelectedDictionaryFile = DictionaryFileOptions.FirstOrDefault();
    }

    private void LoadDictionary()
    {
        RefreshDictionaryFileOptions();

        if (SelectedDictionaryFile == null || !File.Exists(SelectedDictionaryFile.FullPath))
        {
            StatusText = "No UOX3 dictionary file selected.";
            return;
        }

        ClilocEntries.Clear();

        foreach (ClilocEntry entry in uoxDictionaryDataService.Load(SelectedDictionaryFile.FullPath))
        {
            ClilocEntries.Add(entry);
        }

        loadedDictionaryPath = SelectedDictionaryFile.FullPath;
        loadedClilocPath = string.Empty;
        SelectedClilocEntry = ClilocEntries.FirstOrDefault();

        RefreshUoxDictionaryCategories();
        RefreshClilocDuplicateFlags();

        StatusText = "Loaded " + ClilocEntries.Count + " dictionary entries from " + SelectedDictionaryFile.FileName + ".";
        OnPropertyChanged(nameof(FilteredClilocEntries));
    }

    private async Task SelectUoxDictionaryFolderAsync()
    {
        Window? mainWindow = GetMainWindow();

        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        IReadOnlyList<IStorageFolder> folders =
            await mainWindow.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "Select UOX3 Dictionary Folder",
                    AllowMultiple = false
                });

        if (folders.Count == 0)
        {
            StatusText = "UOX3 dictionary folder selection cancelled.";
            return;
        }

        string? path = folders[0].TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            StatusText = "Selected UOX3 dictionary folder is invalid.";
            return;
        }

        UoxDictionaryFolderPath = path;
        RefreshDictionaryFileOptions();

        StatusText = "Selected UOX3 dictionary folder: " + path;
    }

    private void SaveDictionary()
    {
        if (string.IsNullOrWhiteSpace(loadedDictionaryPath))
        {
            StatusText = "Load a dictionary file before saving.";
            return;
        }

        try
        {
            uoxDictionaryDataService.Save(loadedDictionaryPath, ClilocEntries);

            StatusText = "Saved " + ClilocEntries.Count + " dictionary entries to " + Path.GetFileName(loadedDictionaryPath) + ".";
            OnPropertyChanged(nameof(FilteredClilocEntries));
        }
        catch (Exception exception)
        {
            StatusText = "Dictionary save failed: " + exception.Message;
        }
    }

    private async Task ExportClilocAsync()
    {
        if (ClilocEntries.Count == 0)
        {
            StatusText = "Load cliloc or dictionary entries before exporting.";
            return;
        }

        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        bool isDictionary = !string.IsNullOrWhiteSpace(loadedDictionaryPath);

        string defaultName = isDictionary
            ? Path.GetFileNameWithoutExtension(loadedDictionaryPath) + ".txt"
            : Path.GetFileNameWithoutExtension(loadedClilocPath) + ".csv";

        IStorageFolder? suggestedFolder = null;

        if (activeProfile == null)
        {
            activeProfile = GetActiveProfile();
        }

        string outputFolder = activeProfile?.OutputFolderPath ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(outputFolder) && Directory.Exists(outputFolder))
        {
            suggestedFolder = await mainWindow.StorageProvider.TryGetFolderFromPathAsync(outputFolder);
        }

        IStorageFile? file = await mainWindow.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = isDictionary ? "Export Dictionary Text" : "Export Cliloc CSV",
                SuggestedFileName = defaultName,
                SuggestedStartLocation = suggestedFolder
            });

        if (file == null)
        {
            StatusText = "Export cancelled.";
            return;
        }

        string? path = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = "Export path is invalid.";
            return;
        }

        try
        {
            if (isDictionary)
            {
                ExportDictionaryText(path);
                StatusText = "Exported dictionary text to " + Path.GetFileName(path) + ".";
            }
            else
            {
                ExportClilocCsv(path);
                StatusText = "Exported cliloc CSV to " + Path.GetFileName(path) + ".";
            }
        }
        catch (Exception exception)
        {
            StatusText = "Export failed: " + exception.Message;
        }
    }

    private async Task ImportClilocAsync()
    {
        Window? mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusText = "Could not locate main window.";
            return;
        }

        bool isDictionary = !string.IsNullOrWhiteSpace(loadedDictionaryPath);

        IReadOnlyList<IStorageFile> files = await mainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = isDictionary ? "Import Dictionary Text" : "Import Cliloc CSV",
                AllowMultiple = false
            });

        if (files.Count == 0)
        {
            StatusText = "Import cancelled.";
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = "Import file is invalid.";
            return;
        }

        try
        {
            ClilocEntries.Clear();

            List<ClilocEntry> imported = isDictionary
                ? ImportDictionaryText(path)
                : ImportClilocCsv(path);

            foreach (ClilocEntry entry in imported)
            {
                ClilocEntries.Add(entry);
            }

            SelectedClilocEntry = ClilocEntries.FirstOrDefault();

            StatusText = "Imported " + ClilocEntries.Count + " entries from " + Path.GetFileName(path) + ". Save to write changes.";
            OnPropertyChanged(nameof(FilteredClilocEntries));
        }
        catch (Exception exception)
        {
            StatusText = "Import failed: " + exception.Message;
        }
    }

    private void ExportClilocCsv(string path)
    {
        List<string> lines = new()
    {
        "Number,Flag,Text"
    };

        foreach (ClilocEntry entry in ClilocEntries.OrderBy(x => x.Number))
        {
            lines.Add(
                CsvEscape(entry.Number.ToString()) + "," +
                CsvEscape(entry.Flag.ToString()) + "," +
                CsvEscape(entry.Text ?? string.Empty));
        }

        File.WriteAllLines(path, lines, Encoding.UTF8);
    }

    private List<ClilocEntry> ImportClilocCsv(string path)
    {
        List<ClilocEntry> result = new();
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        int startIndex = lines.Length > 0 && lines[0].StartsWith("Number,", StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;

        for (int i = startIndex; i < lines.Length; i++)
        {
            List<string> columns = ParseCsvLine(lines[i]);

            if (columns.Count < 3)
            {
                continue;
            }

            if (!int.TryParse(columns[0], out int number))
            {
                continue;
            }

            if (!byte.TryParse(columns[1], out byte flag))
            {
                flag = 0;
            }

            result.Add(new ClilocEntry
            {
                Number = number,
                Flag = flag,
                Text = columns[2],
                IsDirty = true
            });
        }

        return result.OrderBy(x => x.Number).ToList();
    }

    private void ExportDictionaryText(string path)
    {
        List<string> lines = new()
    {
        "[DICTIONARY]",
        "{"
    };

        foreach (ClilocEntry entry in ClilocEntries.OrderBy(x => x.Number))
        {
            lines.Add(entry.Number + "=" + (entry.Text ?? string.Empty));
        }

        lines.Add("}");

        File.WriteAllLines(path, lines, Encoding.UTF8);
    }

    private List<ClilocEntry> ImportDictionaryText(string path)
    {
        return uoxDictionaryDataService.Load(path)
            .Select(x =>
            {
                x.IsDirty = true;
                return x;
            })
            .ToList();
    }

    private static string CsvEscape(string value)
    {
        value ??= string.Empty;

        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    private static List<string> ParseCsvLine(string line)
    {
        List<string> result = new();
        StringBuilder current = new();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        result.Add(current.ToString());
        return result;
    }

    private void JumpToCliloc()
    {
        string text = (JumpToClilocText ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            StatusText = "Enter an ID to jump to.";
            return;
        }

        int number;

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out number))
            {
                StatusText = "Invalid hex ID.";
                return;
            }
        }
        else if (!int.TryParse(text, out number))
        {
            StatusText = "Invalid ID.";
            return;
        }

        ClilocEntry? entry = ClilocEntries.FirstOrDefault(x => x.Number == number);

        if (entry == null)
        {
            StatusText = "ID " + number + " was not found.";
            return;
        }

        SelectedClilocEntry = entry;
        StatusText = "Selected ID " + number + ".";
    }
}