using Totoro.Plugins.Options;

namespace Totoro.Plugins.Contracts;

public interface IPluginManager
{
    Task Initialize(string folder);
    ValueTask<PluginIndex?> GetAllPlugins();
}

public interface IPluginFactory
{
    void LoadPlugin(string file);
    void LoadPlugins(string folder);
    object? CreatePlugin(string name);
    bool SetOptions(string name, PluginOptions options);
    PluginOptions? GetCurrentConfig(string name);
}

public interface IPluginFactory<T> : IPluginFactory
{
    new T? CreatePlugin(string name);
    void LoadPlugin(IPlugin<T> plugIn);
}