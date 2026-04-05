using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using YomiYa.Core.Resilience;
using YomiYa.Core.Resilience.Handlers;

namespace YomiYa.Core.Imaging;

public static class ImageHelper
{
    private static readonly HttpClient HttpClient;

    private static readonly string CacheDirectory =
        Path.Combine(AppContext.BaseDirectory, "Cache", "ImageCache");

    static ImageHelper()
    {
        var handler = new HttpClientHandler();
        var policyHandler = new PolicyHandler(ResiliencePolicies.GetImageRetryPolicy(), handler);
        HttpClient = new HttpClient(policyHandler);

        Directory.CreateDirectory(CacheDirectory);
    }

    public static async Task<Bitmap?> LoadImageAsync(this string url, CancellationToken cancellationToken = default,
        bool isCover = false)
    {
        if (string.IsNullOrEmpty(url)) return null;

        var uri = new Uri(url);

        switch (uri.Scheme)
        {
            case "http":
            case "https":
                return await LoadFromWebWithCacheAsync(uri, cancellationToken, isCover);

            case "avares":
                return LoadFromResource(uri);

            default:
                Console.WriteLine($"Esquema de URI no soportado: {uri.Scheme}");
                return null;
        }
    }

    private static Bitmap? LoadFromResource(Uri resourceUri)
    {
        try
        {
            return new Bitmap(AssetLoader.Open(resourceUri));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al cargar el recurso de imagen '{resourceUri}': {ex.Message}");
            return null;
        }
    }

    private static async Task<Bitmap?> LoadFromWebWithCacheAsync(Uri url, CancellationToken cancellationToken,
        bool isCover)
    {
        var imagePath = GetCacheFilePath(url.ToString());

        try
        {
            if (!File.Exists(imagePath))
            {
                var imageBytes = await HttpClient.GetByteArrayAsync(url, cancellationToken);
                await File.WriteAllBytesAsync(imagePath, imageBytes, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested) return null;

            await using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);

            // Decodificamos la imagen a un tamaño más pequeño para ahorrar RAM.
            // Para las portadas, usamos un ancho fijo de 200px.
            // Para las páginas del lector, un ancho máximo de 1080px (suficiente para la mayoría de pantallas).
            int decodeWidth = isCover ? 200 : 1080;
            return await Task.Run(() => Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Carga de imagen cancelada para '{url}'");
            return LoadErrorPlaceholder();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al cargar o descargar la imagen desde '{url}': {ex.Message}");
            return LoadErrorPlaceholder();
        }
    }


    private static Bitmap? LoadErrorPlaceholder()
    {
        // Crea un DrawingImage para renderizar el StreamGeometry
        if (Application.Current?.Styles.TryGetResource("CoverError", null, out var resource) == true &&
            resource is StreamGeometry geometry)
        {
            var drawing = new GeometryDrawing
            {
                Geometry = geometry,
                Brush = Brushes.Gray, // Color del ícono
            };

            var drawingImage = new DrawingImage { Drawing = drawing };

            // Renderiza el DrawingImage a un Bitmap
            var bitmap = new RenderTargetBitmap(new Avalonia.PixelSize(200, 280), new Vector(96, 96));
            using (var ctx = bitmap.CreateDrawingContext())
            {
                ctx.DrawImage(drawingImage, new Avalonia.Rect(0, 0, 200, 280));
            }

            return bitmap;
        }

        return null;
    }

    /// <summary>
    /// Asegura que una imagen esté en la caché del disco, descargándola si es necesario.
    /// No carga el Bitmap en memoria, solo guarda el archivo.
    /// </summary>
    public static async Task EnsureImageIsCachedAsync(this string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url)) return;

        var uri = new Uri(url);

        if (uri.Scheme != "http" && uri.Scheme != "https") return;

        var imagePath = GetCacheFilePath(url);
        if (File.Exists(imagePath)) return;

        try
        {
            await HttpClient.GetByteArrayAsync(uri, cancellationToken)
                .ContinueWith(async task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        await File.WriteAllBytesAsync(imagePath, task.Result, cancellationToken);
                    }
                }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al cachear la imagen '{url}': {ex.Message}");
        }
    }

    private static string GetCacheFilePath(string url)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        var sb = new StringBuilder();
        foreach (var b in hash) sb.Append(b.ToString("x2"));

        var extension = Path.GetExtension(url);
        if (string.IsNullOrEmpty(extension) || extension.Length > 5)
            extension = ".jpg";

        return Path.Combine(CacheDirectory, $"{sb}{extension}");
    }
}