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

    private static readonly Dictionary<string, (ParsedHttpSource plugin, PluginLoadContext context)> _pluginLookup =
        new();

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

        var pluginFiles = Directory.GetFiles(PathHelper.PluginsPath, "*.dll");

        foreach (var pluginFile in pluginFiles)
        {
            try
            {
                var loadContext = new PluginLoadContext(pluginFile);
                var assembly = loadContext.LoadFromAssemblyPath(pluginFile);

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
                    _pluginLookup[pluginInstance.Name] = (pluginInstance, loadContext);
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
        foreach (var plugin in _plugins)
            if (plugin is IDisposable disposablePlugin)
                disposablePlugin.Dispose();

        _plugins.Clear();
        _pluginLookup.Clear();
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
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            var assemblyLocation = pluginEntry.plugin.GetType().Assembly.Location;
            if (string.IsNullOrEmpty(assemblyLocation)) 
            {
                assemblyLocation = Path.Combine(PathHelper.PluginsPath, pluginName + ".dll");
            }

            if (!File.Exists(assemblyLocation)) return false;
            try
            {
                File.Delete(assemblyLocation);
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
        foreach (var pluginPath in pluginPaths)
            try
            {
                var destPath = Path.Combine(PathHelper.PluginsPath, Path.GetFileName(pluginPath));
                File.Copy(pluginPath, destPath, true);
                Console.WriteLine($"Plugin instalado {pluginPath}");
                LoadPlugins();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al instalar el plugin {pluginPath}: {ex.Message}");
            }
    }
}