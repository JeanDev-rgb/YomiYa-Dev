using System;
using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using YomiYa.Core.Database;
using YomiYa.Core.Localization;
using YomiYa.Core.Settings;
using YomiYa.Core.Services.DI; // Asegúrate de importar tus extensiones

namespace YomiYa;

internal static class Program
{
    // Propiedad global para acceder al contenedor desde App.axaml.cs
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        // 1. Configurar el contenedor de servicios
        var collection = new ServiceCollection();
        collection.AddCommonServices();
        collection.AddViewModels();

        ServiceProvider = collection.BuildServiceProvider();

        // 2. Obtener las instancias desde el contenedor (NO usar 'new')
        var settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
        var databaseService = ServiceProvider.GetRequiredService<IDatabaseService>();

        // 3. Inicialización lógica
        settingsService.Load();
        databaseService.InitializeDatabase().GetAwaiter().GetResult();

        // 4. Aplicar idioma usando la configuración cargada
        LanguageHelper.SetLanguage(settingsService.Settings.SelectedLanguage);

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}