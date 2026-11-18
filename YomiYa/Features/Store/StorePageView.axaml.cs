using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace YomiYa.Features.Store;

public partial class StorePageView : UserControl
{
    private StorePageViewModel? _viewModel;
    private NativeWebDialog? _storeDialog;
    private bool _openedOnAttach;
    private bool _isAttached;

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
        if (_isAttached && _viewModel is not null)
            _viewModel.OpenStoreRequested += OnOpenStoreRequested;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;

        if (_viewModel is not null)
            _viewModel.OpenStoreRequested += OnOpenStoreRequested;

        if (_openedOnAttach)
            return;

        _openedOnAttach = true;
        OpenStoreDialog();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
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

        var dialog = new NativeWebDialog
        {
            Title = _viewModel?.StoreTitleText ?? "Lootbar",
            Source = StorePageViewModel.LootbarUri,
            CanUserResize = true
        };
        _storeDialog = dialog;

        dialog.EnvironmentRequested += OnEnvironmentRequested;
        dialog.AdapterDestroyed += OnStoreAdapterDestroyed;
        dialog.TryGetWindow()?.Closed += OnStoreWindowClosed;
        dialog.NewWindowRequested += OnNewWindowRequested;

        if (TopLevel.GetTopLevel(this) is { } owner)
            dialog.Show(owner);
        else
            dialog.Show();

        dialog.Resize(1100, 760);
        dialog.TryGetWindow()?.Activate();
    }

    private static void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs args)
    {
        if (args is WindowsWebView2EnvironmentRequestedEventArgs webView2)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            webView2.UserDataFolder = Path.Combine(localAppData, "YomiYa", "WebView2");
            webView2.ProfileName = "Store";
        }
    }

    private void OnStoreWindowClosed(object? sender, EventArgs e) => DisposeStoreDialog();

    private void OnStoreAdapterDestroyed(object? sender, EventArgs e)
    {
        if (_storeDialog?.TryGetWindow() is null)
            DisposeStoreDialog();
    }

    private static void OnNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        if (args.Request is not null)
            OpenExternal(args.Request);
    }

    private void DisposeStoreDialog()
    {
        var dialog = _storeDialog;
        if (dialog is null)
            return;

        _storeDialog = null;
        dialog.EnvironmentRequested -= OnEnvironmentRequested;
        dialog.AdapterDestroyed -= OnStoreAdapterDestroyed;
        dialog.TryGetWindow()?.Closed -= OnStoreWindowClosed;
        dialog.NewWindowRequested -= OnNewWindowRequested;
        dialog.Dispose();
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
