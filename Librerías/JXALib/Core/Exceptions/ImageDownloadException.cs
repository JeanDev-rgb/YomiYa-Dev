namespace YomiYa.Core.Exceptions;

/// <summary>
/// Se lanza cuando ocurre un error específico durante la descarga de una imagen.
/// </summary>
public class ImageDownloadException(string message, Exception innerException) : Exception(message, innerException);