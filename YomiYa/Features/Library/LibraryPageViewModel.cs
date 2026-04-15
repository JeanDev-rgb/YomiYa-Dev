using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Domain.Models;
using YomiYa.Core.Common;
using YomiYa.Core.Database;
using YomiYa.Core.Imaging;
using YomiYa.Core.Localization;
using YomiYa.Core.Navigation;
using YomiYa.Core.Plugins;
using YomiYa.Core.Services;
using YomiYa.Features.Discover;

namespace YomiYa.Features.Library;

public partial class LibraryPageViewModel : ViewModelBase
{
    #region Constructor

    public LibraryPageViewModel()
    {
        LocalizedTexts();
        LoadMangasByDatabase();
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task DeleteManga(SManga? mangaToDelete)
    {
        if (mangaToDelete == null) return;

        // Elimina el manga de la base de datos
        await DatabaseService.DeleteMangaAsync(mangaToDelete.Url);

        // Elimina el manga de la colección visible en la UI
        Mangas.Remove(mangaToDelete);
    }

    [RelayCommand]
    private static void OpenManga(SManga manga)
    {
        // Nota: Se está creando una instancia de TmoManga por defecto.
        // Considera si este es el comportamiento deseado o si el plugin
        // debería obtenerse de otra fuente.
        MangaService.SelectedManga = manga;
        MangaService.SelectedPlugin = PluginManager.GetPlugin(manga.Plugin!);
        NavigationHelper.NavigateTo(new ChapterListPageViewModel());
    }

    #endregion

    #region Properties

    [ObservableProperty] private string? _title;
    [ObservableProperty] private int _columns = 5;
    [ObservableProperty] private string? _randomKamoji;
    [ObservableProperty] private string? _noMangaAdded;
    [ObservableProperty] private ObservableCollection<SManga> _mangas = [];
    [ObservableProperty] private string? _deleteFromLibraryButtonText;

    #endregion

    #region Methods

    private async void LoadMangasByDatabase()
    {
        try
        {
            var mangas = await DatabaseService.GetLibraryMangasAsync();
            foreach (var manga in mangas)
            {
                Mangas.Add(manga);
                _ = manga.LoadCoverAsync();
            }
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