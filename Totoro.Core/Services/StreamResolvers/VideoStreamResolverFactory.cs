using AnitomySharp;

namespace Totoro.Core.Services.StreamResolvers;

public class VideoStreamResolverFactory : IVideoStreamResolverFactory
{
    private readonly IProviderFactory _providerFactory;
    private readonly ISettings _settings;
    private readonly IDebridServiceContext _debridService;

    public VideoStreamResolverFactory(IProviderFactory providerFactory,
                                      ISettings settings,
                                      IDebridServiceContext debridService)
    {
        _providerFactory = providerFactory;
        _settings = settings;
        _debridService = debridService;
    }

    public IVideoStreamModelResolver CreateAnimDLResolver(string baseUrl)
    {
        return new AnimDLVideoStreamResolver(_providerFactory, _settings, baseUrl);
    }

    public async Task<IVideoStreamModelResolver> CreateDebridStreamResolver(string magnet)
    {
        var resolver = new DebridStreamModelResolver(_debridService, magnet);
        await resolver.Populate();
        return resolver;
    }

    public IVideoStreamModelResolver CreateWebTorrentStreamResolver(IEnumerable<Element> parsedResults, string magnet)
    {
        return new WebTorrentStreamModelResolver(parsedResults, magnet);
    }
}

