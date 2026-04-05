using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using YomiYa.Core.Imaging;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Ad;

public partial class AdPageViewModel : ViewModelBase
{
    // Propiedad para el Lemontag para poder enlazarla en la vista
    [ObservableProperty] private string _lemontag = "$jeandev";
    [ObservableProperty] private Bitmap? _qrUrl;

    public AdPageViewModel()
    {
        // Carga los textos iniciales y la imagen QR al crear el ViewModel
        UpdateLocalizedTexts();
        LoadQr();
    }

    protected sealed override void UpdateLocalizedTexts()
    {
        // Carga los textos simples
        GetOneDollarFreeText = LanguageHelper.GetText("GetOneDollarFree");
        WithLemonCashText = LanguageHelper.GetText("WithLemonCash");
        FirstDollarIntroText = LanguageHelper.GetText("FirstDollarIntro");
        RewardIn3StepsText = LanguageHelper.GetText("RewardIn3Steps");
        DownloadTheAppText = LanguageHelper.GetText("DownloadTheApp");
        Step1DescriptionText = LanguageHelper.GetText("Step1Description");
        SignUpText = LanguageHelper.GetText("SignUp");
        Step2DescriptionText = LanguageHelper.GetText("CreateYourAccountDescription");
        EnterLemontagText = LanguageHelper.GetText("EnterLemontag");
        ItsYourMomentText = LanguageHelper.GetText("ItsYourMoment");
        DownloadTheAppButtonText = LanguageHelper.GetText("DownloadTheApp");

        // Divide los textos que necesitan formato especial
        var step3FullText = LanguageHelper.GetText("Step3Description");
        var step3Parts = step3FullText.Split(new[] { "{0}" }, StringSplitOptions.None);
        Step3DescriptionBefore = step3Parts.Length > 0 ? step3Parts[0] : "";
        Step3DescriptionAfter = step3Parts.Length > 1 ? step3Parts[1] : "";

        var finalCallFullText = LanguageHelper.GetText("FinalCallToAction");
        var finalCallParts = finalCallFullText.Split(new[] { "{0}" }, StringSplitOptions.None);
        FinalCallToActionBefore = finalCallParts.Length > 0 ? finalCallParts[0] : "";
        FinalCallToActionAfter = finalCallParts.Length > 1 ? finalCallParts[1] : "";
    }

    private async void LoadQr()
    {
        try
        {
            QrUrl =
                await
                    "https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=https://lemon.go.link/8r5k6&bgcolor=2E8B57&color=F3FFBD"
                        .LoadImageAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine($"No se pudo cargar el código QR. {e.Message}");
        }
    }

    #region Localized Properties

    // Cabecera
    [ObservableProperty] private string _getOneDollarFreeText = string.Empty;
    [ObservableProperty] private string _withLemonCashText = string.Empty;
    [ObservableProperty] private string _firstDollarIntroText = string.Empty;

    // Pasos
    [ObservableProperty] private string _rewardIn3StepsText = string.Empty;
    [ObservableProperty] private string _downloadTheAppText = string.Empty;
    [ObservableProperty] private string _step1DescriptionText = string.Empty;
    [ObservableProperty] private string _signUpText = string.Empty;
    [ObservableProperty] private string _step2DescriptionText = string.Empty;
    [ObservableProperty] private string _enterLemontagText = string.Empty;

    // Propiedades para texto formateado del Paso 3
    [ObservableProperty] private string _step3DescriptionBefore = string.Empty;
    [ObservableProperty] private string _step3DescriptionAfter = string.Empty;

    // Sección Final
    [ObservableProperty] private string _itsYourMomentText = string.Empty;
    [ObservableProperty] private string _downloadTheAppButtonText = string.Empty;

    // Propiedades para texto formateado de la Sección Final
    [ObservableProperty] private string _finalCallToActionBefore = string.Empty;
    [ObservableProperty] private string _finalCallToActionAfter = string.Empty;

    #endregion
}