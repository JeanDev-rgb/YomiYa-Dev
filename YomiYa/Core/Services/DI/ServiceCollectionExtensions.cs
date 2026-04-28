using Microsoft.Extensions.DependencyInjection;
using YomiYa.Core.Database;
using YomiYa.Core.Dialogs;
using YomiYa.Core.Settings;
using YomiYa.Features.Ad;
using YomiYa.Features.Discover;
using YomiYa.Features.Library;
using YomiYa.Features.Main;
using YomiYa.Features.Navigation;
using YomiYa.Features.Reader;
using YomiYa.Features.Settings;

namespace YomiYa.Core.Services.DI;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection services)
    {
        // Servicios de infraestructura (Singleton)
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IDialogService, DialogService>();

        // Servicios de lógica
        services.AddSingleton<GoogleDriveSyncService>();
        services.AddSingleton<SyncManager>();
        services.AddSingleton<MangaService>();
    }

    public static void AddViewModels(this IServiceCollection services)
    {
        // Los ViewModels suelen ser Transient (una instancia nueva cada vez)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SideBarMenuViewModel>();
        services.AddTransient<LibraryPageViewModel>();
        services.AddTransient<MorePageViewModel>();
        services.AddTransient<ReaderViewModel>();
        services.AddTransient<BrowsePageViewModel>();
        services.AddTransient<HistoryPageViewModel>();
        services.AddTransient<AdPageViewModel>();
    }
}