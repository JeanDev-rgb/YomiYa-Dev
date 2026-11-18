using Avalonia;
using Avalonia.Controls;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Ads;

public partial class BannerAdView : UserControl
{
    private bool _usingWideBanner;

    public BannerAdView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        UpdateDisclosureLabel();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LanguageHelper.LanguageChanged += OnLanguageChanged;
        UpdateDisclosureLabel();
        UpdateBanner();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LanguageHelper.LanguageChanged -= OnLanguageChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateBanner();

    private void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e) =>
        AdWebViewConfiguration.ConfigureEnvironment(sender, e);

    private void OnLanguageChanged(object? sender, System.EventArgs e) => UpdateDisclosureLabel();

    private void UpdateDisclosureLabel() => DisclosureLabel.Text = LanguageHelper.GetText("AdDisclosure");

    private void UpdateBanner()
    {
        var useWideBanner = Bounds.Width >= 728;
        if (Bounds.Width <= 0 || (useWideBanner == _usingWideBanner && BannerWebView.Source is not null))
            return;

        _usingWideBanner = useWideBanner;
        BannerWebView.Width = useWideBanner ? 728 : 320;
        BannerWebView.Height = useWideBanner ? 90 : 50;
        BannerWebView.Source = useWideBanner
            ? AdWebViewConfiguration.WideBanner
            : AdWebViewConfiguration.CompactBanner;
    }
}
