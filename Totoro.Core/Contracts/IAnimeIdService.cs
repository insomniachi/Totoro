using Totoro.Core.Services.Simkl;

namespace Totoro.Core.Contracts;

public interface IAnimeIdService
{
    Task<AnimeIdExtended> GetId(ListServiceType serviceType, long id);
    Task<AnimeIdExtended> GetId(long id);
    Task UpdateOfflineMappings();
}
