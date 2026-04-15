using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YomiYa.Core.Plugins;
using YomiYa.Domain.Models;

namespace YomiYa.Core.Services;

/// <summary>
///     Gestiona la obtención y el almacenamiento en caché (basado en archivos) de los detalles de los mangas.
///     Esta caché es para mangas que no están marcados como favoritos en la base de datos,
///     para evitar descargas repetidas al navegar.
/// </summary>
public static class MangaDetailsHelper
{
    private static readonly string CacheDirectory =
        Path.Combine(AppContext.BaseDirectory, "Cache", "MangaDetailsCache");

    // Semáforos para evitar que se descarguen los detalles del mismo manga simultáneamente.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Semaphores = new();

    static MangaDetailsHelper()
    {
        Directory.CreateDirectory(CacheDirectory);
    }

    /// <summary>
    ///     Obtiene los detalles de un manga, usando la caché de archivos si está disponible,
    ///     o descargándolos de la red si no.
    /// </summary>
    public static async Task GetOrFetchDetailsAsync(SManga manga)
    {
        if (manga.Initialized) return;

        var cachePath = GetCacheFilePath(manga.Url);

        // Intento rápido de carga desde caché sin bloqueo.
        if (File.Exists(cachePath))
            try
            {
                var json = await File.ReadAllTextAsync(cachePath);
                var cachedDetails = JsonSerializer.Deserialize<SManga>(json);
                if (cachedDetails is not null)
                {
                    ApplyDetails(manga, cachedDetails);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error al leer caché de detalles de manga '{cachePath}'. Se reintentará la descarga. Error: {ex.Message}");
            }

        var semaphore = Semaphores.GetOrAdd(cachePath, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        try
        {
            // Volver a comprobar la caché. Otro hilo podría haberlo descargado mientras esperábamos.
            if (File.Exists(cachePath))
            {
                var json = await File.ReadAllTextAsync(cachePath);
                var cachedDetails = JsonSerializer.Deserialize<SManga>(json);
                if (cachedDetails is not null)
                {
                    ApplyDetails(manga, cachedDetails);
                    return;
                }
            }

            // Si no está en caché, obtener de la red.
            if (manga.Plugin is null) return;
            var plugin = PluginManager.GetPlugin(manga.Plugin);
            if (plugin is null) return;

            var details = await plugin.GetMangaDetails(manga.Url);
            ApplyDetails(manga, details);

            // Guardar en la caché de archivos para la próxima vez.
            var newJson = JsonSerializer.Serialize(details);
            await File.WriteAllTextAsync(cachePath, newJson);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static void ApplyDetails(SManga manga, SManga details)
    {
        manga.Author = details.Author;
        manga.Artist = details.Artist;
        manga.Description = details.Description;
        manga.Genre = details.Genre;
        manga.Status = details.Status;
        manga.Initialized = true;
    }

    private static string GetCacheFilePath(string url)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        var sb = new StringBuilder();
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return Path.Combine(CacheDirectory, $"{sb}.json");
    }
}