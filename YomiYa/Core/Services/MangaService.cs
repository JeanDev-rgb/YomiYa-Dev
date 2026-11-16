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

public record SearchProgressEvent(SManga? Manga = null, bool IsCompleted = false, Exception? Error = null);

public class MangaService(IDatabaseService databaseService)
{
    #region Static Properties

    public ParsedHttpSource? SelectedPlugin { get; set; }
    public SManga? SelectedManga { get; set; }
    public List<SChapter>? ChapterList { get; set; }
    public int ChapterIndex { get; set; }
    private CancellationTokenSource _searchCancellationTokenSource = new();

    // Limitamos a 4 descargas de imágenes simultáneas para controlar el uso de RAM y red.
    private readonly SemaphoreSlim _imageDownloadSemaphore = new(4);

    #endregion

    #region Public Methods
    
    public async Task<bool> FetchPageAsync(
        Func<int, Task<MangasPage>> pageFetcher,
        int page,
        IProgress<SManga> progress,
        CancellationToken cancellationToken)
    {
        if (SelectedPlugin is null) return false;
        var plugin = SelectedPlugin;

        cancellationToken.ThrowIfCancellationRequested();
        var resultPage = await pageFetcher(page);

        if (!resultPage.Mangas.Any())
            return false;

        var tasks = resultPage.Mangas.Select(async manga =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processed = await ProcessMangaAsync(manga, plugin, cancellationToken);
            if (processed != null && !cancellationToken.IsCancellationRequested)
            {
                progress.Report(processed);
            }
        });

        await Task.WhenAll(tasks);
        return resultPage.HasNextPage;
    }

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
    
    private async Task<SManga?> ProcessMangaAsync(SManga manga, ParsedHttpSource plugin, CancellationToken ct)
    {
        manga.Plugin = plugin.Name;
        try
        {
            await Task.WhenAll(
                LoadCoverAsync(manga, ct),
                LoadFavoriteStatusAsync(manga)
            );
            return manga;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async Task LoadCoverAsync(SManga manga, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(manga.ThumbnailUrl)) return;

        await _imageDownloadSemaphore.WaitAsync(ct);
        try
        {
            manga.Cover = await manga.ThumbnailUrl.LoadImageAsync(ct, true);
        }
        finally
        {
            _imageDownloadSemaphore.Release();
        }
    }

    private async Task LoadFavoriteStatusAsync(SManga manga)
    {
        var favoriteManga = await databaseService.GetMangaByUrlAsync(manga.Url);
        manga.IsFavorite = favoriteManga?.IsFavorite ?? false;
    }

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

    #endregion
}