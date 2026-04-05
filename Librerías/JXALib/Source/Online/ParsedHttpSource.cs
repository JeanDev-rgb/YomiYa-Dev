using HtmlAgilityPack;
using YomiYa.Core.Exceptions;
using YomiYa.Utils;
using YomiYa.Domain.Models;
using YomiYa.Source.Models;
using YomiYa.Core.Resilience;
using Polly;
using YomiYa.Core.Resilience.Handlers;
using HttpRequestException = YomiYa.Core.Exceptions.HttpRequestException;

namespace YomiYa.Source.Online;

public abstract class ParsedHttpSource : IParsedHttpSource
{
    private static readonly HttpClient _httpClient;

    static ParsedHttpSource()
    {
        var handler = new HttpClientHandler();
        if (GlobalProxy.Proxy != null)
        {
            handler.Proxy = GlobalProxy.Proxy;
            handler.UseProxy = true;
        }

        var policyHandler = new PolicyHandler(ResiliencePolicies.GetDefaultRetryPolicy(), handler);

        _httpClient = new HttpClient(policyHandler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
    }

    protected abstract string BaseUrl { get; }
    public abstract string Lang { get; }

    public virtual HttpClient HttpClient => _httpClient;
    public abstract string Name { get; }
    public abstract string Version { get; }
    public abstract Task<List<SChapter>> GetChapters(string mangaUrl);
    public abstract Task<MangasPage> GetLatestUpdates(int page = 1);
    public abstract Task<SManga> GetMangaDetails(string url);
    public abstract Task<List<Page>> GetPages(string chapterUrl);
    public abstract Task<MangasPage> GetPopularManga(int page = 1);
    public abstract Task<MangasPage> SearchManga(string query, int page = 1, string genre = "");

    private async Task<string> GetHtmlContentAsync(string url)
    {
        try
        {
            var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al obtener el contenido HTML de {url}: {ex.Message}", ex);
        }
    }

    [Obsolete]
    protected async Task<byte[]> GetImageBytesAsync(string imageUrl)
    {
        try
        {
            var response = await HttpClient.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode(); // Asegura que la solicitud fue exitosa
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (HttpRequestException ex)
        {
            throw new ImageDownloadException($"Error al obtener la imagen desde {imageUrl}: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new ImageDownloadException($"Error desconocido al obtener la imagen desde {imageUrl}: {ex.Message}", ex);
        }
    }

    protected async Task<HtmlDocument> GetHtmlDocumentAsync(string url)
    {
        try
        {
            var html = await GetHtmlContentAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return doc;
        }
        catch (Exception ex)
        {
            throw new HtmlParsingException($"Error al procesar el documento HTML de {url}: {ex.Message}", ex);
        }
    }
}