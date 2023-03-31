using AnitomySharp;

namespace Totoro.Core.Contracts;

public interface IVideoStreamResolverFactory
{
    IVideoStreamModelResolver CreateAnimDLResolver(string baseUrl);
    Task<IVideoStreamModelResolver> CreateDebridStreamResolver(string magnet);
    IVideoStreamModelResolver CreateWebTorrentStreamResolver(IEnumerable<Element> parsedResults, string magnet);
    IVideoStreamModelResolver CreateGogoAnimDLResolver(string baseUrlSub, string baseUrlDub);
    IVideoStreamModelResolver CreateMonoTorrentStreamResolver(IEnumerable<Element> parsedResults, string magnet);
}

