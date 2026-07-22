using YomiYa.Domain.Models;

namespace YomiYa.Source.Models;

public record MangasPage(List<SManga> Mangas, bool HasNextPage)
{
}