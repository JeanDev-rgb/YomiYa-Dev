using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YomiYa.Core.Database;
using YomiYa.Core.Imaging;
using YomiYa.Domain.Models;
using YomiYa.Source.Models;
using YomiYa.Source.Online;

namespace YomiYa.Core.Services;

public class MangaService
{
    #region Static Properties

    public ParsedHttpSource? SelectedPlugin { get; set; }
    public SManga? SelectedManga { get; set; }
    public List<SChapter>? ChapterList { get; set; }
    public int ChapterIndex { get; set; }
    private CancellationTokenSource _searchCancellationTokenSource = new();

    // Limitamos a 4 descargas de imágenes simultáneas para controlar el uso de RAM y red.
    private readonly SemaphoreSlim ImageDownloadSemaphore = new(4);

    #endregion

    #region Public Methods

    public void GetPopularMangas(IProgress<SManga> progress)
    {
        _ = FetchAndReportMangasAsync(progress, page => SelectedPlugin!.GetPopularManga(page));
    }

    public void GetLatestUpdates(IProgress<SManga> progress)
    {
        _ = FetchAndReportMangasAsync(progress, page => SelectedPlugin!.GetLatestUpdates(page));
    }

    public void SearchManga(string searchText, IProgress<SManga> progress)
    {
        if (SelectedPlugin is null) return;

        _searchCancellationTokenSource.Cancel();
        _searchCancellationTokenSource = new CancellationTokenSource();

        _ = FetchAndReportMangasAsync(progress, page => SelectedPlugin.SearchManga(searchText, page),
            _searchCancellationTokenSource.Token);
    }

    #endregion

    #region Private Helper Methods

    private async Task FetchAndReportMangasAsync(IProgress<SManga> progress,
        Func<int, Task<MangasPage>> pageFetcher, CancellationToken cancellationToken = default)
    {
        if (SelectedPlugin is null) return;
        var plugin = SelectedPlugin;

        try
        {
            MangasPage currentPage;
            var page = 0;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                page++;
                currentPage = await pageFetcher(page);
                if (!currentPage.Mangas.Any()) break;

                var processingTasks = currentPage.Mangas.Select(async manga =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var processedManga = await ProcessMangaAsync(manga, plugin, cancellationToken);
                    if (processedManga != null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        progress.Report(processedManga);
                    }
                });

                await Task.WhenAll(processingTasks);
            } while (currentPage.HasNextPage);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[MangaService]: Operation was cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MangaService Error]: {ex.Message}");
        }
    }

    private async Task<SManga?> ProcessMangaAsync(SManga manga, ParsedHttpSource plugin,
        CancellationToken cancellationToken)
    {
        manga.Plugin = plugin.Name;

        try
        {
            await Task.WhenAll(
                LoadCoverAsync(manga, cancellationToken),
                LoadFavoriteStatusAsync(manga)
            );
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return manga;
    }

    private async Task LoadCoverAsync(SManga manga, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(manga.ThumbnailUrl)) return;

        // Esperamos a que haya un "espacio" disponible para descargar.
        await ImageDownloadSemaphore.WaitAsync(cancellationToken);
        try
        {
            manga.Cover = await manga.ThumbnailUrl.LoadImageAsync(cancellationToken, true);
        }
        finally
        {
            // Liberamos el espacio para que otra descarga pueda comenzar.
            ImageDownloadSemaphore.Release();
        }
    }

    private async Task LoadFavoriteStatusAsync(SManga manga)
    {
        var _databaseService = new DatabaseService();
        var favoriteManga = await _databaseService.GetMangaByUrlAsync(manga.Url);
        manga.IsFavorite = favoriteManga?.IsFavorite ?? false;
    }

    #endregion
}