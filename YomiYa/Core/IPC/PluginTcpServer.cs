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

public class PluginTcpServer : IDisposable
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TcpMessage>> _pendingRequests = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _connectionLock = new();

    private CancellationTokenSource? _serverCts;
    private TcpListener? _listener;
    private TcpClient? _connectedClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    
    // Inicializado con RunContinuationsAsynchronously para evitar deadlocks al completar
    private TaskCompletionSource<bool> _connectionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Process? _pluginProcess;
    private bool _isRunning;

    public event Action? OnPluginDisconnected;

    public bool IsConnected => _connectedClient is { Connected: true };

    public void Start(int port = 50000)
    {
        if (_isRunning) return;

        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _isRunning = true;
        _serverCts = new CancellationTokenSource();

        _ = Task.Run(() => AcceptClientsAsync(_serverCts.Token));
        Console.WriteLine($"[YomiYa TCP] Servidor escuchando en 127.0.0.1:{port}");
    }

    /// <summary>
    /// Vincula el proceso del plugin para detectar cierres inesperados o crashes al instante.
    /// </summary>
    public void AttachPluginProcess(Process process)
    {
        _pluginProcess = process;
        _pluginProcess.EnableRaisingEvents = true;
        _pluginProcess.Exited += (_, _) =>
        {
            Console.WriteLine("[YomiYa TCP] El proceso del plugin ha terminado inesperadamente.");
            HandleClientDisconnect();
        };
    }

    public void Stop()
    {
        _isRunning = false;
        _serverCts?.Cancel();
        _listener?.Stop();
        HandleClientDisconnect();
    }

    private async Task AcceptClientsAsync(CancellationToken token)
    {
        try
        {
            while (_isRunning && !token.IsCancellationRequested)
            {
                var client = await _listener!.AcceptTcpClientAsync(token);
                
                // Si había un cliente anterior colgado, lo limpiamos antes de aceptar el nuevo
                HandleClientDisconnect(notify: false);

                lock (_connectionLock)
                {
                    _connectedClient = client;
                    var stream = client.GetStream();
                    _reader = new StreamReader(stream);
                    _writer = new StreamWriter(stream) { AutoFlush = true };
                    var connectionReader = _reader;
                    _connectionTcs.TrySetResult(true);
                }

                Console.WriteLine("[YomiYa TCP] ¡Una extensión se ha conectado!");
                _ = Task.Run(() => ReceiveMessagesAsync(client, connectionReader, token), token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[YomiYa TCP] Error al aceptar cliente: {ex.Message}");
        }
    }

    public async Task WaitForConnectionAsync(int timeoutMs = 5000, CancellationToken cancellationToken = default)
    {
        if (IsConnected) return;

        try
        {
            await _connectionTcs.Task
                .WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException("La extensión tardó demasiado en conectarse o crasheó al abrirse.");
        }
    }

    private async Task ReceiveMessagesAsync(TcpClient client, StreamReader reader, CancellationToken token)
    {
        try
        {
            while (_isRunning && client.Connected && !token.IsCancellationRequested)
            {
                // ReadLineAsync con token permite abortar la lectura si el servidor se detiene
                var jsonLine = await reader.ReadLineAsync(token);
                if (jsonLine == null) break; // EOF: el cliente cerró el stream limpiamente

                var message = JsonSerializer.Deserialize<TcpMessage>(jsonLine);
                if (message != null)
                {
                    HandleIncomingMessage(message);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[YomiYa TCP] Error de lectura / Extensión desconectada: {ex.Message}");
        }
        finally
        {
            // Solo limpiamos si el cliente que cayó es el actualmente activo
            if (ReferenceEquals(_connectedClient, client))
            {
                HandleClientDisconnect();
            }
        }
    }

    private void HandleIncomingMessage(TcpMessage message)
    {
        if (_pendingRequests.TryRemove(message.RequestId, out var tcs))
        {
            tcs.TrySetResult(message);
        }
    }

    /// <summary>
    /// Limpia la conexión actual, cancela todas las solicitudes en vuelo y prepara el TCS para reconexión.
    /// </summary>
    private void HandleClientDisconnect(bool notify = true)
    {
        lock (_connectionLock)
        {
            if (_connectedClient == null && _connectionTcs.Task.IsCompleted == false)
                return;

            try { _reader?.Dispose(); } catch { }
            try { _writer?.Dispose(); } catch { }
            try { _connectedClient?.Close(); } catch { }

            _reader = null;
            _writer = null;
            _connectedClient = null;

            // Reiniciamos el avisador para permitir una futura reconexión controlada
            if (_connectionTcs.Task.IsCompleted)
            {
                _connectionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        // Fallar inmediatamente todas las peticiones que esperaban respuesta de este cliente muerto
        foreach (var kvp in _pendingRequests)
        {
            if (_pendingRequests.TryRemove(kvp.Key, out var tcs))
            {
                tcs.TrySetException(new IOException("La conexión con el plugin se perdió antes de recibir respuesta."));
            }
        }

        if (notify)
        {
            OnPluginDisconnected?.Invoke();
        }
    }

    public async Task<TcpMessage> SendRequestAsync(
        string action,
        object? payload = null,
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected || (_pluginProcess != null && _pluginProcess.HasExited))
        {
            HandleClientDisconnect();
            throw new InvalidOperationException("No hay ninguna extensión conectada o el proceso ha terminado.");
        }

        var requestId = Guid.NewGuid().ToString();
        var requestMessage = new TcpMessage { Action = action, RequestId = requestId };
        if (payload != null) requestMessage.SetPayload(payload);

        // RunContinuationsAsynchronously evita que el hilo receptor de red ejecute la continuación del llamador
        var tcs = new TaskCompletionSource<TcpMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = tcs;

        try
        {
            var json = JsonSerializer.Serialize(requestMessage, new JsonSerializerOptions { WriteIndented = false });

            // Serializamos las escrituras al socket para evitar que dos hilos mezclen bytes en el stream
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                if (_writer == null) throw new IOException("El stream de escritura no está disponible.");
                await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);
            }
            finally
            {
                _writeLock.Release();
            }

            // Espera con soporte nativo para Timeout y CancellationToken (.NET 6+)
            return await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"El plugin no respondió a la acción '{action}' dentro de {timeoutMs}ms.");
        }
        catch (Exception)
        {
            // Si falla la escritura o se desconecta en mitad del envío, forzamos limpieza si es error de red
            if (!IsConnected) HandleClientDisconnect();
            throw;
        }
        finally
        {
            // Garantiza que nunca quede basura en memoria si hubo Timeout, Cancelación o Excepción de red
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    public void Dispose()
    {
        Stop();
        _writeLock.Dispose();
        _serverCts?.Dispose();
    }
}