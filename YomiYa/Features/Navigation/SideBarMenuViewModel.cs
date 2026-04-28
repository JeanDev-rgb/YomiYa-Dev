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
using YomiYa.Features.Ad;
using YomiYa.Features.Discover;
using YomiYa.Features.Library;
using YomiYa.Features.Settings;

namespace YomiYa.Features.Navigation;

public partial class SideBarMenuViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;

    #region Constructor

    public SideBarMenuViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        // Inicializamos la página por defecto usando el ServiceProvider
        _currentPage = _serviceProvider.GetRequiredService<LibraryPageViewModel>();

        SelectedListItem = Items.First();
        UpdateLocalizedTexts();
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void TriggerPane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    #endregion

    #region Properties

    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private bool _isPaneOpen = true;
    [ObservableProperty] private ListItemTemplate? _selectedListItem;

    public ObservableCollection<ListItemTemplate> Items { get; set; } =
    [
        new(typeof(LibraryPageViewModel), "Library", "Library"),
        new(typeof(BrowsePageViewModel), "Browse", "Browse"),
        new(typeof(HistoryPageViewModel), "History", "History"),
        new(typeof(MorePageViewModel), "More", "More"),
        new(typeof(AdPageViewModel), "Dollar", "Reward1Dollar")
    ];

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

    protected sealed override void UpdateLocalizedTexts()
    {
        foreach (var item in Items) item.UpdateLabel();
    }

    #endregion
}

public partial class ListItemTemplate : ObservableObject
{
    private readonly string? _translationKey;

    public ListItemTemplate(Type type, string iconKey, string? translationKey)
    {
        ModelType = type;
        Application.Current!.TryFindResource(iconKey, out var res);
        ListItemIcon = (StreamGeometry)res!;

        _translationKey = translationKey;
        UpdateLabel();
    }

    public void UpdateLabel()
    {
        Label = _translationKey == null
            ? ModelType.Name.Replace("PageViewModel", string.Empty)
            : LanguageHelper.GetText(_translationKey);
    }

    [ObservableProperty] private string? _label;
    public Type ModelType { get; set; }
    public StreamGeometry ListItemIcon { get; set; }
}