using AnimDL.UI.Core.Models;

namespace AnimDL.UI.Core.Contracts;

public interface IAnimeService
{
    IObservable<IEnumerable<SeasonalAnimeModel>> GetSeasonalAnime();
    IObservable<AnimeModel> GetInformation(long id);
}
