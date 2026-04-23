using System;
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
}