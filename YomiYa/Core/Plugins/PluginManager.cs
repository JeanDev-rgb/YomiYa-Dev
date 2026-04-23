using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YomiYa.Core.Exceptions;
using YomiYa.Core.IO;
using YomiYa.Core.Services;
using YomiYa.Source.Online;

namespace YomiYa.Core.Plugins;

/// <summary>
///     Administra la carga, recuperación y descarga de plugins que implementan la interfaz <see cref="ParsedHttpSource" />
/// </summary>
public class PluginManager
{
    private static readonly List<ParsedHttpSource> _plugins = new();

    private static readonly Dictionary<string, (ParsedHttpSource plugin, PluginLoadContext context, string sourceDllPath)> _pluginLookup =
        new();

    private static string ShadowPluginsPath => Path.Combine(PathHelper.PluginsPath, ".shadow");

    private static bool ReleaseAssemblyFileLock()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDeleteFileWithRetries(string filePath, int attempts = 3, int delayMs = 120)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                if (!File.Exists(filePath)) return true;
                File.Delete(filePath);
                return true;
            }
            catch
            {
                ReleaseAssemblyFileLock();
                if (i < attempts - 1)
                {
                    System.Threading.Thread.Sleep(delayMs);
                }
            }
        }

        return !File.Exists(filePath);
    }

    private static void CleanupShadowDirectory()
    {
        if (!Directory.Exists(ShadowPluginsPath)) return;

        try
        {
            foreach (var file in Directory.GetFiles(ShadowPluginsPath, "*.dll"))
            {
                TryDeleteFileWithRetries(file, attempts: 2, delayMs: 60);
            }
        }
        catch
        {
            // Ignoramos errores de limpieza no críticos.
        }
    }

    private static string CreateShadowCopy(string pluginFile)
    {
        Directory.CreateDirectory(ShadowPluginsPath);
        var shadowFile = Path.Combine(
            ShadowPluginsPath,
            $"{Path.GetFileNameWithoutExtension(pluginFile)}_{Guid.NewGuid():N}.dll");
        File.Copy(pluginFile, shadowFile, true);
        return shadowFile;
    }

    public PluginManager()
    {
        LoadPlugins();
    }

    private static void LoadPlugins()
    {
        if (!Directory.Exists(PathHelper.PluginsPath))
        {
            Directory.CreateDirectory(PathHelper.PluginsPath);
            return;
        }

        _plugins.Clear();
        _pluginLookup.Clear();

        CleanupShadowDirectory();
        var pluginFiles = Directory.GetFiles(PathHelper.PluginsPath, "*.dll");

        foreach (var pluginFile in pluginFiles)
        {
            try
            {
                var shadowPath = CreateShadowCopy(pluginFile);
                var loadContext = new PluginLoadContext(shadowPath);
                var assembly = loadContext.LoadFromAssemblyPath(shadowPath);

                // Cargar plugins clásicos
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(ParsedHttpSource).IsAssignableFrom(t) && 
                                !t.IsAbstract && 
                                !t.IsInterface);

                foreach (var type in pluginTypes)
                {
                    // IMPORTANTE: Asegurarnos de que tenga un constructor vacío para evitar crasheos.
                    var hasEmptyConstructor = type.GetConstructors().Any(c => c.GetParameters().Length == 0);
                    if (!hasEmptyConstructor) continue;

                    var pluginInstance = (ParsedHttpSource)Activator.CreateInstance(type)!;
                    if (_pluginLookup.ContainsKey(pluginInstance.Name)) continue;
                    
                    _plugins.Add(pluginInstance);
                    _pluginLookup[pluginInstance.Name] = (pluginInstance, loadContext, pluginFile);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    new PluginLoadException($"Error al cargar el plugin {pluginFile}: {ex.Message}\n{ex.StackTrace}",
                        ex));
            }
        }
    }

    public static ParsedHttpSource? GetPlugin(string name)
    {
        if (MangaService.SelectedPlugin is null) LoadPlugins();
        return _pluginLookup.TryGetValue(name, out var entry)
            ? entry.plugin
            : null;
    }

    public static List<ParsedHttpSource> GetAllPlugins()
    {
        ReloadPlugins();
        return _plugins;
    }

    private static void UnloadPlugins()
    {
        // Capturamos contextos únicos antes de limpiar estructuras.
        var contexts = _pluginLookup.Values
            .Select(v => v.context)
            .Distinct()
            .ToList();

        foreach (var plugin in _plugins)
            if (plugin is IDisposable disposablePlugin)
                disposablePlugin.Dispose();

        foreach (var context in contexts)
            context.Unload();

        _plugins.Clear();
        _pluginLookup.Clear();
        ReleaseAssemblyFileLock();
        CleanupShadowDirectory();
        Console.WriteLine("Todos los plugins han sido descargados.");
    }

    public static void ReloadPlugins()
    {
        UnloadPlugins();
        LoadPlugins();
    }

    public static bool DeletePlugin(string pluginName)
    {
        if (!_pluginLookup.TryGetValue(pluginName, out var pluginEntry))
            return false;

        // Limpiar recursos del plugin
        if (pluginEntry.plugin is IDisposable disposable)
            disposable.Dispose();

        _plugins.Remove(pluginEntry.plugin);
        _pluginLookup.Remove(pluginName);

        // Verificación de seguridad: si por alguna razón una DLL tiene más de 1 plugin, 
        // no borramos el archivo hasta que todos sean eliminados de la memoria.
        var isContextShared = _pluginLookup.Values.Any(v => v.context == pluginEntry.context);
        
        if (!isContextShared)
        {
            pluginEntry.context.Unload();
            ReleaseAssemblyFileLock();
            
            var assemblyLocation = pluginEntry.sourceDllPath;
            if (string.IsNullOrEmpty(assemblyLocation))
                assemblyLocation = Path.Combine(PathHelper.PluginsPath, pluginName + ".dll");

            if (!File.Exists(assemblyLocation)) return false;
            try
            {
                if (!TryDeleteFileWithRetries(assemblyLocation))
                {
                    Console.WriteLine($"No se pudo eliminar el plugin {assemblyLocation}: archivo en uso.");
                    return false;
                }

                Console.WriteLine($"Plugin DLL {Path.GetFileName(assemblyLocation)} eliminado.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No se pudo eliminar el plugin {assemblyLocation}: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"El plugin {pluginName} fue eliminado de memoria, pero la DLL se mantiene porque otros plugins la usan.");
            return true;
        }

        return false;
    }

    public static List<string> GetPluginNames()
    {
        return _plugins.Select(p => p.Name).ToList();
    }

    public static void InstallPlugins(List<string> pluginPaths)
    {
        var pluginFiles = _pluginLookup.Values
            .Select(v => v.plugin.GetType().Assembly.Location)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var contextsByPath = _pluginLookup.Values
            .GroupBy(v => v.plugin.GetType().Assembly.Location, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToDictionary(g => g.Key, g => g.Select(x => x.context).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var pluginPath in pluginPaths)
            try
            {
                var destPath = Path.Combine(PathHelper.PluginsPath, Path.GetFileName(pluginPath));

                File.Copy(pluginPath, destPath, true);
                Console.WriteLine($"Plugin instalado {pluginPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al instalar el plugin {pluginPath}: {ex.Message}");
            }

        ReloadPlugins();
    }
}
