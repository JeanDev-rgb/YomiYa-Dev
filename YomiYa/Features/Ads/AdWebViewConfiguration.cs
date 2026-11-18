using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Platform;

namespace YomiYa.Features.Ads;

internal static class AdWebViewConfiguration
{
    public static readonly Uri WideBanner = new("https://yomiya.streamifyplus.dev/banner-728.html");
    public static readonly Uri CompactBanner = new("https://yomiya.streamifyplus.dev/banner-320.html");
    public static readonly Uri Native = new("https://yomiya.streamifyplus.dev/native.html");

    public static void ConfigureEnvironment(object? sender, WebViewEnvironmentRequestedEventArgs args)
    {
        if (args is not WindowsWebView2EnvironmentRequestedEventArgs webView2)
            return;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        webView2.UserDataFolder = Path.Combine(localAppData, "YomiYa", "WebView2");
        webView2.ProfileName = "Advertisements";
    }
}
