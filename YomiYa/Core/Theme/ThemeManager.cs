using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace YomiYa.Core.Theme;

public static class ThemeManager
{
    private static readonly Assembly AppAssembly = Assembly.GetExecutingAssembly();
    private static readonly string ResourcePrefix = $"{AppAssembly.GetName().Name}.Assets.Themes.";

    // Hacemos pública la ruta para que sea accesible desde el ViewModel
    public static readonly string ExternalThemesDirectory = Path.Combine(AppContext.BaseDirectory, "Themes");

    static ThemeManager()
    {
        ReloadAvailableThemes(); // Cambiamos el nombre de la llamada inicial
    }

    public static Dictionary<string, (string FilePath, ThemeVariant Variant, bool IsExternal)>
        AvailableThemes { get; } = new();

    // Hacemos el método público y le cambiamos el nombre para que sea más claro
    public static void ReloadAvailableThemes()
    {
        AvailableThemes.Clear();

        // 1. Cargar temas incrustados (predeterminados)
        var resourceNames = AppAssembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix) && name.EndsWith(".xml"));

        foreach (var resourceName in resourceNames)
            try
            {
                using var stream = AppAssembly.GetManifestResourceStream(resourceName);
                if (stream == null) continue;

                var doc = XDocument.Load(stream);
                var themeName = doc.Root?.Attribute("Name")?.Value;
                if (string.IsNullOrEmpty(themeName)) continue;

                var fileName = resourceName.Substring(ResourcePrefix.Length);
                var variant = GetVariantFromString(doc.Root?.Attribute("Variant")?.Value);

                AvailableThemes[themeName] = (fileName, variant, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cargar el tema incrustado {resourceName}: {ex.Message}");
            }

        // 2. Cargar temas externos (del usuario)
        if (!Directory.Exists(ExternalThemesDirectory)) Directory.CreateDirectory(ExternalThemesDirectory);

        var externalThemeFiles = Directory.GetFiles(ExternalThemesDirectory, "*.xml");
        foreach (var file in externalThemeFiles)
            try
            {
                var doc = XDocument.Load(file);
                var themeName = doc.Root?.Attribute("Name")?.Value;
                if (string.IsNullOrEmpty(themeName)) continue;

                var variant = GetVariantFromString(doc.Root?.Attribute("Variant")?.Value);

                AvailableThemes[themeName] = (file, variant, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cargar el tema externo {file}: {ex.Message}");
            }
    }

    public static void ApplyTheme(string themeFilePathOrName)
    {
        if (Application.Current is null || string.IsNullOrEmpty(themeFilePathOrName))
        {
            ApplyDefaultTheme();
            return;
        }

        try
        {
            XDocument doc;
            var isExternal = File.Exists(themeFilePathOrName);

            if (isExternal)
            {
                doc = XDocument.Load(themeFilePathOrName);
            }
            else
            {
                var fullResourceName = ResourcePrefix + themeFilePathOrName;
                using var stream = AppAssembly.GetManifestResourceStream(fullResourceName);
                if (stream == null)
                {
                    Console.WriteLine($"El recurso '{fullResourceName}' no se encontró.");
                    ApplyDefaultTheme();
                    return;
                }

                doc = XDocument.Load(stream);
            }

            var variant = GetVariantFromString(doc.Root?.Attribute("Variant")?.Value);
            Application.Current.RequestedThemeVariant = variant;

            foreach (var colorElement in doc.Descendants("Color"))
            {
                var key = colorElement.Attribute("Key")?.Value;
                var colorHex = colorElement.Value;
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(colorHex))
                    Application.Current.Resources[key] = Color.Parse(colorHex);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al aplicar el tema '{themeFilePathOrName}': {ex.Message}");
        }
    }

    private static void ApplyDefaultTheme()
    {
        var darkTheme = AvailableThemes.Values.FirstOrDefault(v => v.FilePath.EndsWith("dark.xml"));
        if (!string.IsNullOrEmpty(darkTheme.FilePath)) ApplyTheme(darkTheme.FilePath);
    }

    private static ThemeVariant GetVariantFromString(string? variantStr)
    {
        return (variantStr ?? "Dark").Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }
}