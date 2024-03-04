using AnitomySharp;
using Splat;
using Totoro.Core.Services.Debrid;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Services.StreamResolvers;

public class DebridStreamModelResolver(IDebridServiceContext debridService,
                                       ISettings settings,
                                       string magnet) : IVideoStreamModelResolver, IEnableLogger
{
    private readonly IDebridServiceContext _debridService = debridService;
    private readonly ISettings _settings = settings;
    private readonly string _magnet = magnet;
    private List<DirectDownloadLink> _links;

    public async Task Populate()
    {
        _links = (await _debridService.GetDirectDownloadLinks(_magnet)).ToList();

        var options = new Options(title: false, extension: false, group: false);
        foreach (var item in _links)
        {
            var parsedResult = AnitomySharp.AnitomySharp.Parse(item.FileName, options);
            if (parsedResult.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementEpisodeNumber) is { } epString && int.TryParse(epString.Value, out var ep))
            {
                item.Episode = ep;
            }
        }
    }

    public Task<EpisodeModelCollection> ResolveAllEpisodes(StreamType streamType)
    {
        return Task.FromResult(EpisodeModelCollection.FromDirectDownloadLinks(_links, _settings.SkipFillers));
    }

    public Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, StreamType streamType)
    {
        var ddl = _links.FirstOrDefault(x => x.Episode == episode);
        return Task.FromResult(new VideoStreamsForEpisodeModel(ddl));
    }
}

