using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using YomiYa.Core.Services;
using YomiYa.Core.Settings;
using YomiYa.Core.Theme;
using MainWindow = YomiYa.Features.Main.MainWindow;
using MainWindowViewModel = YomiYa.Features.Main.MainWindowViewModel;

namespace YomiYa;

public class App : Application
{
    public static GoogleDriveSyncService DriveService { get; } = new GoogleDriveSyncService();
    public static SyncManager SyncManager { get; } = new SyncManager(DriveService);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            ThemeManager.ApplyTheme(SettingsService.Settings.SelectedTheme);
            switch (ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktop:
                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = new MainWindowViewModel()
                    };
                    break;
                case ISingleViewApplicationLifetime singleView:
                    singleView.MainView = new MainWindow
                    {
                        DataContext = new MainWindowViewModel()
                    };
                    break;
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERROR DURANTE LA INICIALIZACIÓN DE LA APLICACIÓN: {e.Message}");
        }
    }
}