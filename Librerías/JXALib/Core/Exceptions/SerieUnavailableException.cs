namespace YomiYa.Core.Exceptions;

/// <summary>
/// Se lanza cuando se intenta acceder a una serie que no está disponible o no se encuentra.
/// </summary>
public class SerieUnavailableException : Exception
{
    public SerieUnavailableException(string message = "Esta serie no se encuentra disponible.") : base(message)
    {
    }

    public SerieUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}