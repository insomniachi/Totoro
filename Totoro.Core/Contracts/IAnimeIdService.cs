namespace Totoro.Core.Contracts;

public interface IAnimeIdService
{
    Task<AnimeId> GetId(AnimeTrackerType serviceType, long id);
}
