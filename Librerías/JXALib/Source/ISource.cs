using YomiYa.Domain.Models;

namespace YomiYa.Source;

public interface ISource
{
    long Id { get; set; }
    string Name { get; set; }

    Task<SManga> GetMangaDetails(SManga manga);
    Task<List<SChapter>> GetChapterList(SManga manga);
    IObservable<List<Page>> FetchPageList(SChapter chapter);
}