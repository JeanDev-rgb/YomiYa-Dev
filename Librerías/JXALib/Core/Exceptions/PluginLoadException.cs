namespace YomiYa.Core.Exceptions;

/// <summary>
/// Se lanza cuando un plugin no puede ser cargado correctamente.
/// </summary>
public class PluginLoadException(string message, Exception innerException) : Exception(message, innerException);