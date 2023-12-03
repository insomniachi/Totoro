using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Services.Simkl;

internal class SimklAnimeService : IAnimeService
{
    private readonly ISimklClient _simklClient;
    private readonly IAnilistService _anilistService;

    public SimklAnimeService(ISimklClient simklClient,
                             IAnilistService anilistService)
    {
        _simklClient = simklClient;
        _anilistService = anilistService;
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
            var model = SimklToAnimeModelConverter.Convert(info);
            observer.OnNext(model);
            
            _anilistService
                .GetBannerImage(id)
                .ToObservable()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => model.BannerImage = x);
            
            observer.OnCompleted();
            return Disposable.Empty;
        });
    }

    public IObservable<IEnumerable<AnimeModel>> GetSeasonalAnime()
    {
        return Observable.Empty<IEnumerable<AnimeModel>>();
    }
}

// TODO: Can i add these properties to existing class itself ?
public record AnimeIdExtended : AnimeId
{
    public long? Simkl { get; set; }
    public long? LiveChart { get; set; }
}
