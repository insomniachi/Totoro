namespace AnimDL.UI.Core.ViewModels;

public class DiscoverViewModel : NavigatableViewModel, IHaveState
{
    private readonly IRecentEpisodesProvider _recentEpisodesProvider;
    private readonly IFeaturedAnimeProvider _featuredAnimeProvider;

    public DiscoverViewModel(IRecentEpisodesProvider recentEpisodesProvider,
                             IFeaturedAnimeProvider featuredAnimeProvider)
    {
        Observable
            .Timer(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Where(_ => Featured is not null)
            .Subscribe(_ =>
            {
                if (Featured.Count == 0)
                {
                    SelectedIndex = 0;
                    return;
                }

                if (SelectedIndex == Featured.Count - 1)
                {
                    SelectedIndex = 0;
                    return;
                }

                SelectedIndex++;
            });

        _recentEpisodesProvider = recentEpisodesProvider;
        _featuredAnimeProvider = featuredAnimeProvider;
    }

    [Reactive] public IList<FeaturedAnime> Featured { get; set; } = new List<FeaturedAnime>();
    [Reactive] public IList<AiredEpisode> Episodes { get; set; } = new List<AiredEpisode>();
    [Reactive] public int SelectedIndex { get; set; }

    public void RestoreState(IState state)
    {
        Featured = state.GetValue<IList<FeaturedAnime>>(nameof(Featured));
        Episodes = state.GetValue<IList<AiredEpisode>>(nameof(Episodes));
    }

    public Task SetInitialState()
    {
        _featuredAnimeProvider.GetFeaturedAnime()
                              .ObserveOn(RxApp.MainThreadScheduler)
                              .Subscribe(featured => Featured = featured.ToList());

        _recentEpisodesProvider.GetRecentlyAiredEpisodes()
                               .ObserveOn(RxApp.MainThreadScheduler)
                               .Subscribe(eps => Episodes = eps.ToList());

        return Task.CompletedTask;
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(Featured);
        state.AddOrUpdate(Episodes);
    }
}