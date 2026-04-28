using System;
using System.Collections.ObjectModel;
using System.Linq;
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

    #region Constructor

    // Inyectamos los servicios mediante el contenedor DI
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

            if (plugin is null)
            {
                return;
            }

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

    #endregion

    #region Properties

    [ObservableProperty] private string? _title;
    [ObservableProperty] private int _columns = 5;
    [ObservableProperty] private string? _randomKamoji;
    [ObservableProperty] private string? _noMangaAdded;
    [ObservableProperty] private ObservableCollection<SManga> _mangas = [];
    [ObservableProperty] private string? _deleteFromLibraryButtonText;
    [ObservableProperty] private bool _isBusy;

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

            foreach (var manga in mangasFromDb) Mangas.Add(manga);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public void UpdateColumns(double width)
    {
        const int minItemWidth = 318;
        const int maxColumns = 50;
        var columns = Math.Max(1, Math.Min(maxColumns, (int)(width / minItemWidth)));
        Columns = columns;
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
    }

    #endregion
}