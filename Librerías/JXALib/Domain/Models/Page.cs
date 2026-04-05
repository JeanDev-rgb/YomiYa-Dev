using Avalonia.Media.Imaging;

namespace YomiYa.Domain.Models;

public record Page(
    int Index,
    string Url = "",
    string? ImageUrl = null,
    Uri? Uri = null,
    Bitmap? Data = null
) : IDisposable
{
    public void Dispose()
    {
        Data?.Dispose();
    }
}