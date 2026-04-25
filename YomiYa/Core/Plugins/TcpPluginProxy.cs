using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using YomiYa.Core.Interfaces;
using YomiYa.Core.IPC;
using YomiYa.Domain.Models;
using YomiYa.Source.Models;
using YomiYa.Source.Online;

namespace YomiYa.Core.Plugins
{
    public class TcpPluginProxy : ParsedHttpSource, IConfigurableSource, IDisposable
    {
        private readonly PluginTcpServer _tcpServer;
        private readonly Process _pluginProcess;

        public override string Name { get; set; }
        public override string Lang { get; }
        public override string Version { get; }
        public override long Id { get; set; }
        protected override string BaseUrl { get; }

        public TcpPluginProxy(PluginTcpServer tcpServer, Process pluginProcess, JsonElement metadata)
        {
            _tcpServer = tcpServer;
            _pluginProcess = pluginProcess;

            Name = metadata.GetProperty("Name").GetString();
            Lang = metadata.GetProperty("Lang").GetString();
            Version = metadata.GetProperty("Version").GetString();
            Id = metadata.GetProperty("Id").GetInt64();
            BaseUrl = metadata.GetProperty("BaseUrl").GetString();
        }

        public override async Task<MangasPage> GetLatestUpdates(int page = 1)
        {
            var response = await _tcpServer.SendRequestAsync("GetLatestUpdates", new { Page = page });
            return response.GetPayload<MangasPage>();
        }

        public override async Task<MangasPage> GetPopularManga(int page = 1)
        {
            var response = await _tcpServer.SendRequestAsync("GetPopularManga", new { Page = page });
            return response.GetPayload<MangasPage>();
        }

        public override async Task<MangasPage> SearchManga(string query, int page = 1, string genre = "")
        {
            var response = await _tcpServer.SendRequestAsync("SearchManga", new { Query = query, Page = page, Genre = genre });
            return response.GetPayload<MangasPage>();
        }

        public override async Task<SManga> GetMangaDetails(string url)
        {
            var response = await _tcpServer.SendRequestAsync("GetMangaDetails", new { Url = url });
            return response.GetPayload<SManga>();
        }

        public override async Task<List<SChapter>> GetChapters(string mangaUrl)
        {
            var response = await _tcpServer.SendRequestAsync("GetChapters", new { Url = mangaUrl });
            return response.GetPayload<List<SChapter>>();
        }

        public override async Task<List<Page>> GetPages(string chapterUrl)
        {
            var response = await _tcpServer.SendRequestAsync("GetPages", new { Url = chapterUrl });
            return response.GetPayload<List<Page>>();
        }
        
        public async Task<Dictionary<string, bool>> GetConfigurationAsync()
        {
            var response = await _tcpServer.SendRequestAsync("GetConfiguration");
            // Si el plugin no mandó nada, devolvemos un diccionario vacío
            return response.GetPayload<Dictionary<string, bool>>() ?? new Dictionary<string, bool>();
        }

        public async Task SetConfigurationAsync(Dictionary<string, bool> configuration)
        {
            await _tcpServer.SendRequestAsync("SetConfiguration", configuration);
        }

        public void Dispose()
        {
            _tcpServer?.Stop();
            
            if (_pluginProcess != null && !_pluginProcess.HasExited)
            {
                _pluginProcess.Kill();
                _pluginProcess.Dispose();
            }
        }
    }
}