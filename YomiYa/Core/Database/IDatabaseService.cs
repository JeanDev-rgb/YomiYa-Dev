using System.Collections.Generic;
using System.Threading.Tasks;
using YomiYa.Domain.Models;

namespace YomiYa.Core.Database;

public interface IDatabaseService
{
    Task ClearHistoryAsync();
    Task DeleteHistoryItemAsync(string chapterUrl);
    Task DeleteMangaAsync(string url);
    Task<List<SChapter>> GetChaptersAsync(string mangaUrl);
    Task<List<(SManga Manga, SChapter Chapter)>> GetHistoryAsync();
    Task<List<SManga>> GetLibraryMangasAsync();
    Task<SManga?> GetMangaByUrlAsync(string mangaUrl);
    Task InitializeDatabase();
    Task InsertChapters(long mangaId, IEnumerable<SChapter> chapters);
    Task<long> InsertManga(SManga manga);
    Task SetChapterProgress(string chapterUrl, int lastPageRead, bool isRead);
    Task SetMangaFavoriteStatusAsync(SManga manga, bool isFavorite);
}