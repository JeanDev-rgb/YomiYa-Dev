using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YomiYa.Core.IPC;

public class PluginTcpServer
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TcpMessage>> _pendingRequests = new();
    private CancellationTokenSource? _cancellationTokenSource;

    private TcpClient? _connectedClient;
    private TaskCompletionSource<bool>? _connectionTcs;
    private bool _isRunning;
    private TcpListener? _listener;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public void Start(int port = 50000)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        _connectionTcs = new TaskCompletionSource<bool>(); // Iniciamos el avisador

        _ = Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));
        Console.WriteLine($"[YomiYa TCP] Servidor escuchando en 127.0.0.1:{port}");
    }

    public void Stop()
    {
        _isRunning = false;
        _cancellationTokenSource?.Cancel();
        _listener?.Stop();
        _connectedClient?.Close();
    }

    private async Task AcceptClientsAsync(CancellationToken token)
    {
        try
        {
            while (_isRunning && !token.IsCancellationRequested)
            {
                var client = await _listener!.AcceptTcpClientAsync(token);
                Console.WriteLine("[YomiYa TCP] ¡Una extensión se ha conectado!");

                _connectedClient = client;
                _connectionTcs!.TrySetResult(true); // ¡Avisamos que alguien se conectó!

                var stream = client.GetStream();
                _reader = new StreamReader(stream);
                _writer = new StreamWriter(stream) { AutoFlush = true };

                _ = Task.Run(() => ReceiveMessagesAsync(client, token));
            }
        }
        catch (OperationCanceledException)
        {
            /* Ignorar al cerrar */
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YomiYa TCP] Error al aceptar cliente: {ex.Message}");
        }
    }

    public async Task WaitForConnectionAsync(int timeoutMs = 5000)
    {
        var timeoutTask = Task.Delay(timeoutMs);
        var completedTask = await Task.WhenAny(_connectionTcs!.Task, timeoutTask);

        if (completedTask == timeoutTask)
            throw new TimeoutException("La extensión tardó demasiado en conectarse o crasheó al abrirse.");
    }

    private async Task ReceiveMessagesAsync(TcpClient client, CancellationToken token)
    {
        try
        {
            while (_isRunning && client.Connected && !token.IsCancellationRequested)
            {
                var jsonLine = await _reader!.ReadLineAsync();
                if (string.IsNullOrEmpty(jsonLine)) break;

                var message = JsonSerializer.Deserialize<TcpMessage>(jsonLine);
                if (message != null) HandleIncomingMessage(message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YomiYa TCP] Extensión desconectada: {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }

    private void HandleIncomingMessage(TcpMessage message)
    {
        if (_pendingRequests.TryRemove(message.RequestId, out var taskCompletionSource))
            taskCompletionSource.SetResult(message);
    }

    public async Task<TcpMessage> SendRequestAsync(string action, object? payload = null)
    {
        if (_connectedClient == null || !_connectedClient.Connected)
            throw new Exception("No hay ninguna extensión conectada.");

        var requestId = Guid.NewGuid().ToString();

        var requestMessage = new TcpMessage { Action = action, RequestId = requestId };
        if (payload != null) requestMessage.SetPayload(payload);

        var tcs = new TaskCompletionSource<TcpMessage>();
        _pendingRequests[requestId] = tcs;

        var json = JsonSerializer.Serialize(requestMessage, new JsonSerializerOptions { WriteIndented = false });

        await _writer!.WriteLineAsync(json);

        return await tcs.Task;
    }
}