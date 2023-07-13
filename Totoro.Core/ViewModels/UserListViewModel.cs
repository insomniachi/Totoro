using System.Linq;

namespace Totoro.Core.ViewModels;


public class AnimeCollectionFilter : ReactiveObject
{
    [Reactive] public AnimeStatus ListStatus { get; set; } = AnimeStatus.Watching;
    [Reactive] public string SearchText { get; set; }
    [Reactive] public int? Year { get; set; }
    [Reactive] public AiringStatus? AiringStatus { get; set; }
    public ObservableCollection<string> Genres { get; } = new();

    public bool IsVisible(AnimeModel model)
    {
        var listStatusCheck = model.Tracking.Status == ListStatus;
        var searchTextStatus = string.IsNullOrEmpty(SearchText) ||
                               model.Title.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase) ||
                               model.AlternativeTitles.Any(x => x.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase));
        var yearCheck = Year is null || model.Season.Year == Year;
        var genresCheck = !Genres.Any() || Genres.All(x => model.Genres.Any(y => string.Equals(y, x, StringComparison.InvariantCultureIgnoreCase)));
        var airingStatusCheck = AiringStatus is null || AiringStatus == model.AiringStatus;

        return listStatusCheck && searchTextStatus && yearCheck && genresCheck && airingStatusCheck;
    }
}

public class UserListViewModel : NavigatableViewModel, IHaveState
{
    private readonly ITrackingServiceContext _trackingService;
    private readonly IViewService _viewService;
    private readonly SourceCache<AnimeModel, long> _animeCache = new(x => x.Id);
    private readonly SourceCache<AnimeModel, long> _searchCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<AnimeModel> _anime;
    private readonly ReadOnlyObservableCollection<AnimeModel> _searchResults;

    public UserListViewModel(ITrackingServiceContext trackingService,
                             IAnimeServiceContext animeService,
                             IViewService viewService)
    {
        _trackingService = trackingService;
        _viewService = viewService;
        IsAuthenticated = trackingService.IsAuthenticated;

        ChangeCurrentViewCommand = ReactiveCommand.Create<AnimeStatus>(x => CurrentView = x);
        RefreshCommand = ReactiveCommand.CreateFromTask(SetInitialState);
        SetDisplayMode = ReactiveCommand.Create<DisplayMode>(x => Mode = x);

        _animeCache
            .Connect()
            .RefCount()
            .Filter(Filter.WhenAnyPropertyChanged().Select(x => (Func<AnimeModel,bool>)x.IsVisible))
            .Sort(SortExpressionComparer<AnimeModel>.Descending(x => x.MeanScore))
            .Bind(out _anime)
            .DisposeMany()
            .Subscribe()
            .DisposeWith(Garbage);

        _searchCache
            .Connect()
            .RefCount()
            .Bind(out _searchResults)
            .DisposeMany()
            .Subscribe()
            .DisposeWith(Garbage);

        this.WhenAnyValue(x => x.QuickAddSearchText)
            .Where(text => text is { Length: > 3 })
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(animeService.GetAnime)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(list => _searchCache.EditDiff(list, (first, second) => first.Id == second.Id));
    }

    [Reactive] public AnimeStatus CurrentView { get; set; } = AnimeStatus.Watching;
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public DisplayMode Mode { get; set; } = DisplayMode.Grid;
    [Reactive] public string SearchText { get; set; }
    [Reactive] public string QuickAddSearchText { get; set; }
    public bool IsAuthenticated { get; }
    public AnimeCollectionFilter Filter { get; } = new();
    public ReadOnlyObservableCollection<AnimeModel> QuickSearchResults => _searchResults;
    public ReadOnlyObservableCollection<AnimeModel> Anime => _anime;
    public ICommand ChangeCurrentViewCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SetDisplayMode { get; }

    private Func<AnimeModel, bool> FilterByStatusPredicate(AnimeStatus status) => x => x.Tracking.Status == status;
    private static Func<AnimeModel, bool> FilterByTitle(string title) => x => string.IsNullOrEmpty(title) ||
                                                                                  x.Title.ToLower().Contains(title) ||
                                                                                  (x.AlternativeTitles?.Any(x => x.ToLower().Contains(title)) ?? true);

    public Task SetInitialState()
    {
        if (!IsAuthenticated)
        {
            return Task.CompletedTask;
        }

        IsLoading = true;

        _animeCache.Clear();
        _trackingService.GetAnime()
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Finally(() =>
                        {
                            IsLoading = false;
                            Filter.RaisePropertyChanged(nameof(Filter.ListStatus));
                        })
                        .Subscribe(list =>
                        {
                            _animeCache.EditDiff(list, (item1, item2) => item1.Id == item2.Id);
                            IsLoading = false;
                        }, RxApp.DefaultExceptionHandler.OnError)
                        .DisposeWith(Garbage);

        return Task.CompletedTask;
    }

    public void StoreState(IState state)
    {
        if (_animeCache.Count == 0)
        {
            return;
        }

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
