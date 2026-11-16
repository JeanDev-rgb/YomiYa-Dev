using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using YomiYa.Core.Database;
using YomiYa.Core.Localization;
using YomiYa.Core.Navigation;
using YomiYa.Core.Services;
using YomiYa.Domain.Models;
using YomiYa.Helper.Input.Interfaces;
using YomiYa.Source.Online;

namespace YomiYa.Features.Discover;

public partial class PluginPageViewModel : ViewModelBase, ISearchableByKeyboard
{
    private readonly IDatabaseService _databaseService;
    private readonly MangaService _mangaService;
    
    private CancellationTokenSource? _searchCts;
    private int _currentSearchPage;
    private bool _hasMoreSearchPages;

    // Dependencias inyectadas
    private readonly IServiceProvider _serviceProvider;

    public IRelayCommand SearchCommand => SearchMangaCommand;

    #region Constructors

    public PluginPageViewModel()
    {
        _serviceProvider = null!;
        _mangaService = null!;
        _databaseService = null!;

        // Usamos la instancia inyectada en lugar de acceso estático
        Plugin = _mangaService.SelectedPlugin!;

        GetPopularMangas();
        GetLatestUpdates();
    }

    [ActivatorUtilitiesConstructor]
    public PluginPageViewModel(
        IServiceProvider serviceProvider,
        MangaService mangaService,
        IDatabaseService databaseService)
    {
        _serviceProvider = serviceProvider;
        _mangaService = mangaService;
        _databaseService = databaseService;

        // Usamos la instancia inyectada en lugar de acceso estático
        Plugin = _mangaService.SelectedPlugin!;

        GetPopularMangas();
        GetLatestUpdates();
    }

    #endregion

    #region Properties

    [ObservableProperty] private ParsedHttpSource? _plugin;
    [ObservableProperty] private int _columns = 5;
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private ObservableCollection<SManga> _popularMangas = [];
    [ObservableProperty] private ObservableCollection<SManga> _latestUpdatesMangas = [];
    [ObservableProperty] private ObservableCollection<SManga> _searchedMangas = [];
    [ObservableProperty] private string _backButtonText = LanguageHelper.GetText("Back");
    [ObservableProperty] private string _latestUpdatesText = LanguageHelper.GetText("LatestUpdates");
    [ObservableProperty] private string _popularMangasText = LanguageHelper.GetText("PopularMangas");
    [ObservableProperty] private string _searchMangaText = LanguageHelper.GetText("SearchManga");
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private string _foundMangasText = LanguageHelper.GetText("FoundMangas");
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _isSearchEmpty;
    [ObservableProperty] private string? _searchErrorMessage;
    [ObservableProperty] private bool _isSearchTabVisible;

    #endregion

    #region Commands

    // Quitamos "static" y usamos los servicios inyectados
    [RelayCommand]
    private void OpenManga(SManga manga)
    {
        _mangaService.SelectedManga = manga;

        // Resolvemos el ViewModel dinámicamente desde el contenedor
        var chapterListViewModel = _serviceProvider.GetRequiredService<ChapterListPageViewModel>();
        NavigationHelper.NavigateTo(chapterListViewModel);
    }

    [RelayCommand]
    private void Back()
    {
        NavigationHelper.GoBack();
    }

    // Quitamos "static" y usamos _databaseService
    [RelayCommand]
    private async Task ToggleFavorite(SManga? manga)
    {
        if (manga is null) return;

        manga.IsFavorite = !manga.IsFavorite;

        if (manga.IsFavorite) await MangaDetailsHelper.GetOrFetchDetailsAsync(manga);

        // Usamos la base de datos inyectada en lugar de instanciarla con 'new'
        await _databaseService.SetMangaFavoriteStatusAsync(manga, manga.IsFavorite);

        Console.WriteLine(manga.IsFavorite
            ? $"'{manga.Title}' añadido a la biblioteca."
            : $"'{manga.Title}' eliminado de la biblioteca.");
    }

    [RelayCommand]
    private async Task SearchMangaAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        var searchCts = _searchCts = new CancellationTokenSource();
        var token = searchCts.Token;

        SearchErrorMessage = null;
        IsSearchEmpty = false;
        IsSearchTabVisible = !string.IsNullOrWhiteSpace(SearchText);
        _hasMoreSearchPages = false;

        if (string.IsNullOrWhiteSpace(SearchText) || Plugin is null)
        {
            SelectedTabIndex = 0;
            SearchedMangas.Clear();
            IsSearching = false;
            return;
        }

        var query = SearchText.Trim();
        var plugin = Plugin;
        SearchedMangas.Clear();
        SelectedTabIndex = 2;
        _currentSearchPage = 1;
        IsSearching = true;

        try
        {
            var progress = new SearchResultProgress(manga =>
            {
                if (!token.IsCancellationRequested) SearchedMangas.Add(manga);
            });

            _hasMoreSearchPages = await _mangaService.FetchPageAsync(
                plugin,
                page => plugin.SearchManga(query, page),
                _currentSearchPage,
                progress,
                token);

            if (!token.IsCancellationRequested)
            {
                IsSearchEmpty = progress.Count == 0;
                _currentSearchPage = 1;
            }
        }
        catch (OperationCanceledException)
        {
            // Búsqueda reemplazada por una nueva, no mostrar error
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                SearchErrorMessage = $"Error en búsqueda: {ex.Message}";
                IsSearchTabVisible = true;
            }
        }
        finally
        {
            if (ReferenceEquals(_searchCts, searchCts) && !token.IsCancellationRequested)
                IsSearching = false;
        }
    }
    
    [RelayCommand]
    private async Task LoadNextSearchPageAsync()
    {
        if (IsSearching || !_hasMoreSearchPages || Plugin is null || string.IsNullOrWhiteSpace(SearchText))
            return;

        var cts = _searchCts;
        if (cts is null) return;
        var token = cts.Token;
        var plugin = Plugin;
        var query = SearchText.Trim();
        var requestedPage = _currentSearchPage + 1;
        SearchErrorMessage = null;
        IsSearching = true;

        try
        {
            var progress = new SearchResultProgress(manga =>
            {
                if (!token.IsCancellationRequested && !SearchedMangas.Any(existing => existing.Url == manga.Url))
                    SearchedMangas.Add(manga);
            });

            _hasMoreSearchPages = await _mangaService.FetchPageAsync(
                plugin,
                page => plugin.SearchManga(query, page),
                requestedPage,
                progress,
                token);
            if (!token.IsCancellationRequested)
            {
                _currentSearchPage = requestedPage;
                IsSearchEmpty = SearchedMangas.Count == 0;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!token.IsCancellationRequested) SearchErrorMessage = ex.Message;
        }
        finally
        {
            if (ReferenceEquals(_searchCts, cts)) IsSearching = false;
        }
    }

    [RelayCommand]
    private Task RetrySearchPageAsync() => SearchedMangas.Count == 0 ? SearchMangaAsync() : LoadNextSearchPageAsync();

    private sealed class SearchResultProgress(Action<SManga> onResult) : IProgress<SManga>
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void Report(SManga value)
        {
            Interlocked.Increment(ref _count);
            if (Dispatcher.UIThread.CheckAccess()) onResult(value);
            else Dispatcher.UIThread.Post(() => onResult(value));
        }
    }

    #endregion

    #region Methods

    public void UpdateColumns(double width)
    {
        const int minItemWidth = 318;
        const int maxColumns = 50;
        var columns = Math.Max(1, Math.Min(maxColumns, (int)(width / minItemWidth)));
        Columns = columns;
    }

    private void GetPopularMangas()
    {
        PopularMangas.Clear();
        var progress = new Progress<SManga>(manga => PopularMangas.Add(manga));

        // Usamos la instancia inyectada
        _mangaService.GetPopularMangas(progress);
    }

    private void GetLatestUpdates()
    {
        LatestUpdatesMangas.Clear();
        var progress = new Progress<SManga>(manga => LatestUpdatesMangas.Add(manga));

        // Usamos la instancia inyectada
        _mangaService.GetLatestUpdates(progress);
    }

    protected override void UpdateLocalizedTexts()
    {
        BackButtonText = LanguageHelper.GetText("Back");
        LatestUpdatesText = LanguageHelper.GetText("LatestUpdates");
        PopularMangasText = LanguageHelper.GetText("PopularMangas");
        SearchMangaText = LanguageHelper.GetText("SearchManga");
        FoundMangasText = LanguageHelper.GetText("FoundMangas");
    }

    #endregion
}
