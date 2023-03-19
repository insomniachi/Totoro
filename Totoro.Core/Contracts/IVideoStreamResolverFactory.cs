using AnitomySharp;
using Totoro.Core.Services;
using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Contracts;

public interface IVideoStreamResolverFactory
{
    IVideoStreamModelResolver CreateAnimDLResolver(string baseUrl);
    Task<IVideoStreamModelResolver> CreateDebridStreamResolver(string magnet);
    IVideoStreamModelResolver CreateWebTorrentStreamResolver(IEnumerable<Element> parsedResults, string magnet);
}

