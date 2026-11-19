using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace YomiYa.Features.Ads;

internal sealed class NativeWebViewAdapterLifetime : IDisposable
{
    private static readonly object ActiveAdaptersSync = new();
    private static int _activeAdapterCount;
    private static TaskCompletionSource? _allAdaptersDestroyed;

    private readonly object _sync = new();
    private readonly NativeWebView? _webView;
    private readonly NativeWebDialog? _webDialog;
    private bool _isAdapterAlive;
    private TaskCompletionSource? _adapterDestroyed;

    public NativeWebViewAdapterLifetime(NativeWebView webView)
    {
        _webView = webView;
        webView.AdapterCreated += OnAdapterCreated;
        webView.AdapterDestroyed += OnAdapterDestroyed;
    }

    public NativeWebViewAdapterLifetime(NativeWebDialog webDialog)
    {
        _webDialog = webDialog;
        webDialog.AdapterCreated += OnAdapterCreated;
        webDialog.AdapterDestroyed += OnAdapterDestroyed;
    }

    public bool IsAdapterAlive
    {
        get
        {
            lock (_sync)
                return _isAdapterAlive;
        }
    }

    public static Task WaitForAllAdaptersDestroyedAsync()
    {
        lock (ActiveAdaptersSync)
        {
            if (_activeAdapterCount == 0)
                return Task.CompletedTask;

            _allAdaptersDestroyed ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _allAdaptersDestroyed.Task;
        }
    }

    public Task WaitForAdapterDestroyedAsync()
    {
        lock (_sync)
            return _isAdapterAlive ? _adapterDestroyed!.Task : Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_webView is not null)
        {
            _webView.AdapterCreated -= OnAdapterCreated;
            _webView.AdapterDestroyed -= OnAdapterDestroyed;
        }

        if (_webDialog is not null)
        {
            _webDialog.AdapterCreated -= OnAdapterCreated;
            _webDialog.AdapterDestroyed -= OnAdapterDestroyed;
        }
    }

    private void OnAdapterCreated(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            if (_isAdapterAlive)
                return;

            _isAdapterAlive = true;
            _adapterDestroyed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        lock (ActiveAdaptersSync)
            _activeAdapterCount++;
    }

    private void OnAdapterDestroyed(object? sender, EventArgs e)
    {
        TaskCompletionSource? adapterDestroyed;
        lock (_sync)
        {
            if (!_isAdapterAlive)
                return;

            _isAdapterAlive = false;
            adapterDestroyed = _adapterDestroyed;
        }

        adapterDestroyed?.TrySetResult();

        TaskCompletionSource? allAdaptersDestroyed = null;
        lock (ActiveAdaptersSync)
        {
            _activeAdapterCount--;
            if (_activeAdapterCount == 0)
            {
                allAdaptersDestroyed = _allAdaptersDestroyed;
                _allAdaptersDestroyed = null;
            }
        }

        allAdaptersDestroyed?.TrySetResult();
    }
}
