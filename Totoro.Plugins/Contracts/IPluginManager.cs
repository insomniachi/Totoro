using System.Runtime.CompilerServices;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Contracts;

public interface IPluginManager
{
    Task Initialize(string folder);
}

public interface IPluginFactory
{
    void LoadPlugins(string folder);
    object? CreatePlugin(string name);
    bool SetOptions(string name, PluginOptions options);
    PluginOptions? GetOptions(string name);
}

public interface IPluginFactory<T> : IPluginFactory
{
    new T? CreatePlugin(string name);
    void LoadPlugin(IPlugin<T> plugIn);
}