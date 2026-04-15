using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using YomiYa.Core.Localization;

namespace YomiYa.Core.Settings;

/// <summary>
///     Gestiona la carga y guardado de la configuración de la aplicación en un archivo JSON.
///     Este servicio mantiene el estado de la configuración, que es accesible a través de la propiedad estática Settings.
/// </summary>
public static class SettingsService
{
    // Centralizamos la configuración de rutas y del serializador
    private static readonly string SettingsDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "app-settings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true, // Hace el archivo JSON legible para humanos
        Converters = { new JsonStringEnumConverter() }
    };

    // Un constructor estático se asegura de que el directorio exista antes de cualquier operación
    static SettingsService()
    {
        Directory.CreateDirectory(SettingsDirectory);
    }

    // El servicio ahora "posee" el objeto de configuración
    public static AppSettings Settings { get; private set; } = new();

    /// <summary>
    ///     Carga la configuración desde el archivo. Si no existe, crea uno con valores por defecto.
    ///     Este método es síncrono para facilitar su uso al inicio de la aplicación.
    /// </summary>
    public static void Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            Settings.SelectedLanguage = LanguageHelper.CurrentLanguage;
            Save(); // Si no hay archivo, creamos uno con los valores por defecto
            return;
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR AL CARGAR CONFIGURACIÓN: {ex.Message}. Se usarán los valores por defecto.");
            Settings = new AppSettings(); // En caso de error, usamos la configuración por defecto para evitar un crash
        }
    }

    /// <summary>
    ///     Guarda la configuración actual en el archivo de forma síncrona.
    /// </summary>
    public static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, SerializerOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR AL GUARDAR CONFIGURACIÓN: {ex.Message}");
        }
    }
}