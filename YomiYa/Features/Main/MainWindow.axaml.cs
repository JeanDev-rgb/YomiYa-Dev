using System;
using FluentAvalonia.UI.Windowing;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Main;

public partial class MainWindow : AppWindow
{
    public MainWindow()
    {
        InitializeComponent();
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;
        Instance = this;
        UpdateTitle();
        LanguageHelper.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        Title = LanguageHelper.GetText("AppTitle");
    }

    public static MainWindow? Instance { get; private set; }
}