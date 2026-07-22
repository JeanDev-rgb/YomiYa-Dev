using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Store;

public partial class StorePageViewModel : ViewModelBase
{
    private const string LootbarUrl = "https://www.lootbar.com/es/shop/streamifystore";

    [ObservableProperty] private Uri _storeSource = new(LootbarUrl);
    [ObservableProperty] private bool _isLoading = true;

    // Textos localizados
    [ObservableProperty] private string _storeTitleText = string.Empty;
    [ObservableProperty] private string _storeSubtitleText = string.Empty;
    [ObservableProperty] private string _loadingText = string.Empty;
    [ObservableProperty] private string _refreshText = string.Empty;
    [ObservableProperty] private string _openInBrowserText = string.Empty;

    public StorePageViewModel()
    {
        UpdateLocalizedTexts();
    }

    [RelayCommand]
    private void Refresh()
    {
        IsLoading = true;
        StoreSource = new Uri("about:blank");
        StoreSource = new Uri(LootbarUrl);
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = LootbarUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Ignorar si no se puede abrir el navegador
        }
    }

    public void OnNavigationCompleted()
    {
        IsLoading = false;
    }

    protected sealed override void UpdateLocalizedTexts()
    {
        StoreTitleText = LanguageHelper.GetText("StoreTitle");
        StoreSubtitleText = LanguageHelper.GetText("StoreSubtitle");
        LoadingText = LanguageHelper.GetText("Loading");
        RefreshText = LanguageHelper.GetText("Refresh");
        OpenInBrowserText = LanguageHelper.GetText("OpenInBrowser");
    }
}
