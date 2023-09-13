using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Manga;
using Totoro.Plugins.Torrents.Contracts;

namespace Totoro.Core.Services;

public class PluginOptionsStorage
{
    private readonly IPluginOptionsStorage<AnimeProvider> _animePlugins;
    private readonly IPluginOptionsStorage<ITorrentTracker> _torrentPlugins;
    private readonly IPluginOptionsStorage<MangaProvider> _mangaPlugins;

    public PluginOptionsStorage(IPluginOptionsStorage<AnimeProvider> animePlugins,
                                IPluginOptionsStorage<ITorrentTracker> torrentPlugins,
                                IPluginOptionsStorage<MangaProvider> mangaPlugins)
    {
        _animePlugins = animePlugins;
        _torrentPlugins = torrentPlugins;
        _mangaPlugins = mangaPlugins;
    }

    public void Initialize()
    {
        _animePlugins.Initialize();
        _mangaPlugins.Initialize();
        _torrentPlugins.Initialize();
    }
}
