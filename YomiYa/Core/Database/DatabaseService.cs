using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Polly.Retry;
using YomiYa.Core.IO;
using YomiYa.Core.Resilience;
using YomiYa.Domain.Models;

namespace YomiYa.Core.Database;

public class DatabaseService : IDatabaseService
{
    private static readonly string DbPath = PathHelper.GetDatabasePath();

    private static readonly AsyncRetryPolicy DbRetryPolicy = ResiliencePolicies.GetDatabaseRetryPolicy();

    static DatabaseService()
    {
        var directory = Path.GetDirectoryName(DbPath);
        if (directory is not null) Directory.CreateDirectory(directory);
    }

    private string GetConnectionString()
    {
        return $"Data Source={DbPath}";
    }

    public async Task InitializeDatabase()
    {
        await DbRetryPolicy.ExecuteAsync(async () =>
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            // Tabla de Mangas
            var createMangasTableCmd = connection.CreateCommand();
            createMangasTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS mangas (
                id INTEGER PRIMARY KEY, url TEXT NOT NULL UNIQUE, title TEXT NOT NULL,
                artist TEXT, author TEXT, description TEXT, genre TEXT,
                status INTEGER NOT NULL DEFAULT 0, thumbnail_url TEXT,
                is_favorite INTEGER NOT NULL DEFAULT 0, plugin TEXT NOT NULL
            );";
            await createMangasTableCmd.ExecuteNonQueryAsync();

            // Tabla de Capítulos (con campos de progreso restaurados)
            var createChaptersTableCmd = connection.CreateCommand();
            createChaptersTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS chapters (
                id INTEGER PRIMARY KEY, manga_id INTEGER NOT NULL, url TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL, scanlator TEXT, chapter_number REAL NOT NULL DEFAULT -1,
                date_upload INTEGER NOT NULL DEFAULT 0, last_page_read INTEGER NOT NULL DEFAULT 0,
                read INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY(manga_id) REFERENCES mangas(id) ON DELETE CASCADE
            );";
            await createChaptersTableCmd.ExecuteNonQueryAsync();

            // Tabla de Historial
            var createHistoryTableCmd = connection.CreateCommand();
            createHistoryTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS history (
                id INTEGER PRIMARY KEY,
                chapter_id INTEGER NOT NULL UNIQUE,
                last_read INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY(chapter_id) REFERENCES chapters(id) ON DELETE CASCADE
            );";
            await createHistoryTableCmd.ExecuteNonQueryAsync();
        });
    }

    // --- MÉTODOS DE INSERCIÓN Y ACTUALIZACIÓN ---

    private async Task<long?> GetMangaId(SqliteConnection connection, string mangaUrl)
    {
        return await DbRetryPolicy.ExecuteAsync(async () =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT id FROM mangas WHERE url = $url;";
            command.Parameters.AddWithValue("$url", mangaUrl);
            return (long?)await command.ExecuteScalarAsync();
        });
    }

    public async Task<long> InsertManga(SManga manga)
    {
        return await DbRetryPolicy.ExecuteAsync(async () =>
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var existingId = await GetMangaId(connection, manga.Url);
            if (existingId.HasValue) return existingId.Value;

            var command = connection.CreateCommand();
            command.CommandText = @"
            INSERT OR IGNORE INTO mangas (url, title, author, artist, description, genre, status, thumbnail_url, plugin, is_favorite)
            VALUES ($url, $title, $author, $artist, $description, $genre, $status, $thumbnail_url, $plugin, $is_favorite);
        ";

            command.Parameters.AddWithValue("$url", manga.Url);
            command.Parameters.AddWithValue("$title", manga.Title);
            command.Parameters.AddWithValue("$author", manga.Author ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$artist", manga.Artist ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$description", manga.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$genre",
                manga.Genre != null ? string.Join(",", manga.Genre) : DBNull.Value);
            command.Parameters.AddWithValue("$status", manga.Status);
            command.Parameters.AddWithValue("$thumbnail_url", manga.ThumbnailUrl ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$plugin", manga.Plugin);
            command.Parameters.AddWithValue("$is_favorite", manga.IsFavorite ? 1 : 0);

            await command.ExecuteNonQueryAsync();
            return await GetMangaId(connection, manga.Url) ?? 0;
        });
    }

    public async Task InsertChapters(long mangaId, IEnumerable<SChapter> chapters)
    {
        await DbRetryPolicy.ExecuteAsync(async () =>
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();

            foreach (var chapter in chapters)
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                INSERT OR IGNORE INTO chapters (manga_id, url, name, scanlator, chapter_number, date_upload)
                VALUES ($manga_id, $url, $name, $scanlator, $chapter_number, $date_upload);";
                command.Parameters.AddWithValue("$manga_id", mangaId);
                command.Parameters.AddWithValue("$url", chapter.Url);
                command.Parameters.AddWithValue("$name", chapter.Name);
                command.Parameters.AddWithValue("$scanlator", chapter.Scanlator ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$chapter_number", chapter.ChapterNumber);
                command.Parameters.AddWithValue("$date_upload", chapter.DateUpload);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        });
    }

    public async Task SetMangaFavoriteStatusAsync(SManga manga, bool isFavorite)
    {
        await DbRetryPolicy.ExecuteAsync(async () =>
        {
            await InsertManga(manga);
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE mangas SET is_favorite = $is_favorite WHERE url = $url;";
            command.Parameters.AddWithValue("$is_favorite", isFavorite ? 1 : 0);
            command.Parameters.AddWithValue("$url", manga.Url);
            await command.ExecuteNonQueryAsync();
        });
    }

    /// <summary>
    ///     Guarda el progreso de un capítulo y actualiza su fecha en el historial.
    /// </summary>
    public async Task SetChapterProgress(string chapterUrl, int lastPageRead, bool isRead)
    {
        await DbRetryPolicy.ExecuteAsync(async () =>
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();

            // 1. Actualizar progreso en la tabla de capítulos
            var chapterUpdateCmd = connection.CreateCommand();
            chapterUpdateCmd.CommandText =
                "UPDATE chapters SET last_page_read = $last_page_read, read = $read WHERE url = $url;";
            chapterUpdateCmd.Parameters.AddWithValue("$last_page_read", lastPageRead);
            chapterUpdateCmd.Parameters.AddWithValue("$read", isRead ? 1 : 0);
            chapterUpdateCmd.Parameters.AddWithValue("$url", chapterUrl);
            await chapterUpdateCmd.ExecuteNonQueryAsync();

            // 2. Obtener ID del capítulo
            var chapterIdCmd = connection.CreateCommand();
            chapterIdCmd.CommandText = "SELECT id FROM chapters WHERE url = $url;";
            chapterIdCmd.Parameters.AddWithValue("$url", chapterUrl);
            var chapterId = (long?)await chapterIdCmd.ExecuteScalarAsync();
            if (chapterId == null) return;

            // 3. Actualizar la fecha en la tabla de historial
            var historyUpdateCmd = connection.CreateCommand();
            historyUpdateCmd.CommandText = @"
            INSERT OR REPLACE INTO history (chapter_id, last_read)
            VALUES ($chapter_id, $last_read);";
            historyUpdateCmd.Parameters.AddWithValue("$chapter_id", chapterId);
            historyUpdateCmd.Parameters.AddWithValue("$last_read", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await historyUpdateCmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        });
    }

    // --- Métodos de Consulta ---

    public async Task<SManga?> GetMangaByUrlAsync(string mangaUrl)
    {
        return await DbRetryPolicy.ExecuteAsync(async () =>
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM mangas WHERE url = $url LIMIT 1;";
            command.Parameters.AddWithValue("$url", mangaUrl);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new SManga
            {
                Url = reader.GetString(reader.GetOrdinal("url")),
                Title = reader.GetString(reader.GetOrdinal("title")),
                Author =
                    reader.IsDBNull(reader.GetOrdinal("author")) ? null : reader.GetString(reader.GetOrdinal("author")),
                Artist =
                    reader.IsDBNull(reader.GetOrdinal("artist")) ? null : reader.GetString(reader.GetOrdinal("artist")),
                Description = reader.IsDBNull(reader.GetOrdinal("description"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("description")),
                Genre = reader.IsDBNull(reader.GetOrdinal("genre"))
                    ? []
                    : reader.GetString(reader.GetOrdinal("genre")).Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .ToList(),
                Status = reader.GetInt32(reader.GetOrdinal("status")),
                ThumbnailUrl = reader.IsDBNull(reader.GetOrdinal("thumbnail_url"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("thumbnail_url")),
                Plugin = reader.GetString(reader.GetOrdinal("plugin")),
                IsFavorite = reader.GetInt32(reader.GetOrdinal("is_favorite")) == 1
            };
        });
    }

    public async Task<List<SChapter>> GetChaptersAsync(string mangaUrl)
    {
        return await DbRetryPolicy.ExecuteAsync(async () =>
        {
            var chapters = new List<SChapter>();
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();
            var mangaId = await GetMangaId(connection, mangaUrl);
            if (mangaId == null) return chapters;

            var command = connection.CreateCommand();
            command.CommandText = @"SELECT * FROM chapters WHERE manga_id = $manga_id ORDER BY chapter_number DESC;";
            command.Parameters.AddWithValue("$manga_id", mangaId);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                chapters.Add(new SChapter
                {
                    Url = reader.GetString(reader.GetOrdinal("url")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    ChapterNumber = (float)reader.GetDouble(reader.GetOrdinal("chapter_number")),
                    Scanlator = reader.IsDBNull(reader.GetOrdinal("scanlator"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("scanlator")),
                    DateUpload = reader.GetInt64(reader.GetOrdinal("date_upload")),
                    IsRead = reader.GetInt32(reader.GetOrdinal("read")) == 1,
                    LastPageRead = reader.GetInt32(reader.GetOrdinal("last_page_read"))
                });

            return chapters;
        });
    }

    public async Task<List<SManga>> GetLibraryMangasAsync()
    {
        return await DbRetryPolicy.ExecuteAsync(async () =>
        {
            var mangas = new List<SManga>();
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM mangas WHERE is_favorite = 1 ORDER BY title;";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                mangas.Add(new SManga
                {
                    Url = reader.GetString(reader.GetOrdinal("url")),
                    Title = reader.GetString(reader.GetOrdinal("title")),
                    ThumbnailUrl = reader.IsDBNull(reader.GetOrdinal("thumbnail_url"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("thumbnail_url")),
                    Plugin = reader.GetString(reader.GetOrdinal("plugin")),
                    IsFavorite = true
                });

            return mangas;
        });
    }

    public async Task<List<(SManga Manga, SChapter Chapter)>> GetHistoryAsync()
    {
        return await DbRetryPolicy.ExecuteAsync(async () =>
        {
            var history = new List<(SManga, SChapter)>();
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT
                m.url AS manga_url, m.title, m.thumbnail_url, m.plugin,
                c.url AS chapter_url, c.name, h.last_read
            FROM history h
            JOIN chapters c ON h.chapter_id = c.id
            JOIN mangas m ON c.manga_id = m.id
            ORDER BY h.last_read DESC
            LIMIT 100;";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                history.Add((
                    new SManga
                    {
                        Url = reader.GetString(reader.GetOrdinal("manga_url")),
                        Title = reader.GetString(reader.GetOrdinal("title")),
                        ThumbnailUrl = reader.IsDBNull(reader.GetOrdinal("thumbnail_url"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("thumbnail_url")),
                        Plugin = reader.GetString(reader.GetOrdinal("plugin"))
                    },
                    new SChapter
                    {
                        Url = reader.GetString(reader.GetOrdinal("chapter_url")),
                        Name = reader.GetString(reader.GetOrdinal("name")),
                        DateUpload = reader.GetInt64(reader.GetOrdinal("last_read"))
                    }
                ));

            return history;
        });
    }

    public async Task DeleteHistoryItemAsync(string chapterUrl)
    {
        await DbRetryPolicy.ExecuteAsync(async () =>
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var chapterIdCmd = connection.CreateCommand();
            chapterIdCmd.CommandText = "SELECT id FROM chapters WHERE url = $url;";
            chapterIdCmd.Parameters.AddWithValue("$url", chapterUrl);
            var chapterId = (long?)await chapterIdCmd.ExecuteScalarAsync();

            if (chapterId != null)
            {
                var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM history WHERE chapter_id = $chapter_id;";
                deleteCmd.Parameters.AddWithValue("$chapter_id", chapterId);
                await deleteCmd.ExecuteNonQueryAsync();
            }
        });
    }

    public async Task ClearHistoryAsync()
    {
        await DbRetryPolicy.ExecuteAsync(async () =>
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM history;";
            await command.ExecuteNonQueryAsync();
        });
    }

    // --- MÉTODO NUEVO PARA ELIMINAR MANGAS ---
    public async Task DeleteMangaAsync(string url)
    {
        await DbRetryPolicy.ExecuteAsync(async () =>
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM mangas WHERE Url = $url";
            command.Parameters.AddWithValue("$url", url);

            await command.ExecuteNonQueryAsync();
        });
    }
}