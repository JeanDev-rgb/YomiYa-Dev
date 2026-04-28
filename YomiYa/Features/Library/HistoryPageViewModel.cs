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
        if (!string.IsNullOrEmpty(Manga.ThumbnailUrl))
            Manga.Cover = await Manga.ThumbnailUrl.LoadImageAsync();
    }
}

public partial class HistoryPageViewModel : ViewModelBase
{
    // Dependencias inyectadas
    private readonly IDatabaseService _databaseService;
    private readonly MangaService _mangaService;

    [ObservableProperty] private string? _clearAllButtonText;
    [ObservableProperty] private string? _deleteFromHistoryButtonText;
    [ObservableProperty] private ObservableCollection<HistoryItemViewModel> _historyItems = [];
    [ObservableProperty] private string? _historyTitle;
    [ObservableProperty] private string? _noHistoryAdded;
    [ObservableProperty] private string? _randomKamoji;
    [ObservableProperty] private bool _isBusy;

    #region Constructor

    // El contenedor de DI inyectará IDatabaseService y MangaService automáticamente
    public HistoryPageViewModel(IDatabaseService databaseService, MangaService mangaService)
    {
        _databaseService = databaseService;
        _mangaService = mangaService;

        _ = LoadHistoryAsync();
    }

    #endregion

    /// <summary>
    ///     Carga o actualiza el historial desde la base de datos.
    /// </summary>
    public async Task LoadHistoryAsync()
    {
        LocalizedTexts();

        // Usamos la instancia de base de datos inyectada en lugar de 'new'
        var historyData = await _databaseService.GetHistoryAsync();

        var historyVms = historyData.Select(x => new HistoryItemViewModel(x.Manga, x.Chapter)).ToList();
        var coverLoadTasks = historyVms.Select(vm => vm.LoadCoverAsync()).ToList();

        await Task.WhenAll(coverLoadTasks);

        HistoryItems.Clear();
        foreach (var vm in historyVms) HistoryItems.Add(vm);
    }

    [RelayCommand]
    [Obsolete("Obsolete")]
    private async Task OpenChapter(HistoryItemViewModel? item)
    {
        if (item?.Manga?.Plugin is null || IsBusy) return;

        IsBusy = true;
        try
        {
            // 1. Obtener plugin de forma segura (asíncrona)
            var plugin = await PluginManager.GetPluginAsync(item.Manga.Plugin);
            if (plugin is null) return;

            // 2. Cargar datos frescos de la DB inyectada
            var fullManga = await _databaseService.GetMangaByUrlAsync(item.Manga.Url);
            if (fullManga is null) return;

            // Actualizamos la instancia compartida de MangaService
            _mangaService.SelectedPlugin = plugin;
            _mangaService.SelectedManga = fullManga;

            // 3. Obtener capítulos (DB o Remoto)
            var chapters = await _databaseService.GetChaptersAsync(fullManga.Url);
            if (!chapters.Any())
                chapters = await plugin.GetChapters(fullManga.Url);

            if (!chapters.Any()) return;

            // Asignamos la lista de capítulos al MangaService para que el ReaderViewModel los tome
            _mangaService.ChapterList = chapters.OrderBy(c => c.ChapterNumber).ToList();

            // Buscamos el índice del capítulo actual basándonos en la URL
            var chapterIndex = _mangaService.ChapterList.FindIndex(c => c.Url == item.Chapter.Url);
            _mangaService.ChapterIndex = chapterIndex >= 0 ? chapterIndex : 0;

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

        // Usamos la DB inyectada
        await _databaseService.DeleteHistoryItemAsync(item.Chapter.Url);
        HistoryItems.Remove(item);
    }

    [RelayCommand]
    private async Task ClearHistory()
    {
        // Usamos la DB inyectada
        await _databaseService.ClearHistoryAsync();
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