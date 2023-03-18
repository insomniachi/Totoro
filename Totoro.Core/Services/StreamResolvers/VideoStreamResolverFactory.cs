using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Services.StreamResolvers;

public class VideoStreamResolverFactory : IVideoStreamResolverFactory
{
    private readonly IProviderFactory _providerFactory;
    private readonly ISettings _settings;

    public VideoStreamResolverFactory(IProviderFactory providerFactory,
                                      ISettings settings)
    {
        _providerFactory = providerFactory;
        _settings = settings;
    }

    public IVideoStreamModelResolver CreateAnimDLResolver(string baseUrl)
    {
        return new AnimDLVideoStreamResolver(_providerFactory, _settings, baseUrl);
    }

    public IVideoStreamModelResolver CreateTorrentStreamResolver(IEnumerable<DirectDownloadLink> links)
    {
        return new TorrentStreamModelResolver(links);
    }
}

