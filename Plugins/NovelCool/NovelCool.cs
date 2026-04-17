using System.Collections.Concurrent;
using System.Text.Json;
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
    private static readonly Dictionary<string, string> LanguageUrls;
    private readonly NovelCoolSettings _settings;

    static NovelCool()
    {
        var langFilePath = Path.Combine(AppContext.BaseDirectory, "Plugins", "novelcool-lang.json");
        if (File.Exists(langFilePath))
        {
            var json = File.ReadAllText(langFilePath);
            LanguageUrls = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        else
        {
            LanguageUrls = new Dictionary<string, string>();
        }
    }

    public NovelCool()
    {
        _settings = NovelCoolSettings.Load();
    }

    protected override string BaseUrl => LanguageUrls.TryGetValue(_settings.SelectedLanguages.FirstOrDefault() ?? "es", out var url) ? url : "https://www.novelcool.com/";
    public override string Lang => string.Join(", ", _settings.SelectedLanguages);
    public override string Name => "Novel Cool";
    public override string Version => "1.2.0";

    public override HttpClient HttpClient
    {
        get
        {
            var client = base.HttpClient;
            client.DefaultRequestHeaders.Referrer = new Uri(BaseUrl);
            return client;
        }
    }

    public Task<Dictionary<string, bool>> GetConfigurationAsync()
    {
        var config = LanguageUrls.ToDictionary(
            lang => lang.Key,
            lang => _settings.SelectedLanguages.Contains(lang.Key)
        );
        return Task.FromResult(config);
    }

    public Task SetConfigurationAsync(Dictionary<string, bool> configuration)
    {
        _settings.SelectedLanguages = configuration.Where(c => c.Value).Select(c => c.Key).ToList();
        _settings.Save();
        return Task.CompletedTask;
    }

    public override async Task<List<SChapter>> GetChapters(string mangaUrl)
    {
        var doc = await GetHtmlDocumentAsync(mangaUrl);

        return doc.DocumentNode.CssSelect("div.chp-item")
            .Select(ChapterFromElement)
            .ToList();
    }

    public override async Task<MangasPage> GetLatestUpdates(int page = 1)
    {
        var url = $"{BaseUrl}category/latest.html";

        var doc = await GetHtmlDocumentAsync(url);

        var mangas = doc.DocumentNode.CssSelect("div.book-item")
            .Select(LatestUpdatesFromElement)
            .ToList();

        return new MangasPage(mangas, false);
    }


    public override async Task<SManga> GetMangaDetails(string url)
    {
        var doc = await GetHtmlDocumentAsync(url);

        var statusText = doc.DocumentNode.CssSelect("div.bk-going a").FirstOrDefault()?.InnerText.Trim();

        return new SManga
        {
            Description =
                HttpUtility.HtmlDecode(
                        doc.DocumentNode.CssSelect("div.bk-summary-txt").FirstOrDefault()?.InnerText
                    )
                    ?.Trim()
                ?? "Sin descripción",
            Genre =
                doc.DocumentNode.CssSelect("div.bk-cate-item span[itemprop='keywords']")
                    .Select(node => node.InnerText.Trim())
                    .ToList(),
            Status = statusText?.ToLower() switch
            {
                "en marcha" => SManga.Ongoing,
                "completado" => SManga.Completed,
                _ => SManga.Unknown // Valor por defecto si no se encuentra o es otro texto.
            },
        };
    }

    public override async Task<List<Page>> GetPages(string chapterUrl)
    {
        var doc = await GetHtmlDocumentAsync(chapterUrl);
        var baseUri = new Uri(chapterUrl);

        // 1. Obtener todas las URLs de las páginas desde el menú desplegable.
        var pageOptions = doc.DocumentNode.CssSelect("select.sl-page option");
        var pageUrls = pageOptions
            .Select(option => new Uri(baseUri, option.GetAttributeValue("value", string.Empty)).ToString())
            .Where(url => !string.IsNullOrEmpty(url))
            .Distinct() // Asegurarse de que no hay URLs duplicadas
            .ToList();

        // Si no hay un menú desplegable, intenta extraer la imagen de la página actual.
        if (!pageUrls.Any())
        {
            var imageNode = doc.DocumentNode.CssSelect("img.mangaread-manga-pic").FirstOrDefault();
            if (imageNode != null)
            {
                var imageUrl = imageNode.GetAttributeValue("src", string.Empty);
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    return new List<Page>
                    {
                        new Page(Index: 1, ImageUrl: imageUrl, Uri: new Uri(imageUrl))
                    };
                }
            }

            return new List<Page>();
        }

        // 2. Crear un diccionario seguro para hilos para almacenar los resultados.
        var pageData = new ConcurrentDictionary<string, string>();
        var tasks = new List<Task>();

        // 3. Crear una tarea para cada URL de página para ejecutar las peticiones en paralelo.
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
                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            // Guardar la URL de la página y la URL de la imagen encontrada.
                            pageData[pageUrl] = imageUrl;
                        }
                    }
                }
                catch
                {
                    // Manejar errores de red para una página individual si es necesario.
                }
            }));
        }

        // 4. Esperar a que todas las tareas se completen.
        await Task.WhenAll(tasks);

        // 5. Construir la lista final de 'Page' en el orden correcto.
        var pages = new List<Page>();
        for (int i = 0; i < pageUrls.Count; i++)
        {
            if (pageData.TryGetValue(pageUrls[i], out var imageUrl))
            {
                pages.Add(new Page(
                    Index: i + 1,
                    ImageUrl: imageUrl,
                    Uri: new Uri(imageUrl)
                ));
            }
        }

        return pages;
    }

    public override async Task<MangasPage> GetPopularManga(int page = 1)
    {
        var url = $"{BaseUrl}category/popular.html";
        var doc = await GetHtmlDocumentAsync(url);

        var mangas = doc.DocumentNode.CssSelect("div.book-item")
            .Select(PopularMangaFromElement)
            .ToList();

        return new MangasPage(mangas, false);
    }

    public override async Task<MangasPage> SearchManga(string query, int page = 1, string genre = "")
    {
        var searchTasks = _settings.SelectedLanguages.Select(lang =>
        {
            var url = $"{LanguageUrls[lang]}search/?wd={query}&page={page}.html";
            return GetHtmlDocumentAsync(url);
        });

        var docs = await Task.WhenAll(searchTasks);
        var mangas = docs.SelectMany(doc => doc.DocumentNode.CssSelect("div.book-item").Select(SearchedMangaFromElement)).ToList();
        var hasNextPage = docs.Any(doc => doc.DocumentNode.CssSelect("div.page-navone div.row-item.next").Any());

        return new MangasPage(mangas, hasNextPage);
    }

    #region Helper Methods

    private static SManga PopularMangaFromElement(HtmlNode element)
    {
        var manga = new SManga();

        var picAnchor = element.CssSelect("div.book-pic > a").FirstOrDefault();
        var infoAnchor = element.CssSelect("div.book-info > a").FirstOrDefault();

        if (picAnchor != null)
        {
            manga.ThumbnailUrl = picAnchor.CssSelect("img").FirstOrDefault()
                ?.GetAttributeValue("lazy_url", string.Empty);
        }

        if (infoAnchor != null)
        {
            manga.Url = infoAnchor.GetAttributeValue("href", string.Empty);
            manga.Title = infoAnchor.CssSelect("div.book-name").FirstOrDefault()
                ?.InnerText.Trim() ?? "Título no encontrado";
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
        {
            manga.ThumbnailUrl = picAnchor.CssSelect("img").FirstOrDefault()
                ?.GetAttributeValue("src", string.Empty);
        }

        if (infoAnchor != null)
        {
            manga.Url = infoAnchor.GetAttributeValue("href", string.Empty);
            manga.Title = infoAnchor.CssSelect("div.book-name").FirstOrDefault()
                ?.InnerText.Trim() ?? "Título no encontrado";
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