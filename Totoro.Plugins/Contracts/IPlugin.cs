using Totoro.Plugins.Options;

namespace Totoro.Plugins.Contracts;

public interface IPlugin
{
    object Create();
    void SetConfig(PluginOptions options);
    PluginOptions GetCurrentConfig();
    PluginOptions GetDefaultConfig();
    PluginInfo GetInfo();
}

public interface IPlugin<T> : IPlugin
{
    new T Create();
}


public abstract class Plugin<TObject, TConfig> : IPlugin<TObject>
    where TConfig : ConfigObject, new()
{
    public abstract TObject Create();

    public PluginOptions GetCurrentConfig() => ConfigManager<TConfig>.Current.ToPluginOptions();

    public PluginOptions GetDefaultConfig() => ConfigManager<TConfig>.Default.ToPluginOptions();

    public abstract PluginInfo GetInfo();

    public void SetConfig(PluginOptions options) => ConfigManager<TConfig>.Current.UpdateValues(options);

    object IPlugin.Create() => Create()!;
}