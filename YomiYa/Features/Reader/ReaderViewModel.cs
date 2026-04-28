using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Core.Database;
using YomiYa.Core.Imaging;
using YomiYa.Core.Services;
using YomiYa.Domain.Enums;
using YomiYa.Domain.Models;
using YomiYa.Helper.Input.Interfaces;
using YomiYa.Source.Online;

namespace YomiYa.Features.Reader;

public partial class ReaderViewModel : ViewModelBase, IKeyboardNavigable, IDisposable
{
    // Dependencias inyectadas
    private readonly IDatabaseService _databaseService;
    private readonly MangaService _mangaService;

    #region Fields

    private readonly ParsedHttpSource _plugin;
    private readonly List<SChapter> _chapterList;

    #endregion

    #region Constructor

    // El contenedor de servicios inyecta las dependencias necesarias
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedChapter))]
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
        IsLoading = true;
        // Limpiamos la colección de páginas anterior
        foreach (var page in Pages) page.Dispose();
        Pages.Clear();

        try
        {
            // 1. Obtenemos la lista de páginas (solo con URLs) desde el plugin
            var initialPageList = await _plugin.GetPages(SelectedChapter!.Url);
            if (!initialPageList.Any()) return;

            // 2. Cargamos todas las imágenes en segundo plano y obtenemos una nueva lista con las imágenes ya cargadas
            var loadedPages = await LoadAllPageBitmapsAsync(initialPageList);

            // 3. Asignamos la lista ya completa a la colección de la UI de una sola vez
            Pages = new ObservableCollection<Page>(loadedPages);

            // 4. Configuramos la página inicial según el historial
            CurrentPageIndex = SelectedChapter!.LastPageRead;

            OnPropertyChanged(nameof(PageInfo));
            NextPageCommand.NotifyCanExecuteChanged();
            PreviousPageCommand.NotifyCanExecuteChanged();
        }
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