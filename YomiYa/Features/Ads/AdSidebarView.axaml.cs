using System.Threading.Tasks;
using Avalonia.Controls;

namespace YomiYa.Features.Ads;

public partial class AdSidebarView : UserControl
{
    public AdSidebarView() => InitializeComponent();

    public Task DetachWebViewsForShutdownAsync()
    {
        var bannerShutdown = BannerAd.DetachWebViewsForShutdownAsync();
        var nativeAdShutdown = NativeAd.DetachWebViewForShutdownAsync();
        return Task.WhenAll(bannerShutdown, nativeAdShutdown);
    }
}
