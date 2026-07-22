using System.Text.Json.Serialization;
using YomiYa.Domain.Models;

namespace YomiYa.Extensions.Es;

public class PayloadPopularDto
{
    [JsonPropertyName("top")]
    public PopularDto Data { get; set; }
}

public class PopularDto
{
    [JsonPropertyName("manhwas_esp")]
    public List<PopularComicDto> Weekly { get; set; }
    [JsonPropertyName("manhwas_raw")]
    public List<PopularComicDto> Total { get; set; }
}

public class PopularComicDto
{
    [JsonPropertyName("link")]
    public string Slug { get; set; }

    [JsonPropertyName("numero")]
    public int Views { get; set; }

    private string Name { get; set; }

    [JsonPropertyName("imagen")]
    private string Thumbnail { get; set; }

    public SManga ToSManga()
    {
        return new SManga
        {
            Title = Name,
            ThumbnailUrl = Thumbnail,
            Url = Slug.TrimStart('/').Replace("manga/", "manhwa/")
        };
    }
}

 public class PayloadLatestDto
    {
        [JsonPropertyName("manhwas")]
        public LatestDto Data { get; set; }
    }

    public class LatestDto
    {
        [JsonPropertyName("manhwas_esp")]
        public List<LatestComicDto> Esp { get; set; }

        [JsonPropertyName("manhwas_raw")]
        public List<LatestComicDto> Raw18 { get; set; }

        [JsonPropertyName("_manhwas")]
        public List<LatestComicDto> Esp18 { get; set; }
    }

    public class LatestComicDto
    {
        [JsonPropertyName("create")]
        public long LatestChapterDate { get; set; }

        [JsonPropertyName("id_rel")]
        public string Slug { get; set; }

        [JsonPropertyName("name_manhwa")]
        private string Name { get; set; }

        [JsonPropertyName("img")]
        private string Thumbnail { get; set; }

        public SManga ToSManga()
        {
            return new SManga
            {
                Title = Name,
                ThumbnailUrl = Thumbnail,
                Url = $"manhwa/{Slug}"
            };
        }
    }

    public class PayloadSearchDto
    {
        [JsonPropertyName("data")]
        public List<SearchComicDto> Data { get; set; }

        [JsonPropertyName("next")]
        public bool HasNextPage { get; set; }
    }

    public class SearchComicDto
    {
        [JsonPropertyName("real_id")]
        public string Slug { get; set; }

        [JsonPropertyName("the_real_name")]
        private string Name { get; set; }

        [JsonPropertyName("_imagen")]
        private string Thumbnail { get; set; }

        public SManga ToSManga()
        {
            return new SManga
            {
                Title = Name,
                ThumbnailUrl = Thumbnail,
                Url = $"manhwa/{Slug}"
            };
        }
    }

    public class ComicDetailsDto
    {
        [JsonPropertyName("name_esp")]
        private string TitleValue { get; set; }

        [JsonPropertyName("_sinopsis")]
        private string DescriptionValue { get; set; }

        [JsonPropertyName("_status")]
        private string StatusValue { get; set; }

        [JsonPropertyName("_name")]
        private string AlternativeName { get; set; }

        [JsonPropertyName("_imagen")]
        private string Thumbnail { get; set; }

        [JsonPropertyName("_categoris")]
        public List<Dictionary<int, string>> Genres { get; set; }

        [JsonPropertyName("_extras")]
        public ComicDetailsExtrasDto Extras { get; set; }

        public SManga ToSManga()
        {
            var manga = new SManga
            {
                Title = TitleValue,
                ThumbnailUrl = Thumbnail,
                Description = DescriptionValue,
                Status = ParseStatus(StatusValue),
                Genre = Genres
                    .Select(g => g.Values.FirstOrDefault())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList(),
                Author = string.Join(", ", Extras.Authors)
            };

            if (!string.IsNullOrWhiteSpace(AlternativeName))
            {
                if (!string.IsNullOrWhiteSpace(manga.Description))
                    manga.Description += "\n\n";

                manga.Description += $"Nombres alternativos: {AlternativeName}";
            }

            return manga;
        }

        private static int ParseStatus(string status)
        {
            return status switch
            {
                "publicandose" => SManga.Ongoing,
                "finalizado" => SManga.Completed,
                _ => SManga.Unknown
            };
        }
    }

    public class ComicDetailsExtrasDto
    {
        [JsonPropertyName("autores")]
        public List<string> Authors { get; set; }
    }

    public class PayloadChapterDto
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; }

        [JsonPropertyName("real_id")]
        public string RealId { get; set; }

        [JsonPropertyName("chapters")]
        public List<ChapterDto> Chapters { get; set; }
    }

    public class ChapterDto
    {
        [JsonPropertyName("chapter")]
        public float Number { get; set; }

        [JsonPropertyName("link")]
        public string EspUrl { get; set; }

        [JsonPropertyName("link_raw")]
        public string RawUrl { get; set; }

        [JsonPropertyName("create")]
        public long? CreatedAt { get; set; }
    }

    public class PayloadPageDto
    {
        [JsonPropertyName("chapter")]
        public PageDto Data { get; set; }
    }

    public class PageDto
    {
        [JsonPropertyName("img")]
        public List<string> Images { get; set; }
    }