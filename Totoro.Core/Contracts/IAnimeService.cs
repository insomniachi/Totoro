namespace Totoro.Core.Contracts;

public interface IAnimeService
{
    ListServiceType Type { get; }
    IObservable<IEnumerable<AnimeModel>> GetSeasonalAnime();
    IObservable<AnimeModel> GetInformation(long id);
    IObservable<IEnumerable<AnimeModel>> GetAnime(string name);
    IObservable<IEnumerable<AnimeModel>> GetAiringAnime();
}

public interface IAnimeServiceContext
{
    IObservable<IEnumerable<AnimeModel>> GetSeasonalAnime();
    IObservable<AnimeModel> GetInformation(long id);
    IObservable<IEnumerable<AnimeModel>> GetAnime(string name);
    IObservable<IEnumerable<AnimeModel>> GetAiringAnime();
    ListServiceType? Current { get; }
}
