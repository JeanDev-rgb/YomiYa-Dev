using System.Globalization;
using System.Net;
using System.Web;
using HtmlAgilityPack;
using Polly;
using ScrapySharp.Extensions;
using YomiYa.Core.Resilience.Handlers;
using YomiYa.Domain.Models;
using YomiYa.Extensions.Es.Handlers;
using YomiYa.Source.Models;
using YomiYa.Source.Online;
using YomiYa.Utils;

namespace YomiYa.Extensions.Es;

public class Akaya : ParsedHttpSource
{
    protected sealed override string BaseUrl => "https://akaya.io";
    public sealed override string Lang => "es";
    public sealed override long Id { get; set; } 
    public sealed override string Name { get; set; } = "Akaya Manga";
    public override string Version => "1.0.1";
    private string _csrfToken = "";

    public override HttpClient HttpClient { get; }

    public Akaya()
    {
        Id = GenerateId.GenerateSourceId(Name, Lang);
        var rateLimitPolicy = Policy.RateLimitAsync(1, TimeSpan.FromSeconds(1));

        var retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => r.StatusCode == (HttpStatusCode)419)
            .RetryAsync(1, async (delegateResult, retryCount) => { await GetCsrfTokenAsync(); });

        var rateLimitHandler = new RateLimiterHandler(rateLimitPolicy)
        {
            InnerHandler = new HttpClientHandler()
        };

        var redirectHandler = new SerieRedirectHandler(BaseUrl, rateLimitHandler);

        var retryHandler = new PolicyHandler(retryPolicy, redirectHandler);

        HttpClient = new HttpClient(retryHandler);
    }

    private async Task GetCsrfTokenAsync()
    {
        var doc = await GetHtmlDocumentAsync(BaseUrl);
        var tokenNode = doc.DocumentNode.SelectSingleNode("//meta[@name='csrf-token']");
        _csrfToken = tokenNode?.GetAttributeValue("content", "") ?? "";
    }

    private FormUrlEncodedContent CreateSearchFormBody(string query)
    {
        var formData = new Dictionary<string, string>
        {
            { "_token", _csrfToken },
            { "search", query }
        };
        return new FormUrlEncodedContent(formData);
    }

    public override async Task<List<SChapter>> GetChapters(string mangaUrl)
    {
        var doc = await GetHtmlDocumentAsync(mangaUrl);

        var chapters = doc.DocumentNode.CssSelect("div.chapter-desktop div.chapter-item")
            .Select(ChapterFromElement)
            .ToList();

        return chapters;
    }

    public override async Task<MangasPage> GetLatestUpdates(int page = 1)
    {
        var url = $"{BaseUrl}/collection/588c7f2c-63ee-4632-8453-8145dacea7aa?page={page}";
        var doc = await GetHtmlDocumentAsync(url);

        var mangas = doc.DocumentNode.CssSelect("div.library-grid-item").Select(PopularMangaFromElement).ToList();
        var hasNextPage = doc.DocumentNode.CssSelect("nav a[rel='next']").Any();

        return new MangasPage(mangas, hasNextPage);
    }

    public override async Task<SManga> GetMangaDetails(string url)
    {
        try
        {
            var doc = await GetHtmlDocumentAsync(url);
            var description = doc.DocumentNode.CssSelect("div.sidebar p").FirstOrDefault()!.InnerText;
            var genres = doc.DocumentNode.CssSelect("div.artists-links h5.titles-blocks")
                .Select(n => n.InnerText?.Trim() ?? string.Empty).ToList();

            return new SManga
            {
                Description = description,
                Genre = genres
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        return new SManga();
    }

    public override async Task<List<Page>> GetPages(string chapterUrl)
    {
        var doc = await GetHtmlDocumentAsync(chapterUrl);

        var pages = doc.DocumentNode.CssSelect("div.vertical-images img.img-fluid")
            .Select((img, index) =>
            {
                var imageurl = GetImgAttr(img);
                return new Page(index + 1, ImageUrl: imageurl);
            }).ToList();

        return pages;
    }

    public override async Task<MangasPage> GetPopularManga(int page = 1)
    {
        var url = $"{BaseUrl}/collection/bd90cb43-9bf2-4759-b8cc-c9e66a526bc6?page={page}";
        var doc = await GetHtmlDocumentAsync(url);

        var mangas = doc.DocumentNode.CssSelect("div.library-grid-item").Select(PopularMangaFromElement).ToList();

        var hasNextPage = doc.DocumentNode.CssSelect("nav.navigation a[rel='next']").Any();

        return new MangasPage(mangas, hasNextPage);
    }

    public override async Task<MangasPage> SearchManga(string query, int page = 1, string genre = "")
    {
        if (string.IsNullOrEmpty(_csrfToken))
        {
            await GetCsrfTokenAsync();
        }

        var searchUrl = $"{BaseUrl}/search";
        var formContent = CreateSearchFormBody(query);

        var response = await HttpClient.PostAsync(searchUrl, formContent);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var mangas = doc.DocumentNode.CssSelect("div.rowDivEmpty div.list-search").Select(SearchedMangasFromElement)
            .ToList();

        var hasNextPage = doc.DocumentNode.CssSelect("nav a[rel='next']").Any();

        return new MangasPage(mangas, hasNextPage);
    }

    private static SManga SearchedMangasFromElement(HtmlNode element)
    {
        var manga = new SManga();
        var titleNode = element.CssSelect("div.name-serie-search a").First();

        manga.Title = titleNode.InnerText.Trim();
        manga.Url = titleNode.GetAttributeValue("href", string.Empty).Trim();

        var innerImg = element.CssSelect("div.inner-img-search").FirstOrDefault();
        if (innerImg != null)
        {
            var style = innerImg.GetAttributeValue("style", null);
            if (!string.IsNullOrEmpty(style))
            {
                manga.ThumbnailUrl = style
                    .SubstringAfter("url(")
                    .SubstringBefore(")")
                    .Trim('\'', '"');
            }
        }

        if (string.IsNullOrEmpty(manga.ThumbnailUrl))
        {
            var imgFluid = element.CssSelect("div.img-fluid").FirstOrDefault();
            if (imgFluid != null)
            {
                manga.ThumbnailUrl = imgFluid.GetAttributeValue("abs:src", string.Empty).Trim();
            }
        }

        return manga;
    }

    private static SChapter ChapterFromElement(HtmlNode element)
    {
        var h6Node = element.CssSelect("h6 a").FirstOrDefault();
        var h3Node = element.CssSelect("h3 a").FirstOrDefault();
        var dateNode = element.CssSelect("p.date").FirstOrDefault();

        var chapterNumberText = HttpUtility.HtmlDecode(h6Node?.InnerText?.Trim()) ?? "";
        var chapterNameText = HttpUtility.HtmlDecode(h3Node?.InnerText?.Trim()) ?? "";

        var fullName = $"{chapterNumberText}: {chapterNameText}".Trim();
        if (fullName.EndsWith(":"))
        {
            fullName = fullName.TrimEnd(':').Trim();
        }

        var chapterNumber = chapterNumberText.ExtractFloat() ?? -1f;
        var url = h3Node?.GetAttributeValue("href", "") ?? string.Empty;

        long dateTimestamp = 0;
        var dateString = dateNode?.InnerText.Trim();
        if (!string.IsNullOrEmpty(dateString) &&
            DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var parsedDate))
        {
            dateTimestamp = new DateTimeOffset(parsedDate).ToUnixTimeSeconds();
        }

        return new SChapter
        {
            Name = fullName,
            Url = url,
            ChapterNumber = chapterNumber,
            DateUpload = dateTimestamp
        };
    }

    private static SManga PopularMangaFromElement(HtmlNode element)
    {
        var manga = new SManga();

        var anchor = element.CssSelect("a").FirstOrDefault();
        if (anchor != null)
            manga.Url = anchor.GetAttributeValue("href", string.Empty).Trim();

        var titleNode = element.CssSelect("span > h5 > strong").FirstOrDefault();
        if (titleNode != null)
            manga.Title = titleNode.InnerText.Trim();

        var innerImg = element.CssSelect("div.inner-img").FirstOrDefault();
        if (innerImg != null)
        {
            var style = innerImg.GetAttributeValue("style", null);
            if (!string.IsNullOrEmpty(style))
            {
                manga.ThumbnailUrl = style
                    .SubstringAfter("url(")
                    .SubstringBefore(")")
                    .Trim('\'', '"');
            }
        }

        if (string.IsNullOrEmpty(manga.ThumbnailUrl))
        {
            var imgFluid = element.CssSelect("div.img-fluid").FirstOrDefault();
            if (imgFluid != null)
            {
                manga.ThumbnailUrl = imgFluid.GetAttributeValue("abs:src", string.Empty).Trim();
            }
        }

        return manga;
    }

    private static string GetImgAttr(HtmlNode? img)
    {
        if (img is null) return "";

        return img.GetAttributeValue("data-src",
            img.GetAttributeValue("data-lazy-src",
                img.GetAttributeValue("src", "")));
    }
}