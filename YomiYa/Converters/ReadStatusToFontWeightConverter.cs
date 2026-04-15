using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace YomiYa.Converters;

public class ReadStatusToFontWeightConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Si el valor es 'true', devuelve un grosor de fuente normal
        if (value is bool isRead && isRead) return FontWeight.Normal;
        // De lo contrario, devuelve un grosor de fuente semi-negrita
        return FontWeight.SemiBold;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}