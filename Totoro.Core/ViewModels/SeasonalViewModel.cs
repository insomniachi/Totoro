using Totoro.Core.Helpers;

namespace Totoro.Core.ViewModels;

public class SeasonalViewModel : NavigatableViewModel, IHaveState
{
    private readonly IAnimeServiceContext _animeService;
    private readonly IViewService _viewService;
    private readonly SourceCache<AnimeModel, long> _animeCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<AnimeModel> _anime;

    public SeasonalViewModel(IAnimeServiceContext animeService,
                             IViewService viewService,
                             INavigationService navigationService)
    {
        _animeService = animeService;
        _viewService = viewService;

        SetSeasonCommand = ReactiveCommand.Create<string>(SwitchSeasonFilter);
        AddToListCommand = ReactiveCommand.CreateFromTask<AnimeModel>(AddToList);
        ItemClickedCommand = ReactiveCommand.Create<AnimeModel>(m => navigationService.NavigateTo<AboutAnimeViewModel>(parameter: new Dictionary<string, object> { ["Id"] = m.Id }));
        SetSortCommand = ReactiveCommand.Create<Sort>(s => Sort = s);

        var sort = this.WhenAnyValue(x => x.Sort)
            .Select(sort => sort switch
            {
                Sort.Popularity => animeService.Current == ListServiceType.AniList 
                    ? SortExpressionComparer<AnimeModel>.Descending(x => x.Popularity)
                    : SortExpressionComparer<AnimeModel>.Ascending(x => x.Popularity),
                Sort.Score => SortExpressionComparer<AnimeModel>.Descending(x => x.MeanScore ?? 0),
                _ => throw new NotSupportedException(),
            });

        _animeCache
            .Connect()
            .RefCount()
            .Filter(this.WhenAnyValue(x => x.Season).WhereNotNull().Select(FilterBySeason))
            .Filter(this.WhenAnyValue(x => x.SearchText).Select(x => x?.ToLower()).Select(FilterByTitle))
            .Sort(sort)
            .Page(PagerViewModel.AsPager())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(changes => PagerViewModel.Update(changes.Response))
            .Bind(out _anime)
            .DisposeMany()
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);

        this.WhenAnyValue(x => x.SeasonFilter).Subscribe(SwitchSeasonFilter);
    }

    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public Season Season { get; set; }
    [Reactive] public string SeasonFilter { get; set; } = "Current";
    [Reactive] public string SearchText { get; set; }
    [Reactive] public Sort Sort { get; set; } = Sort.Popularity;
    public PagerViewModel PagerViewModel { get; } = new PagerViewModel(0, 15);
    public ReadOnlyObservableCollection<AnimeModel> Anime => _anime;
    public ICommand SetSeasonCommand { get; }
    public ICommand AddToListCommand { get; }
    public ICommand ItemClickedCommand { get; }
    public ICommand SetSortCommand { get; }

    public Task SetInitialState()
    {
        IsLoading = true;

        _animeService.GetSeasonalAnime()
                     .ObserveOn(RxApp.MainThreadScheduler)
                     .Subscribe(list =>
                     {
                         _animeCache.Edit(x => x.AddOrUpdate(list));
                         IsLoading = false;
                     }, RxApp.DefaultExceptionHandler.OnError);

        Season = Current;

        return Task.CompletedTask;
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(_animeCache.Items, nameof(Anime));
        state.AddOrUpdate(SeasonFilter);
        state.AddOrUpdate(Sort);
    }

    public void RestoreState(IState state)
    {
        var anime = state.GetValue<IEnumerable<AnimeModel>>(nameof(Anime));
        SeasonFilter = state.GetValue<string>(nameof(SeasonFilter));
        Sort = state.GetValue<Sort>(nameof(Sort));
        _animeCache.Edit(x => x.AddOrUpdate(anime));
    }

    private void SwitchSeasonFilter(string filter)
    {
        Season = filter switch
        {
            "Current" => Current,
            "Previous" => Prev,
            "Next" => Next,
            _ => throw new InvalidOperationException()
        };
    }

    private async Task AddToList(AnimeModel a) => await _viewService.UpdateTracking(a);
    private static Func<AnimeModel, bool> FilterBySeason(Season s) => x => x.Season == s;
    private static Func<AnimeModel, bool> FilterByTitle(string title) => x => string.IsNullOrEmpty(title) ||
                                                                                      x.Title.ToLower().Contains(title) ||
                                                                                      (x.AlternativeTitles?.Any(x => x.ToLower().Contains(title)) ?? true);
    public static Season Current => AnimeHelpers.CurrentSeason();
    public static Season Next => AnimeHelpers.NextSeason();
    public static Season Prev => AnimeHelpers.PrevSeason();
    public void RefreshData() => _animeCache.Refresh();

}
