namespace YomiYa.Core.Exceptions;

/// <summary>
/// Se lanza cuando ocurre un error durante el análisis del contenido HTML.
/// </summary>
public class HtmlParsingException : Exception
{
    public HtmlParsingException(string message) : base(message)
    {
    }

    public HtmlParsingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}