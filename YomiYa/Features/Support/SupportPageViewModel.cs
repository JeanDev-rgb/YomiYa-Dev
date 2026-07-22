using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Support;

public partial class SupportPageViewModel : ViewModelBase
{
    private const string PaypalUrl = "https://paypal.me/JeanDev09";
    private const string KofiUrl = "https://ko-fi.com/jeandev";
    private const string PatreonUrl = "https://www.patreon.com/JeanDev09";

    // Textos localizados
    [ObservableProperty] private string _supportTitleText = string.Empty;
    [ObservableProperty] private string _supportSubtitleText = string.Empty;
    [ObservableProperty] private string _supportMessageText = string.Empty;
    [ObservableProperty] private string _donatePaypalText = string.Empty;
    [ObservableProperty] private string _donateKofiText = string.Empty;
    [ObservableProperty] private string _donatePatreonText = string.Empty;
    [ObservableProperty] private string _whySupportText = string.Empty;
    [ObservableProperty] private string _reason1Text = string.Empty;
    [ObservableProperty] private string _reason2Text = string.Empty;
    [ObservableProperty] private string _reason3Text = string.Empty;
    [ObservableProperty] private string _thankYouText = string.Empty;

    public SupportPageViewModel()
    {
        UpdateLocalizedTexts();
    }

    [RelayCommand]
    private void OpenPaypal() => OpenUrl(PaypalUrl);

    [RelayCommand]
    private void OpenKofi() => OpenUrl(KofiUrl);

    [RelayCommand]
    private void OpenPatreon() => OpenUrl(PatreonUrl);

    private static void OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch
        {
            // Ignorar si el navegador no responde o falla
        }
    }

    protected sealed override void UpdateLocalizedTexts()
    {
        SupportTitleText = LanguageHelper.GetText("SupportTitle");
        SupportSubtitleText = LanguageHelper.GetText("SupportSubtitle");
        SupportMessageText = LanguageHelper.GetText("SupportMessage");
        DonatePaypalText = LanguageHelper.GetText("DonatePaypal");
        DonateKofiText = LanguageHelper.GetText("DonateKofi");
        DonatePatreonText = LanguageHelper.GetText("DonatePatreon");
        WhySupportText = LanguageHelper.GetText("WhySupport");
        Reason1Text = LanguageHelper.GetText("SupportReason1");
        Reason2Text = LanguageHelper.GetText("SupportReason2");
        Reason3Text = LanguageHelper.GetText("SupportReason3");
        ThankYouText = LanguageHelper.GetText("ThankYou");
    }
}