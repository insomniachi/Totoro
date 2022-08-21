namespace AnimDL.UI.Core.ViewModels;


public class UserListViewModel : NavigatableViewModel, IHaveState
{
    private readonly ITrackingService _trackingService;
    private readonly INavigationService _navigationService;
    private readonly SourceCache<AnimeModel, long> _animeCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<AnimeModel> _anime;

    public UserListViewModel(ITrackingService trackingService,
                             INavigationService navigationService)
    {
        _trackingService = trackingService;
        _navigationService = navigationService;
        ItemClickedCommand = ReactiveCommand.Create<AnimeModel>(OnItemClicked);
        ChangeCurrentViewCommand = ReactiveCommand.Create<AnimeStatus>(x => CurrentView = x);
        RefreshCommand = ReactiveCommand.CreateFromTask(SetInitialState);
        SetDisplayMode = ReactiveCommand.Create<DisplayMode>(x => Mode = x);

        _animeCache
            .Connect()
            .RefCount()
            .Filter(this.WhenAnyValue(x => x.CurrentView).Select(FilterByStatusPredicate))
            .Sort(SortExpressionComparer<AnimeModel>.Descending(x => x.MeanScore))
            .Bind(out _anime)
            .DisposeMany()
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);
    }

    [Reactive] public AnimeStatus CurrentView { get; set; } = AnimeStatus.Watching;
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public DisplayMode Mode { get; set; } = DisplayMode.Grid;

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

    public Task SetInitialState()
    {
        _animeCache.Clear();

        _trackingService.GetAnime()
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(list => _animeCache.EditDiff(list, (item1, item2) => item1.Id == item2.Id));

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
}
