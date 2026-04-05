using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Core.Localization;
using YomiYa.Features.Ad;
using YomiYa.Features.Discover;
using YomiYa.Features.Library;
using YomiYa.Features.Settings;

namespace YomiYa.Features.Navigation;

public partial class SideBarMenuViewModel : ViewModelBase
{
    #region Constructor

    public SideBarMenuViewModel()
    {
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

    [ObservableProperty] private ViewModelBase _currentPage = new LibraryPageViewModel();
    [ObservableProperty] private bool _isPaneOpen = true;
    [ObservableProperty] private ListItemTemplate? _selectedListItem;

    public ObservableCollection<ListItemTemplate> Items { get; set; } =
    [
        new(typeof(LibraryPageViewModel), "Library", "Library"),
        new(typeof(BrowsePageViewModel), "Browse", "Browse"),
        // new(typeof(UpdatesPageViewModel), "Updates", "Updates"),
        new(typeof(HistoryPageViewModel), "History", "History"),
        new(typeof(MorePageViewModel), "More", "More"),
        new(typeof(AdPageViewModel), "Dollar", "Reward1Dollar")
    ];

    #endregion

    #region Methods

    partial void OnSelectedListItemChanged(ListItemTemplate? value)
    {
        if (value is null) return;
        var instance = Activator.CreateInstance(value.ModelType);
        if (instance is null) return;
        CurrentPage = (ViewModelBase)instance;
    }

    protected sealed override void UpdateLocalizedTexts()
    {
        foreach (var item in Items) item.UpdateLabel(); // Llama al método para actualizar el texto en cada ítem
    }

    #endregion
}

public partial class ListItemTemplate : ObservableObject
{
    #region Fields

    private readonly string? _translationKey; // Guardamos la clave de traducción

    #endregion

    #region Constructor

    public ListItemTemplate(Type type, string iconKey, string? translationKey)
    {
        ModelType = type;
        Application.Current!.TryFindResource(iconKey, out var res);
        ListItemIcon = (StreamGeometry)res!;

        _translationKey = translationKey;
        UpdateLabel(); // Establece el texto inicial
    }

    #endregion

    #region Public Methods

    // Actualiza el Label cuando el idioma cambia
    public void UpdateLabel()
    {
        Label = _translationKey == null
            ? ModelType.Name.Replace("PageViewModel", string.Empty)
            : LanguageHelper.GetText(_translationKey);
    }

    #endregion

    #region Properties

    [ObservableProperty] private string? _label;

    public Type ModelType { get; set; }
    public StreamGeometry ListItemIcon { get; set; }

    #endregion
}