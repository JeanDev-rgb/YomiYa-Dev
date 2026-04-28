using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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
    // Dependencias inyectadas
    private readonly IServiceProvider _serviceProvider;
    private readonly MangaService _mangaService;
    private readonly IDatabaseService _databaseService;

    #region Constructors

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

    public IRelayCommand SearchCommand => SearchMangaCommand;

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
    private Task SearchManga()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                SelectedTabIndex = 0;
                SearchedMangas.Clear();
                return Task.CompletedTask;
            }

            SearchedMangas.Clear();
            SelectedTabIndex = 2; // Cambiamos a la pestaña de búsqueda de inmediato

            var progress = new Progress<SManga>(async void (manga) =>
            {
                try
                {
                    await Task.Delay(60);
                    SearchedMangas.Add(manga);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            });

            // Usamos la instancia inyectada
            _mangaService.SearchManga(SearchText, progress);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        return Task.CompletedTask;
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
        var progress = new Progress<SManga>(async void (manga) =>
        {
            try
            {
                await Task.Delay(60);
                PopularMangas.Add(manga);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        });

        // Usamos la instancia inyectada
        _mangaService.GetPopularMangas(progress);
    }

    private void GetLatestUpdates()
    {
        LatestUpdatesMangas.Clear();
        var progress = new Progress<SManga>(async void (manga) =>
        {
            try
            {
                await Task.Delay(60);
                LatestUpdatesMangas.Add(manga);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        });

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