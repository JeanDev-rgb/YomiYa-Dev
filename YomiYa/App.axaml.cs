using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using YomiYa.Core.Settings;
using YomiYa.Core.Theme;
using YomiYa.Features.Main;

namespace YomiYa;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            // Resolvemos las dependencias desde el Program
            var settingsService = Program.ServiceProvider.GetRequiredService<ISettingsService>();
            var mainViewModel = Program.ServiceProvider.GetRequiredService<MainWindowViewModel>();

            // Aplicar tema
            ThemeManager.ApplyTheme(settingsService.Settings.SelectedTheme);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = new MainWindow
                {
                    DataContext = mainViewModel
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERROR EN INICIALIZACIÓN: {e.Message}");
        }
    }
}