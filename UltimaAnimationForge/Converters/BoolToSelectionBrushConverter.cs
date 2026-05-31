using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace UltimaAnimationForge.Converters;

public sealed class BoolToSelectionBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool selected = value is bool boolValue && boolValue;

        return selected
            ? new SolidColorBrush(Color.Parse("#FFD166"))
            : new SolidColorBrush(Color.Parse("#66A6FF"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return false;
    }
}