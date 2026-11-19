using System;
using Avalonia.Controls;
using YomiYa.Features.Ads;
using FluentAvalonia.UI.Windowing;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Main;

public partial class MainWindow : FAAppWindow
{
    private bool _webViewShutdownStarted;
    private bool _webViewShutdownComplete;

    public MainWindow()
    {
        InitializeComponent();
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = FATitleBarHitTestType.Complex;
        Instance = this;
        UpdateTitle();
        LanguageHelper.LanguageChanged += OnLanguageChanged;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    public static MainWindow? Instance { get; private set; }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        Title = LanguageHelper.GetText("AppTitle");
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_webViewShutdownComplete)
            return;

        e.Cancel = true;
        if (_webViewShutdownStarted)
            return;

        _webViewShutdownStarted = true;
        try
        {
            await AdSidebarControl.DetachWebViewsForShutdownAsync();
            await NativeWebViewAdapterLifetime.WaitForAllAdaptersDestroyedAsync();
            AdSidebarHost.Child = null;
            _webViewShutdownComplete = true;
            Close();
        }
        catch (Exception ex)
        {
            _webViewShutdownStarted = false;
            Console.WriteLine($"No se pudieron liberar los WebView antes del cierre: {ex}");
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        LanguageHelper.LanguageChanged -= OnLanguageChanged;
        if (ReferenceEquals(Instance, this))
            Instance = null;
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.Dispose();
    }
}
