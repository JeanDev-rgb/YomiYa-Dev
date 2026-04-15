using System.Text.Json.Serialization;
using YomiYa.Domain.Enums;

namespace YomiYa.Core.Settings;

public class AppSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Language SelectedLanguage { get; set; }

    public string SelectedTheme { get; set; } = "dark.xml";
}