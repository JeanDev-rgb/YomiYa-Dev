using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using YomiYa.Core.Database;
using YomiYa.Core.Navigation;
using YomiYa.Core.Plugins;
using YomiYa.Core.Services;
using YomiYa.Domain.Models;
using YomiYa.Features.Discover;
using YomiYa.Features.Library;

namespace YomiYa.Features.Main;

public partial class DashboardPageViewModel : ViewModelBase
{
    private readonly IDatabaseService _database;
    private readonly MangaService _mangaService;
    private readonly IGoogleDriveSyncService _drive;
    private readonly SidebarNavigationService _navigation;

    [ObservableProperty] private ObservableCollection<HistoryItemViewModel> _recentReadings = [];
    [ObservableProperty] private string _syncStatus = "Comprobando sincronización…";
    [ObservableProperty] private string? _latestReadingTitle;
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _isBusy;

    public DashboardPageViewModel(
        IDatabaseService database,
        MangaService mangaService,
        IGoogleDriveSyncService drive,
        SidebarNavigationService navigation)
    {
        _database = database;
        _mangaService = mangaService;
        _drive = drive;
        _navigation = navigation;
        _ = LoadDashboardAsync();
    }

    [RelayCommand]
    private void OpenLibrary() => _navigation.NavigateTo<LibraryPageViewModel>();

    [RelayCommand]
    private void OpenDiscovery() => _navigation.NavigateTo<BrowsePageViewModel>();

    [RelayCommand]
    private async Task ContinueReading()
    {
        if (RecentReadings.Count > 0) await OpenRecentReading(RecentReadings[0]);
        else OpenLibrary();
    }

    [RelayCommand]
    private async Task OpenRecentReading(HistoryItemViewModel? item)
    {
        if (item is null || IsBusy) return;
        IsBusy = true;
        try
        {
            var pluginName = item.Manga.Plugin;
            if (string.IsNullOrWhiteSpace(pluginName)) return;
            var plugin = await PluginManager.GetPluginAsync(pluginName);
            if (plugin is null) return;
            var manga = await _database.GetMangaByUrlAsync(item.Manga.Url) ?? item.Manga;
            _mangaService.SelectedPlugin = plugin;
            _mangaService.SelectedManga = manga;
            var chapters = await _database.GetChaptersAsync(manga.Url);
            if (chapters.Count == 0)
            {
                chapters = await plugin.GetChapters(manga.Url);
                if (chapters.Count > 0)
                {
                    var mangaId = await _database.InsertManga(manga);
                    await _database.InsertChapters(mangaId, chapters);
                }
            }
            _mangaService.ChapterList = chapters.OrderBy(chapter => chapter.ChapterNumber).ToList();
            _mangaService.ChapterIndex = _mangaService.ChapterList.FindIndex(chapter => chapter.Url == item.Chapter.Url);
            if (_mangaService.ChapterIndex < 0) return;
            NavigationHelper.OpenReader();
        }
        catch (Exception ex) { Console.WriteLine($"[Dashboard] No se pudo reanudar la lectura: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    private async Task LoadDashboardAsync()
    {
        try
        {
            var history = await _database.GetHistoryAsync();
            var items = history.Take(6).Select(entry => new HistoryItemViewModel(entry.Manga, entry.Chapter)).ToList();
            await Task.WhenAll(items.Select(item => item.LoadCoverAsync()));
            RecentReadings.Clear();
            foreach (var item in items) RecentReadings.Add(item);
            LatestReadingTitle = items.FirstOrDefault()?.Manga.Title;
        }
        catch (Exception ex) { Console.WriteLine($"[Dashboard] No se pudo cargar el historial: {ex.Message}"); }
        finally { IsLoading = false; }

        try
        {
            SyncStatus = await _drive.IsAuthenticatedAsync()
                ? "Google Drive conectado"
                : "Google Drive no conectado";
        }
        catch { SyncStatus = "Estado de sincronización no disponible"; }
    }

    protected override void UpdateLocalizedTexts() { }
}
