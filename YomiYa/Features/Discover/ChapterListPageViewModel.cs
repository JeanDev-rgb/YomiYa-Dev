using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Core.Database;
using YomiYa.Domain.Models;
using YomiYa.Source.Online;
using YomiYa.Core.Localization;
using YomiYa.Core.Navigation;
using YomiYa.Core.Services;
using YomiYa.Domain.Enums;

namespace YomiYa.Features.Discover;

public partial class ChapterListPageViewModel : ViewModelBase
{
    [ObservableProperty] private string _backButtonText = LanguageHelper.GetText("Back");
    [ObservableProperty] private ObservableCollection<SChapter> _chapters = [];
    [ObservableProperty] private SManga _manga;
    [ObservableProperty] private ParsedHttpSource _plugin;
    [ObservableProperty] private Bitmap? _cover;
    [ObservableProperty] private string? _artist;
    [ObservableProperty] private string? _author;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private List<string>? _genres;

    private List<SChapter> _allChapters = [];

    [ObservableProperty] private ChapterSort _selectedSort = ChapterSort.Default;

    [ObservableProperty] private ChapterReadFilter _selectedReadFilter = ChapterReadFilter.ShowAll;

    public ChapterListPageViewModel()
    {
        Manga = MangaService.SelectedManga!;
        Plugin = MangaService.SelectedPlugin!;
        _ = LoadMangaDetailsAndChaptersAsync();
    }

    private async Task LoadMangaDetailsAndChaptersAsync()
    {
        var dbManga = await DatabaseService.GetMangaByUrlAsync(Manga.Url);
        if (dbManga is { IsFavorite: true })
        {
            Manga.Author = dbManga.Author;
            Manga.Artist = dbManga.Artist;
            Manga.Description = dbManga.Description;
            Manga.Genre = dbManga.Genre;
            Manga.Status = dbManga.Status;
        }
        else
        {
            await MangaDetailsHelper.GetOrFetchDetailsAsync(Manga);
        }

        LoadDetails();
        await LoadChaptersAsync();
    }

    private void LoadDetails()
    {
        Cover = Manga.Cover;
        Artist = Manga.Artist;
        Author = Manga.Author;
        Description = Manga.Description;
        Genres = Manga.Genre;
    }

    private async Task LoadChaptersAsync()
    {
        try
        {
            _allChapters.Clear();

            var mangaId = await DatabaseService.InsertManga(Manga);
            if (mangaId == 0) return;

            var remoteChapters = await Plugin.GetChapters(Manga.Url);
            if (remoteChapters.Count == 0) return;

            await DatabaseService.InsertChapters(mangaId, remoteChapters);

            var allChaptersFromDb = await DatabaseService.GetChaptersAsync(Manga.Url);

            _allChapters = allChaptersFromDb.ToList();
            ApplyFiltersAndSort();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    partial void OnSelectedSortChanged(ChapterSort value)
    {
        ApplyFiltersAndSort();
    }

    // ¡NUEVO! Este método se ejecutará automáticamente cuando cambie el filtro de leídos.
    partial void OnSelectedReadFilterChanged(ChapterReadFilter value)
    {
        ApplyFiltersAndSort();
    }

    // El comando ya no es necesario, pero el método se mantiene para la lógica.
    private void ApplyFiltersAndSort()
    {
        Chapters.Clear();

        IEnumerable<SChapter> filteredChapters = _allChapters;

        switch (SelectedReadFilter)
        {
            case ChapterReadFilter.ShowRead:
                filteredChapters = filteredChapters.Where(c => c.IsRead);
                break;
            case ChapterReadFilter.ShowUnread:
                filteredChapters = filteredChapters.Where(c => !c.IsRead);
                break;
        }

        var sortedChapters = SelectedSort switch
        {
            ChapterSort.Ascending => filteredChapters.OrderBy(c => c.ChapterNumber),
            ChapterSort.Descending => filteredChapters.OrderByDescending(c => c.ChapterNumber),
            _ => filteredChapters
        };

        foreach (var chapter in sortedChapters)
        {
            Chapters.Add(chapter);
        }
    }

    [RelayCommand]
    private static void Back()
    {
        NavigationHelper.GoBack();
    }

    [RelayCommand]
    [Obsolete("Obsolete")]
    private void OpenChapter(SChapter? chapter)
    {
        if (chapter is null) return;
        MangaService.ChapterList = Chapters.OrderBy(c => c.ChapterNumber).ToList();
        MangaService.ChapterIndex = MangaService.ChapterList.IndexOf(chapter);
        NavigationHelper.OpenReader();
    }

    protected override void UpdateLocalizedTexts()
    {
        BackButtonText = LanguageHelper.GetText("Back");
    }
}