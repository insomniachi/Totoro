using Splat;

namespace Totoro.Core.Services.StreamResolvers;

public class AnimDLVideoStreamResolver : IVideoStreamModelResolver, IEnableLogger
{
    private readonly IProvider _provider;
    private readonly ISettings _settings;
    private readonly string _baseUrlSub;
    private readonly string _baseUrlDub;

    public AnimDLVideoStreamResolver(IProvider provider,
                                     ISettings settings,
                                     string baseUrlSub)
    {

        _provider = provider;
        _settings = settings;
        _baseUrlSub = baseUrlSub;
    }

    public AnimDLVideoStreamResolver(IProvider provider,
                                     ISettings settings,
                                     string baseUrlSub,
                                     string baseUrlDub)
    {

        _provider = provider;
        _settings = settings;
        _baseUrlSub = baseUrlSub;
        _baseUrlDub = baseUrlDub;
    }

    public async Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, string subStream)
    {
        this.Log().Debug("resolving stream link for episode {0}", episode);

        var results = await GetStreams(episode, subStream);

        if (!results.Any())
        {
            this.Log().Debug("No streams found");
            return null;
        }

        return new VideoStreamsForEpisodeModel(results.First());
    }

    private async Task<List<VideoStreamsForEpisode>> GetStreams(int episode, string subStream)
    {
        try
        {
            var url = _baseUrlSub;
            if (_settings.DefaultProviderType == "gogo")
            {
                url = subStream == "Dub" ? _baseUrlDub : _baseUrlSub;
            }

            return _provider?.StreamProvider switch
            {
                IMultiAudioStreamProvider mp => await mp.GetStreams(url, episode..episode, subStream).ToListAsync(),
                IStreamProvider sp => await sp.GetStreams(url, episode..episode).ToListAsync(),
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
            var url = _baseUrlSub;
            if (_settings.DefaultProviderType == "gogo")
            {
                url = subStream == "Dub" ? _baseUrlDub : _baseUrlSub;
            }

            var count = _provider?.StreamProvider switch
            {
                IMultiAudioStreamProvider mp => await mp.GetNumberOfStreams(url, subStream),
                IStreamProvider sp => await sp.GetNumberOfStreams(url),
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

