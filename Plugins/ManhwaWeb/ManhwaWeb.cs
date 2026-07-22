using System.Net;
using System.Text.Json;
using Polly;
using YomiYa.Core.Resilience.Handlers;
using YomiYa.Domain.Models;
using YomiYa.Source.Models;
using YomiYa.Source.Online;
using YomiYa.Utils;

namespace YomiYa.Extensions.Es;

public class ManhwaWeb : ParsedHttpSource
{
    private string _csrfToken = "";
    private readonly string _apiUrl = "https://manhwawebbackend-production.up.railway.app";
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ManhwaWeb()
    {
        Id = GenerateId.GenerateSourceId(Name, Lang);

        // Rate limit: 1 request por segundo
        var rateLimitPolicy = Policy.RateLimitAsync(1, TimeSpan.FromSeconds(1));

        // Retry si recibimos 419 (CSRF token expirado)
        var retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => r.StatusCode == (HttpStatusCode)419)
            .RetryAsync(1, async (response, retryCount, context) => { await GetCsrfTokenAsync(); });

        // Configuración del HttpClient
        var rateLimitHandler = new RateLimiterHandler(rateLimitPolicy)
        {
            InnerHandler = new HttpClientHandler()
        };

        var retryHandler = new PolicyHandler(retryPolicy, rateLimitHandler);
        HttpClient = new HttpClient(retryHandler);
        HttpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/*,*/*;q=0.8");
    }

    protected sealed override string BaseUrl => "https://manhwaweb.com";
    public sealed override string Lang => "es";
    public override string Version => "1.0.0";
    public override HttpClient HttpClient { get; }
    public sealed override long Id { get; set; }
    public sealed override string Name { get; set; } = "ManhwaWeb";

    private async Task GetCsrfTokenAsync()
    {
        var response = await HttpClient.GetStringAsync(BaseUrl);
        // Aquí usarías HtmlAgilityPack o similar para extraer el token CSRF
        // var doc = new HtmlDocument();
        // doc.LoadHtml(response);
        // _csrfToken = doc.DocumentNode.SelectSingleNode("//meta[@name='csrf-token']")?.GetAttributeValue("content", "") ?? "";
    }

    public override async Task<MangasPage> GetPopularManga(int page = 1)
    {
        var response = await HttpClient.GetStringAsync($"{_apiUrl}/manhwa/nuevos");
        var result = JsonSerializer.Deserialize<PayloadPopularDto>(response, _jsonOptions)!;

        var mangas = result.Data.Weekly
            .Concat(result.Data.Total)
            .GroupBy(m => m.Slug)
            .Select(g => g.First().ToSManga())
            .OrderByDescending(m => m.Status) 
            .ToList();

        return new MangasPage(mangas, false);
    }

    public override async Task<MangasPage> GetLatestUpdates(int page = 1)
    {
        var response = await HttpClient.GetStringAsync($"{_apiUrl}/latest/new-manhwa");
        var result = JsonSerializer.Deserialize<PayloadLatestDto>(response, _jsonOptions)!;

        var mangas = result.Data.Esp
            .Concat(result.Data.Raw18)
            .Concat(result.Data.Esp18)
            .GroupBy(m => m.Slug)
            .Select(g => g.First().ToSManga())
            .OrderByDescending(m => m.Status)
            .ToList();

        return new MangasPage(mangas, false);
    }

    public override async Task<MangasPage> SearchManga(string query, int page = 1, string genre = "")
    {
        var url = $"{_apiUrl}/manhwa/library?buscar={Uri.EscapeDataString(query)}&page={page - 1}";
        if (!string.IsNullOrEmpty(genre))
            url += $"&generes={genre}";

        var response = await HttpClient.GetStringAsync(url);
        var result = JsonSerializer.Deserialize<PayloadSearchDto>(response, _jsonOptions)!;

        var mangas = result.Data.Select(m => m.ToSManga()).ToList();
        return new MangasPage(mangas, result.HasNextPage);
    }

    public override async Task<SManga> GetMangaDetails(string url)
    {
        var slug = url.TrimEnd('/').Split('/').Last();
        var response = await HttpClient.GetStringAsync($"{_apiUrl}/manhwa/see/{slug}");
        var result = JsonSerializer.Deserialize<ComicDetailsDto>(response, _jsonOptions)!;
        return result.ToSManga();
    }

    public override async Task<List<SChapter>> GetChapters(string mangaUrl)
    {
        var slug = mangaUrl.TrimEnd('/').Split('/').Last();
        var response = await HttpClient.GetStringAsync($"{_apiUrl}/manhwa/see/{slug}");
        var result = JsonSerializer.Deserialize<PayloadChapterDto>(response, _jsonOptions)!;

        var chapters = result.Chapters
            .Where(c => c.CreatedAt.HasValue && (c.EspUrl != null || c.RawUrl != null))
            .Select(c => new SChapter
            {
                Name = $"Capítulo {c.Number}".TrimEnd('0').TrimEnd('.'),
                ChapterNumber = c.Number,
                DateUpload = c.CreatedAt ?? 0,
                Url = (c.EspUrl ?? c.RawUrl!).Replace(result.Id, result.RealId),
                Scanlator = c.EspUrl != null ? "Esp" : "Raw"
            })
            .OrderByDescending(c => c.ChapterNumber)
            .ToList();

        return chapters;
    }

    public override async Task<List<Page>> GetPages(string chapterUrl)
    {
        var slug = chapterUrl.TrimEnd('/').Split('/').Last();
        var response = await HttpClient.GetStringAsync($"{_apiUrl}/chapters/see/{slug}");
        var result = JsonSerializer.Deserialize<PayloadPageDto>(response, _jsonOptions)!;

        return result.Data.Images
            .Where(img => img.StartsWith("http"))
            .Select((img, i) => new Page(i, img))
            .ToList();
    }
}