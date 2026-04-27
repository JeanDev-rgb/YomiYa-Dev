using System.Collections.Concurrent;
using System.Web;
using HtmlAgilityPack;
using ScrapySharp.Extensions;
using YomiYa.Core.Interfaces;
using YomiYa.Domain.Models;
using YomiYa.Source.Models;
using YomiYa.Source.Online;
using YomiYa.Utils;

namespace YomiYa.Extensions.Es;

public class NovelCool : ParsedHttpSource, IConfigurableSource
{
    #region Properties
    protected override string BaseUrl => "https://www.novelcool.com";

    public sealed override string Lang => "Multi";
    public sealed override string Name { get; set; } = "NovelCool";
    public override string Version => "1.2.1";

    public sealed override long Id { get; set; }

    private readonly Dictionary<string, (string Code, string Url)> _supportedLanguages = new()
    {
        { "English", ("en", "https://www.novelcool.com") },
        { "Español", ("es", "https://es.novelcool.com") },
        { "Italiano", ("it", "https://it.novelcool.com") },
        { "Русский", ("ru", "https://ru.novelcool.com") },
        { "Deutsch", ("de", "https://de.novelcool.com") },
        { "Français", ("fr", "https://fr.novelcool.com") }
    };

    public NovelCool()
    {
        Id = GenerateId.GenerateSourceId(Name, Lang);
    }

    #endregion

    #region Configuración del Flyout (IConfigurableSource)

    public Task<Dictionary<string, bool>> GetConfigurationAsync()
    {
        var settings = NovelCoolSettings.Load();
        var config = new Dictionary<string, bool>();

        foreach (var lang in _supportedLanguages)
        {
            config.Add(lang.Key, settings.SelectedLanguages.Contains(lang.Value.Code));
        }

        return Task.FromResult(config);
    }

    public Task SetConfigurationAsync(Dictionary<string, bool> configuration)
    {
        var settings = NovelCoolSettings.Load();
        settings.SelectedLanguages.Clear();

        foreach (var (languageName, isChecked) in configuration)
        {
            if (isChecked && _supportedLanguages.TryGetValue(languageName, out var langData))
            {
                settings.SelectedLanguages.Add(langData.Code);
            }
        }

        if (settings.SelectedLanguages.Count == 0)
        {
            var culture = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;

            var defaultLang = _supportedLanguages.Values.FirstOrDefault(v => v.Code == culture);
            settings.SelectedLanguages.Add(defaultLang.Code ?? "en");

        }

        settings.Save();
        return Task.CompletedTask;
    }

    #endregion

    #region Public API Methods (Scraping Multilenguaje)

    public override async Task<MangasPage> GetLatestUpdates(int page = 1)
    {
        var settings = NovelCoolSettings.Load();
        var allMangas = new ConcurrentBag<SManga>();

        var tasks = settings.SelectedLanguages.Select(async langCode =>
        {
            var baseUrl = _supportedLanguages.Values.FirstOrDefault(v => v.Code == langCode).Url;
            if (string.IsNullOrEmpty(baseUrl)) return;

            try
            {
                var url = $"{baseUrl}/category/latest.html";
                var doc = await GetHtmlDocumentAsync(url);
                var mangas = doc.DocumentNode.CssSelect("div.book-item").Select(LatestUpdatesFromElement);

                foreach (var m in mangas) allMangas.Add(m);
            }
            catch
            {
                /* Ignorar si un idioma falla para no detener los demás */
            }
        });

        await Task.WhenAll(tasks);
        return new MangasPage(allMangas.ToList(), false);
    }

    public override async Task<MangasPage> GetPopularManga(int page = 1)
    {
        var settings = NovelCoolSettings.Load();
        var allMangas = new ConcurrentBag<SManga>();

        var tasks = settings.SelectedLanguages.Select(async langCode =>
        {
            var baseUrl = _supportedLanguages.Values.FirstOrDefault(v => v.Code == langCode).Url;
            if (string.IsNullOrEmpty(baseUrl)) return;

            try
            {
                var url = $"{baseUrl}/category/popular.html";
                var doc = await GetHtmlDocumentAsync(url);
                var mangas = doc.DocumentNode.CssSelect("div.book-item").Select(PopularMangaFromElement);

                foreach (var m in mangas) allMangas.Add(m);
            }
            catch
            {
            }
        });

        await Task.WhenAll(tasks);
        return new MangasPage(allMangas.ToList(), false);
    }

    public override async Task<MangasPage> SearchManga(string query, int page = 1, string genre = "")
    {
        var settings = NovelCoolSettings.Load();
        var allMangas = new ConcurrentBag<SManga>();
        var hasNextPage = false;

        var tasks = settings.SelectedLanguages.Select(async langCode =>
        {
            var baseUrl = _supportedLanguages.Values.FirstOrDefault(v => v.Code == langCode).Url;
            if (string.IsNullOrEmpty(baseUrl)) return;

            try
            {
                var url = $"{baseUrl}/search/?wd={query}&page={page}.html";
                var doc = await GetHtmlDocumentAsync(url);

                var mangas = doc.DocumentNode.CssSelect("div.book-item").Select(SearchedMangaFromElement);
                foreach (var m in mangas) allMangas.Add(m);

                // Si al menos un idioma tiene siguiente página, habilitamos el scroll infinito
                if (doc.DocumentNode.CssSelect("div.page-navone div.row-item.next").Any())
                {
                    hasNextPage = true;
                }
            }
            catch
            {
            }
        });

        await Task.WhenAll(tasks);
        return new MangasPage(allMangas.ToList(), hasNextPage);
    }

    #endregion

    #region Parsing de Capítulos y Detalles (Sin cambios en la lógica base)

    public override async Task<SManga> GetMangaDetails(string url)
    {
        var doc = await GetHtmlDocumentAsync(url);
        var statusText = doc.DocumentNode.CssSelect("div.bk-going a").FirstOrDefault()?.InnerText.Trim();

        return new SManga
        {
            Description =
                HttpUtility.HtmlDecode(doc.DocumentNode.CssSelect("div.bk-summary-txt").FirstOrDefault()?.InnerText)
                    ?.Trim() ?? "Sin descripción",
            Genre = doc.DocumentNode.CssSelect("div.bk-cate-item span[itemprop='keywords']")
                .Select(node => node.InnerText.Trim()).ToList(),
            Status = statusText?.ToLower() switch
            {
                "en marcha" => SManga.Ongoing,
                "completado" => SManga.Completed,
                _ => SManga.Unknown
            },
        };
    }

    public override async Task<List<SChapter>> GetChapters(string mangaUrl)
    {
        var doc = await GetHtmlDocumentAsync(mangaUrl);
        return doc.DocumentNode.CssSelect("div.chp-item").Select(ChapterFromElement).ToList();
    }

    public override async Task<List<Page>> GetPages(string chapterUrl)
    {
        var doc = await GetHtmlDocumentAsync(chapterUrl);
        var baseUri = new Uri(chapterUrl);

        var pageOptions = doc.DocumentNode.CssSelect("select.sl-page option");
        var pageUrls = pageOptions
            .Select(option => new Uri(baseUri, option.GetAttributeValue("value", string.Empty)).ToString())
            .Where(url => !string.IsNullOrEmpty(url))
            .Distinct()
            .ToList();

        if (!pageUrls.Any())
        {
            var imageNode = doc.DocumentNode.CssSelect("img.mangaread-manga-pic").FirstOrDefault();
            if (imageNode != null)
            {
                var imageUrl = imageNode.GetAttributeValue("src", string.Empty);
                if (!string.IsNullOrEmpty(imageUrl))
                    return new List<Page> { new Page(Index: 1, ImageUrl: imageUrl, Uri: new Uri(imageUrl)) };
            }

            return new List<Page>();
        }

        var pageData = new ConcurrentDictionary<string, string>();
        var tasks = new List<Task>();

        foreach (var pageUrl in pageUrls)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var pageDoc = await GetHtmlDocumentAsync(pageUrl);
                    var imageNode = pageDoc.DocumentNode.CssSelect("img.mangaread-manga-pic").FirstOrDefault();
                    if (imageNode != null)
                    {
                        var imageUrl = imageNode.GetAttributeValue("src", string.Empty);
                        if (!string.IsNullOrEmpty(imageUrl)) pageData[pageUrl] = imageUrl;
                    }
                }
                catch
                {
                }
            }));
        }

        await Task.WhenAll(tasks);

        var pages = new List<Page>();
        for (int i = 0; i < pageUrls.Count; i++)
        {
            if (pageData.TryGetValue(pageUrls[i], out var imageUrl))
            {
                pages.Add(new Page(Index: i + 1, ImageUrl: imageUrl, Uri: new Uri(imageUrl)));
            }
        }

        return pages;
    }

    #endregion

    #region Helper Methods

    private static SManga PopularMangaFromElement(HtmlNode element)
    {
        var manga = new SManga();
        var picAnchor = element.CssSelect("div.book-pic > a").FirstOrDefault();
        var infoAnchor = element.CssSelect("div.book-info > a").FirstOrDefault();

        if (picAnchor != null)
            manga.ThumbnailUrl =
                picAnchor.CssSelect("img").FirstOrDefault()?.GetAttributeValue("lazy_url", string.Empty);

        if (infoAnchor != null)
        {
            manga.Url = infoAnchor.GetAttributeValue("href", string.Empty);
            manga.Title = infoAnchor.CssSelect("div.book-name").FirstOrDefault()?.InnerText.Trim() ??
                          "Título no encontrado";
        }

        return manga;
    }

    private static SManga LatestUpdatesFromElement(HtmlNode element) => PopularMangaFromElement(element);

    private static SManga SearchedMangaFromElement(HtmlNode element)
    {
        var manga = new SManga();
        var picAnchor = element.CssSelect("div.book-pic > a").FirstOrDefault();
        var infoAnchor = element.CssSelect("div.book-info > a").FirstOrDefault();

        if (picAnchor != null)
            manga.ThumbnailUrl = picAnchor.CssSelect("img").FirstOrDefault()?.GetAttributeValue("src", string.Empty);

        if (infoAnchor != null)
        {
            manga.Url = infoAnchor.GetAttributeValue("href", string.Empty);
            manga.Title = infoAnchor.CssSelect("div.book-name").FirstOrDefault()?.InnerText.Trim() ??
                          "Título no encontrado";
        }

        return manga;
    }

    private static SChapter ChapterFromElement(HtmlNode element)
    {
        var chapter = new SChapter();
        var anchor = element.CssSelect("a").FirstOrDefault();

        if (anchor != null)
        {
            chapter.Url = anchor.GetAttributeValue("href", string.Empty);
            chapter.Name = anchor.CssSelect(".chapter-item-headtitle").FirstOrDefault()?.InnerText.Trim() ??
                           string.Empty;
            chapter.ChapterNumber = chapter.Name.ExtractFloat() ?? -1;
        }

        return chapter;
    }

    #endregion
}