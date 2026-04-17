using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace YomiYa.Extensions.Es;

public class NovelCoolSettings
{
    public List<string> SelectedLanguages { get; set; } = new() { "es" };

    private static readonly string SettingsFilePath = Path.Combine(AppContext.BaseDirectory, "Plugins", "novelcool-settings.json");

    public void Save()
    {
        var json = JsonSerializer.Serialize(this);
        File.WriteAllText(SettingsFilePath, json);
    }

    public static NovelCoolSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new NovelCoolSettings();
        }

        var json = File.ReadAllText(SettingsFilePath);
        return JsonSerializer.Deserialize<NovelCoolSettings>(json) ?? new NovelCoolSettings();
    }
}