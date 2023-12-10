using Totoro.Core.Services.Simkl;

namespace Totoro.Core.Contracts;

public interface IAnimeIdService
{
    ValueTask<AnimeIdExtended> GetId(ListServiceType serviceType, long id);
    ValueTask<AnimeIdExtended> GetId(long id);
}
