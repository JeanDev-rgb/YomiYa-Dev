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
}
