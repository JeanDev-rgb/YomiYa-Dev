using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YomiYa.Core.IPC;

public sealed class PluginTcpServer : IDisposable
{
    private readonly ConcurrentDictionary<string, PendingRequest> _pendingRequests = new();
    private readonly object _connectionLock = new();
    private readonly SemaphoreSlim _connectionWaitLock = new(1, 1);
    private TcpListener? _listener;
    private CancellationTokenSource? _serverCts;
    private Connection? _connection;
    private TaskCompletionSource<bool> _connectionTcs = NewConnectionSignal();
    private Process? _pluginProcess;
    private bool _isRunning;

    public event Action? OnPluginDisconnected;

    public bool IsConnected
    {
        get
        {
            Connection? connection;
            lock (_connectionLock) connection = _connection;
            return connection?.IsAlive == true;
        }
    }

    public void Start(int port = 50000)
    {
        if (_isRunning) return;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _isRunning = true;
        _serverCts = new CancellationTokenSource();
        _ = AcceptClientsAsync(_serverCts.Token);
        Console.WriteLine($"[YomiYa TCP] Servidor escuchando en 127.0.0.1:{port}");
    }

    public void AttachPluginProcess(Process process)
    {
        if (_pluginProcess is not null) _pluginProcess.Exited -= PluginProcess_Exited;
        _pluginProcess = process;
        process.EnableRaisingEvents = true;
        process.Exited += PluginProcess_Exited;
    }

    private void PluginProcess_Exited(object? sender, EventArgs e)
    {
        Console.WriteLine("[YomiYa TCP] El proceso del plugin terminó.");
        DisconnectCurrentConnection();
    }

    public void Stop()
    {
        _isRunning = false;
        _serverCts?.Cancel();
        _listener?.Stop();
        DisconnectCurrentConnection();
    }

    private async Task AcceptClientsAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var client = await _listener!.AcceptTcpClientAsync(token);
                var next = new Connection(client);
                Connection? previous;
                lock (_connectionLock)
                {
                    previous = _connection;
                    _connection = next;
                    _connectionTcs.TrySetResult(true);
                }
                if (previous is not null) Disconnect(previous, notify: false);
                Console.WriteLine("[YomiYa TCP] Una extensión se ha conectado.");
                _ = ReceiveMessagesAsync(next, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Console.WriteLine($"[YomiYa TCP] Error al aceptar cliente: {ex}"); }
    }

    public async Task WaitForConnectionAsync(int timeoutMs = 5000, CancellationToken cancellationToken = default)
    {
        if (IsConnected) return;
        await _connectionWaitLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected) return;
            lock (_connectionLock)
            {
                if (_connection?.IsAlive == true) return;
                if (_connectionTcs.Task.IsCompleted) _connectionTcs = NewConnectionSignal();
            }
            try { await _connectionTcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken); }
            catch (TimeoutException) { throw new TimeoutException("La extensión tardó demasiado en conectarse o terminó inesperadamente."); }
        }
        finally { _connectionWaitLock.Release(); }
    }

    private async Task ReceiveMessagesAsync(Connection connection, CancellationToken serverToken)
    {
        try
        {
            while (!serverToken.IsCancellationRequested && !connection.Token.IsCancellationRequested)
            {
                var line = await connection.Reader.ReadLineAsync(connection.Token);
                if (line is null) break;
                var message = JsonSerializer.Deserialize<TcpMessage>(line);
                if (message is not null && _pendingRequests.TryGetValue(message.RequestId, out var pending) &&
                    ReferenceEquals(pending.Connection, connection) && _pendingRequests.TryRemove(message.RequestId, out pending))
                    pending.Completion.TrySetResult(message);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException ex) { Console.WriteLine($"[YomiYa TCP] La conexión terminó durante la lectura: {ex.Message}"); }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { Console.WriteLine($"[YomiYa TCP] Error de lectura: {ex}"); }
        finally { Disconnect(connection); }
    }

    private void DisconnectCurrentConnection()
    {
        Connection? connection;
        lock (_connectionLock) connection = _connection;
        if (connection is not null) Disconnect(connection);
    }

    private void Disconnect(Connection connection, bool notify = true)
    {
        var wasCurrent = false;
        lock (_connectionLock)
        {
            if (ReferenceEquals(_connection, connection))
            {
                _connection = null;
                wasCurrent = true;
                if (_connectionTcs.Task.IsCompleted) _connectionTcs = NewConnectionSignal();
            }
        }
        connection.Dispose();
        foreach (var pair in _pendingRequests)
        {
            if (ReferenceEquals(pair.Value.Connection, connection) && _pendingRequests.TryRemove(pair.Key, out var pending))
                pending.Completion.TrySetException(new IOException("La conexión con el plugin se perdió antes de recibir respuesta."));
        }
        if (wasCurrent && notify) OnPluginDisconnected?.Invoke();
    }

    public async Task<TcpMessage> SendRequestAsync(
        string action,
        object? payload = null,
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        Connection connection;
        lock (_connectionLock) connection = _connection ?? throw new InvalidOperationException("No hay ninguna extensión conectada.");
        if (!connection.IsAlive || (_pluginProcess is not null && _pluginProcess.HasExited))
        {
            Disconnect(connection);
            throw new IOException("La conexión con la extensión se cerró.");
        }

        var requestId = Guid.NewGuid().ToString("N");
        var message = new TcpMessage { Action = action, RequestId = requestId };
        if (payload is not null) message.SetPayload(payload);
        var completion = new TaskCompletionSource<TcpMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = new PendingRequest(connection, completion);
        try
        {
            var json = JsonSerializer.Serialize(message);
            await connection.WriteLock.WaitAsync(cancellationToken);
            try
            {
                if (!connection.IsAlive) throw new IOException("La conexión con el plugin se cerró antes de enviar la solicitud.");
                await connection.Writer.WriteLineAsync(json.AsMemory(), cancellationToken);
            }
            finally { connection.WriteLock.Release(); }
            return await completion.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);
        }
        catch (TimeoutException) { throw new TimeoutException($"El plugin no respondió a '{action}' en {timeoutMs} ms."); }
        catch (IOException)
        {
            Disconnect(connection);
            throw;
        }
        finally { _pendingRequests.TryRemove(requestId, out _); }
    }

    private static TaskCompletionSource<bool> NewConnectionSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Dispose()
    {
        Stop();
        if (_pluginProcess is not null) _pluginProcess.Exited -= PluginProcess_Exited;
        _serverCts?.Dispose();
        _connectionWaitLock.Dispose();
    }

    private sealed record PendingRequest(Connection Connection, TaskCompletionSource<TcpMessage> Completion);

    private sealed class Connection : IDisposable
    {
        private int _disposed;
        public Connection(TcpClient client)
        {
            Client = client;
            Client.NoDelay = true;
            var stream = client.GetStream();
            Reader = new StreamReader(stream);
            Writer = new StreamWriter(stream) { AutoFlush = true };
        }
        public TcpClient Client { get; }
        public StreamReader Reader { get; }
        public StreamWriter Writer { get; }
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
        public CancellationTokenSource Cancellation { get; } = new();
        public CancellationToken Token => Cancellation.Token;
        public bool IsAlive
        {
            get
            {
                if (Volatile.Read(ref _disposed) != 0) return false;
                try { return Client.Client.Poll(0, SelectMode.SelectRead) ? Client.Client.Available > 0 : true; }
                catch (SocketException) { return false; }
                catch (ObjectDisposedException) { return false; }
            }
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Cancellation.Cancel();
            try { Client.Close(); } catch { }
            Reader.Dispose();
            Writer.Dispose();
            Cancellation.Dispose();
        }
    }
}
