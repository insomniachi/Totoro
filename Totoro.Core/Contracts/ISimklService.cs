using Totoro.Core.Services.Simkl;

namespace Totoro.Core.Contracts;

public interface ISimklService
{
    Task<AnimeIdExtended> GetId(ListServiceType type, long id);
    Task<IEnumerable<EpisodeModel>> GetEpisodes(long id);
}
