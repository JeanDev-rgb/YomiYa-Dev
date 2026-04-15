using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Domain.Models;
using YomiYa.Source.Online;
using YomiYa.Core.Database;
using YomiYa.Core.Localization;
using YomiYa.Core.Navigation;
using YomiYa.Core.Services;
using YomiYa.Helper.Input.Interfaces;

namespace YomiYa.Features.Discover;

public partial class PluginPageViewModel : ViewModelBase, ISearchableByKeyboard
{
    #region Constructors

    public PluginPageViewModel()
    {
        Plugin = MangaService.SelectedPlugin!;
        GetPopularMangas();
        GetLatestUpdates();
    }

    #endregion

    #region Properties

    [ObservableProperty] private ParsedHttpSource? _plugin;
    [ObservableProperty] private int _columns = 5;
    [ObservableProperty] private int _selectedTabIndex = 0;
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

    [RelayCommand]
    private static void OpenManga(SManga manga)
    {
        MangaService.SelectedManga = manga;
        NavigationHelper.NavigateTo(new ChapterListPageViewModel());
    }

    [RelayCommand]
    private static void Back()
    {
        NavigationHelper.GoBack();
    }

    [RelayCommand]
    private static async Task ToggleFavorite(SManga? manga)
    {
        if (manga is null) return;

        manga.IsFavorite = !manga.IsFavorite;

        if (manga.IsFavorite)
        {
            await MangaDetailsHelper.GetOrFetchDetailsAsync(manga);
        }

        // Ahora usamos el nuevo método, que es más simple y directo.
        await DatabaseService.SetMangaFavoriteStatusAsync(manga, manga.IsFavorite);

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

            MangaService.SearchManga(SearchText, progress);
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
        MangaService.GetPopularMangas(progress);
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
        MangaService.GetLatestUpdates(progress);
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

    public IRelayCommand SearchCommand => SearchMangaCommand;
}