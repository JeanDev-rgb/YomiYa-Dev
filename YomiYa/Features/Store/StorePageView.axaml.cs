using Avalonia.Controls;

namespace YomiYa.Features.Store;

public partial class StorePageView : UserControl
{
    public StorePageView()
    {
        InitializeComponent();

        WebView.NavigationCompleted += (_, _) =>
        {
            if (DataContext is StorePageViewModel vm)
                vm.OnNavigationCompleted();
        };
    }
}
