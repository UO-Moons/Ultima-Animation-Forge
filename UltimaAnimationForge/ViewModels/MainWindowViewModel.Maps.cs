using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.Services;

namespace UltimaAnimationForge.ViewModels;

public partial class MainWindowViewModel
{
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

    [ObservableProperty]
    private bool mapUseAltitudeShading = true;

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

        BuildMapOptions();
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

    partial void OnMapUseAltitudeShadingChanged(bool value)
    {
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
            MapUseAltitudeShading,
MapShowStatics);

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
}