using System;
using Avalonia.Controls;
using FluentAvalonia.UI.Windowing;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Main;

public partial class MainWindow : FAAppWindow
{
    public MainWindow()
    {
        InitializeComponent();
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = FATitleBarHitTestType.Complex;
        Instance = this;
        UpdateTitle();
        LanguageHelper.LanguageChanged += OnLanguageChanged;
        SizeChanged += OnSizeChanged;
        Opened += OnOpened;
        DataContextChanged += OnDataContextChanged;
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

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateAdSidebarLayout(e.NewSize.Width);

    private void OnOpened(object? sender, EventArgs e) => UpdateAdSidebarLayout(Bounds.Width);

    private void OnDataContextChanged(object? sender, EventArgs e) => UpdateAdSidebarLayout(Bounds.Width);

    private void UpdateAdSidebarLayout(double width)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.UpdateWindowWidth(width);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        LanguageHelper.LanguageChanged -= OnLanguageChanged;
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.Dispose();
    }
}
