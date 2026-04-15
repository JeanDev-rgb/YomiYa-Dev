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
/// <remarks>
///     La clase <see cref="PluginManager" /> es responsable de cargar dinámicamente los plugins desde un
///     directorio especificado, mantener una colección de plugins cargados y proporcionar métodos para recuperarlos o
///     descargarlos.
///     Se espera que los plugins sean ensamblados (.NET assemblies) que contengan tipos que implementen la interfaz
///     <see cref="ParsedHttpSource" />.
/// </remarks>
public class PluginManager
{
    /// <summary>
    ///     Representa una colección de fuentes HTTP procesadas para los plugins.
    /// </summary>
    /// <remarks>
    ///     Este campo almacena una lista de objetos <see cref="ParsedHttpSource" />, los cuales representan
    ///     datos procesados de fuentes HTTP relacionadas con los plugins. Está destinado para uso interno y no debe ser
    ///     accedido directamente fuera de la clase contenedora.
    /// </remarks>
    private static readonly List<ParsedHttpSource> _plugins = new();

    /// <summary>
    ///     Diccionario que optimiza la búsqueda de plugins por su nombre.
    /// </summary>
    /// <remarks>Este diccionario almacena los plugins cargados y permite obtenerlos rápidamente mediante su nombre.</remarks>
    private static readonly Dictionary<string, (ParsedHttpSource plugin, PluginLoadContext context)> _pluginLookup =
        new();


    /// <summary>
    ///     Inicializa una nueva instancia de la clase <see cref="PluginManager" /> y carga los plugins desde el directorio
    ///     especificado.
    /// </summary>
    /// <remarks>
    ///     El constructor invoca automáticamente el proceso de carga de plugins para el directorio especificado.
    ///     Asegúrese de que el directorio exista y contenga archivos de plugin válidos para evitar errores durante
    ///     la carga.
    /// </remarks>
    /// <param name="directory">La ruta al directorio que contiene los archivos de plugin. No debe ser nula ni vacía.</param>
    public PluginManager()
    {
        LoadPlugins();
    }

    /// <summary>
    ///     Carga los ensamblados de plugins desde el directorio especificado e inicializa instancias de los tipos que
    ///     implementan la
    ///     interfaz <see cref="ParsedHttpSource" />.
    /// </summary>
    /// <remarks>
    ///     Este método busca archivos `.dll` en el directorio especificado, los carga como ensamblados e identifica los tipos
    ///     que implementan la interfaz <see cref="ParsedHttpSource" />. Para cada tipo coincidente, se crea una instancia y se
    ///     agrega
    ///     a la colección interna de plugins. Si ocurre un error durante la carga de un plugin, el error se registra en la
    ///     consola
    ///     y el método continúa procesando otros plugins.
    /// </remarks>
    /// <param name="directory">
    ///     La ruta al directorio que contiene los ensamblados de plugins. Si el directorio no existe, será
    ///     creado.
    /// </param>
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
            try
            {
                var loadContext = new PluginLoadContext(pluginFile);
                var assembly = loadContext.LoadFromAssemblyPath(pluginFile);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(ParsedHttpSource).IsAssignableFrom(t) && !t.IsAbstract)
                    .ToList();

                foreach (var type in pluginTypes)
                {
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

    /// <summary>
    ///     Recupera un plugin por su nombre desde la colección de plugins disponibles.
    /// </summary>
    /// <param name="name">El nombre del plugin a recuperar. La comparación no distingue entre mayúsculas y minúsculas.</param>
    /// <returns>
    ///     La instancia de <see cref="ParsedHttpSource" /> que coincide con el nombre especificado, o <see langword="null" />
    ///     si
    ///     no se encuentra un plugin coincidente.
    /// </returns>
    public static ParsedHttpSource? GetPlugin(string name)
    {
        if (MangaService.SelectedPlugin is null) LoadPlugins();
        return _pluginLookup.TryGetValue(name, out var entry)
            ? entry.plugin
            : null;
    }

    /// <summary>
    ///     Recupera todos los plugins disponibles.
    /// </summary>
    /// <returns>
    ///     Una lista de objetos <see cref="ParsedHttpSource" /> que representan los plugins disponibles. La lista puede estar
    ///     vacía
    ///     si no hay plugins disponibles.
    /// </returns>
    public static List<ParsedHttpSource> GetAllPlugins()
    {
        ReloadPlugins();
        return _plugins;
    }

    /// <summary>
    ///     Descarga todos los plugins actualmente cargados y limpia la colección de plugins.
    /// </summary>
    /// <remarks>
    ///     Este método elimina todos los plugins de la colección interna y libera cualquier
    ///     recurso asociado. Después de llamar a este método, la colección de plugins estará vacía.
    /// </remarks>
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

        // Llamar Dispose si es necesario
        if (pluginEntry.plugin is IDisposable disposable)
            disposable.Dispose();

        _plugins.Remove(pluginEntry.plugin);
        _pluginLookup.Remove(pluginName);

        // Descargar el contexto
        pluginEntry.context.Unload();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Construir la ruta del archivo directamente
        var pluginFile = Path.Combine(PathHelper.PluginsPath, pluginName + ".dll");

        if (!File.Exists(pluginFile)) return false;
        try
        {
            File.Delete(pluginFile);
            Console.WriteLine($"Plugin {pluginName} eliminado.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo eliminar el plugin {pluginFile}: {ex.Message}");
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