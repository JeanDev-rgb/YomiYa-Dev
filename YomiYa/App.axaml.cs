using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using YomiYa.Core.Dialogs;
using YomiYa.Core.Services;
using YomiYa.Core.Services.DI;
using YomiYa.Core.Settings;
using YomiYa.Core.Theme;
using MainWindow = YomiYa.Features.Main.MainWindow;
using MainWindowViewModel = YomiYa.Features.Main.MainWindowViewModel;

namespace YomiYa;

public class App : Application
{
    public static GoogleDriveSyncService DriveService { get; } = new();
    public static SyncManager SyncManager { get; } = new(DriveService);
    public static IDialogService DialogService { get; } = new DialogService();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            var collection = new ServiceCollection();
            collection.AddCommonServices();

            var services = collection.BuildServiceProvider();
            var vm = services.GetRequiredService<MainWindowViewModel>();

            ThemeManager.ApplyTheme(SettingsService.Settings.SelectedTheme);
            switch (ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktop:
                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = vm
                    };
                    break;
                case ISingleViewApplicationLifetime singleView:
                    singleView.MainView = new MainWindow
                    {
                        DataContext = vm
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