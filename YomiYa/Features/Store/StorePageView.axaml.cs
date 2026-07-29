using System;
using Avalonia;
using Avalonia.Controls;

namespace YomiYa.Features.Store;

public partial class StorePageView : UserControl
{
    private StorePageViewModel? _viewModel;
    private NativeWebDialog? _storeDialog;
    private bool _openedOnAttach;

    public StorePageView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.OpenStoreRequested -= OnOpenStoreRequested;

        _viewModel = DataContext as StorePageViewModel;

        if (_viewModel is not null)
            _viewModel.OpenStoreRequested += OnOpenStoreRequested;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_openedOnAttach)
            return;

        _openedOnAttach = true;
        OpenStoreDialog();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.OpenStoreRequested -= OnOpenStoreRequested;

        base.OnDetachedFromVisualTree(e);
    }

    private void OnOpenStoreRequested(object? sender, EventArgs e)
    {
        OpenStoreDialog();
    }

    private void OpenStoreDialog()
    {
        if (_storeDialog is not null)
        {
            _storeDialog.TryGetWindow()?.Activate();
            _storeDialog.Refresh();
            return;
        }

        _storeDialog = new NativeWebDialog
        {
            Title = _viewModel?.StoreTitleText ?? "Lootbar",
            Source = StorePageViewModel.LootbarUri,
            CanUserResize = true
        };

        _storeDialog.Closing += (_, _) =>
        {
            _storeDialog?.Dispose();
            _storeDialog = null;
        };

        _storeDialog.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            if (args.Request is not null)
                OpenExternal(args.Request);
        };

        if (TopLevel.GetTopLevel(this) is { } owner)
            _storeDialog.Show(owner);
        else
            _storeDialog.Show();

        _storeDialog.Resize(1100, 760);
        _storeDialog.TryGetWindow()?.Activate();
    }

    private static void OpenExternal(Uri uri)
    {
        try
        {
            using var _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = uri.ToString(),
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore external browser failures; the embedded view remains usable.
        }
    }
}
