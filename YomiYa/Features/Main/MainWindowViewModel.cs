using CommunityToolkit.Mvvm.ComponentModel;
using YomiYa.Core.Localization;
using YomiYa.Core.Navigation;

namespace YomiYa.Features.Main;

public partial class MainWindowViewModel : ViewModelBase, System.IDisposable
{
    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private string _adSidebarHeaderText = string.Empty;
    public double AdSidebarWidth => AdSidebarLayoutPolicy.ExpandedWidth;

    public MainWindowViewModel()
    {
        CurrentPage = NavigationHelper.CurrentViewModel;
        NavigationHelper.OnCurrentViewModelChanged += OnNavigationHelperCurrentViewModelChanged;
        UpdateLocalizedTexts();
    }

    private void OnNavigationHelperCurrentViewModelChanged(ViewModelBase newViewModel) => CurrentPage = newViewModel;

    protected override void UpdateLocalizedTexts()
    {
        AdSidebarHeaderText = LanguageHelper.GetText("AdSidebarHeader");
    }

    public void Dispose()
    {
        NavigationHelper.OnCurrentViewModelChanged -= OnNavigationHelperCurrentViewModelChanged;
        System.GC.SuppressFinalize(this);
    }
}
