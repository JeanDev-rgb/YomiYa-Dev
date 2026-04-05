using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace YomiYa.Converters;

public class EnumToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;

        var enumValue = value.ToString();
        var targetValue = parameter.ToString();

        return enumValue?.Equals(targetValue, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is not string enumString) return BindingOperations.DoNothing;
        try
        {
            return Enum.Parse(targetType, enumString, true);
        }
        catch (ArgumentException)
        {
            return BindingOperations.DoNothing;
        }
    }
}