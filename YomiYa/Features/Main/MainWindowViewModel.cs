using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using YomiYa.Core.Localization;
using YomiYa.Core.Navigation;
using YomiYa.Core.Settings;

namespace YomiYa.Features.Main;

public partial class MainWindowViewModel : ViewModelBase, System.IDisposable
{
    private readonly ISettingsService? _settingsService;

    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private bool _isAdSidebarExpanded = true;
    [ObservableProperty] private bool _isReaderOpen;
    [ObservableProperty] private bool _isCompactWindow = true;
    [ObservableProperty] private string _adSidebarHeaderText = string.Empty;
    [ObservableProperty] private string _adSidebarToggleText = string.Empty;

    public string AdSidebarToggleGlyph => IsAdSidebarExpanded ? "\u203A" : "\u2039";

    public double AdSidebarWidth => AdSidebarLayoutPolicy.GetWidth(IsReaderOpen, IsCompactWindow, IsAdSidebarExpanded);

    public double AdSidebarContentOpacity => IsReaderOpen || IsCompactWindow || !IsAdSidebarExpanded ? 0 : 1;

    public MainWindowViewModel()
    {
        _settingsService = Program.ServiceProvider?.GetService<ISettingsService>();
        _isAdSidebarExpanded = _settingsService?.Settings.IsAdSidebarExpanded ?? true;
        CurrentPage = NavigationHelper.CurrentViewModel;
        IsReaderOpen = NavigationHelper.IsReaderOpen;
        NavigationHelper.OnCurrentViewModelChanged += OnNavigationHelperCurrentViewModelChanged;
        NavigationHelper.ReaderVisibilityChanged += OnReaderVisibilityChanged;
        UpdateLocalizedTexts();
    }

    private void OnNavigationHelperCurrentViewModelChanged(ViewModelBase newViewModel) => CurrentPage = newViewModel;

    private void OnReaderVisibilityChanged(bool isOpen) => IsReaderOpen = isOpen;

    public void UpdateWindowWidth(double width)
    {
        if (width <= 0)
            return;

        IsCompactWindow = AdSidebarLayoutPolicy.IsCompact(width);
    }

    [RelayCommand]
    private void ToggleAdSidebar()
    {
        var expanded = !IsAdSidebarExpanded;
        if (_settingsService is not null && _settingsService.Settings.IsAdSidebarExpanded != expanded)
        {
            _settingsService.Settings.IsAdSidebarExpanded = expanded;
            _settingsService.Save();
        }

        IsAdSidebarExpanded = expanded;
    }

    partial void OnIsAdSidebarExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(AdSidebarWidth));
        OnPropertyChanged(nameof(AdSidebarContentOpacity));
        OnPropertyChanged(nameof(AdSidebarToggleGlyph));
        UpdateToggleText();
    }

    partial void OnIsReaderOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(AdSidebarWidth));
        OnPropertyChanged(nameof(AdSidebarContentOpacity));
    }

    partial void OnIsCompactWindowChanged(bool value)
    {
        OnPropertyChanged(nameof(AdSidebarWidth));
        OnPropertyChanged(nameof(AdSidebarContentOpacity));
    }

    protected override void UpdateLocalizedTexts()
    {
        AdSidebarHeaderText = LanguageHelper.GetText("AdSidebarHeader");
        UpdateToggleText();
    }

    private void UpdateToggleText() => AdSidebarToggleText = LanguageHelper.GetText(
        IsAdSidebarExpanded ? "CollapseAdSidebar" : "ExpandAdSidebar");

    public void Dispose()
    {
        NavigationHelper.OnCurrentViewModelChanged -= OnNavigationHelperCurrentViewModelChanged;
        NavigationHelper.ReaderVisibilityChanged -= OnReaderVisibilityChanged;
        System.GC.SuppressFinalize(this);
    }
}
