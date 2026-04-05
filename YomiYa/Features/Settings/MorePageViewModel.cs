using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Core.Localization;
using YomiYa.Core.Settings;
using YomiYa.Core.Theme;
using YomiYa.Domain.Enums;
using YomiYa.Utils;

namespace YomiYa.Features.Settings;

public partial class MorePageViewModel : ViewModelBase
{
    #region Constructor

    public MorePageViewModel()
    {
        AvailableThemes = new ObservableCollection<string>(ThemeManager.AvailableThemes.Keys);

        var savedThemePath = SettingsService.Settings.SelectedTheme;

        // Encuentra el nombre del tema (la clave del diccionario) basándose en la ruta guardada.
        // Esto funciona tanto para temas incrustados (nombre de archivo) como externos (ruta completa).
        SelectedTheme = ThemeManager.AvailableThemes
            .FirstOrDefault(t => t.Value.FilePath == savedThemePath)
            .Key ?? "Oscuro"; // Si no se encuentra, selecciona "Oscuro" por defecto.

        SelectedLanguage = SettingsService.Settings.SelectedLanguage;

        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SelectedLanguage))
            {
                LanguageHelper.SetLanguage(SelectedLanguage);
                SettingsService.Settings.SelectedLanguage = SelectedLanguage;
                SettingsService.Save();
            }
            else if (e.PropertyName == nameof(SelectedTheme) && !string.IsNullOrEmpty(SelectedTheme))
            {
                // Asegurarse de que el tema seleccionado existe en el diccionario
                if (ThemeManager.AvailableThemes.TryGetValue(SelectedTheme, out var themeInfo))
                {
                    // Aplicar y guardar usando la ruta del archivo (FilePath)
                    ThemeManager.ApplyTheme(themeInfo.FilePath);
                    SettingsService.Settings.SelectedTheme = themeInfo.FilePath;
                    SettingsService.Save();
                }
            }
        };
    }

    #endregion

    #region Methods

    protected override void UpdateLocalizedTexts()
    {
        AdditionalSettingsText = LanguageHelper.GetText("AdditionalSettings");
        ApplicationLanguageText = LanguageHelper.GetText("ApplicationLanguage");
        HostText = LanguageHelper.GetText("Host");
        ManualProxyConfigurationText = LanguageHelper.GetText("ManualProxyConfiguration");
        PasswordText = LanguageHelper.GetText("Password");
        PortText = LanguageHelper.GetText("Port");
        ResetText = LanguageHelper.GetText("Reset");
        SaveText = LanguageHelper.GetText("Save");
        UsernameOptionalText = LanguageHelper.GetText("UsernameOptional");
    }

    #endregion

    #region Properties

    public ObservableCollection<string> AvailableThemes { get; }
    public ObservableCollection<Language> Languages { get; } = new(Enum.GetValues<Language>());

    [ObservableProperty] private string _selectedTheme;
    [ObservableProperty] private Language _selectedLanguage;

    // Propiedades para la configuración del proxy
    [ObservableProperty] private string _proxyHost = string.Empty;
    [ObservableProperty] private int _proxyPort;
    [ObservableProperty] private string _proxyUsername = string.Empty;
    [ObservableProperty] private string _proxyPassword = string.Empty;

    [ObservableProperty] private string _additionalSettingsText = LanguageHelper.GetText("AdditionalSettings");
    [ObservableProperty] private string _applicationLanguageText = LanguageHelper.GetText("ApplicationLanguage");
    [ObservableProperty] private string _hostText = LanguageHelper.GetText("Host");

    [ObservableProperty]
    private string _manualProxyConfigurationText = LanguageHelper.GetText("ManualProxyConfiguration");

    [ObservableProperty] private string _passwordText = LanguageHelper.GetText("Password");
    [ObservableProperty] private string _portText = LanguageHelper.GetText("Port");
    [ObservableProperty] private string _resetText = LanguageHelper.GetText("Reset");
    [ObservableProperty] private string _saveText = LanguageHelper.GetText("Save");
    [ObservableProperty] private string _usernameOptionalText = LanguageHelper.GetText("UsernameOptional");

    #endregion

    #region Commands

    [RelayCommand]
private async Task ImportThemeAsync()
{
    // Obtener la ventana principal para mostrar el diálogo de archivo
    if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null)
    {
        return;
    }

    // --- CORRECCIÓN 1: Guardamos el tema seleccionado actualmente ---
    var previouslySelectedTheme = SelectedTheme;

    // Configurar el selector de archivos para que solo muestre archivos XML
    var filePickerOptions = new FilePickerOpenOptions
    {
        Title = "Importar Temas",
        AllowMultiple = true, // Permitir seleccionar varios temas a la vez
        FileTypeFilter = new[] { new FilePickerFileType("Archivos de Tema XML") { Patterns = new[] { "*.xml" } } }
    };

    // Mostrar el diálogo
    var selectedFiles = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(filePickerOptions);

    if (selectedFiles.Count == 0)
    {
        return; // El usuario no seleccionó nada
    }

    foreach (var file in selectedFiles)
    {
        try
        {
            var destinationPath = Path.Combine(ThemeManager.ExternalThemesDirectory, file.Name);
            
            // Copiar el archivo seleccionado a la carpeta de temas del usuario
            await using var sourceStream = await file.OpenReadAsync();
            await using var destinationStream = File.Create(destinationPath);
            await sourceStream.CopyToAsync(destinationStream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al importar el tema '{file.Name}': {ex.Message}");
        }
    }
    
    // Recargar la lista de temas para que incluya los nuevos
    ThemeManager.ReloadAvailableThemes();
    
    // Actualizar la lista desplegable en la interfaz
    AvailableThemes.Clear();
    foreach (var themeName in ThemeManager.AvailableThemes.Keys)
    {
        AvailableThemes.Add(themeName);
    }
    
    // --- CORRECCIÓN 2: Restauramos la selección anterior ---
    // Si el tema anterior todavía existe, lo volvemos a seleccionar.
    if (!string.IsNullOrEmpty(previouslySelectedTheme) && AvailableThemes.Contains(previouslySelectedTheme))
    {
        SelectedTheme = previouslySelectedTheme;
    }
}
    [RelayCommand]
    private void SaveProxy()
    {
        if (string.IsNullOrWhiteSpace(ProxyHost) || ProxyPort <= 0) return;

        var proxyUri = new Uri($"http://{ProxyHost}:{ProxyPort}");
        var proxy = new WebProxy(proxyUri);

        if (!string.IsNullOrWhiteSpace(ProxyUsername) && !string.IsNullOrWhiteSpace(ProxyPassword))
            proxy.Credentials = new NetworkCredential(ProxyUsername, ProxyPassword);

        GlobalProxy.Proxy = proxy;
    }

    [RelayCommand]
    private void ResetProxy()
    {
        ProxyHost = string.Empty;
        ProxyPort = 0;
        ProxyUsername = string.Empty;
        ProxyPassword = string.Empty;
        GlobalProxy.Proxy = null;
    }

    #endregion
}