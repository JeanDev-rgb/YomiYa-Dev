using System.Globalization;
using System.Text.RegularExpressions;

namespace YomiYa.Utils;

public static class StringUtils
{
    public static string SubstringAfter(this string source, string delimiter)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(delimiter))
            return source;

        var index = source.IndexOf(delimiter, StringComparison.Ordinal);
        if (index == -1 || index + delimiter.Length >= source.Length)
            return string.Empty;

        return source[(index + delimiter.Length)..];
    }

    public static string SubstringBefore(this string source, string delimiter)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(delimiter))
            return source;

        var index = source.IndexOf(delimiter, StringComparison.Ordinal);
        return index == -1 ? source : source[..index];
    }

    /// <summary>
    /// Extrae el primer número flotante de una cadena de texto.
    /// </summary>
    /// <param name="source">La cadena de entrada.</param>
    /// <returns>El número flotante extraído, o null si no se encontró ninguno.</returns>
    public static float? ExtractFloat(this string source)
    {
        if (string.IsNullOrEmpty(source)) return null;

        var match = Regex.Match(source, @"[\d.]+");

        if (match.Success &&
            float.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

        return null;
    }
}