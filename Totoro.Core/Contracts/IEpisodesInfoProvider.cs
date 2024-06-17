using Totoro.Core.Services.Anizip;

namespace Totoro.Core.Contracts;


public interface IEpisodesInfoProvider
{
    IAsyncEnumerable<EpisodeInfo> GetEpisodeInfos(long id, string serviceType);
}
