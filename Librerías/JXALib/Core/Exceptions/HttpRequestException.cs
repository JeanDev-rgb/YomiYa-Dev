namespace YomiYa.Core.Exceptions;

/// <summary>
/// Se lanza cuando una petición HTTP falla por razones de red u otras excepciones HTTP.
/// </summary>
public class HttpRequestException(string message, Exception innerException) : Exception(message, innerException);