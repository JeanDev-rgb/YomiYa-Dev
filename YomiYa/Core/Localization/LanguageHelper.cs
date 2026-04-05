using System;
using System.Globalization;
using System.Resources;
using System.Threading;
using YomiYa.Domain.Enums;

namespace YomiYa.Core.Localization;

public static class LanguageHelper
{
    private static readonly ResourceManager ResourceManager =
        new("YomiYa.Assets.I18n.Strings", typeof(LanguageHelper).Assembly);

    public static Language CurrentLanguage
    {
        get
        {
            var culture = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
            return culture switch
            {
                "en" => Language.English,
                "es" => Language.Spanish,
                "fr" => Language.French,
                "ja" => Language.Japanese,
                "zh" => Language.Chinese,
                _ => Language.Spanish
            };
        }
    }

    public static event EventHandler LanguageChanged = null!;

    /// <summary>
    ///     Cambia el idioma de la aplicación.
    /// </summary>
    /// <param name="languageCode">Codigo del idioma, ejemplo: en, es, fr, etc.</param>
    public static void SetLanguage(Language language)
    {
        var langCode = language switch
        {
            Language.English => "en",
            Language.Spanish => "es",
            Language.French => "fr",
            Language.Japanese => "ja",
            Language.Chinese => "zh",
            _ => "es"
        };

        try
        {
            var culture = new CultureInfo(langCode);
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;

            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
        catch (CultureNotFoundException ex)
        {
            Console.WriteLine($"Idioma no soportado: {langCode}. Error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Recupera el texto localizado asociado con la clave especificada.
    /// </summary>
    /// <param name="key">La clave que identifica el texto localizado que se recuperará. No puede ser nula ni estar vacía.</param>
    /// <returns>
    ///     El texto localizado correspondiente a la clave especificada. Si no se encuentra la clave, devuelve una cadena con
    ///     el formato
    ///     <c>"[key]"</c>.
    /// </returns>
    public static string GetText(string key)
    {
        return ResourceManager.GetString(key) ?? $"[{key}]";
    }
}