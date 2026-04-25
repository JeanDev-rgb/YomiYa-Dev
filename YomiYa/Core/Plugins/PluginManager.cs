using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using YomiYa.Core.Exceptions;
using YomiYa.Core.IPC;
using YomiYa.Core.IO;
using YomiYa.Core.Services;
using YomiYa.Source.Online;

namespace YomiYa.Core.Plugins;

public class PluginManager
{
    private static readonly List<ParsedHttpSource> _plugins = new();
    private static readonly Dictionary<string, (ParsedHttpSource plugin, string exePath)> _pluginLookup = new();
    private static Task _initializationTask;
    public static event Action OnPluginsChanged;

    static PluginManager()
    {
        if (_initializationTask == null || _initializationTask.IsCompleted)
        {
            _initializationTask = LoadPluginsAsync();
        }
    }

    private static int GetAvailablePort()
    {
        TcpListener l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static async Task LoadPluginsAsync()
    {
        if (!Directory.Exists(PathHelper.PluginsPath))
        {
            Directory.CreateDirectory(PathHelper.PluginsPath);
            return;
        }

        _plugins.Clear();
        _pluginLookup.Clear();

        var pluginFiles = Directory.GetFiles(PathHelper.PluginsPath, "*.exe");

        foreach (var pluginPath in pluginFiles)
        {
            try
            {
                int port = GetAvailablePort();
                var server = new PluginTcpServer();
                server.Start(port);

                var processInfo = new ProcessStartInfo
                {
                    FileName = pluginPath,
                    Arguments = port.ToString(),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var process = Process.Start(processInfo);

                if (process == null) throw new Exception("No se pudo iniciar el proceso del plugin.");

                // Espera inteligente
                await server.WaitForConnectionAsync(5000);

                var response = await server.SendRequestAsync("GetMetadata");
                var metadata = response.GetPayload<JsonElement>();

                var proxy = new TcpPluginProxy(server, process, metadata);

                if (!_pluginLookup.ContainsKey(proxy.Name))
                {
                    _plugins.Add(proxy);
                    _pluginLookup[proxy.Name] = (proxy, pluginPath);
                    Console.WriteLine($"[PluginManager] Plugin cargado exitosamente: {proxy.Name} en puerto {port}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(new PluginLoadException($"Error al cargar el plugin EXE {pluginPath}: {ex.Message}",
                    ex));
            }
        }

        Dispatcher.UIThread.Post(() => { OnPluginsChanged?.Invoke(); });
    }

    public static ParsedHttpSource? GetPlugin(string name)
    {
        if (_initializationTask != null && !_initializationTask.IsCompleted)
        {
            Console.WriteLine("[PluginManager] La UI pidió un plugin pero aún se están conectando. Esperando...");
            _initializationTask.Wait();
        }

        if (!string.IsNullOrEmpty(name) && _pluginLookup.TryGetValue(name, out var entry))
        {
            return entry.plugin;
        }

        // Fallback seguro
        var fallbackPlugin = _plugins.FirstOrDefault();
        if (fallbackPlugin != null)
        {
            Console.WriteLine(
                $"[PluginManager] No se encontró '{name}', usando '{fallbackPlugin.Name}' como repuesto.");
            return fallbackPlugin;
        }

        Console.WriteLine("[PluginManager] ¡Peligro! No hay ningún plugin cargado en absoluto.");
        return null;
    }

    public static List<ParsedHttpSource> GetAllPlugins()
    {
        return _plugins;
    }

    private static void UnloadPlugins()
    {
        foreach (var plugin in _plugins)
        {
            if (plugin is IDisposable disposablePlugin)
            {
                disposablePlugin.Dispose();
            }
        }

        _plugins.Clear();
        _pluginLookup.Clear();
        Console.WriteLine("[PluginManager] Todos los plugins han sido descargados y los procesos terminados.");
    }

    public static void ReloadPlugins()
    {
        UnloadPlugins();
        _initializationTask = LoadPluginsAsync();
    }

    public static bool DeletePlugin(string pluginName)
    {
        if (!_pluginLookup.TryGetValue(pluginName, out var pluginEntry)) return false;

        if (pluginEntry.plugin is IDisposable disposable) disposable.Dispose();

        _plugins.Remove(pluginEntry.plugin);
        _pluginLookup.Remove(pluginName);

        try
        {
            System.Threading.Thread.Sleep(200);
            if (File.Exists(pluginEntry.exePath))
            {
                File.Delete(pluginEntry.exePath);
                Console.WriteLine($"[PluginManager] Ejecutable {Path.GetFileName(pluginEntry.exePath)} eliminado.");
            }

            Dispatcher.UIThread.Post(() => { OnPluginsChanged?.Invoke(); });
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PluginManager] No se pudo eliminar {pluginEntry.exePath}: {ex.Message}");
            return false;
        }
    }

    public static List<string> GetPluginNames()
    {
        return _plugins.Select(p => p.Name).ToList();
    }

    public static void InstallPlugins(List<string> pluginPaths)
    {
        // 1. Asegurarnos de que la carpeta existe ANTES de copiar
        if (!Directory.Exists(PathHelper.PluginsPath))
        {
            Directory.CreateDirectory(PathHelper.PluginsPath);
        }

        foreach (var pluginPath in pluginPaths)
        {
            try
            {
                var destPath = Path.Combine(PathHelper.PluginsPath, Path.GetFileName(pluginPath));
                File.Copy(pluginPath, destPath, true);
                Console.WriteLine($"[PluginManager] Plugin instalado: {destPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PluginManager] Error al instalar el plugin {pluginPath}: {ex.Message}");
            }
        }

        ReloadPlugins();
    }
}