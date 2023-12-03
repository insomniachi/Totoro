namespace Totoro.Core.Services.Simkl;

internal class SimklService : IAnimeService
{
    private readonly ISimklClient _simklClient;

    public SimklService(ISimklClient simklClient)
    {
        _simklClient = simklClient;
    }

    public ListServiceType Type => ListServiceType.Simkl;

    public IObservable<IEnumerable<AnimeModel>> GetAiringAnime()
    {
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            var result = await _simklClient.GetAiringAnime();
            observer.OnNext(result.Select(SimklToAnimeModelConverter.Convert));
            observer.OnCompleted();
            return Disposable.Empty;
        });
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime(string name)
    {
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            var result = await _simklClient.Search(name, ItemType.Anime);
            observer.OnNext(result.Select(SimklToAnimeModelConverter.Convert));
            observer.OnCompleted();
            return Disposable.Empty;
        });
    }

    public IObservable<AnimeModel> GetInformation(long id)
    {
        return Observable.Create<AnimeModel>(async observer =>
        {
            var info = await _simklClient.GetSummary(id);
            observer.OnNext(SimklToAnimeModelConverter.Convert(info));
            observer.OnCompleted();
            return Disposable.Empty;
        });
    }

    public IObservable<IEnumerable<AnimeModel>> GetSeasonalAnime()
    {
        return Observable.Empty<IEnumerable<AnimeModel>>();
    }
}
