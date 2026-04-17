using System.Security.Cryptography;
using System.Text;

namespace YomiYa.Utils;

public static class GenerateId
{
    /// <summary>
    /// Genera un ID estable similar a la implementación de Mihon/Tachiyomi.
    /// Crea un hash MD5 o SHA basado en el nombre y el idioma.
    /// </summary>
    public static long GenerateSourceId(string name, string lang)
    {
        var key = $"{name}/{lang}";
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(key));

        return BitConverter.ToInt64(hashBytes, 0);
    }
}