using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Ads;

public partial class BannerAdView : UserControl
{
    private const double WideBannerScale = 320d / 728d;
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

    private async void OnWideBannerNavigationCompleted(
        object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
            return;

        try
        {
            // Mantiene el viewport nativo en 320 px y reduce el creativo 728x90
            // proporcionalmente dentro del propio motor web, no mediante RenderTransform.
            var zoom = WideBannerScale.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            await WideBannerWebView.InvokeScript(
                $"document.documentElement.style.zoom='{zoom}';" +
                "document.body.style.margin='0';");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo escalar el banner 728x90: {ex.Message}");
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateDisclosureLabel();

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
