using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Ads;

public partial class NativeAdView : UserControl
{
    public NativeAdView()
    {
        InitializeComponent();
        NativeWebViewControl.Source = AdWebViewConfiguration.Native;
        UpdateDisclosureLabel();
        StatusText.Text = LanguageHelper.GetText("AdLoadingText");
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LanguageHelper.LanguageChanged += OnLanguageChanged;
        UpdateDisclosureLabel();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LanguageHelper.LanguageChanged -= OnLanguageChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private static void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e) =>
        AdWebViewConfiguration.ConfigureEnvironment(sender, e);

    private void OnLanguageChanged(object? sender, System.EventArgs e) => UpdateDisclosureLabel();

    private void UpdateDisclosureLabel() => DisclosureLabel.Text = LanguageHelper.GetText("AdDisclosure");

    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        NativeWebViewControl.IsVisible = true;
        LoadingIndicator.IsVisible = true;
        StatusText.Text = LanguageHelper.GetText("AdLoadingText");
        StatusPanel.IsVisible = true;
    }

    private void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        LoadingIndicator.IsVisible = false;
        if (e.IsSuccess)
        {
            StatusPanel.IsVisible = false;
            return;
        }

        NativeWebViewControl.IsVisible = false;
        StatusText.Text = LanguageHelper.GetText("AdsUnavailableText");
        StatusPanel.IsVisible = true;
    }
}
