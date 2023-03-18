using Totoro.Core.Services;
using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Contracts;

public interface IVideoStreamResolverFactory
{
    IVideoStreamModelResolver CreateAnimDLResolver(string baseUrl);
    IVideoStreamModelResolver CreateTorrentStreamResolver(IEnumerable<DirectDownloadLink> links);
}

