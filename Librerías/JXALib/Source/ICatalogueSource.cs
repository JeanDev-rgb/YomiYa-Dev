using YomiYa.Source.Models;

namespace YomiYa.Source;

public interface ICatalogueSource : ISource
{
    string Lang { get; }
    bool SupportsLatest { get; }

    IObservable<MangasPage> FetchPopularManga(int page);

    IObservable<MangasPage> FetchSearchManga(int page, string query /*, FilterList filter*/);

    IObservable<MangasPage> FetchLatestUpdates(int page);

    // FilterList GetFilterList();
}