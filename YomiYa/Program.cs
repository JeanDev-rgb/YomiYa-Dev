using System;
using Avalonia;
using YomiYa.Core.Database;
using YomiYa.Core.Localization;
using YomiYa.Core.Settings;
using YomiYa.Core.Theme;

namespace YomiYa;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Carga la configuración de forma síncrona
        SettingsService.Load();

        // Inicializa la base de datos
        DatabaseService.InitializeDatabase().GetAwaiter().GetResult();

        // Aplica el idioma guardado
        LanguageHelper.SetLanguage(SettingsService.Settings.SelectedLanguage);
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}