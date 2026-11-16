using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using YomiYa.Core.Localization;
using YomiYa.Core.Navigation;
using YomiYa.Features.Ad;
using YomiYa.Features.Discover;
using YomiYa.Features.Main;
using YomiYa.Features.Library;
using YomiYa.Features.Points;
using YomiYa.Features.Settings;
using YomiYa.Features.Store;
using YomiYa.Features.Support;

namespace YomiYa.Features.Navigation;

public partial class SideBarMenuViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SidebarNavigationService _navigationService;

    #region Commands

    [RelayCommand]
    private void TriggerPane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    #endregion

    #region Constructor

    public SideBarMenuViewModel()
    {
        _serviceProvider = Program.ServiceProvider;
        _navigationService = null!;

        // Inicializamos la página por defecto usando el ServiceProvider
        CurrentPage = _serviceProvider.GetRequiredService<DashboardPageViewModel>();

        SplitItems();
        SelectedListItem = MangaItems.First();
        UpdateLocalizedTexts();
    }

    [ActivatorUtilitiesConstructor]
    public SideBarMenuViewModel(IServiceProvider serviceProvider, SidebarNavigationService navigationService)
    {
        _serviceProvider = serviceProvider;
        _navigationService = navigationService;
        _navigationService.NavigationRequested += OnNavigationRequested;

        // Inicializamos la página por defecto usando el ServiceProvider
        CurrentPage = _serviceProvider.GetRequiredService<DashboardPageViewModel>();

        SplitItems();
        SelectedListItem = MangaItems.First();
        UpdateLocalizedTexts();
    }

    #endregion

    #region Properties

    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private bool _isPaneOpen = true;
    [ObservableProperty] private ListItemTemplate? _selectedListItem;

    public ObservableCollection<ListItemTemplate> MangaItems { get; } = [];
    public ObservableCollection<ListItemTemplate> OtherItems { get; } = [];

    public ObservableCollection<ListItemTemplate> Items { get; set; } =
    [
        new(typeof(DashboardPageViewModel), "Library", "Dashboard"),
        new(typeof(LibraryPageViewModel), "Library", "Library"),
        new(typeof(BrowsePageViewModel), "Browse", "Browse"),
        new(typeof(HistoryPageViewModel), "History", "History"),
        new(typeof(StorePageViewModel), "ShoppingCart", "Lootbar"),
        new(typeof(PointsPageViewModel), "Star", "PointsTitle"),
        new(typeof(SupportPageViewModel), "HeartFilled", "SupportTitle"),
        new(typeof(MorePageViewModel), "More", "More"),
        new(typeof(AdPageViewModel), "Dollar", "Reward1Dollar")
    ];

    private void SplitItems()
    {
        MangaItems.Clear();
        OtherItems.Clear();

        foreach (var item in Items)
            if (item.ModelType == typeof(DashboardPageViewModel) ||
                item.ModelType == typeof(LibraryPageViewModel) ||
                item.ModelType == typeof(BrowsePageViewModel) ||
                item.ModelType == typeof(HistoryPageViewModel))
                MangaItems.Add(item);
            else
                OtherItems.Add(item);
    }

    #endregion

    #region Methods

    partial void OnSelectedListItemChanged(ListItemTemplate? value)
    {
        if (value is null) return;

        // CAMBIO CLAVE: En lugar de Activator.CreateInstance, usamos el contenedor de servicios.
        // Esto permite que los sub-viewmodels reciban sus dependencias (DI).
        var viewModel = (ViewModelBase)_serviceProvider.GetRequiredService(value.ModelType);
        CurrentPage = viewModel;
    }

    private void OnNavigationRequested(Type viewModelType)
    {
        var item = MangaItems.Concat(OtherItems).FirstOrDefault(candidate => candidate.ModelType == viewModelType);
        if (item is not null) SelectedListItem = item;
    }

    protected sealed override void UpdateLocalizedTexts()
    {
        foreach (var item in MangaItems) item.UpdateLabel();
        foreach (var item in OtherItems) item.UpdateLabel();
    }

    #endregion
}

public partial class ListItemTemplate : ObservableObject
{
    private readonly string? _translationKey;

    [ObservableProperty] private string? _label;

    public ListItemTemplate(Type type, string iconKey, string? translationKey)
    {
        ModelType = type;
        Application.Current!.TryFindResource(iconKey, out var res);
        ListItemIcon = (StreamGeometry)res!;

        _translationKey = translationKey;
        UpdateLabel();
    }

    public Type ModelType { get; set; }
    public StreamGeometry ListItemIcon { get; set; }

    public void UpdateLabel()
    {
        Label = _translationKey == null
            ? ModelType.Name.Replace("PageViewModel", string.Empty)
            : LanguageHelper.GetText(_translationKey);
    }
}
