using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace UltimaAnimationForge.Models;

public enum GumpBuilderElementType
{
    Background = 0,
    Image = 1,
    Button = 2,
    Text = 3,
    Html = 4,
    TextEntry = 5,
    Checkbox = 6,
    Radio = 7,
    TiledImage = 8,
    Item = 9,
    Tooltip = 10,
    ButtonTileArt = 11,
    CheckerTrans = 12,
    GroupStart = 13,
    GroupEnd = 14,
    Page = 15,
    ItemProperty = 16,
    ClilocToolTip = 17,
    PicInPic = 18,
    PageButton = 19,
    CroppedText = 20,
    XmfHtml = 21,
    XmfHtmlColor = 22,
    XmfHtmlTok = 23
}

public partial class GumpBuilderElement : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public List<WriteableBitmap> BackgroundParts { get; set; } = new();

    [ObservableProperty]
    private int textId;

    [ObservableProperty]
    private int maxLength;

    [ObservableProperty]
    private bool limitedTextEntry;

    [ObservableProperty]
    private int spriteX;

    [ObservableProperty]
    private int spriteY;

    [ObservableProperty]
    private bool hasBackground;

    [ObservableProperty]
    private bool hasScrollbar;

    [ObservableProperty]
    private bool hasBorder;

    [ObservableProperty]
    private int rgbColour;

    [ObservableProperty]
    private string clilocArg1 = string.Empty;

    [ObservableProperty]
    private string clilocArg2 = string.Empty;

    [ObservableProperty]
    private string clilocArg3 = string.Empty;

    [ObservableProperty]
    private WriteableBitmap? tileSourceBitmap;

    [ObservableProperty]
    private int pageNumber = 1;

    [ObservableProperty]
    private string itemPropertyObject = "myItem";

    [ObservableProperty]
    private int clilocNumber = 1015094;

    [ObservableProperty]
    private int groupNumber = 1;

    [ObservableProperty]
    private WriteableBitmap? pressedBitmap;

    [ObservableProperty]
    private WriteableBitmap? sourceBitmap;

    [ObservableProperty]
    private WriteableBitmap? bitmap;

    public bool HasBitmap => Bitmap != null;

    [ObservableProperty]
    private GumpBuilderElementType type;

    [ObservableProperty]
    private string name = "Element";

    [ObservableProperty]
    private int x;

    [ObservableProperty]
    private int y;

    [ObservableProperty]
    private int z;

    [ObservableProperty]
    private int width = 100;

    [ObservableProperty]
    private int height = 40;

    [ObservableProperty]
    private int gumpId;

    [ObservableProperty]
    private int pressedGumpId;

    [ObservableProperty]
    private int buttonId;

    [ObservableProperty]
    private int hue;

    [ObservableProperty]
    private int font;

    [ObservableProperty]
    private string text = string.Empty;

    [ObservableProperty]
    private bool isSelected;

    public string DisplayText =>
        Type + " | X " + X + " Y " + Y + " Z " + Z;

    [ObservableProperty]
    private int tileId;

    [ObservableProperty]
    private int tileX = 10;

    [ObservableProperty]
    private int tileY = 10;

    [ObservableProperty]
    private bool defaultStatus;

    partial void OnTypeChanged(GumpBuilderElementType value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnXChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnYChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnBitmapChanged(WriteableBitmap? value)
    {
        OnPropertyChanged(nameof(HasBitmap));
    }

    partial void OnWidthChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnHeightChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnZChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }
}