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
    [ObservableProperty] private SManga _manga;
    [ObservableProperty] private SChapter _chapter;

    public HistoryItemViewModel(SManga manga, SChapter chapter)
    {
        _manga = manga;
        _chapter = chapter;
        LoadCoverAsync();
    }

    private async void LoadCoverAsync()
    {
        if (!string.IsNullOrEmpty(Manga.ThumbnailUrl))
        {
            Manga.Cover = await Manga.ThumbnailUrl.LoadImageAsync();
        }
    }
}

public partial class HistoryPageViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<HistoryItemViewModel> _historyItems = [];
    [ObservableProperty] private string? _historyTitle;
    [ObservableProperty] private string? _randomKamoji;
    [ObservableProperty] private string? _noHistoryAdded;
    [ObservableProperty] private string? _clearAllButtonText;
    [ObservableProperty] private string? _deleteFromHistoryButtonText;


    /// <summary>
    /// Carga o actualiza el historial desde la base de datos.
    /// </summary>
    public async Task LoadHistoryAsync()
    {
        LocalizedTexts();
        HistoryItems.Clear();
        var historyData = await DatabaseService.GetHistoryAsync();
        foreach (var (manga, chapter) in historyData)
        {
            HistoryItems.Add(new HistoryItemViewModel(manga, chapter));
        }
    }

    [RelayCommand]
    [Obsolete("Obsolete")]
    private async Task OpenChapter(HistoryItemViewModel? item)
    {
        if (item?.Manga?.Plugin is null) return;

        var fullManga = await DatabaseService.GetMangaByUrlAsync(item.Manga.Url);
        if (fullManga is null) return;

        MangaService.SelectedPlugin = PluginManager.GetPlugin(fullManga.Plugin);
        if (MangaService.SelectedPlugin is null) return;

        MangaService.SelectedManga = fullManga;

        var chapters = await DatabaseService.GetChaptersAsync(fullManga.Url);
        if (!chapters.Any())
        {
            chapters = await MangaService.SelectedPlugin.GetChapters(fullManga.Url);
        }

        if (!chapters.Any()) return;

        var chapterToOpen = chapters.FirstOrDefault(c => c.Url == item.Chapter.Url);
        if (chapterToOpen is null) return;

        MangaService.ChapterList = chapters.OrderBy(c => c.ChapterNumber).ToList();
        MangaService.ChapterIndex = MangaService.ChapterList.IndexOf(chapterToOpen);

        NavigationHelper.OpenReader();
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