using System.Text.Json;

namespace YomiYa.Extensions.Es;

public class NovelCoolSettings
{
    // Puedes definir aquí qué idiomas vienen activados por defecto la primera vez
    public List<string> SelectedLanguages { get; set; } = new() { "es", "en" };

    // Ruta donde se guardará el JSON de configuración de este plugin
    private static readonly string SettingsFilePath = Path.Combine(AppContext.BaseDirectory, "Settings", "novelcool-settings.json");

    public void Save()
    {
        // WriteIndented hace que el JSON sea legible (con saltos de línea) si necesitas debugearlo
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        
        // Asegurarnos de que el directorio Plugins exista antes de guardar
        var directory = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(SettingsFilePath, json);
    }

    public static NovelCoolSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new NovelCoolSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<NovelCoolSettings>(json) ?? new NovelCoolSettings();
        }
        catch (JsonException)
        {
            // Si por alguna razón el archivo JSON se corrompe o queda vacío,
            // evitamos que el plugin crashee devolviendo una nueva instancia limpia.
            return new NovelCoolSettings();
        }
    }
}