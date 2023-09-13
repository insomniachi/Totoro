using System.Runtime.Loader;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins;

public class PluginFactory<T> : IPluginFactory<T>
{
    class Plugin
    {
        required public PluginInfo Info { get; init; }
        required public IPlugin<T> Module { get; init; }
        required public Lazy<T> Instance { get; init; }
    }

    private readonly List<AssemblyLoadContext> _assemblyLoadContexts = new();
    private readonly List<Plugin> _plugins = new();

    public static PluginFactory<T> Instance { get; } = new();
    public IEnumerable<PluginInfo> Plugins => _plugins.Select(x => x.Info).OrderBy(x => x.DisplayName);

    object? IPluginFactory.CreatePlugin(string name) => CreatePlugin(name);

    public T? CreatePlugin(string name)
    {
        if (_plugins.FirstOrDefault(x => x.Info.Name == name) is not { } plugin)
        {
            return default;
        }

        return plugin.Instance.Value;
    }

    public bool SetOptions(string name, PluginOptions options)
    {
        if (_plugins.FirstOrDefault(x => x.Info.Name == name) is not { } plugin)
        {
            return false;
        }

        plugin.Module.SetOptions(options);
        return true;
    }

    public PluginOptions? GetOptions(string name)
    {
        if (_plugins.FirstOrDefault(x => x.Info.Name == name) is not { } plugin)
        {
            return default;
        }

        return plugin.Module.GetOptions();
    }

    public void LoadPlugin(IPlugin<T> plugIn)
    {
        _plugins.Add(new Plugin
        {
            Info = plugIn.GetInfo(),
            Module = plugIn,
            Instance = new(plugIn.Create)
        });
    }

    public void LoadPlugins(string folder)
    {
        var files = Directory.GetFiles(folder, "*.dll");
        foreach (var dll in files)
        {
            var context = new AssemblyLoadContext(Path.GetFullPath(dll), true);

            var assembly = context.LoadFromAssemblyPath(dll);
            var plugins = assembly.GetExportedTypes().Where(x => x.IsAssignableTo(typeof(IPlugin<T>)) && !x.IsAbstract).ToList();

            if (plugins.Count == 0)
            {
                context.Unload();
                continue;
            }

            foreach (var type in plugins)
            {
                if (!type.IsAssignableTo(typeof(IPlugin<T>)) || !(type.FullName is { } pluginType))
                {
                    continue;
                }

                if (assembly.CreateInstance(pluginType) is not IPlugin<T> plugIn)
                {
                    continue;
                }

                var pluginInfo = plugIn.GetInfo();

                if(_plugins.FirstOrDefault(x => x.Info.Name == pluginInfo.Name) is { })
                {
                    continue;
                }

                _plugins.Add(new Plugin
                {
                    Info = pluginInfo,
                    Module = plugIn,
                    Instance = new(plugIn.Create) 
                });
            }

            _assemblyLoadContexts.Add(context);
        }
    }
}
