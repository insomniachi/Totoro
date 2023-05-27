using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Torrents.Contracts;

namespace Totoro.Core.Services;

public class PluginOptionsStorage
{
    private readonly IPluginOptionsStorage<AnimeProvider> _animePlugins;
    private readonly IPluginOptionsStorage<ITorrentTracker> _torrentPlugins;

    public PluginOptionsStorage(IPluginOptionsStorage<AnimeProvider> animePlugins,
                                IPluginOptionsStorage<ITorrentTracker> torrentPlugins)
    {
        _animePlugins = animePlugins;
        _torrentPlugins = torrentPlugins;
    }

    public void Initialize()
    {
        _animePlugins.Initialize();
        _torrentPlugins.Initialize();
    }
}
