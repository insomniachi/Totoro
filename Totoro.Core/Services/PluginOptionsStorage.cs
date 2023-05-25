using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Core.Services;

public class PluginOptionsStorage
{
    private readonly IPluginOptionsStorage<AnimeProvider> _animePlugins;

    public PluginOptionsStorage(IPluginOptionsStorage<AnimeProvider> animePlugins)
    {
        _animePlugins = animePlugins;
    }

    public void Initialize()
    {
        _animePlugins.Initialize();
    }
}
