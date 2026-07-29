using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Store;

public partial class StorePageViewModel : ViewModelBase
{
    public static readonly Uri LootbarUri = new("https://www.lootbar.com/es/shop/streamifystore");

    // Textos localizados
    [ObservableProperty] private string _storeTitleText = string.Empty;
    [ObservableProperty] private string _storeSubtitleText = string.Empty;
    [ObservableProperty] private string _refreshText = string.Empty;
    [ObservableProperty] private string _openInBrowserText = string.Empty;
    [ObservableProperty] private string _openStoreText = string.Empty;

    public StorePageViewModel()
    {
        UpdateLocalizedTexts();
    }

    public event EventHandler? OpenStoreRequested;

    [RelayCommand]
    private void OpenStore()
    {
        OpenStoreRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = LootbarUri.ToString(),
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Ignorar si no se puede abrir el navegador
        }
    }

    protected sealed override void UpdateLocalizedTexts()
    {
        StoreTitleText = LanguageHelper.GetText("StoreTitle");
        StoreSubtitleText = LanguageHelper.GetText("StoreSubtitle");
        RefreshText = LanguageHelper.GetText("Refresh");
        OpenInBrowserText = LanguageHelper.GetText("OpenInBrowser");
        OpenStoreText = LanguageHelper.GetText("OpenStore");
    }
}
