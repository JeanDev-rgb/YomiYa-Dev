using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using YomiYa.Core.Interfaces;
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
    #region Constructor

    public BrowsePageViewModel()
    {
        LoadPlugins();
        LocalizedTexts();
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
        var pluginPaths = await PathHelper.SelectPath(LanguageHelper.GetText("SelectPluginFiles"), true);

        if (pluginPaths is not null && pluginPaths.Count != 0)
        {
            PluginManager.InstallPlugins(pluginPaths);
            LoadPlugins(); // Reutilizamos el método para evitar duplicar código
        }
    }

    [RelayCommand]
    private static void OpenPlugin(ParsedHttpSource? plugin)
    {
        if (plugin is null) return;
        MangaService.SelectedPlugin = plugin;
        NavigationHelper.NavigateTo(new PluginPageViewModel());
    }

    // Nuevo comando para eliminar
    [RelayCommand]
    private void DeletePlugin(ParsedHttpSource? plugin)
    {
        if (plugin is null) return;

        bool success = PluginManager.DeletePlugin(plugin.Name);
        if (success)
        {
            Plugins.Remove(plugin);
            
            // Opcional: Si este era el plugin seleccionado actualmente, lo limpiamos
            if (MangaService.SelectedPlugin?.Name == plugin.Name)
            {
                MangaService.SelectedPlugin = null;
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
            // Búsqueda insensible a mayúsculas y minúsculas (parcial)
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
        UpdatePluginsList(loadedPlugins);
    }

    // Helper para actualizar la ObservableCollection de manera limpia
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