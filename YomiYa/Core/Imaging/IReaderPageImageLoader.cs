using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace YomiYa.Core.Imaging;

public interface IReaderPageImageLoader
{
    Task<Bitmap?> LoadAsync(string imageUrl, CancellationToken cancellationToken);
}

public sealed class ReaderPageImageLoader : IReaderPageImageLoader
{
    public Task<Bitmap?> LoadAsync(string imageUrl, CancellationToken cancellationToken) =>
        imageUrl.LoadImageStrictAsync(cancellationToken);
}
