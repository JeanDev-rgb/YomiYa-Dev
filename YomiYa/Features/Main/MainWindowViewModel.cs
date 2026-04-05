using CommunityToolkit.Mvvm.ComponentModel;
using YomiYa.Core.Navigation;

namespace YomiYa.Features.Main;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentPage;

    public MainWindowViewModel()
    {
        NavigationHelper.OnCurrentViewModelChanged += OnNavigationHelperCurrentViewModelChanged;
        _currentPage = NavigationHelper.CurrentViewModel;
    }

    private void OnNavigationHelperCurrentViewModelChanged(ViewModelBase newViewModel)
    {
        CurrentPage = newViewModel;
    }

    protected override void UpdateLocalizedTexts()
    {
    }
}