using Splat;

namespace Totoro.Core.Services.StreamResolvers;

public class AnimDLVideoStreamResolver : IVideoStreamModelResolver, IEnableLogger
{
    private readonly IProvider _provider;
    private readonly string _baseUrl;

    public AnimDLVideoStreamResolver(IProviderFactory providerFactory,
                                     ISettings settings,
                                     string baseUrl)
    {

        _provider = providerFactory.GetProvider(settings.DefaultProviderType);
        _baseUrl = baseUrl;
    }

    public async Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, string subStream)
    {
        var results = await GetStreams(episode, subStream);

        if (!results.Any())
        {
            return null;
        }

        return new VideoStreamsForEpisodeModel(results.First());
    }

    private async Task<List<VideoStreamsForEpisode>> GetStreams(int episode, string subStream)
    {
        try
        {
            return _provider?.StreamProvider switch
            {
                IMultiAudioStreamProvider mp => await mp.GetStreams(_baseUrl, episode..episode, subStream).ToListAsync(),
                IStreamProvider sp => await sp.GetStreams(_baseUrl, episode..episode).ToListAsync(),
                _ => new List<VideoStreamsForEpisode>()
            };
        }
        catch (Exception ex)
        {
            this.Log().Fatal(ex);
            return new List<VideoStreamsForEpisode>();
        }
    }

    public async Task<int> GetNumberOfEpisodes(string subStream)
    {
        try
        {
            var count = _provider?.StreamProvider switch
            {
                IMultiAudioStreamProvider mp => await mp.GetNumberOfStreams(_baseUrl, subStream),
                IStreamProvider sp => await sp.GetNumberOfStreams(_baseUrl),
                _ => 0
            };

            return count;
        }
        catch (Exception ex)
        {
            this.Log().Fatal(ex);
            return 0;
        }
    }

    public async Task<EpisodeModelCollection> ResolveAllEpisodes(string subStream)
    {
        return EpisodeModelCollection.FromEpisodeCount(await GetNumberOfEpisodes(subStream));
    }
}

