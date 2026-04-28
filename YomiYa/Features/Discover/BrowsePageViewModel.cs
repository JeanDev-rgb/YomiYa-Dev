using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using YomiYa.Core.Common;
using YomiYa.Core.IO;
using YomiYa.Core.Localization;
using YomiYa.Core.Navigation;
using YomiYa.Core.Plugins;
using YomiYa.Core.Services;
using YomiYa.Helper.Input.Interfaces;
using YomiYa.Source.Online;

namespace YomiYa.Features.Discover;

public partial class BrowsePageViewModel : ViewModelBase, ISearchableByKeyboard
{
    // Dependencias inyectadas
    private readonly IServiceProvider _serviceProvider;
    private readonly MangaService _mangaService;

    #region Constructor

    public BrowsePageViewModel(IServiceProvider serviceProvider, MangaService mangaService)
    {
        _serviceProvider = serviceProvider;
        _mangaService = mangaService;

        LoadPlugins();
        LocalizedTexts();

        PluginManager.OnPluginsChanged += LoadPlugins;
    }

    #endregion

    public IRelayCommand SearchCommand => SearchMangaCommand;

    #region Properties

    [ObservableProperty] private string? _title;
    [ObservableProperty] private string? _refreshButtonText;
    [ObservableProperty] private string? _installPluginsButtonText;
    [ObservableProperty] private string? _openButtonText;
    [ObservableProperty] private string? _deleteButtonText;
    [ObservableProperty] private string? _noPluginsInstalled;
    [ObservableProperty] private string? _randomKamoji;
    [ObservableProperty] private ObservableCollection<ParsedHttpSource> _plugins = [];
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private string? _searchPluginsWatermark;

    #endregion

    #region Commands

    [RelayCommand]
    private async Task InstallPlugins()
    {
        // NOTA IMPORTANTE: Asegúrate de que en PathHelper.SelectPath el filtro 
        // esté configurado para buscar archivos ".exe" y no ".dll".
        var pluginPaths = await PathHelper.SelectPath(LanguageHelper.GetText("SelectPluginFiles"), true);

        if (pluginPaths is not null && pluginPaths.Count != 0)
        {
            PluginManager.InstallPlugins(pluginPaths);
        }
    }

    // CAMBIO CLAVE: Se quitó el "static" para poder acceder a los servicios inyectados
    [RelayCommand]
    private void OpenPlugin(ParsedHttpSource? plugin)
    {
        if (plugin is null) return;

        // Usamos la instancia inyectada en lugar del acceso estático
        _mangaService.SelectedPlugin = plugin;

        // Resolvemos el ViewModel del plugin desde el contenedor
        var pluginViewModel = _serviceProvider.GetRequiredService<PluginPageViewModel>();
        NavigationHelper.NavigateTo(pluginViewModel);
    }

    [RelayCommand]
    private void DeletePlugin(ParsedHttpSource? plugin)
    {
        if (plugin is null) return;

        bool success = PluginManager.DeletePlugin(plugin.Name);
        if (success)
        {
            // Usamos la instancia inyectada
            if (_mangaService.SelectedPlugin?.Name == plugin.Name)
            {
                _mangaService.SelectedPlugin = null;
            }
        }
    }

    [RelayCommand]
    private void SearchManga()
    {
        var allPlugins = PluginManager.GetAllPlugins();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            UpdatePluginsList(allPlugins);
        }
        else
        {
            var filtered = allPlugins
                .Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            UpdatePluginsList(filtered);
        }
    }

    #endregion

    #region Private Methods

    private void LoadPlugins()
    {
        var loadedPlugins = PluginManager.GetAllPlugins();

        // Si tienes texto en el buscador, mantenemos el filtro al recargar
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            loadedPlugins = loadedPlugins
                .Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        UpdatePluginsList(loadedPlugins);
    }

    private void UpdatePluginsList(List<ParsedHttpSource> newPlugins)
    {
        Plugins.Clear();
        foreach (var plugin in newPlugins)
        {
            Plugins.Add(plugin);
        }
    }

    protected override void UpdateLocalizedTexts()
    {
        LocalizedTexts();
    }

    private void LocalizedTexts()
    {
        Title = LanguageHelper.GetText("Browse");
        RefreshButtonText = LanguageHelper.GetText("Refresh");
        InstallPluginsButtonText = LanguageHelper.GetText("InstallPlugins");
        OpenButtonText = LanguageHelper.GetText("Open");
        DeleteButtonText = LanguageHelper.GetText("Delete");
        RandomKamoji = KamojiHelper.GetRandomKamoji();
        NoPluginsInstalled = LanguageHelper.GetText("NoPluginsInstalled");
        SearchPluginsWatermark = LanguageHelper.GetText("SearchPlugins");
    }

    #endregion
}