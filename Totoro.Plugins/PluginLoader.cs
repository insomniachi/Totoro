using System.Runtime.Loader;
using Totoro.Plugins.Contracts;

namespace Totoro.Plugins;

public class PluginLoader
{
    private readonly List<AssemblyLoadContext> _assemblyLoadContexts = new();
    private readonly Dictionary<string, IPlugin> _plugins = new();

    public void LoadPlugins(string folder)
    {
        var files = Directory.GetFiles(folder, "*.dll");
        foreach (var dll in files)
        {
            var context = new AssemblyLoadContext(Path.GetFullPath(dll), true);

            var assembly = context.LoadFromAssemblyPath(dll);
            var plugins = assembly.GetExportedTypes().Where(x => x.IsAssignableTo(typeof(IPlugin))).ToList();

            if (plugins.Count == 0)
            {
                context.Unload();
                continue;
            }

            foreach (var type in plugins)
            {
                if (!type.IsAssignableTo(typeof(IPlugin)) || !(type.FullName is { } pluginType))
                {
                    continue;
                }

                if (assembly.CreateInstance(pluginType) is not IPlugin plugIn)
                {
                    continue;
                }

                _plugins.Add(plugIn.GetType().Assembly.GetName().Name!, plugIn);
            }

            _assemblyLoadContexts.Add(context);
        }
    }
}
