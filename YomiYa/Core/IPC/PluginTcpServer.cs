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
    private TcpListener? _listener;
    private bool _isRunning;
    private CancellationTokenSource? _cancellationTokenSource;

    // Aquí guardaremos las peticiones que están esperando respuesta.
    // El string es el RequestId de la petición.
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TcpMessage>> _pendingRequests = new();

    private TcpClient? _connectedClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    /// <summary>
    /// Inicia el servidor TCP en un puerto local.
    /// </summary>
    public void Start(int port = 5000)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();

        // Iniciamos la escucha en un hilo secundario para no bloquear la interfaz gráfica (UI) de YomiYa
        _ = Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));
    }

    /// <summary>
    /// Detiene el servidor y desconecta extensiones.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _cancellationTokenSource?.Cancel();
        _listener?.Stop();
        _connectedClient?.Close();
    }

    /// <summary>
    /// Bucle infinito que espera a que una extensión (ejecutable) se conecte.
    /// </summary>
    private async Task AcceptClientsAsync(CancellationToken token)
    {
        try
        {
            while (_isRunning && !token.IsCancellationRequested)
            {
                // Espera aquí hasta que un programa intente conectarse al puerto 50000
                var client = await _listener!.AcceptTcpClientAsync(token);
                Console.WriteLine("[YomiYa TCP] ¡Una extensión se ha conectado!");

                _connectedClient = client;
                var stream = client.GetStream();

                // Usamos StreamReader/Writer para leer los mensajes de texto (JSON) fácilmente
                _reader = new StreamReader(stream);
                _writer = new StreamWriter(stream) { AutoFlush = true }; // AutoFlush envía el texto inmediatamente

                // Empezamos a escuchar los mensajes que envíe esta extensión en otro hilo
                _ = Task.Run(() => ReceiveMessagesAsync(client, token), token);
            }
        }
        catch (OperationCanceledException)
        {
            /* Ignorar, el servidor se está cerrando */
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YomiYa TCP] Error al aceptar cliente: {ex.Message}");
        }
    }

    /// <summary>
    /// Lee constantemente la información que nos envía la extensión.
    /// </summary>
    private async Task ReceiveMessagesAsync(TcpClient client, CancellationToken token)
    {
        try
        {
            while (_isRunning && client.Connected && !token.IsCancellationRequested)
            {
                // Leemos una línea completa. Todo nuestro JSON debe viajar en una sola línea sin saltos de carro.
                string? jsonLine = await _reader!.ReadLineAsync(token);
                if (string.IsNullOrEmpty(jsonLine)) break; // Si es nulo, la extensión se cerró

                // Deserializamos el texto a nuestro objeto
                var message = JsonSerializer.Deserialize<TcpMessage>(jsonLine);
                if (message != null)
                {
                    HandleIncomingMessage(message);
                }
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

    /// <summary>
    /// Revisa qué hacer con el mensaje recién llegado.
    /// </summary>
    private void HandleIncomingMessage(TcpMessage message)
    {
        // Buscamos si tenemos una petición esperando esta respuesta (usando el RequestId)
        if (_pendingRequests.TryRemove(message.RequestId, out var taskCompletionSource))
        {
            // ¡Encontramos a quién lo pidió! Esto despierta el hilo que estaba esperando en "SendRequestAsync"
            taskCompletionSource.SetResult(message);
        }
    }

    /// <summary>
    /// Método PRINCIPAL que usará YomiYa para pedirle algo a la extensión.
    /// Ejemplo: await server.SendRequestAsync("GetChapterImages", new { Url = "..." });
    /// </summary>
    public async Task<TcpMessage> SendRequestAsync(string action, object payload = null)
    {
        if (_connectedClient == null || !_connectedClient.Connected)
            throw new Exception("No hay ninguna extensión conectada.");

        // 1. Creamos un identificador único para el ticket de espera
        var requestId = Guid.NewGuid().ToString();

        var requestMessage = new TcpMessage
        {
            Action = action,
            RequestId = requestId
        };

        requestMessage.SetPayload(payload);

        // 2. Preparamos el "TaskCompletionSource" (Es como una sala de espera)
        var tcs = new TaskCompletionSource<TcpMessage>();
        _pendingRequests[requestId] = tcs;

        // 3. Convertimos el mensaje a texto (sin indentar, para que sea una sola línea)
        var json = JsonSerializer.Serialize(requestMessage, new JsonSerializerOptions { WriteIndented = false });

        // 4. Enviamos el mensaje por la red
        await _writer!.WriteLineAsync(json);

        // 5. El código se "Pausa" aquí de forma asíncrona hasta que `HandleIncomingMessage` llame a `SetResult`
        return await tcs.Task;
    }
}