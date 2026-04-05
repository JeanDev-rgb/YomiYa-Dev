namespace YomiYa.Source.Models;

public record MangasPage(List<Domain.Models.SManga> Mangas, bool HasNextPage)
{
}