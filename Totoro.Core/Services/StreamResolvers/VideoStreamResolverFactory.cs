using Anitomy;
using MonoTorrent;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;

namespace Totoro.Core.Services.StreamResolvers;

public class VideoStreamResolverFactory(IPluginFactory<AnimeProvider> providerFactory,
                                        ISettings settings,
                                        IDebridServiceContext debridService,
                                        ITorrentEngine torrentEngine) : IVideoStreamResolverFactory
{
    private readonly IPluginFactory<AnimeProvider> _providerFactory = providerFactory;
    private readonly ISettings _settings = settings;
    private readonly IDebridServiceContext _debridService = debridService;
    private readonly ITorrentEngine _torrentEngine = torrentEngine;

    public IVideoStreamModelResolver CreateAnimDLResolver(string providerType, string baseUrl)
    {
        return new AnimDLVideoStreamResolver(_providerFactory.CreatePlugin(providerType), _settings, baseUrl);
    }

    public IVideoStreamModelResolver CreateSubDubResolver(string providerType, string baseUrlSub, string baseUrlDub)
    {
        return new AnimDLVideoStreamResolver(_providerFactory.CreatePlugin(providerType), _settings, baseUrlSub, baseUrlDub);
    }

    public async Task<IVideoStreamModelResolver> CreateDebridStreamResolver(string magnet)
    {
        var resolver = new DebridStreamModelResolver(_debridService, _settings, magnet);
        await resolver.Populate();
        return resolver;
    }

    public IVideoStreamModelResolver CreateMonoTorrentStreamResolver(IEnumerable<Element> parsedResults, string magnet)
    {
        return new MonoTorrentStreamModelResolver(_torrentEngine, parsedResults, _settings, magnet);
    }

    public IVideoStreamModelResolver CreateMonoTorrentStreamResolver(Torrent torrent)
    {
        return new MonoTorrentStreamModelResolver(_torrentEngine, _settings, torrent);
    }

    public IVideoStreamModelResolver CreateLocalStreamResolver(string directory)
    {
        return new FileSystemStreamResolver(directory, _settings);
    }
}

