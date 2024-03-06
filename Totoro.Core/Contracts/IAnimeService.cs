namespace Totoro.Core.Contracts;

public interface IAnimeService
{
    ListServiceType Type { get; }
    IAsyncEnumerable<AnimeModel> GetSeasonalAnime();
    Task<AnimeModel> GetInformation(long id);
    IAsyncEnumerable<AnimeModel> GetAnime(string name);
    IAsyncEnumerable<AnimeModel> GetAiringAnime();
}

public interface IAnimeServiceContext
{
    IAsyncEnumerable<AnimeModel> GetSeasonalAnime();
    Task<AnimeModel> GetInformation(long id);
    IAsyncEnumerable<AnimeModel> GetAnime(string name);
    IAsyncEnumerable<AnimeModel> GetAiringAnime();
    ListServiceType Current { get; }
}
