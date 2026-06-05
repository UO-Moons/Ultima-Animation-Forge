using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{

    [ObservableProperty]
    private int pendingMapMarkerX;

    [ObservableProperty]
    private int pendingMapMarkerY;

    [ObservableProperty]
    private string pendingMapMarkerText = "No marker spot selected.";

    [ObservableProperty]
    private bool isPlacingMapMarker;

    public ObservableCollection<UoMapMarker> MapMarkers { get; } = new();

    [ObservableProperty]
    private UoMapMarker? selectedMapMarker;

    [ObservableProperty]
    private string newMapMarkerLabel = "Marker";

    public IRelayCommand AddMapMarkerCommand { get; private set; } = null!;
    public IRelayCommand RemoveMapMarkerCommand { get; private set; } = null!;
    public IRelayCommand GoToMapMarkerCommand { get; private set; } = null!;
    public IRelayCommand StartPlaceMapMarkerCommand { get; private set; } = null!;

    [ObservableProperty]
    private string mapTileDetailsText = "Hover or click the map to inspect a tile.";

    [ObservableProperty]
    private bool mapShowStatics = true;

    partial void OnMapShowStaticsChanged(bool value)
    {
        RefreshMapPreview();
    }

    private DispatcherTimer? mapRefreshDebounceTimer;
    private readonly UoMapDataService mapDataService = new();

    public ObservableCollection<UoMapOption> MapOptions { get; } = new();

    [ObservableProperty]
    private UoMapOption? selectedMapOption;

    [ObservableProperty]
    private WriteableBitmap? mapPreviewBitmap;

    [ObservableProperty]
    private string mapStatusText = "Open a UO folder, then load a map.";

    [ObservableProperty]
    private int mapViewX;

    [ObservableProperty]
    private int mapViewY;

    [ObservableProperty]
    private double mapZoom = 1.0;

    public ObservableCollection<UoMapAltitudeMode> MapAltitudeModes { get; } = new();
    public ObservableCollection<UoMapAltitudePreset> MapAltitudePresets { get; } = new();

    [ObservableProperty]
    private UoMapAltitudeMode selectedMapAltitudeMode = UoMapAltitudeMode.NormalWithAltitude;

    [ObservableProperty]
    private UoMapAltitudePreset selectedMapAltitudePreset = UoMapAltitudePreset.Normal;

    [ObservableProperty]
    private int mapAltitudeIntensity = 15;

    public IRelayCommand RefreshMapPreviewCommand { get; private set; } = null!;
    public IRelayCommand MapMoveUpCommand { get; private set; } = null!;
    public IRelayCommand MapMoveDownCommand { get; private set; } = null!;
    public IRelayCommand MapMoveLeftCommand { get; private set; } = null!;
    public IRelayCommand MapMoveRightCommand { get; private set; } = null!;

    private void InitializeMapsTab()
    {
        mapRefreshDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };

        mapRefreshDebounceTimer.Tick += (_, _) =>
        {
            mapRefreshDebounceTimer.Stop();
            RefreshMapPreview();
        };

        RefreshMapPreviewCommand = new RelayCommand(RefreshMapPreview);
        MapMoveUpCommand = new RelayCommand(() => MoveMapView(0, -256));
        MapMoveDownCommand = new RelayCommand(() => MoveMapView(0, 256));
        MapMoveLeftCommand = new RelayCommand(() => MoveMapView(-256, 0));
        MapMoveRightCommand = new RelayCommand(() => MoveMapView(256, 0));

        MapZoomFullCommand = new RelayCommand(() =>
        {
            MapZoom = 0.125;
            RefreshMapPreview();
        });

        MapZoomHalfCommand = new RelayCommand(() =>
        {
            MapZoom = 0.5;
            RefreshMapPreview();
        });

        MapZoomOneCommand = new RelayCommand(() =>
        {
            MapZoom = 1.0;
            RefreshMapPreview();
        });

        MapGoOriginCommand = new RelayCommand(() =>
        {
            MapViewX = 0;
            MapViewY = 0;
            RefreshMapPreview();
        });

        MapAltitudeModes.Clear();
        MapAltitudeModes.Add(UoMapAltitudeMode.Normal);
        MapAltitudeModes.Add(UoMapAltitudeMode.NormalWithAltitude);
        MapAltitudeModes.Add(UoMapAltitudeMode.AltitudeMap);

        MapAltitudePresets.Clear();
        MapAltitudePresets.Add(UoMapAltitudePreset.Sharp);
        MapAltitudePresets.Add(UoMapAltitudePreset.Normal);
        MapAltitudePresets.Add(UoMapAltitudePreset.Soft);

        AddMapMarkerCommand = new RelayCommand(AddMapMarker);
        RemoveMapMarkerCommand = new RelayCommand(RemoveMapMarker);
        GoToMapMarkerCommand = new RelayCommand(GoToMapMarker);

        StartPlaceMapMarkerCommand = new RelayCommand(() =>
        {
            IsPlacingMapMarker = true;
            PendingMapMarkerText = "Click a spot on the map for the marker.";
        });

        BuildMapOptions();
    }

    partial void OnSelectedMapAltitudeModeChanged(UoMapAltitudeMode value)
    {
        RefreshMapPreview();
    }

    partial void OnSelectedMapAltitudePresetChanged(UoMapAltitudePreset value)
    {
        RefreshMapPreview();
    }

    partial void OnMapAltitudeIntensityChanged(int value)
    {
        RefreshMapPreview();
    }

    private void BuildMapOptions()
    {
        MapOptions.Clear();

        MapOptions.Add(new UoMapOption { Name = "Felucca", FileIndex = 0, MapId = 0, Width = 6144, Height = 4096 });
        MapOptions.Add(new UoMapOption { Name = "Trammel", FileIndex = 1, MapId = 1, Width = 6144, Height = 4096 });
        MapOptions.Add(new UoMapOption { Name = "Ilshenar", FileIndex = 2, MapId = 2, Width = 2304, Height = 1600 });
        MapOptions.Add(new UoMapOption { Name = "Malas", FileIndex = 3, MapId = 3, Width = 2560, Height = 2048 });
        MapOptions.Add(new UoMapOption { Name = "Tokuno", FileIndex = 4, MapId = 4, Width = 1448, Height = 1448 });
        MapOptions.Add(new UoMapOption { Name = "Ter Mur", FileIndex = 5, MapId = 5, Width = 1280, Height = 4096 });

        SelectedMapOption = MapOptions.Count > 0 ? MapOptions[0] : null;
    }

    partial void OnSelectedMapOptionChanged(UoMapOption? value)
    {
        MapViewX = 0;
        MapViewY = 0;
        RefreshMapPreview();
    }

    private void MoveMapView(int dx, int dy)
    {
        if (SelectedMapOption == null)
        {
            return;
        }

        MapViewX = Math.Clamp(MapViewX + dx, 0, SelectedMapOption.Width - 1);
        MapViewY = Math.Clamp(MapViewY + dy, 0, SelectedMapOption.Height - 1);

        RefreshMapPreview();
    }

    private void RefreshMapPreview()
    {
        if (SelectedMapOption == null)
        {
            MapPreviewBitmap = null;
            MapStatusText = "No map selected.";
            return;
        }

        string folderPath = activeProfile?.UoFolderPath ?? string.Empty;

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            MapPreviewBitmap = null;
            MapStatusText = "Open a valid UO folder first.";
            return;
        }

        double safeZoom = MapZoom <= 0 ? 1.0 : MapZoom;

        const int outputWidth = 1024;
        const int outputHeight = 768;

        int worldWidth = (int)(outputWidth / safeZoom);
        int worldHeight = (int)(outputHeight / safeZoom);

        UoMapRenderResult result = mapDataService.RenderMapArea(
            folderPath,
            SelectedMapOption,
            MapViewX,
            MapViewY,
            worldWidth,
            worldHeight,
            outputWidth,
            outputHeight,
SelectedMapAltitudeMode,
SelectedMapAltitudePreset,
MapAltitudeIntensity,
MapShowStatics,
MapMarkers.Where(x => x.MapId == SelectedMapOption.MapId).ToList());

        MapPreviewBitmap = result.Bitmap;
        MapStatusText = result.Message;
    }

    public IRelayCommand MapZoomFullCommand { get; private set; } = null!;
    public IRelayCommand MapZoomHalfCommand { get; private set; } = null!;
    public IRelayCommand MapZoomOneCommand { get; private set; } = null!;
    public IRelayCommand MapGoOriginCommand { get; private set; } = null!;

    partial void OnMapZoomChanged(double value)
    {
        mapRefreshDebounceTimer?.Stop();
        mapRefreshDebounceTimer?.Start();
    }

    partial void OnMapViewXChanged(int value)
    {
        mapRefreshDebounceTimer?.Stop();
        mapRefreshDebounceTimer?.Start();
    }

    partial void OnMapViewYChanged(int value)
    {
        mapRefreshDebounceTimer?.Stop();
        mapRefreshDebounceTimer?.Start();
    }

    public void NavigateMapFromPreview(
    double previewX,
    double previewY,
    double previewWidth,
    double previewHeight)
    {
        if (SelectedMapOption == null || previewWidth <= 0 || previewHeight <= 0)
        {
            return;
        }

        double safeZoom = MapZoom <= 0 ? 1.0 : MapZoom;

        const int outputWidth = 1024;
        const int outputHeight = 768;

        int worldWidth = (int)(outputWidth / safeZoom);
        int worldHeight = (int)(outputHeight / safeZoom);

        double percentX = previewX / previewWidth;
        double percentY = previewY / previewHeight;

        int clickedWorldX = MapViewX + (int)(worldWidth * percentX);
        int clickedWorldY = MapViewY + (int)(worldHeight * percentY);

        MapViewX = Math.Clamp(clickedWorldX - (worldWidth / 2), 0, Math.Max(0, SelectedMapOption.Width - worldWidth));
        MapViewY = Math.Clamp(clickedWorldY - (worldHeight / 2), 0, Math.Max(0, SelectedMapOption.Height - worldHeight));

        RefreshMapPreview();
    }

    public void ZoomMapFromWheel(
        double wheelDelta,
        double previewX,
        double previewY,
        double previewWidth,
        double previewHeight)
    {
        if (SelectedMapOption == null || previewWidth <= 0 || previewHeight <= 0)
        {
            return;
        }

        double oldZoom = MapZoom <= 0 ? 1.0 : MapZoom;

        const int outputWidth = 1024;
        const int outputHeight = 768;

        int oldWorldWidth = (int)(outputWidth / oldZoom);
        int oldWorldHeight = (int)(outputHeight / oldZoom);

        double percentX = previewX / previewWidth;
        double percentY = previewY / previewHeight;

        int anchorWorldX = MapViewX + (int)(oldWorldWidth * percentX);
        int anchorWorldY = MapViewY + (int)(oldWorldHeight * percentY);

        double newZoom = wheelDelta > 0
            ? oldZoom * 1.25
            : oldZoom / 1.25;

        newZoom = Math.Clamp(newZoom, 0.125, 4.0);

        MapZoom = newZoom;

        int newWorldWidth = (int)(outputWidth / newZoom);
        int newWorldHeight = (int)(outputHeight / newZoom);

        MapViewX = Math.Clamp(anchorWorldX - (int)(newWorldWidth * percentX), 0, Math.Max(0, SelectedMapOption.Width - newWorldWidth));
        MapViewY = Math.Clamp(anchorWorldY - (int)(newWorldHeight * percentY), 0, Math.Max(0, SelectedMapOption.Height - newWorldHeight));

        RefreshMapPreview();
    }

    public void InspectMapFromPreview(
    double previewX,
    double previewY,
    double previewWidth,
    double previewHeight)
    {
        if (SelectedMapOption == null || previewWidth <= 0 || previewHeight <= 0)
        {
            return;
        }

        string folderPath = activeProfile?.UoFolderPath ?? string.Empty;

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            MapTileDetailsText = "Open a valid UO folder first.";
            return;
        }

        double safeZoom = MapZoom <= 0 ? 1.0 : MapZoom;

        const int outputWidth = 1024;
        const int outputHeight = 768;

        int worldWidth = (int)(outputWidth / safeZoom);
        int worldHeight = (int)(outputHeight / safeZoom);

        double percentX = previewX / previewWidth;
        double percentY = previewY / previewHeight;

        int worldX = MapViewX + (int)(worldWidth * percentX);
        int worldY = MapViewY + (int)(worldHeight * percentY);

        worldX = Math.Clamp(worldX, 0, SelectedMapOption.Width - 1);
        worldY = Math.Clamp(worldY, 0, SelectedMapOption.Height - 1);

        UoMapTileDetails? details = mapDataService.GetTileDetails(
            folderPath,
            SelectedMapOption,
            worldX,
            worldY);

        if (details == null)
        {
            MapTileDetailsText = $"X: {worldX}\nY: {worldY}\nNo tile details found.";
            return;
        }

        string text = details.DisplayText;

        if (details.Statics.Count > 0)
        {
            text += "\n\nStatics:";
            foreach (UoMapStaticDetails staticTile in details.Statics.Take(12))
            {
                text += "\n" + staticTile.DisplayText;
            }
        }

        MapTileDetailsText = text;
    }

    private void AddMapMarker()
    {
        if (SelectedMapOption == null)
        {
            return;
        }

        UoMapMarker marker = new()
        {
            Label = string.IsNullOrWhiteSpace(NewMapMarkerLabel) ? "Marker" : NewMapMarkerLabel.Trim(),
            X = PendingMapMarkerX,
            Y = PendingMapMarkerY,
            MapId = SelectedMapOption.MapId
        };

        MapMarkers.Add(marker);
        SelectedMapMarker = marker;

        NewMapMarkerLabel = "Marker";
        IsPlacingMapMarker = false;
        PendingMapMarkerText = $"Selected marker spot: {PendingMapMarkerX}, {PendingMapMarkerY}";

        RefreshMapPreview();
    }

    public void SelectMarkerSpotFromPreview(
    double previewX,
    double previewY,
    double previewWidth,
    double previewHeight)
    {
        if (SelectedMapOption == null || previewWidth <= 0 || previewHeight <= 0)
        {
            return;
        }

        double safeZoom = MapZoom <= 0 ? 1.0 : MapZoom;

        const int outputWidth = 1024;
        const int outputHeight = 768;

        int worldWidth = (int)(outputWidth / safeZoom);
        int worldHeight = (int)(outputHeight / safeZoom);

        double percentX = previewX / previewWidth;
        double percentY = previewY / previewHeight;

        int worldX = MapViewX + (int)(worldWidth * percentX);
        int worldY = MapViewY + (int)(worldHeight * percentY);

        PendingMapMarkerX = Math.Clamp(worldX, 0, SelectedMapOption.Width - 1);
        PendingMapMarkerY = Math.Clamp(worldY, 0, SelectedMapOption.Height - 1);

        PendingMapMarkerText = $"Selected marker spot: {PendingMapMarkerX}, {PendingMapMarkerY}";
        IsPlacingMapMarker = false;
    }

    private void RemoveMapMarker()
    {
        if (SelectedMapMarker == null)
        {
            return;
        }

        MapMarkers.Remove(SelectedMapMarker);
        SelectedMapMarker = null;

        RefreshMapPreview();
    }

    private void GoToMapMarker()
    {
        if (SelectedMapOption == null || SelectedMapMarker == null)
        {
            return;
        }

        double safeZoom = MapZoom <= 0 ? 1.0 : MapZoom;

        const int outputWidth = 1024;
        const int outputHeight = 768;

        int worldWidth = (int)(outputWidth / safeZoom);
        int worldHeight = (int)(outputHeight / safeZoom);

        MapViewX = Math.Clamp(
            SelectedMapMarker.X - (worldWidth / 2),
            0,
            Math.Max(0, SelectedMapOption.Width - worldWidth));

        MapViewY = Math.Clamp(
            SelectedMapMarker.Y - (worldHeight / 2),
            0,
            Math.Max(0, SelectedMapOption.Height - worldHeight));

        RefreshMapPreview();
    }
}