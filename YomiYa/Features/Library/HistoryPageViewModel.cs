using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Core.Common;
using YomiYa.Core.Database;
using YomiYa.Core.Imaging;
using YomiYa.Core.Localization;
using YomiYa.Core.Navigation;
using YomiYa.Core.Plugins;
using YomiYa.Core.Services;
using YomiYa.Domain.Models;

namespace YomiYa.Features.Library;

public partial class HistoryItemViewModel : ObservableObject
{
    [ObservableProperty] private SChapter _chapter;
    [ObservableProperty] private SManga _manga;

    public HistoryItemViewModel(SManga manga, SChapter chapter)
    {
        _manga = manga;
        _chapter = chapter;
    }

    public async Task LoadCoverAsync()
    {
        if (!string.IsNullOrEmpty(Manga.ThumbnailUrl)) Manga.Cover = await Manga.ThumbnailUrl.LoadImageAsync();
    }
}

public partial class HistoryPageViewModel : ViewModelBase
{
    [ObservableProperty] private string? _clearAllButtonText;
    [ObservableProperty] private string? _deleteFromHistoryButtonText;
    [ObservableProperty] private ObservableCollection<HistoryItemViewModel> _historyItems = [];
    [ObservableProperty] private string? _historyTitle;
    [ObservableProperty] private string? _noHistoryAdded;
    [ObservableProperty] private string? _randomKamoji;
    [ObservableProperty] private bool _isBusy;

    public HistoryPageViewModel()
    {
        _ = LoadHistoryAsync();
    }

    /// <summary>
    ///     Carga o actualiza el historial desde la base de datos.
    /// </summary>
    public async Task LoadHistoryAsync()
    {
        LocalizedTexts();
        var historyData = await DatabaseService.GetHistoryAsync();

        var historyVms = historyData.Select(x => new HistoryItemViewModel(x.Manga, x.Chapter)).ToList();
        var coverLoadTasks = historyVms.Select(vm => vm.LoadCoverAsync()).ToList();

        await Task.WhenAll(coverLoadTasks);

        HistoryItems.Clear();
        foreach (var vm in historyVms) HistoryItems.Add(vm);
    }

    [RelayCommand]
    [Obsolete]
    private async Task OpenChapter(HistoryItemViewModel? item)
    {
        if (item?.Manga?.Plugin is null || IsBusy) return;

        IsBusy = true;
        try
        {
            // 1. Obtener plugin de forma segura (asíncrona)
            var plugin = await PluginManager.GetPluginAsync(item.Manga.Plugin);
            if (plugin is null) return;

            // 2. Cargar datos frescos de la DB
            var fullManga = await DatabaseService.GetMangaByUrlAsync(item.Manga.Url);
            if (fullManga is null) return;

            MangaService.SelectedPlugin = plugin;
            MangaService.SelectedManga = fullManga;

            // 3. Obtener capítulos (DB o Remoto)
            var chapters = await DatabaseService.GetChaptersAsync(fullManga.Url);
            if (!chapters.Any())
                chapters = await plugin.GetChapters(fullManga.Url);

            if (!chapters.Any()) return;

            // ... resto de la lógica de índices ...
            NavigationHelper.OpenReader();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteHistoryItem(HistoryItemViewModel? item)
    {
        if (item is null) return;

        await DatabaseService.DeleteHistoryItemAsync(item.Chapter.Url);
        HistoryItems.Remove(item);
    }

    [RelayCommand]
    private async Task ClearHistory()
    {
        await DatabaseService.ClearHistoryAsync();
        HistoryItems.Clear();
    }

    protected override void UpdateLocalizedTexts()
    {
        LocalizedTexts();
    }

    private void LocalizedTexts()
    {
        HistoryTitle = LanguageHelper.GetText("History");
        RandomKamoji = KamojiHelper.GetRandomKamoji();
        NoHistoryAdded = LanguageHelper.GetText("NoHistoryAdded");
        ClearAllButtonText = LanguageHelper.GetText("ClearAll");
        DeleteFromHistoryButtonText = LanguageHelper.GetText("DeleteFromHistory");
    }
}