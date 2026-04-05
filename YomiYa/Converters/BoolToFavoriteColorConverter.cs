using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace YomiYa.Converters;

public class BoolToFavoriteColorConverter : IValueConverter
{
    // Estos serán los colores que usaremos. Puedes personalizarlos.
    public IBrush TrueBrush { get; set; } = Brushes.Red;
    public IBrush FalseBrush { get; set; } = Brushes.Black;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Si el valor es un booleano y es 'true', devuelve el color para "favorito".
        if (value is bool isFavorite && isFavorite) return TrueBrush;
        // En cualquier otro caso, devuelve el color por defecto.
        return FalseBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}