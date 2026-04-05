using YomiYa.Source.Models;
using YomiYa.Domain.Models;

namespace YomiYa.Source.Online;

public interface IParsedHttpSource
{
    HttpClient HttpClient { get; }
    string Name { get; }
    string Version { get; }

    Task<List<SChapter>> GetChapters(string mangaUrl);
    Task<MangasPage> GetLatestUpdates(int page = 1);
    Task<SManga> GetMangaDetails(string url);
    Task<List<Page>> GetPages(string chapterUrl);
    Task<MangasPage> GetPopularManga(int page = 1);
    Task<MangasPage> SearchManga(string query, int page = 1, string genre = "");
}