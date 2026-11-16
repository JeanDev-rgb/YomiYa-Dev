using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using YomiYa.Core.Common;
using YomiYa.Core.Database;
using YomiYa.Core.Localization;
using YomiYa.Core.Navigation;
using YomiYa.Core.Plugins;
using YomiYa.Core.Services;
using YomiYa.Domain.Models;
using YomiYa.Features.Discover;

namespace YomiYa.Features.Library;

public partial class LibraryPageViewModel : ViewModelBase
{
    // Dependencias inyectadas
    private readonly IDatabaseService _databaseService;
    private readonly MangaService _mangaService;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<SManga> _allMangas = [];

    #region Constructor

    // Inyectamos los servicios mediante el contenedor DI
    public LibraryPageViewModel()
    {
        _databaseService = null!;
        _mangaService = null!;
        _serviceProvider = null!;

        LocalizedTexts();
        _ = LoadMangasByDatabase();
    }

    [ActivatorUtilitiesConstructor]
    public LibraryPageViewModel(
        IDatabaseService databaseService,
        MangaService mangaService,
        IServiceProvider serviceProvider)
    {
        _databaseService = databaseService;
        _mangaService = mangaService;
        _serviceProvider = serviceProvider;

        LocalizedTexts();
        _ = LoadMangasByDatabase();
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task DeleteManga(SManga? mangaToDelete)
    {
        if (mangaToDelete == null) return;

        // Utilizamos la base de datos inyectada
        await _databaseService.DeleteMangaAsync(mangaToDelete.Url);

        // Elimina el manga de la colección visible en la UI
        _allMangas.Remove(mangaToDelete);
        Mangas.Remove(mangaToDelete);
    }

    [RelayCommand]
    private async Task OpenManga(SManga manga)
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            var plugin = await PluginManager.GetPluginAsync(manga.Plugin!);

            if (plugin is null) return;

            // Usamos la instancia del servicio inyectado
            _mangaService.SelectedManga = manga;

            // Reutilizamos la variable 'plugin' que ya obtuvimos asíncronamente
            _mangaService.SelectedPlugin = plugin;

            // Resolvemos el ViewModel de la lista de capítulos a través del proveedor
            var chapterListVm = _serviceProvider.GetRequiredService<ChapterListPageViewModel>();
            NavigationHelper.NavigateTo(chapterListVm);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ContinueReading(SManga manga)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var plugin = await PluginManager.GetPluginAsync(manga.Plugin!);
            if (plugin is null) return;
            _mangaService.SelectedPlugin = plugin;
            _mangaService.SelectedManga = manga;
            var chapters = await _databaseService.GetChaptersAsync(manga.Url);
            if (chapters.Count == 0)
            {
                var mangaId = await _databaseService.InsertManga(manga);
                chapters = await plugin.GetChapters(manga.Url);
                if (chapters.Count > 0) await _databaseService.InsertChapters(mangaId, chapters);
            }
            if (chapters.Count == 0) return;
            _mangaService.ChapterList = chapters.OrderBy(c => c.ChapterNumber).ToList();
            var nextUnread = _mangaService.ChapterList.FindIndex(c => !c.IsRead);
            _mangaService.ChapterIndex = nextUnread >= 0 ? nextUnread : _mangaService.ChapterList.Count - 1;
            NavigationHelper.OpenReader();
        }
        finally { IsBusy = false; }
    }

    #endregion

    #region Properties

    [ObservableProperty] private string? _title;
    [ObservableProperty] private int _columns = 5;
    [ObservableProperty] private string? _randomKamoji;
    [ObservableProperty] private string? _noMangaAdded;
    [ObservableProperty] private ObservableCollection<SManga> _mangas = [];
    [ObservableProperty] private string? _deleteFromLibraryButtonText;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _sortDescending;
    [ObservableProperty] private bool _isListView;
    [ObservableProperty] private bool _unreadOnly;
    public bool IsGridView => !IsListView;
    public int DisplayColumns => IsListView ? 1 : Columns;
    public bool IsLibraryEmpty => _allMangas.Count == 0;
    public string EmptyMessage => IsLibraryEmpty ? NoMangaAdded ?? string.Empty : "No hay títulos que coincidan con la búsqueda.";

    #endregion

    #region Methods

    private async Task LoadMangasByDatabase()
    {
        try
        {
            // Usamos la base de datos inyectada, eliminando el "new DatabaseService()"
            var mangasFromDb = await _databaseService.GetLibraryMangasAsync();
            var coverLoadTasks = mangasFromDb.Select(manga => manga.LoadCoverAsync()).ToList();

            await Task.WhenAll(coverLoadTasks);
            _allMangas.Clear();
            _allMangas.AddRange(mangasFromDb);
            ApplyFilters();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSortDescendingChanged(bool value) => ApplyFilters();
    partial void OnUnreadOnlyChanged(bool value) => ApplyFilters();
    partial void OnIsListViewChanged(bool value) => OnPropertyChanged(nameof(IsGridView));

    private void ApplyFilters()
    {
        var query = SearchText.Trim();
        var filtered = _allMangas.Where(manga => (query.Length == 0 ||
            manga.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)) &&
            (!UnreadOnly || manga.ReadProgress < 1));
        filtered = SortDescending
            ? filtered.OrderByDescending(manga => manga.Title, StringComparer.CurrentCultureIgnoreCase)
            : filtered.OrderBy(manga => manga.Title, StringComparer.CurrentCultureIgnoreCase);
        Mangas.Clear();
        foreach (var manga in filtered) Mangas.Add(manga);
        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    public void UpdateColumns(double width)
    {
        const int minItemWidth = 318;
        const int maxColumns = 50;
        var columns = Math.Max(1, Math.Min(maxColumns, (int)(width / minItemWidth)));
        Columns = columns;
        OnPropertyChanged(nameof(DisplayColumns));
    }

    protected override void UpdateLocalizedTexts()
    {
        LocalizedTexts();
    }

    private void LocalizedTexts()
    {
        Title = LanguageHelper.GetText("Library");
        RandomKamoji = KamojiHelper.GetRandomKamoji();
        NoMangaAdded = LanguageHelper.GetText("NoMangaAdded");
        DeleteFromLibraryButtonText = LanguageHelper.GetText("DeleteFromLibrary");
        OnPropertyChanged(nameof(EmptyMessage));
    }

    #endregion
}
