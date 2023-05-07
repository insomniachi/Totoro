using AnitomySharp;
using MonoTorrent;

namespace Totoro.Core.Contracts;

public interface IVideoStreamResolverFactory
{
    IVideoStreamModelResolver CreateAnimDLResolver(string providerType, string baseUrl);
    IVideoStreamModelResolver CreateGogoAnimDLResolver(string providerType, string baseUrlSub, string baseUrlDub);
    Task<IVideoStreamModelResolver> CreateDebridStreamResolver(string magnet);
    IVideoStreamModelResolver CreateWebTorrentStreamResolver(IEnumerable<Element> parsedResults, string magnet);
    IVideoStreamModelResolver CreateMonoTorrentStreamResolver(IEnumerable<Element> parsedResults, string magnet);
    IVideoStreamModelResolver CreateMonoTorrentStreamResolver(Torrent torrent);
    IVideoStreamModelResolver CreateLocalStreamResolver(string directory);
}

