using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace YomiYa.Converters;

public class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count and 0)
            return true; // Para IsVisible
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}