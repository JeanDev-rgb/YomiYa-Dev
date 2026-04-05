using FluentAvalonia.UI.Windowing;

namespace YomiYa.Features.Main;

public partial class MainWindow : AppWindow
{
    public MainWindow()
    {
        InitializeComponent();
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;
        Instance = this;
    }

    public static MainWindow? Instance { get; private set; }
}