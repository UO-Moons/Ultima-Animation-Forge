using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MultiEditorViewModel : ObservableObject
{
    private bool isClearRoofPlacementMode;

    public ObservableCollection<string> RoofDirections { get; } = new()
{
    "East/West Gable",
    "North/South Gable"
};

    [ObservableProperty]
    private string selectedRoofDirection = "East/West Gable";

    private readonly MultiEditorRoofProfileService roofProfileService = new();

    public ObservableCollection<MultiEditorRoofProfile> RoofProfiles { get; } = new();

    private bool isRoofPlacementMode;

    [ObservableProperty]
    private MultiEditorRoofProfile? selectedRoofProfile;

    [ObservableProperty]
    private int roofWidth = 6;

    [ObservableProperty]
    private int roofLength = 8;

    [ObservableProperty]
    private int roofBaseZ;

    private bool isRectFillStarted;
    private int rectStartX;
    private int rectStartY;

    [ObservableProperty]
    private int visibleMaxZ = 127;

    [ObservableProperty]
    private string tilePickerSearchText = string.Empty;

    [ObservableProperty]
    private bool drawVirtualFloor = true;

    private readonly MultiEditorTileGroupService tileGroupService = new();
    public ObservableCollection<MultiEditorGridTile> GridTiles { get; } = new();

    public ObservableCollection<MultiEditorTileGroup> TileGroups { get; } = new();
    public ObservableCollection<MultiEditorTileSubgroup> TileSubgroups { get; } = new();
    public ObservableCollection<ArtEntry> TilePickerItems { get; } = new();

    [ObservableProperty]
    private MultiEditorTileGroup? selectedTileGroup;

    [ObservableProperty]
    private MultiEditorTileSubgroup? selectedTileSubgroup;

    public int MultiId { get; }

    public ObservableCollection<MultiEditorTile> Components { get; } = new();
    public ObservableCollection<MultiEditorTile> RenderedComponents { get; } = new();

    private readonly Stack<List<MultiComponentEntry>> undoStack = new();
    private readonly Stack<List<MultiComponentEntry>> redoStack = new();
    private const int MaxUndoCount = 50;

    private double renderMinX;
    private double renderMinY;
    private double renderOffsetX;
    private double renderOffsetY;

    partial void OnTilePickerSearchTextChanged(string value)
    {
        RebuildTilePickerItems();
    }

    private void RebuildRenderedComponents()
    {
        RenderedComponents.Clear();
        List<MultiEditorTile> renderList = Components
            .Where(x => GetEditorEffectiveZ(x) <= VisibleMaxZ)
            .ToList();

        if (DrawVirtualFloor && FloorZ <= VisibleMaxZ)
        {
            int minX = Components.Count > 0 ? Components.Min(x => x.X) - 2 : -5;
            int maxX = Components.Count > 0 ? Components.Max(x => x.X) + 2 : 5;
            int minY = Components.Count > 0 ? Components.Min(x => x.Y) - 2 : -5;
            int maxY = Components.Count > 0 ? Components.Max(x => x.Y) + 2 : 5;

            WriteableBitmap floorBitmap = GetVirtualFloorBitmap();

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    MultiEditorTile floor = new()
                    {
                        ItemId = 0,
                        X = x,
                        Y = y,
                        Z = FloorZ,
                        Flags = 1,
                        Bitmap = floorBitmap,
                        IsVirtualFloor = true
                    };

                    PositionTile(floor);
                    renderList.Add(floor);
                }
            }
        }

        foreach (MultiEditorTile tile in renderList
            .OrderBy(x => x.X + x.Y)
            .ThenBy(x => x.Z)
            .ThenBy(x => GetEditorTileThreshold(x))
            .ThenBy(x => x.X)
            .ThenBy(x => x.Y)
            .ThenBy(x => x.ItemId))
        {
            RenderedComponents.Add(tile);
        }
    }

    private int GetEditorEffectiveZ(MultiEditorTile tile)
    {
        if (tile.IsVirtualFloor)
        {
            return tile.Z;
        }

        TileDataEntry? entry = tileDataEntries.FirstOrDefault(x =>
            !x.IsLand &&
            x.Id == tile.ItemId);

        return tile.Z + (entry?.Height ?? 0);
    }

    private int GetEditorTileThreshold(MultiEditorTile tile)
    {
        if (tile.IsVirtualFloor)
        {
            return 0;
        }

        TileDataEntry? entry = tileDataEntries.FirstOrDefault(x =>
            !x.IsLand &&
            x.Id == tile.ItemId);

        int threshold = 0;

        if ((entry?.Height ?? 0) > 0)
        {
            threshold++;
        }

        const ulong BackgroundFlag = 1UL << 0;

        bool isBackground = entry != null &&
                            (entry.Flags & BackgroundFlag) != 0;

        if (!isBackground)
        {
            threshold++;
        }

        return threshold;
    }

    private void PositionTile(MultiEditorTile tile)
    {
        int width = tile.Bitmap?.PixelSize.Width ?? 44;
        int height = tile.Bitmap?.PixelSize.Height ?? 44;

        int px = (tile.X - tile.Y) * 22;
        int py = (tile.X + tile.Y) * 22;

        px -= width / 2;
        py -= tile.Z << 2;
        py -= height;

        tile.SetScreenPosition(px - renderMinX + renderOffsetX, py - renderMinY + renderOffsetY);
    }

    [ObservableProperty]
    private MultiEditorTile? selectedComponent;

    [ObservableProperty]
    private string statusText = "Multi editor ready.";

    [ObservableProperty]
    private int drawItemId;

    [ObservableProperty]
    private int drawZ;

    [ObservableProperty]
    private int floorZ;

    [ObservableProperty]
    private string selectedTool = "Select";

    public bool WasApplied { get; private set; }

    private readonly List<ArtEntry> artEntries;
    private readonly List<TileDataEntry> tileDataEntries;

    public MultiEditorViewModel(
        int multiId,
        List<MultiComponentEntry> sourceComponents,
        List<ArtEntry> artEntries,
        List<TileDataEntry> tileDataEntries)
    {
        MultiId = multiId;
        this.artEntries = artEntries;
        this.tileDataEntries = tileDataEntries;

        LoadTileGroups();
        LoadRoofProfiles();

        foreach (MultiComponentEntry part in sourceComponents)
        {
            Components.Add(new MultiEditorTile
            {
                ItemId = part.ItemId,
                X = part.X,
                Y = part.Y,
                Z = part.Z,
                Flags = part.Flags,
                Bitmap = GetArtBitmap(part.ItemId)
            });
        }

        RecalculateScreenPositions();

        StatusText = "Loaded multi 0x" + multiId.ToString("X4") + " components: " + Components.Count;
    }

    public List<MultiComponentEntry> ToComponents()
    {
        return Components
            .Where(x => !x.IsVirtualFloor)
            .Select(x => new MultiComponentEntry
            {
                ItemId = x.ItemId,
                X = (short)x.X,
                Y = (short)x.Y,
                Z = (short)x.Z,
                Flags = x.Flags,
                Unknown = 0
            })
            .ToList();
    }

    private WriteableBitmap? GetArtBitmap(int itemId)
    {
        return artEntries
            .FirstOrDefault(x =>
                x.ArtId == itemId &&
                !x.IsFreeSlot &&
                string.Equals(x.Type, "Static", StringComparison.OrdinalIgnoreCase))
            ?.Thumbnail;
    }

    partial void OnSelectedComponentChanged(MultiEditorTile? value)
    {
        if (value != null)
        {
            value.PropertyChanged += SelectedComponent_PropertyChanged;
        }
    }

    private void SelectedComponent_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MultiEditorTile.X) ||
            e.PropertyName == nameof(MultiEditorTile.Y) ||
            e.PropertyName == nameof(MultiEditorTile.Z))
        {
            RecalculateScreenPositions();
        }
    }

    [RelayCommand]
    private void UseSelectTool()
    {
        SelectedTool = "Select";
    }

    [RelayCommand]
    private void UseDrawTool()
    {
        SelectedTool = "Draw";
    }

    [RelayCommand]
    private void UseRemoveTool()
    {
        SelectedTool = "Remove";
    }

    [RelayCommand]
    private void AddTile()
    {
        if (DrawItemId <= 0)
        {
            StatusText = "Enter an item ID first.";
            return;
        }
        PushUndoSnapshot();

        Components.Add(new MultiEditorTile
        {
            ItemId = (ushort)DrawItemId,
            X = 0,
            Y = 0,
            Z = DrawZ,
            Flags = 1,
            Bitmap = GetArtBitmap(DrawItemId)
        });

        RecalculateScreenPositions();

        StatusText = "Added tile 0x" + DrawItemId.ToString("X4") + ".";
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedComponent == null)
        {
            return;
        }

        PushUndoSnapshot();

        Components.Remove(SelectedComponent);
        SelectedComponent = null;

        RecalculateScreenPositions();

        StatusText = "Removed selected tile.";
    }

    [RelayCommand]
    private void Apply()
    {
        WasApplied = true;
        StatusText = "Applied changes.";
    }

    partial void OnDrawVirtualFloorChanged(bool value)
    {
        RecalculateScreenPositions();
    }

    private void RecalculateScreenPositions()
    {
        if (Components.Count == 0)
        {
            return;
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (MultiEditorTile tile in Components)
        {
            int width = tile.Bitmap?.PixelSize.Width ?? 44;
            int height = tile.Bitmap?.PixelSize.Height ?? 44;

            int px = (tile.X - tile.Y) * 22;
            int py = (tile.X + tile.Y) * 22;

            px -= width / 2;
            py -= tile.Z << 2;
            py -= height;

            minX = Math.Min(minX, px);
            minY = Math.Min(minY, py);
            maxX = Math.Max(maxX, px + width);
            maxY = Math.Max(maxY, py + height);
        }

        double contentWidth = maxX - minX;
        double contentHeight = maxY - minY;

        renderMinX = minX;
        renderMinY = minY;

        renderOffsetX = Math.Max(40, (1200 - contentWidth) / 2.0);
        renderOffsetY = Math.Max(40, (900 - contentHeight) / 2.0);

        foreach (MultiEditorTile tile in Components)
        {
            PositionTile(tile);
        }

        RebuildRenderedComponents();
    }

    public void SelectTileFromCanvas(MultiEditorTile tile)
    {
        foreach (MultiEditorTile component in Components)
        {
            component.IsSelected = false;
        }

        tile.IsSelected = true;
        SelectedComponent = tile;
        SelectedTool = "Select";
        StatusText = "Selected tile 0x" + tile.ItemId.ToString("X4") +
                     " X:" + tile.X +
                     " Y:" + tile.Y +
                     " Z:" + tile.Z + ".";
    }

    public void HandleTileCanvasClick(MultiEditorTile tile)
    {
        if (isClearRoofPlacementMode || SelectedTool == "Clear Roof")
        {
            ClearRoofAreaAt(tile.X, tile.Y);
            isClearRoofPlacementMode = false;
            SelectedTool = "Select";
            return;
        }

        if (isRoofPlacementMode || SelectedTool == "Roof")
        {
            BuildRoofAt(tile.X, tile.Y);
            return;
        }

        if (SelectedTool == "Rect Fill")
        {
            HandleCanvasClick(tile.ScreenX + 22, tile.ScreenY + 22);
            return;
        }

        if (SelectedTool == "Draw")
        {
            PushUndoSnapshot();

            Components.Add(new MultiEditorTile
            {
                ItemId = (ushort)DrawItemId,
                X = tile.X,
                Y = tile.Y,
                Z = FloorZ,
                Flags = 1,
                Bitmap = GetArtBitmap(DrawItemId)
            });

            RecalculateScreenPositions();

            StatusText = "Drew tile 0x" + DrawItemId.ToString("X4") +
                         " on X:" + tile.X +
                         " Y:" + tile.Y +
                         " Z:" + FloorZ + ".";
            return;
        }

        if (SelectedTool == "Remove")
        {
            PushUndoSnapshot();

            Components.Remove(tile);

            if (SelectedComponent == tile)
            {
                SelectedComponent = null;
            }

            RecalculateScreenPositions();

            StatusText = "Removed tile 0x" + tile.ItemId.ToString("X4") + ".";
            return;
        }

        if (SelectedTool == "Pipette")
        {
            DrawItemId = tile.ItemId;
            DrawZ = tile.Z;
            SelectedTool = "Draw";

            StatusText = "Picked tile 0x" + tile.ItemId.ToString("X4") + ".";
            return;
        }

        SelectTileFromCanvas(tile);
    }

    public void HandleCanvasClick(double screenX, double screenY)
    {
        if (isClearRoofPlacementMode || SelectedTool == "Clear Roof")
        {
            var clearTile = ScreenToMultiTile(screenX, screenY, FloorZ);
            ClearRoofAreaAt(clearTile.X, clearTile.Y);
            isClearRoofPlacementMode = false;
            SelectedTool = "Select";
            return;
        }

        if (isRoofPlacementMode || SelectedTool == "Roof")
        {
            var roofTile = ScreenToMultiTile(screenX, screenY, FloorZ);
            BuildRoofAt(roofTile.X, roofTile.Y);
            return;
        }

        if (SelectedTool == "Rect Fill")
        {
            if (DrawItemId <= 0)
            {
                StatusText = "Enter an item ID first.";
                return;
            }

            var rectTile = ScreenToMultiTile(screenX, screenY, FloorZ);
            int rectTileX = rectTile.X;
            int rectTileY = rectTile.Y;

            if (!isRectFillStarted)
            {
                rectStartX = rectTileX;
                rectStartY = rectTileY;
                isRectFillStarted = true;

                StatusText = "Rect Fill: first corner X:" +
                             rectTileX +
                             " Y:" +
                             rectTileY +
                             ". Click second corner.";
                return;
            }

            PushUndoSnapshot();

            int minX = Math.Min(rectStartX, rectTileX);
            int maxX = Math.Max(rectStartX, rectTileX);
            int minY = Math.Min(rectStartY, rectTileY);
            int maxY = Math.Max(rectStartY, rectTileY);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Components.Add(new MultiEditorTile
                    {
                        ItemId = (ushort)DrawItemId,
                        X = x,
                        Y = y,
                        Z = FloorZ,
                        Flags = 1,
                        Bitmap = GetArtBitmap(DrawItemId)
                    });
                }
            }

            isRectFillStarted = false;
            RecalculateScreenPositions();

            StatusText = "Rect filled X:" + minX + "-" + maxX +
                         " Y:" + minY + "-" + maxY +
                         " Z:" + FloorZ + ".";
            return;
        }

        if (SelectedTool != "Draw")
        {
            return;
        }

        if (DrawItemId <= 0)
        {
            StatusText = "Enter an item ID first.";
            return;
        }

        PushUndoSnapshot();

        var drawTile = ScreenToMultiTile(screenX, screenY, FloorZ);
        int drawTileX = drawTile.X;
        int drawTileY = drawTile.Y;

        Components.Add(new MultiEditorTile
        {
            ItemId = (ushort)DrawItemId,
            X = drawTileX,
            Y = drawTileY,
            Z = FloorZ,
            Flags = 1,
            Bitmap = GetArtBitmap(DrawItemId)
        });

        RecalculateScreenPositions();

        StatusText = "Drew tile 0x" + DrawItemId.ToString("X4") +
                     " at X:" + drawTileX +
                     " Y:" + drawTileY +
                     " Z:" + FloorZ + ".";
    }

    [RelayCommand]
    private void UsePipetteTool()
    {
        SelectedTool = "Pipette";
    }

    private (int X, int Y) ScreenToMultiTile(double screenX, double screenY, int z)
    {
        double worldX = screenX + renderMinX - renderOffsetX;
        double worldY = screenY + renderMinY - renderOffsetY;

        worldY += z * 4;

        double a = worldX / 22.0;
        double b = worldY / 22.0;

        int tileX = (int)Math.Round((a + b) / 2.0);
        int tileY = (int)Math.Round((b - a) / 2.0);

        return (tileX, tileY);
    }

    [RelayCommand]
    private void MoveSelectedLeft()
    {
        PushUndoSnapshot();
        MoveSelected(-1, 0, 0);
    }

    [RelayCommand]
    private void MoveSelectedRight()
    {
        PushUndoSnapshot();
        MoveSelected(1, 0, 0);
    }

    [RelayCommand]
    private void MoveSelectedUp()
    {
        PushUndoSnapshot();
        MoveSelected(0, -1, 0);
    }

    [RelayCommand]
    private void MoveSelectedDown()
    {
        PushUndoSnapshot();
        MoveSelected(0, 1, 0);
    }

    [RelayCommand]
    private void MoveSelectedZUp()
    {
        PushUndoSnapshot();
        MoveSelected(0, 0, 1);
    }

    [RelayCommand]
    private void MoveSelectedZDown()
    {
        PushUndoSnapshot();
        MoveSelected(0, 0, -1);
    }

    private void MoveSelected(int dx, int dy, int dz)
    {
        if (SelectedComponent == null)
        {
            StatusText = "No tile selected.";
            return;
        }

        SelectedComponent.X += dx;
        SelectedComponent.Y += dy;
        SelectedComponent.Z += dz;

        RecalculateScreenPositions();

        StatusText =
            "Moved tile 0x" + SelectedComponent.ItemId.ToString("X4") +
            " to X:" + SelectedComponent.X +
            " Y:" + SelectedComponent.Y +
            " Z:" + SelectedComponent.Z + ".";
    }

    [RelayCommand]
    private void Undo()
    {
        if (undoStack.Count == 0)
        {
            StatusText = "Nothing to undo.";
            return;
        }

        redoStack.Push(ToComponents());

        List<MultiComponentEntry> snapshot = undoStack.Pop();
        RestoreSnapshot(snapshot);

        StatusText = "Undo.";
    }

    [RelayCommand]
    private void Redo()
    {
        if (redoStack.Count == 0)
        {
            StatusText = "Nothing to redo.";
            return;
        }

        undoStack.Push(ToComponents());

        List<MultiComponentEntry> snapshot = redoStack.Pop();
        RestoreSnapshot(snapshot);

        StatusText = "Redo.";
    }

    private void PushUndoSnapshot()
    {
        undoStack.Push(ToComponents());

        while (undoStack.Count > MaxUndoCount)
        {
            Stack<List<MultiComponentEntry>> temp = new(undoStack.Reverse().Take(MaxUndoCount).Reverse());
            undoStack.Clear();

            foreach (List<MultiComponentEntry> snapshot in temp)
            {
                undoStack.Push(snapshot);
            }

            break;
        }

        redoStack.Clear();
    }

    private void RestoreSnapshot(List<MultiComponentEntry> snapshot)
    {
        Components.Clear();

        foreach (MultiComponentEntry part in snapshot)
        {
            Components.Add(new MultiEditorTile
            {
                ItemId = part.ItemId,
                X = part.X,
                Y = part.Y,
                Z = part.Z,
                Flags = part.Flags,
                Bitmap = GetArtBitmap(part.ItemId)
            });
        }

        SelectedComponent = null;

        RecalculateScreenPositions();
    }

    private void LoadTileGroups()
    {
        string path = Path.Combine(AppContext.BaseDirectory,  "multi_editor_tile_groups.json");

        MultiEditorTileGroupConfig config = tileGroupService.Load(path);

        TileGroups.Clear();

        foreach (MultiEditorTileGroup group in config.Groups)
        {
            TileGroups.Add(group);
        }

        SelectedTileGroup = TileGroups.FirstOrDefault();
    }

    partial void OnSelectedTileGroupChanged(MultiEditorTileGroup? value)
    {
        TileSubgroups.Clear();
        TilePickerItems.Clear();

        if (value == null)
        {
            return;
        }

        foreach (MultiEditorTileSubgroup subgroup in value.Subgroups)
        {
            TileSubgroups.Add(subgroup);
        }

        SelectedTileSubgroup = TileSubgroups.FirstOrDefault();
    }

    partial void OnSelectedTileSubgroupChanged(MultiEditorTileSubgroup? value)
    {
        RebuildTilePickerItems();
    }

    private void RebuildTilePickerItems()
    {
        TilePickerItems.Clear();

        if (SelectedTileSubgroup == null)
        {
            return;
        }

        string search = TilePickerSearchText.Trim();

        foreach (int itemId in SelectedTileSubgroup.Items)
        {
            ArtEntry? entry = artEntries.FirstOrDefault(x =>
                x.ArtId == itemId &&
                !x.IsFreeSlot &&
                string.Equals(x.Type, "Static", StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                bool matches =
                    entry.ArtId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    ("0x" + entry.ArtId.ToString("X4")).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.DisplayText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.SecondaryText.Contains(search, StringComparison.OrdinalIgnoreCase);

                if (!matches)
                {
                    continue;
                }
            }

            TilePickerItems.Add(entry);
        }
    }

    [RelayCommand]
    private void SelectPickerTile(ArtEntry? entry)
    {
        if (entry == null)
        {
            return;
        }

        DrawItemId = entry.ArtId;
        SelectedTool = "Draw";
        StatusText = "Selected draw tile 0x" + entry.ArtId.ToString("X4") + ".";
    }

    [RelayCommand]
    private void DrawZDown()
    {
        DrawZ--;
        RecalculateScreenPositions();
        StatusText = "Draw Z: " + DrawZ;
    }

    [RelayCommand]
    private void DrawZUp()
    {
        DrawZ++;
        RecalculateScreenPositions();
        StatusText = "Draw Z: " + DrawZ;
    }

    [RelayCommand]
    private void FloorZDown()
    {
        FloorZ -= 5;
        DrawZ = FloorZ;
        RecalculateScreenPositions();
        StatusText = "Virtual floor Z: " + FloorZ;
    }

    [RelayCommand]
    private void FloorZUp()
    {
        FloorZ += 5;
        DrawZ = FloorZ;
        RecalculateScreenPositions();
        StatusText = "Virtual floor Z: " + FloorZ;
    }

    private void RebuildGridTiles()
    {
        GridTiles.Clear();

        if (!DrawVirtualFloor)
        {
            return;
        }

        int minX = Components.Count > 0 ? Components.Min(x => x.X) - 2 : -5;
        int maxX = Components.Count > 0 ? Components.Max(x => x.X) + 2 : 5;
        int minY = Components.Count > 0 ? Components.Min(x => x.Y) - 2 : -5;
        int maxY = Components.Count > 0 ? Components.Max(x => x.Y) + 2 : 5;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                int px = (x - y) * 22;
                int py = (x + y) * 22;

                py -= FloorZ * 4;

                GridTiles.Add(new MultiEditorGridTile
                {
                    X = x,
                    Y = y,
                    ScreenX = px - renderMinX + renderOffsetX - 22,
                    ScreenY = py - renderMinY + renderOffsetY - 11
                });
            }
        }
    }

    private WriteableBitmap? virtualFloorBitmap;

    private WriteableBitmap GetVirtualFloorBitmap()
    {
        if (virtualFloorBitmap != null)
        {
            return virtualFloorBitmap;
        }

        const int width = 44;
        const int height = 44;

        byte[] pixels = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double dx = Math.Abs(x - 22) / 22.0;
                double dy = Math.Abs(y - 22) / 22.0;

                if (dx + dy <= 1.0)
                {
                    int offset = ((y * width) + x) * 4;

                    pixels[offset + 0] = 32;
                    pixels[offset + 1] = 192;
                    pixels[offset + 2] = 32;
                    pixels[offset + 3] = 96;
                }
            }
        }

        WriteableBitmap bitmap = new(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);

        virtualFloorBitmap = bitmap;
        return bitmap;
    }

    [RelayCommand]
    private void UseRectFillTool()
    {
        SelectedTool = "Rect Fill";
        isRectFillStarted = false;
        StatusText = "Rect Fill: click first corner.";
    }

    [RelayCommand]
    private void ShowAllZ()
    {
        VisibleMaxZ = 127;
        RecalculateScreenPositions();
        StatusText = "Showing all Z levels.";
    }

    partial void OnVisibleMaxZChanged(int value)
    {
        RebuildRenderedComponents();
        StatusText = "Showing tiles up to Z: " + VisibleMaxZ;
    }

    private void LoadRoofProfiles()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "multi_editor_roof_profiles.json");

        MultiEditorRoofProfileConfig config = roofProfileService.Load(path);

        RoofProfiles.Clear();

        foreach (MultiEditorRoofProfile profile in config.RoofProfiles)
        {
            RoofProfiles.Add(profile);
        }

        SelectedRoofProfile = RoofProfiles.FirstOrDefault();

        if (SelectedRoofProfile != null)
        {
            StatusText = "Loaded roof profiles: " + RoofProfiles.Count;
        }
    }

    [RelayCommand]
    private void BuildRoof()
    {
        BuildRoofAt(0, 0);
    }

    [RelayCommand]
    private void PlaceRoofFromClick()
    {
        if (SelectedRoofProfile == null)
        {
            StatusText = "Select a roof profile first.";
            return;
        }

        isRoofPlacementMode = true;
        SelectedTool = "Roof";
        StatusText = "Roof placement: click the starting tile.";
    }

    private void BuildRoofAt(int startX, int startY)
    {
        if (SelectedRoofProfile == null)
        {
            StatusText = "Select a roof profile first.";
            return;
        }

        if (RoofWidth <= 0 || RoofLength <= 0)
        {
            StatusText = "Roof width and length must be greater than 0.";
            return;
        }

        PushUndoSnapshot();

        int baseZ = RoofBaseZ;
        int halfWidth = RoofWidth / 2;
        bool northSouth = SelectedRoofDirection == "North/South Gable";

        for (int lengthIndex = 0; lengthIndex < RoofLength; lengthIndex++)
        {
            for (int widthIndex = 0; widthIndex < RoofWidth; widthIndex++)
            {
                int itemId;
                int zOffset;

                int leftSlope = northSouth
                    ? SelectedRoofProfile.NorthSouthLeftSlope
                    : SelectedRoofProfile.EastWestLeftSlope;

                int rightSlope = northSouth
                    ? SelectedRoofProfile.NorthSouthRightSlope
                    : SelectedRoofProfile.EastWestRightSlope;

                int ridge = northSouth
                    ? SelectedRoofProfile.NorthSouthRidge
                    : SelectedRoofProfile.EastWestRidge;

                if (widthIndex < halfWidth)
                {
                    itemId = leftSlope;
                    zOffset = widthIndex;
                }
                else if (widthIndex > halfWidth)
                {
                    itemId = rightSlope;
                    zOffset = RoofWidth - widthIndex - 1;
                }
                else
                {
                    itemId = ridge;
                    zOffset = halfWidth;
                }

                int tileX;
                int tileY;

                if (northSouth)
                {
                    tileX = startX + lengthIndex;
                    tileY = startY + widthIndex;
                }
                else
                {
                    tileX = startX + widthIndex;
                    tileY = startY + lengthIndex;
                }

                Components.Add(new MultiEditorTile
                {
                    ItemId = (ushort)itemId,
                    X = tileX,
                    Y = tileY,
                    Z = baseZ + zOffset,
                    Flags = 1,
                    Bitmap = GetArtBitmap(itemId)
                });
            }
        }

        isRoofPlacementMode = false;
        SelectedTool = "Select";

        RecalculateScreenPositions();

        StatusText = "Built roof " + SelectedRoofProfile.Name +
                     " " + SelectedRoofDirection +
                     " at X:" + startX +
                     " Y:" + startY +
                     " Size:" + RoofWidth +
                     "x" + RoofLength +
                     " Z:" + RoofBaseZ + ".";
    }

    [RelayCommand]
    private void ClearRoofArea()
    {
        ClearRoofAreaAt(0, 0);
    }

    private void ClearRoofAreaAt(int startX, int startY)
    {
        if (RoofWidth <= 0 || RoofLength <= 0)
        {
            StatusText = "Roof width and length must be greater than 0.";
            return;
        }

        PushUndoSnapshot();

        bool northSouth = SelectedRoofDirection == "North/South Gable";

        int minX;
        int maxX;
        int minY;
        int maxY;

        if (northSouth)
        {
            minX = startX;
            maxX = startX + RoofLength - 1;
            minY = startY;
            maxY = startY + RoofWidth - 1;
        }
        else
        {
            minX = startX;
            maxX = startX + RoofWidth - 1;
            minY = startY;
            maxY = startY + RoofLength - 1;
        }

        int minZ = RoofBaseZ - 2;
        int maxZ = RoofBaseZ + RoofWidth + 4;

        var removeList = Components
            .Where(tile =>
                tile.X >= minX &&
                tile.X <= maxX &&
                tile.Y >= minY &&
                tile.Y <= maxY &&
                tile.Z >= minZ &&
                tile.Z <= maxZ)
            .ToList();

        foreach (var tile in removeList)
        {
            Components.Remove(tile);
        }

        int removed = removeList.Count;

        RecalculateScreenPositions();

        StatusText = "Cleared roof area. Removed: " + removed + ".";
    }

    [RelayCommand]
    private void ClearRoofFromClick()
    {
        isClearRoofPlacementMode = true;
        SelectedTool = "Clear Roof";
        StatusText = "Clear Roof: click starting tile.";
    }
}