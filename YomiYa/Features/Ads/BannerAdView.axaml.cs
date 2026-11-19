using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Ads;

public partial class BannerAdView : UserControl
{
    private readonly NativeWebViewAdapterLifetime _wideBannerLifetime;
    private readonly NativeWebViewAdapterLifetime _compactBannerLifetime;

    public BannerAdView()
    {
        InitializeComponent();
        _wideBannerLifetime = new NativeWebViewAdapterLifetime(WideBannerWebView);
        _compactBannerLifetime = new NativeWebViewAdapterLifetime(CompactBannerWebView);
        UpdateDisclosureLabel();
        WideBannerWebView.Source = AdWebViewConfiguration.WideBanner;
        CompactBannerWebView.Source = AdWebViewConfiguration.CompactBanner;
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

    private void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e) =>
        AdWebViewConfiguration.ConfigureEnvironment(sender, e);

    private void OnLanguageChanged(object? sender, System.EventArgs e) => UpdateDisclosureLabel();

    private void UpdateDisclosureLabel() => DisclosureLabel.Text = LanguageHelper.GetText("AdDisclosure");

    public async Task DetachWebViewsForShutdownAsync()
    {
        var shutdownTasks = new[]
        {
            _wideBannerLifetime.WaitForAdapterDestroyedAsync(),
            _compactBannerLifetime.WaitForAdapterDestroyedAsync()
        };

        WideBannerWebView.Stop();
        CompactBannerWebView.Stop();
        BannerHost.Children.Remove(WideBannerWebView);
        BannerHost.Children.Remove(CompactBannerWebView);
        await Task.WhenAll(shutdownTasks);
        _wideBannerLifetime.Dispose();
        _compactBannerLifetime.Dispose();
    }
}
