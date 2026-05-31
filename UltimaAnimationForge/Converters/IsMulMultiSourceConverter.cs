using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace UltimaAnimationForge.Converters;

public sealed class IsMulMultiSourceConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return value is string source &&
               source.Equals("multi.mul / multi.idx", StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}