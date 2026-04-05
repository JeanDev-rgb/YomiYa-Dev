using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace YomiYa.Converters;

public class ReadStatusToBrushConverter : IValueConverter
{
    // Define los colores para el estado leído y no leído
    private static readonly IBrush ReadBrush = new SolidColorBrush(Color.Parse("#888888"));
    private static readonly IBrush UnreadBrush = new SolidColorBrush(Color.Parse("#E0E0E0"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Si el valor es 'true', devuelve el pincel para texto leído (gris)
        if (value is bool isRead && isRead)
        {
            return ReadBrush;
        }
        // De lo contrario, devuelve el pincel para texto no leído (blanco)
        return UnreadBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}