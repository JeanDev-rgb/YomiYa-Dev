using Avalonia.Controls;

namespace YomiYa.Features.Library;

public partial class LibraryPageView : UserControl
{
    public LibraryPageView()
    {
        InitializeComponent();
        PropertyChanged += (sender, e) =>
        {
            if (e.Property == BoundsProperty && DataContext is LibraryPageViewModel vm) vm.UpdateColumns(Bounds.Width);
        };
    }
}