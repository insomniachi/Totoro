using Totoro.Core.Services.Simkl;

namespace Totoro.Core.Contracts;

public interface IAnimeIdService
{
    ValueTask<AnimeIdExtended> GetId(ListServiceType from, ListServiceType to, long id);
    ValueTask<AnimeIdExtended> GetId(long id);
}
