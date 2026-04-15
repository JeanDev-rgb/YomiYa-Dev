using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Source.Online;
using YomiYa.Core.Common;
using YomiYa.Core.IO;
using YomiYa.Core.Localization;
using YomiYa.Core.Navigation;
using YomiYa.Core.Plugins;
using YomiYa.Core.Services;
using YomiYa.Helper.Input.Interfaces;

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
            var installedPlugins = PluginManager.GetAllPlugins();
            Plugins.Clear();
            foreach (var plugin in installedPlugins) Plugins.Add(plugin);
        }
    }

    [RelayCommand]
    private static void OpenPlugin(ParsedHttpSource? plugin)
    {
        if (plugin is null) return;
        MangaService.SelectedPlugin = plugin;
        NavigationHelper.NavigateTo(new PluginPageViewModel());
    }

    [RelayCommand]
    private void SearchManga()
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            var plugins = PluginManager.GetAllPlugins();
            Plugins.Clear();
            foreach (var source in plugins)
            {
                Plugins.Add(source);
            }
        }
        else
        {
            var plugin = PluginManager.GetPlugin(SearchText);
            Plugins.Clear();
            if (string.IsNullOrWhiteSpace(plugin!.Name))
            {
                return;
            }
            Plugins.Add(plugin);
        }
    }

    #endregion

    #region Private Methods

    private void LoadPlugins()
    {
        var loadedPlugins = PluginManager.GetAllPlugins();
        Plugins.Clear();
        foreach (var plugin in loadedPlugins) Plugins.Add(plugin);
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
        RandomKamoji = KamojiHelper.GetRandomKamoji();
        NoPluginsInstalled = LanguageHelper.GetText("NoPluginsInstalled");
        SearchPluginsWatermark = LanguageHelper.GetText("SearchPlugins");
    }

    #endregion

    public IRelayCommand SearchCommand => SearchMangaCommand;
}