namespace Totoro.Core.ViewModels;


public class UserListViewModel : NavigatableViewModel, IHaveState
{
    private readonly ITrackingService _trackingService;
    private readonly INavigationService _navigationService;
    private readonly IViewService _viewService;
    private readonly SourceCache<AnimeModel, long> _animeCache = new(x => x.Id);
    private readonly SourceCache<SearchResultModel, long> _searchCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<AnimeModel> _anime;
    private readonly ReadOnlyObservableCollection<SearchResultModel> _searchResults;

    public UserListViewModel(ITrackingService trackingService,
                             INavigationService navigationService,
                             IAnimeService animeService,
                             IViewService viewService)
    {
        _trackingService = trackingService;
        _navigationService = navigationService;
        _viewService = viewService;
        ItemClickedCommand = ReactiveCommand.Create<AnimeModel>(OnItemClicked);
        ChangeCurrentViewCommand = ReactiveCommand.Create<AnimeStatus>(x => CurrentView = x);
        RefreshCommand = ReactiveCommand.CreateFromTask(SetInitialState);
        SetDisplayMode = ReactiveCommand.Create<DisplayMode>(x => Mode = x);

        _animeCache
            .Connect()
            .RefCount()
            .Filter(this.WhenAnyValue(x => x.CurrentView).Select(FilterByStatusPredicate))
            .Filter(this.WhenAnyValue(x => x.SearchText).Select(x => x?.ToLower()).Select(FilterByTitle))
            .Sort(SortExpressionComparer<AnimeModel>.Descending(x => x.MeanScore))
            .Bind(out _anime)
            .DisposeMany()
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);

        _searchCache
            .Connect()
            .RefCount()
            .Bind(out _searchResults)
            .DisposeMany()
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);

        this.WhenAnyValue(x => x.QuickAddSearchText)
            .Where(text => text is { Length: > 3 })
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(text => animeService.GetAnime(text))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(list => _searchCache.EditDiff(list, (first, second) => first.Id == second.Id));
    }

    [Reactive] public AnimeStatus CurrentView { get; set; } = AnimeStatus.Watching;
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public DisplayMode Mode { get; set; } = DisplayMode.Grid;
    [Reactive] public string SearchText { get; set; }
    [Reactive] public string QuickAddSearchText { get; set; }
    public ReadOnlyObservableCollection<SearchResultModel> QuickSearchResults => _searchResults;
    public ReadOnlyObservableCollection<AnimeModel> Anime => _anime;
    public ICommand ItemClickedCommand { get; }
    public ICommand ChangeCurrentViewCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SetDisplayMode { get; }

    private void OnItemClicked(AnimeModel anime)
    {
        _navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object> { ["Anime"] = anime });
    }

    private Func<AnimeModel, bool> FilterByStatusPredicate(AnimeStatus status) => x => x.Tracking.Status == status;
    private static Func<AnimeModel, bool> FilterByTitle(string title) => x => string.IsNullOrEmpty(title) ||
                                                                                  x.Title.ToLower().Contains(title) ||
                                                                                  (x.AlternativeTitles?.Any(x => x.ToLower().Contains(title)) ?? true);

    public Task SetInitialState()
    {
        IsLoading = true;

        _animeCache.Clear();
        _trackingService.GetAnime()
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(list =>
                        {
                            _animeCache.EditDiff(list, (item1, item2) => item1.Id == item2.Id);
                            IsLoading = false;
                        })
                        .DisposeWith(Garbage);

        return Task.CompletedTask;
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(_animeCache.Items, nameof(Anime));
        state.AddOrUpdate(CurrentView);
    }

    public void RestoreState(IState state)
    {
        var anime = state.GetValue<IEnumerable<AnimeModel>>(nameof(Anime));
        _animeCache.Edit(x => x.AddOrUpdate(anime));
        CurrentView = state.GetValue<AnimeStatus>(nameof(CurrentView));
    }

    public async Task<Unit> UpdateAnime(IAnimeModel model) => await _viewService.UpdateTracking(model);

    public void ClearSearch()
    {
        QuickAddSearchText = string.Empty;
        _searchCache.Clear();
    }
}
