using System.Text.Json.Serialization;
using YomiYa.Core.Localization;
using YomiYa.Domain.Enums;

namespace YomiYa.Core.Settings;

public class AppSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Language SelectedLanguage { get; set; } = LanguageHelper.CurrentLanguage;
    public string SelectedTheme { get; set; } = "dark.xml";
}