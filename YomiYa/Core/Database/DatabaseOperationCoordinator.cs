using System;
using System.Threading;
using System.Threading.Tasks;

namespace YomiYa.Core.Database;

/// <summary>Allows normal database operations to run concurrently while maintenance waits for them to drain.</summary>
public sealed class DatabaseOperationCoordinator
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _maintenanceSerial = new(1, 1);
    private TaskCompletionSource _changed = NewSignal();
    private int _activeOperations;
    private bool _maintenanceRequested;

    public async ValueTask<IDisposable> EnterOperationAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task waitTask;
            lock (_sync)
            {
                if (!_maintenanceRequested)
                {
                    _activeOperations++;
                    return new Lease(this, false);
                }
                waitTask = _changed.Task;
            }
            await waitTask.WaitAsync(cancellationToken);
        }
    }

    public async Task<IDisposable> EnterMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        await _maintenanceSerial.WaitAsync(cancellationToken);
        lock (_sync)
        {
            _maintenanceRequested = true;
            _changed.TrySetResult();
            _changed = NewSignal();
        }

        try
        {
            while (true)
            {
                Task waitTask;
                lock (_sync)
                {
                    if (_activeOperations == 0) return new Lease(this, true);
                    waitTask = _changed.Task;
                }
                await waitTask.WaitAsync(cancellationToken);
            }
        }
        catch
        {
            lock (_sync)
            {
                _maintenanceRequested = false;
                _changed.TrySetResult();
                _changed = NewSignal();
            }
            _maintenanceSerial.Release();
            throw;
        }
    }

    private void Exit(bool maintenance)
    {
        lock (_sync)
        {
            if (maintenance)
            {
                _maintenanceRequested = false;
                _changed.TrySetResult();
                _changed = NewSignal();
            }
            else
            {
                _activeOperations--;
                if (_activeOperations == 0)
                {
                    _changed.TrySetResult();
                    _changed = NewSignal();
                }
            }
        }
        if (maintenance) _maintenanceSerial.Release();
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Lease(DatabaseOperationCoordinator owner, bool maintenance) : IDisposable
    {
        private DatabaseOperationCoordinator? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Exit(maintenance);
    }
}
