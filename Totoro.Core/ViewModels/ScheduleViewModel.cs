using System.Reactive.Concurrency;

namespace Totoro.Core.ViewModels;

public class ScheduleViewModel : NavigatableViewModel, IHaveState
{
    private readonly SourceCache<AnimeModel, long> _animeCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<AnimeModel> _anime;
    private readonly ITrackingServiceContext _trackingService;

    public ScheduleViewModel(ITrackingServiceContext trackingService)
    {
        _trackingService = trackingService;

        _animeCache
            .Connect()
            .RefCount()
            .AutoRefresh(x => x.NextEpisodeAt)
            .Sort(SortExpressionComparer<AnimeModel>.Ascending(x => x.NextEpisodeAt))
            .Bind(out _anime)
            .DisposeMany()
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);
    }

    [Reactive] public bool IsLoading { get; set; }
    public ReadOnlyObservableCollection<AnimeModel> Anime => _anime;
    public IEnumerable<AnimeModel> AllAnime => _animeCache.Items;

    public void RestoreState(IState state)
    {
        var anime = state.GetValue<IEnumerable<AnimeModel>>(nameof(Anime));
        _animeCache.Edit(x => x.AddOrUpdate(anime));
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(_animeCache.Items, nameof(Anime));
    }

    public Task SetInitialState()
    {
        _trackingService.GetCurrentlyAiringTrackedAnime()
                        .Finally(() => _animeCache.Refresh())
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(list => _animeCache.Edit(x => x.AddOrUpdate(list)));

        return Task.CompletedTask;
    }

    private void SetLoading(bool isLoading) => RxApp.MainThreadScheduler.Schedule(() => IsLoading = isLoading);
}
