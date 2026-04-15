using Avalonia;
using Avalonia.Controls;

namespace YomiYa.Features.Library;

public partial class HistoryPageView : UserControl
{
    public HistoryPageView()
    {
        InitializeComponent();
        AttachedToVisualTree += HistoryPageView_AttachedToVisualTree;
    }

    private void HistoryPageView_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is HistoryPageViewModel vm) _ = vm.LoadHistoryAsync();
    }
}