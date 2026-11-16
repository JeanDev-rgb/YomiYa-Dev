using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using YomiYa.Helper.Input.Classes;
using YomiYa.Helper.Input.Interfaces;

namespace YomiYa.Features.Discover;

public partial class PluginPageView : UserControl
{
    private SearchBoxKeyboardHelper? _searchHelper;

    public PluginPageView()
    {
        InitializeComponent();
        PropertyChanged += (sender, e) =>
        {
            if (e.Property == BoundsProperty && DataContext is PluginPageViewModel vm) vm.UpdateColumns(Bounds.Width);
        };
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _searchHelper?.Dispose();
        if (DataContext is ISearchableByKeyboard searchableViewModel)
            _searchHelper = new SearchBoxKeyboardHelper(SearchTextBox, searchableViewModel);
    }

    private void SearchScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer viewer || DataContext is not PluginPageViewModel vm) return;
        if (viewer.Offset.Y + viewer.Viewport.Height >= viewer.Extent.Height - 320)
            vm.LoadNextSearchPageCommand.Execute(null);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _searchHelper?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }
}
