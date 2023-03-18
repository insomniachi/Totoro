using AnitomySharp;
using Splat;
using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Services.StreamResolvers;

public class TorrentStreamModelResolver : IVideoStreamModelResolver, IEnableLogger
{
    private readonly List<DirectDownloadLink> _links;

    public TorrentStreamModelResolver(IEnumerable<DirectDownloadLink> links)
    {
        _links = links.ToList();
    }

    public Task<int> GetNumberOfEpisodes(string subStream)
    {
        return Task.FromResult(_links.Count);
    }

    public Task<VideoStreamsForEpisodeModel> Resolve(int episode, string subStream)
    {
        var ddl = _links.FirstOrDefault(x => x.Episode == episode);
        return Task.FromResult(new VideoStreamsForEpisodeModel(ddl));
    }
}

