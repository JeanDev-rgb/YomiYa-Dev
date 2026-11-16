using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
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
    private CancellationTokenSource? _chapterLoadCts;

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
        _plugin = null!;
        _chapterList = [new SChapter { Name = "Capítulo 1 - Vista Previa", ChapterNumber = 1 }];
        _chapterIndex = 0;
        IsLoading = false;
    }

    [ActivatorUtilitiesConstructor]
    public ReaderViewModel(IDatabaseService databaseService, MangaService mangaService)
    {
        _databaseService = databaseService;
        _mangaService = mangaService;

        // Inicializamos los campos usando la instancia inyectada de MangaService
        _plugin = _mangaService.SelectedPlugin!;
        _chapterList = _mangaService.ChapterList!;
        _chapterIndex = _mangaService.ChapterIndex;

        _ = InitializeChapterAsync();
    }

    #endregion

    #region Properties

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SelectedChapter))]
    private int _chapterIndex;

    public SChapter? SelectedChapter => _chapterList.ElementAtOrDefault(ChapterIndex);

    [ObservableProperty] private ObservableCollection<Page> _pages = [];
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

    [RelayCommand(CanExecute = nameof(CanGoToPreviousChapter))]
    private void PreviousChapter()
    {
        ChapterIndex--;
        _ = InitializeChapterAsync();
    }

    private bool CanGoToPreviousChapter()
    {
        return ChapterIndex > 0;
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextChapter))]
    private void NextChapter()
    {
        ChapterIndex++;
        _ = InitializeChapterAsync();
    }

    private bool CanGoToNextChapter()
    {
        return ChapterIndex < _chapterList.Count - 1;
    }

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
    _chapterLoadCts?.Cancel();
    _chapterLoadCts?.Dispose();
    _chapterLoadCts = new CancellationTokenSource();
    var token = _chapterLoadCts.Token;

    IsLoading = true;
    foreach (var page in Pages) page.Dispose();
    Pages.Clear();

    try
    {
        if (SelectedChapter is null) return;

        var initialPageList = await _plugin.GetPages(SelectedChapter.Url);
        token.ThrowIfCancellationRequested();

        if (!initialPageList.Any()) return;

        // 1. Mostramos las páginas inmediatamente en la UI (aún sin bitmap)
        Pages = new ObservableCollection<Page>(initialPageList);
        CurrentPageIndex = Math.Clamp(SelectedChapter.LastPageRead, 0, Pages.Count - 1);
        IsLoading = false; // El lector ya es interactivo

        // 2. Ordenamos la cola de descarga empezando desde CurrentPageIndex hacia adelante
        var orderedIndices = Enumerable.Range(CurrentPageIndex, Pages.Count - CurrentPageIndex)
            .Concat(Enumerable.Range(0, CurrentPageIndex))
            .ToList();

        using var semaphore = new SemaphoreSlim(5); // Máximo 5 imágenes simultáneas
        var downloadTasks = orderedIndices.Select(async index =>
        {
            await semaphore.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                var page = Pages[index];
                if (page.Data is not null || string.IsNullOrEmpty(page.ImageUrl)) return;

                var bitmap = await page.ImageUrl.LoadImageAsync(token);
                if (!token.IsCancellationRequested && index < Pages.Count)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (!token.IsCancellationRequested && index < Pages.Count)
                            Pages[index] = page with { Data = bitmap };
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cargando página {index}: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(downloadTasks);
    }
    catch (OperationCanceledException) { }
    catch (Exception e)
    {
        Console.WriteLine($"Error al inicializar el capítulo: {e.Message}");
    }
    finally
    {
        IsLoading = false;
    }
}

    /// <summary>
    ///     Carga todas las imágenes de una lista de páginas y devuelve una nueva lista con los datos.
    /// </summary>
    private async Task<List<Page>> LoadAllPageBitmapsAsync(List<Page> pagesToLoad)
    {
        var loadingTasks = pagesToLoad.Select(LoadPageBitmapAsync).ToList();
        var loadedPages = await Task.WhenAll(loadingTasks);
        return loadedPages.ToList();
    }

    /// <summary>
    ///     Carga el bitmap para una sola página y devuelve un nuevo objeto Page con la imagen.
    /// </summary>
    private async Task<Page> LoadPageBitmapAsync(Page page)
    {
        if (page.Data is not null ||
            string.IsNullOrEmpty(page.ImageUrl)) return page; // Si no hay nada que cargar, la devolvemos como está

        try
        {
            var imageBitmap = await page.ImageUrl.LoadImageAsync();
            // Devolvemos una nueva instancia de Page con la imagen cargada
            return page with { Data = imageBitmap };
        }
        catch (Exception e)
        {
            Console.WriteLine($"Fallo al cargar la imagen de la página {page.Index}: {e.Message}");
            return page; // En caso de error, devolvemos la página original
        }
    }

    async partial void OnCurrentPageIndexChanged(int value)
    {
        try
        {
            if (IsLoading || Pages.Count == 0 || SelectedChapter is null) return;

            SelectedChapter.LastPageRead = value;
            SelectedChapter.IsRead = value >= Pages.Count - 1;

            // Usamos la instancia de _databaseService inyectada en lugar de crear una nueva
            await _databaseService.SetChapterProgress(SelectedChapter.Url, SelectedChapter.LastPageRead,
                SelectedChapter.IsRead);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al guardar el progreso del capítulo: {ex.Message}");
        }
    }

    public void Dispose()
    {
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