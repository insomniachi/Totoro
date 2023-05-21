using Totoro.Plugins.Options;

namespace Totoro.Plugins.Contracts;

public interface IPlugin
{
    object Create();
    void SetOptions(PluginOptions options);
    PluginOptions GetOptions();
}
