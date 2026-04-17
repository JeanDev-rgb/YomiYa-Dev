using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace YomiYa.Converters;

public class TypeToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter as Type == null)
        {
            return false;
        }

        return ((Type)parameter).IsInstanceOfType(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}