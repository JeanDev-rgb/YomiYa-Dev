using System;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using YomiYa.Core.Interfaces;
using YomiYa.Source.Online;

namespace YomiYa.Core.IPC
{
    public static class TcpPluginRunner
    {
        public static async Task RunAsync(ParsedHttpSource plugin, int port = 50000)
        {
            Console.WriteLine($"[{plugin.Name}] Iniciando ejecutable independiente...");

            using TcpClient client = new TcpClient();
            while (!client.Connected)
            {
                try
                {
                    await client.ConnectAsync("127.0.0.1", port);
                    Console.WriteLine($"[{plugin.Name}] ¡Conectado a YomiYa!");
                }
                catch
                {
                    await Task.Delay(2000);
                }
            }

            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new StreamReader(stream);
            using StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

            try
            {
                while (true)
                {
                    string jsonLine = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(jsonLine)) break;

                    var incomingMessage = JsonSerializer.Deserialize<TcpMessage>(jsonLine);
                    if (incomingMessage != null)
                    {
                        Console.WriteLine($"[{plugin.Name}] Recibida orden: {incomingMessage.Action}");
                        _ = Task.Run(() => ProcessMessageAsync(incomingMessage, writer, plugin));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{plugin.Name}] Desconectado: {ex.Message}");
            }
        }

        private static async Task ProcessMessageAsync(TcpMessage request, StreamWriter writer, ParsedHttpSource plugin)
        {
            var response = new TcpMessage
            {
                Action = request.Action + "_Response",
                RequestId = request.RequestId
            };

            try
            {
                switch (request.Action)
                {
                    case "GetMetadata":
                        var metadata = new
                        {
                            Name = plugin.Name,
                            Lang = plugin.Lang,
                            Version = plugin.Version,
                            Id = plugin.Id,
                            BaseUrl = plugin.GetType()
                                .GetProperty("BaseUrl",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                ?.GetValue(plugin)?.ToString() ?? ""
                        };
                        response.SetPayload(metadata);
                        break;

                    case "GetConfiguration":
                        // Verificamos si el plugin actual (ej. NovelCool) tiene configuraciones
                        if (plugin is IConfigurableSource configPlugin)
                        {
                            var config = await configPlugin.GetConfigurationAsync();
                            response.SetPayload(config);
                        }
                        else
                        {
                            response.SetPayload(new Dictionary<string, bool>());
                        }

                        break;

                    case "SetConfiguration":
                        if (plugin is IConfigurableSource configPlugin2)
                        {
                            var dict = request.GetPayload<Dictionary<string, bool>>();
                            await configPlugin2.SetConfigurationAsync(dict);
                        }

                        break;

                    case "GetLatestUpdates":
                        var pageReq = request.GetPayload<JsonElement>().GetProperty("Page").GetInt32();
                        var updates = await plugin.GetLatestUpdates(pageReq);
                        response.SetPayload(updates);
                        break;

                    case "GetPopularManga":
                        var popPageReq = request.GetPayload<JsonElement>().GetProperty("Page").GetInt32();
                        var popUpdates = await plugin.GetPopularManga(popPageReq);
                        response.SetPayload(popUpdates);
                        break;

                    case "SearchManga":
                        var query = request.GetPayload<JsonElement>().GetProperty("Query").GetString();
                        var sPage = request.GetPayload<JsonElement>().GetProperty("Page").GetInt32();
                        var genre = request.GetPayload<JsonElement>().GetProperty("Genre").GetString();
                        var searchRes = await plugin.SearchManga(query, sPage, genre);
                        response.SetPayload(searchRes);
                        break;

                    case "GetMangaDetails":
                        var urlManga = request.GetPayload<JsonElement>().GetProperty("Url").GetString();
                        var details = await plugin.GetMangaDetails(urlManga);
                        response.SetPayload(details);
                        break;

                    case "GetChapters":
                        var urlCapitulos = request.GetPayload<JsonElement>().GetProperty("Url").GetString();
                        var chapters = await plugin.GetChapters(urlCapitulos);
                        response.SetPayload(chapters);
                        break;

                    case "GetPages":
                        var urlPages = request.GetPayload<JsonElement>().GetProperty("Url").GetString();
                        var pages = await plugin.GetPages(urlPages);
                        response.SetPayload(pages);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{plugin.Name}] Error en {request.Action}: {ex.Message}");
            }

            string responseJson =
                JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = false });

            lock (writer)
            {
                writer.WriteLine(responseJson);
            }
        }
    }
}