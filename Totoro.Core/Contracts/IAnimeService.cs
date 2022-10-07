namespace Totoro.Core.Contracts;

public interface IAnimeService
{
    IObservable<IEnumerable<SeasonalAnimeModel>> GetSeasonalAnime();
    IObservable<FullAnimeModel> GetInformation(long id);
    IObservable<IEnumerable<SearchResultModel>> GetAnime(string name);
    IObservable<IEnumerable<AnimeModel>> GetAiringAnime();
}
