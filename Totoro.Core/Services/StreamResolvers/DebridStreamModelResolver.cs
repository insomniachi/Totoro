using AnitomySharp;
using MalApi;
using MonoTorrent;
using Splat;
using Totoro.Core.Contracts;
using Totoro.Core.Models;
using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Services.StreamResolvers;

public class DebridStreamModelResolver : IVideoStreamModelResolver, IEnableLogger
{
    private readonly IDebridServiceContext _debridService;
    private readonly string _magnet;
    private IEnumerable<DirectDownloadLink> _links;

    public DebridStreamModelResolver(IDebridServiceContext debridService,
                                     string magnet)
    {
        _debridService = debridService;
        _magnet = magnet;
    }

    public async Task Populate()
    {
        _links = await _debridService.GetDirectDownloadLinks(_magnet);

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

    public Task<EpisodeModelCollection> ResolveAllEpisodes(string subStream)
    {
        return Task.FromResult(EpisodeModelCollection.FromDirectDownloadLinks(_links));
    }

    public Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, string subStream)
    {
        var ddl = _links.FirstOrDefault(x => x.Episode == episode);
        return Task.FromResult(new VideoStreamsForEpisodeModel(ddl));
    }
}

