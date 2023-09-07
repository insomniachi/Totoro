using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Contracts;

public interface IAnimeIdService
{
    Task<AnimeId> GetId(ListServiceType serviceType, long id);
    Task<AnimeId> GetId(long id);
}
