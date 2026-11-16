using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using YomiYa.Core.Database;
using YomiYa.Core.Imaging;
using YomiYa.Core.Services;
using YomiYa.Domain.Enums;
using YomiYa.Domain.Models;
using YomiYa.Helper.Input.Interfaces;
using YomiYa.Source.Online;
using Page = YomiYa.Domain.Models.Page;

namespace YomiYa.Features.Reader;

public partial class ReaderViewModel : ViewModelBase, IKeyboardNavigable, IDisposable
{
    // Dependencias inyectadas
    private readonly IDatabaseService _databaseService;
    private readonly MangaService _mangaService;
    private readonly IReaderPageImageLoader _imageLoader;
    private CancellationTokenSource? _chapterLoadCts;
    private CancellationTokenSource? _progressSaveCts;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _imageDownloadSemaphore = new(5);

    #region Fields

    private readonly ParsedHttpSource _plugin;
    private readonly List<SChapter> _chapterList;

    #endregion

    #region Constructor

    // El contenedor de servicios inyecta las dependencias necesarias
    public ReaderViewModel()
    {
        if (!Design.IsDesignMode)
            throw new InvalidOperationException("Este constructor es exclusivo para el diseñador XAML.");

        _databaseService = null!;
        _mangaService = null!;
        _imageLoader = null!;
        _plugin = null!;
        _chapterList = [new SChapter { Name = "Capítulo 1 - Vista Previa", ChapterNumber = 1 }];
        _chapterIndex = 0;
        IsLoading = false;
    }

    [ActivatorUtilitiesConstructor]
    public ReaderViewModel(IDatabaseService databaseService, MangaService mangaService,
        IReaderPageImageLoader imageLoader)
    {
        _databaseService = databaseService;
        _mangaService = mangaService;
        _imageLoader = imageLoader;

        // Inicializamos los campos usando la instancia inyectada de MangaService
        _plugin = _mangaService.SelectedPlugin!;
        _chapterList = _mangaService.ChapterList!;
        _chapterIndex = _mangaService.ChapterIndex;

        _ = InitializeChapterAsync();
    }

    #endregion

    #region Properties

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedChapter))]
    [NotifyCanExecuteChangedFor(nameof(PreviousChapterCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextChapterCommand))]
    private int _chapterIndex;

    public SChapter? SelectedChapter => _chapterList.ElementAtOrDefault(ChapterIndex);

    [ObservableProperty] private ObservableCollection<ReaderPageViewModel> _pages = [];
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private ReadingMode _readingMode = ReadingMode.Paginated;
    [ObservableProperty] private ReadingWidthMode _readingWidth = ReadingWidthMode.Fitted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    private int _currentPageIndex;

    public string PageInfo => Pages.Count == 0 ? "0 / 0" : $"{CurrentPageIndex + 1} / {Pages.Count}";

    #endregion

    #region Commands

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void PreviousPage()
    {
        CurrentPageIndex--;
    }

    private bool CanGoToPreviousPage()
    {
        return CurrentPageIndex > 0;
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void NextPage()
    {
        CurrentPageIndex++;
    }

    private bool CanGoToNextPage()
    {
        return CurrentPageIndex < Pages.Count - 1;
    }

    [RelayCommand]
    private async Task RetryPageAsync(ReaderPageViewModel? page)
    {
        var chapterPages = Pages;
        if (page is null || !chapterPages.Contains(page) || !page.HasError || string.IsNullOrWhiteSpace(page.ImageUrl))
            return;

        page.BeginLoading();
        await LoadPageAsync(page, chapterPages, _lifetimeCts.Token);
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousChapter))]
    private void PreviousChapter()
    {
        ChapterIndex--;
    }

    private bool CanGoToPreviousChapter()
    {
        return ChapterIndex > 0;
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextChapter))]
    private void NextChapter()
    {
        ChapterIndex++;
    }

    private bool CanGoToNextChapter()
    {
        return ChapterIndex < _chapterList.Count - 1;
    }

    partial void OnChapterIndexChanged(int value) => _ = InitializeChapterAsync();

    [RelayCommand]
    private void ToggleReadingMode()
    {
        var currentPage = CurrentPageIndex;
        ReadingMode = ReadingMode == ReadingMode.Paginated ? ReadingMode.Cascade : ReadingMode.Paginated;

        Dispatcher.UIThread.Post(() => { CurrentPageIndex = currentPage; }, DispatcherPriority.Background);
    }

    [RelayCommand]
    private void ToggleReadingWidth()
    {
        ReadingWidth = ReadingWidth == ReadingWidthMode.Fitted ? ReadingWidthMode.Wide : ReadingWidthMode.Fitted;
    }

    #endregion

    #region Core Logic

    private async Task InitializeChapterAsync()
{
    var previousLoad = _chapterLoadCts;
    previousLoad?.Cancel();
    var loadCts = new CancellationTokenSource();
    _chapterLoadCts = loadCts;
    var token = loadCts.Token;
    var chapter = SelectedChapter;

    IsLoading = true;
    foreach (var page in Pages) page.Dispose();
    Pages.Clear();

    try
    {
        if (chapter is null) return;

        var initialPageList = await _plugin.GetPages(chapter.Url);
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(_chapterLoadCts, loadCts)) return;

        if (!initialPageList.Any()) return;

        // 1. Mostramos las páginas inmediatamente en la UI (aún sin bitmap)
        Pages = new ObservableCollection<ReaderPageViewModel>(initialPageList.Select(page => new ReaderPageViewModel(page)));
        // Keep each page item stable: replacing an item in the Carousel source can reset its selection.
        var chapterPages = Pages;
        CurrentPageIndex = Math.Clamp(chapter.LastPageRead, 0, chapterPages.Count - 1);
        IsLoading = false; // El lector ya es interactivo

        // 2. Ordenamos la cola de descarga empezando desde CurrentPageIndex hacia adelante
        var startIndex = CurrentPageIndex;
        var orderedIndices = Enumerable.Range(startIndex, chapterPages.Count - startIndex)
            .Concat(Enumerable.Range(0, startIndex))
            .ToList();

        var downloadTasks = orderedIndices.Select(index =>
            LoadPageAsync(chapterPages[index], chapterPages, token));

        await Task.WhenAll(downloadTasks);
    }
    catch (OperationCanceledException) { }
    catch (Exception e)
    {
        Console.WriteLine($"Error al inicializar el capítulo: {e.Message}");
    }
    finally
    {
        if (ReferenceEquals(_chapterLoadCts, loadCts))
        {
            IsLoading = false;
            _chapterLoadCts = null;
        }
        loadCts.Dispose();
    }
}

    private async Task LoadPageAsync(ReaderPageViewModel page,
        ObservableCollection<ReaderPageViewModel> chapterPages, CancellationToken token)
    {
        if (page.Data is not null || string.IsNullOrWhiteSpace(page.ImageUrl)) return;

        var semaphoreEntered = false;
        Bitmap? bitmap = null;
        try
        {
            await _imageDownloadSemaphore.WaitAsync(token);
            semaphoreEntered = true;
            token.ThrowIfCancellationRequested();
            bitmap = await _imageLoader.LoadAsync(page.ImageUrl, token);
            await UpdatePageOnUiThreadAsync(() =>
            {
                if (token.IsCancellationRequested || !ReferenceEquals(Pages, chapterPages) || !chapterPages.Contains(page))
                {
                    bitmap?.Dispose();
                    bitmap = null;
                    return;
                }

                page.CompleteLoading(bitmap);
                bitmap = null;
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cargando página {page.Index}: {ex.Message}");
            await UpdatePageOnUiThreadAsync(() =>
            {
                if (!token.IsCancellationRequested && ReferenceEquals(Pages, chapterPages) && chapterPages.Contains(page))
                    page.CompleteLoading(null);
            });
        }
        finally
        {
            bitmap?.Dispose();
            if (semaphoreEntered) _imageDownloadSemaphore.Release();
        }
    }

    private static Task UpdatePageOnUiThreadAsync(Action update)
    {
        if (Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            update();
            return Task.CompletedTask;
        }
        return Dispatcher.UIThread.InvokeAsync(update).GetTask();
    }

    partial void OnCurrentPageIndexChanged(int value)
    {
        if (IsLoading || Pages.Count == 0 || SelectedChapter is null) return;
        var chapter = SelectedChapter;
        var isRead = value >= Pages.Count - 1;
        chapter.LastPageRead = value;
        chapter.IsRead = isRead;
        _progressSaveCts?.Cancel();
        _progressSaveCts?.Dispose();
        var saveCts = _progressSaveCts = new CancellationTokenSource();
        _ = SaveProgressAfterDelayAsync(chapter.Url, value, isRead, saveCts.Token);
    }

    private async Task SaveProgressAfterDelayAsync(string chapterUrl, int pageIndex, bool isRead, CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), token);
            await _databaseService.SetChapterProgress(chapterUrl, pageIndex, isRead);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine($"Error al guardar progreso de lectura: {ex.Message}"); }
    }

    public void Dispose()
    {
        var chapterLoad = _chapterLoadCts;
        _chapterLoadCts = null;
        chapterLoad?.Cancel();
        var progressSave = _progressSaveCts;
        _progressSaveCts = null;
        progressSave?.Cancel();
        _lifetimeCts.Cancel();
        foreach (var page in Pages) page.Dispose();

        Pages.Clear();
        GC.SuppressFinalize(this);
    }

    protected override void UpdateLocalizedTexts()
    {
        // No hay textos que actualizar en esta vista.
    }

    #endregion
}
