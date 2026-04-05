using System.Reflection;
using System.Runtime.Loader;

namespace YomiYa.Core.Plugins;

public class PluginLoadContext(string name) : AssemblyLoadContext(name, true)
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        return null; // Delega al contexto predeterminado
    }
}