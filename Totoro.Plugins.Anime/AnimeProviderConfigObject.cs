using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime;

public abstract class AnimeProviderConfigObject : ConfigObject
{
    protected override object? GetValue(PluginOptions options, string name, Type t, object? defaultValue)
    {
        if(t == typeof(StreamType))
        {
            return options.GetStreamType(name, (StreamType)defaultValue!);
        }

        return base.GetValue(options, name, t, defaultValue);
    }
}
