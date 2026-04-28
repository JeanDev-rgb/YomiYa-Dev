using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Core.Database;
using YomiYa.Core.Localization;
using YomiYa.Core.Navigation;
using YomiYa.Core.Services;
using YomiYa.Domain.Enums;
using YomiYa.Domain.Models;
using YomiYa.Source.Online;

namespace YomiYa.Features.Discover;

public partial class ChapterListPageViewModel : ViewModelBase
{
    // Dependencias inyectadas
    private readonly IDatabaseService _databaseService;
    private readonly MangaService _mangaService;

    private List<SChapter> _allChapters = [];

    #region Properties

    [ObservableProperty] private string _allText = LanguageHelper.GetText("All");
    [ObservableProperty] private string? _artist;
    [ObservableProperty] private string _ascendingText = LanguageHelper.GetText("Ascending");
    [ObservableProperty] private string? _author;
    [ObservableProperty] private string _backButtonText = LanguageHelper.GetText("Back");
    [ObservableProperty] private ObservableCollection<SChapter> _chapters = [];
    [ObservableProperty] private Bitmap? _cover;
    [ObservableProperty] private string _defaultText = LanguageHelper.GetText("Default");
    [ObservableProperty] private string _descendingText = LanguageHelper.GetText("Descending");
    [ObservableProperty] private string? _description;
    [ObservableProperty] private List<string>? _genres;
    [ObservableProperty] private SManga _manga;
    [ObservableProperty] private ParsedHttpSource _plugin;
    [ObservableProperty] private string _readText = LanguageHelper.GetText("Read");
    [ObservableProperty] private ChapterReadFilter _selectedReadFilter = ChapterReadFilter.ShowAll;
    [ObservableProperty] private ChapterSort _selectedSort = ChapterSort.Default;
    [ObservableProperty] private string _showText = LanguageHelper.GetText("Show");
    [ObservableProperty] private string _sortByText = LanguageHelper.GetText("SortBy");
    [ObservableProperty] private string _unreadText = LanguageHelper.GetText("Unread");

    #endregion

    #region Constructor

    // El contenedor de servicios inyectará la base de datos y el servicio de manga
    public ChapterListPageViewModel(IDatabaseService databaseService, MangaService mangaService)
    {
        _databaseService = databaseService;
        _mangaService = mangaService;

        // Obtenemos los valores desde la instancia compartida de MangaService
        Manga = _mangaService.SelectedManga!;
        Plugin = _mangaService.SelectedPlugin!;

        _ = LoadMangaDetailsAndChaptersAsync();
    }

    #endregion

    #region Methods

    private async Task LoadMangaDetailsAndChaptersAsync()
    {
        // Usamos la instancia inyectada de IDatabaseService
        var dbManga = await _databaseService.GetMangaByUrlAsync(Manga.Url);

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

            // Usamos la instancia de la base de datos inyectada en lugar de instanciarla con 'new'
            var mangaId = await _databaseService.InsertManga(Manga);
            if (mangaId == 0) return;

            var remoteChapters = await Plugin.GetChapters(Manga.Url);
            if (remoteChapters.Count == 0) return;

            await _databaseService.InsertChapters(mangaId, remoteChapters);

            var allChaptersFromDb = await _databaseService.GetChaptersAsync(Manga.Url);

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

    partial void OnSelectedReadFilterChanged(ChapterReadFilter value)
    {
        ApplyFiltersAndSort();
    }

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

        foreach (var chapter in sortedChapters) Chapters.Add(chapter);
    }

    protected override void UpdateLocalizedTexts()
    {
        BackButtonText = LanguageHelper.GetText("Back");
        SortByText = LanguageHelper.GetText("SortBy");
        DefaultText = LanguageHelper.GetText("Default");
        AscendingText = LanguageHelper.GetText("Ascending");
        DescendingText = LanguageHelper.GetText("Descending");
        ShowText = LanguageHelper.GetText("Show");
        AllText = LanguageHelper.GetText("All");
        ReadText = LanguageHelper.GetText("Read");
        UnreadText = LanguageHelper.GetText("Unread");
    }

    #endregion

    #region Commands

    [RelayCommand]
    private static void Back()
    {
        NavigationHelper.GoBack();
    }

    // Le quitamos el static para poder acceder a _mangaService
    [RelayCommand]
    [Obsolete("Obsolete")]
    private void OpenChapter(SChapter? chapter)
    {
        if (chapter is null) return;

        // Guardamos los datos en el servicio Singleton compartido
        _mangaService.ChapterList = Chapters.OrderBy(c => c.ChapterNumber).ToList();
        _mangaService.ChapterIndex = _mangaService.ChapterList.IndexOf(chapter);

        NavigationHelper.OpenReader();
    }

    #endregion
}