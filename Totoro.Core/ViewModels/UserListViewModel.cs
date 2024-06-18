using System.Data;
using System.Reactive.Concurrency;

namespace Totoro.Core.ViewModels;

public enum ViewState
{
    Loading,
    NotAuthenticated,
    Authenticated
}

public class UserListViewModel : NavigatableViewModel, IHaveState
{
    private readonly ITrackingServiceContext _trackingService;
    private readonly IViewService _viewService;
    private readonly SourceCache<AnimeModel, long> _animeCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<AnimeModel> _anime;
    private readonly HashSet<string> _genres = [];
    private readonly List<AnimeStatus> _allStatuses = [AnimeStatus.Watching, AnimeStatus.PlanToWatch, AnimeStatus.Completed, AnimeStatus.OnHold, AnimeStatus.Dropped];

    public UserListViewModel(ITrackingServiceContext trackingService,
                             IViewService viewService,
                             ISettings settings,
                             IConnectivityService connectivityService)
    {
        _trackingService = trackingService;
        _viewService = viewService;
        ViewState = trackingService.IsAuthenticated ? ViewState.Authenticated : ViewState.NotAuthenticated;
        IsAuthenticated = trackingService.IsAuthenticated;
        ListType = settings.DefaultListService;

        ChangeCurrentViewCommand = ReactiveCommand.Create<AnimeStatus>(x => Filter.ListStatus = x);
        RefreshCommand = ReactiveCommand.CreateFromTask(SetInitialState, this.WhenAnyValue(x => x.ViewState).Select(x => x is not ViewState.Loading));
        SetDisplayMode = ReactiveCommand.Create<DisplayMode>(x => Mode = x);
        SetSortProperty = ReactiveCommand.Create<string>(columnName => SelectedSortProperty = columnName);
        SetSortOrder = ReactiveCommand.Create<bool>(isAscending => IsSortByAscending = isAscending);
        Mode = settings.ListDisplayMode;
        GridViewSettings = settings.UserListGridViewSettings;
        DataGridSettings = SettingsModel.UserListTableViewSettings;
        (SelectedSortProperty, IsSortByAscending) = DataGridSettings.Sort;
        CheckNewColumns();

        var sort = this.WhenAnyValue(x => x.DataGridSettings)
            .WhereNotNull()
            .SelectMany(settings => settings.WhenAnyValue(x => x.Sort))
            .DistinctUntilChanged()
            .Select(x => GetSortComparer(x, settings.UseEnglishTitles));

        this.WhenAnyValue(x => x.SelectedSortProperty, x => x.IsSortByAscending)
            .Where(x => !string.IsNullOrEmpty(x.Item1))
            .Select(x => new DataGridSort(x.Item1, x.Item2))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => DataGridSettings.Sort = x);

        this.ObservableForProperty(x => x.DataGridSettings, x => x)
            .Select(_ => Unit.Default)
            .InvokeCommand(SaveDataGridSettings);

        this.WhenAnyValue(x => x.DataGridSettings)
            .WhereNotNull()
            .SelectMany(x => x.OnColumnChanged())
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Select(_ => Unit.Default)
            .InvokeCommand(SaveDataGridSettings);

        this.WhenAnyValue(x => x.Filter)
            .SelectMany(x => x.WhenAnyPropertyChanged())
            .Subscribe(_ => _animeCache.Refresh());

        _animeCache
            .Connect()
            .RefCount()
            .AutoRefresh(x => x.Tracking)
            .AutoRefresh(x => x.NextEpisodeAt, propertyChangeThrottle: TimeSpan.FromMilliseconds(500), scheduler: RxApp.MainThreadScheduler)
            .Filter(this.WhenAnyValue(x => x.Filter).SelectMany(x => x.WhenAnyPropertyChanged()).Select(x => (Func<AnimeModel, bool>)x.IsVisible))
            .Sort(sort)
            .Bind(out _anime)
            .DisposeMany()
            .Subscribe()
            .DisposeWith(Garbage);

        connectivityService
            .Connected
            .Subscribe(_ => FetchAnime())
            .DisposeWith(Garbage);

        this.WhenAnyValue(x => x.Mode)
            .Select(x => x == DisplayMode.List)
            .ToPropertyEx(this, x => x.IsListView);

        Observable.Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))
            .Where(_ => Mode == DisplayMode.Grid)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                try
                {
                    foreach (var item in Anime.Where(x => x.NextEpisodeAt is not null))
                    {
                        item.RaisePropertyChanged(nameof(item.NextEpisodeAt));
                    }
                }
                catch { }
            }, RxApp.DefaultExceptionHandler.OnError)
            .DisposeWith(Garbage);
    }

    [Reactive] public ViewState ViewState { get; set; }
    [Reactive] public DisplayMode Mode { get; set; }
    [Reactive] public List<string> Genres { get; set; }
    [Reactive] public AnimeCollectionFilter Filter { get; set; } = new();
    [Reactive] public DataGridSettings DataGridSettings { get; set; }
    [Reactive] public GridViewSettings GridViewSettings { get; set; }
    [Reactive] public string SelectedSortProperty { get; set; }
    [Reactive] public bool IsSortByAscending { get; set; }
    [Reactive] public List<AnimeStatus> Statuses { get; set; } 
    [ObservableAsProperty] public bool IsListView { get; }

    public ListServiceType ListType { get; }
    public bool IsAuthenticated { get; }
    public ReadOnlyObservableCollection<AnimeModel> Anime => _anime;
    public List<string> SortableProperties { get; } = ["Title", "Mean Score", "User Score", "Date Started", "Date Completed", "Last Updated", "Type", "Next Episode"];
    public ICommand ChangeCurrentViewCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SetDisplayMode { get; }
    public ICommand ResetDataGridColumns { get; }
    public ICommand SaveDataGridSettings { get; }
    public ICommand SetSortProperty { get; }
    public ICommand SetSortOrder { get; }

    public Task SetInitialState()
    {
        if (!IsAuthenticated)
        {
            return Task.CompletedTask;
        }

        FetchAnime();

        return Task.CompletedTask;
    }

    public async Task ShowSearchDialog() => await _viewService.ShowSearchListServiceDialog();

    private void FetchAnime()
    {
        ViewState = ViewState.Loading;
        _genres.Clear();
        _animeCache.Clear();
        _trackingService.GetAnime()
                        .ToObservable()
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Finally(() =>
                        {
                            RxApp.MainThreadScheduler.Schedule(() =>
                            {
                                UpdateStatuses();
                                Genres = new(_genres);
                                ViewState = ViewState.Authenticated;
                            });
                        })
                        .Subscribe(anime =>
                        {
                            _animeCache.AddOrUpdate(anime);
                            foreach (var genre in anime.Genres)
                            {
                                _genres.Add(genre);
                            }
                            Filter.RaisePropertyChanged(nameof(Filter.ListStatus));
                        }, RxApp.DefaultExceptionHandler.OnError)
                        .DisposeWith(Garbage);
    } 

    public void StoreState(IState state)
    {
        if (_animeCache.Count == 0)
        {
            return;
        }

        state.AddOrUpdate(_animeCache.Items, nameof(Anime));
        state.AddOrUpdate(Filter);
        state.AddOrUpdate(Mode);
    }

    public void RestoreState(IState state)
    {
        Mode = state.GetValue<DisplayMode>(nameof(Mode));
        var anime = state.GetValue<IEnumerable<AnimeModel>>(nameof(Anime));
        _animeCache.Edit(x => x.AddOrUpdate(anime));
        Filter = state.GetValue<AnimeCollectionFilter>(nameof(Filter));
        UpdateStatuses();
        Filter.RaisePropertyChanged(nameof(Filter.ListStatus));
    }

    public async Task<Unit> UpdateAnime(IAnimeModel model) => await _viewService.UpdateTracking(model);

    private static SortExpressionComparer<AnimeModel> GetSortComparer(DataGridSort sort, bool useEnglishTitles)
    {
        if (sort is null)
        {
            return null;
        }

        var direction = sort.IsAscending ? SortDirection.Ascending : SortDirection.Descending;

        return sort switch
        {
            { ColumnName: "Title" } => SortyByTitle(sort.IsAscending, useEnglishTitles),
            { ColumnName: "User Score" } => CreateComparer(x => x.Tracking.Score, sort.IsAscending),
            { ColumnName: "Mean Score" } => CreateComparer(x => x.MeanScore, sort.IsAscending),
            { ColumnName: "Date Started" } => CreateComparer(x => x.Tracking.StartDate, sort.IsAscending),
            { ColumnName: "Date Completed" } => CreateComparer(x => x.Tracking.FinishDate, sort.IsAscending),
            { ColumnName: "Type" } => CreateComparer(x => x.Type, sort.IsAscending),
            { ColumnName: "Next Episode" } => CreateComparer(x => x.NextEpisodeAt, sort.IsAscending),
            { ColumnName: "Last Updated" } => CreateComparer(x => x.Tracking.UpdatedAt, sort.IsAscending),
            _ => null
        };
    }

    private void UpdateStatuses()
    {
        var prevStatus = Filter.ListStatus ?? AnimeStatus.Watching;
        Statuses = _allStatuses.Where(x => _animeCache.Items.Any(y => y.Tracking?.Status == x)).ToList();
        Filter.ListStatus = prevStatus;
    }

    private static SortExpressionComparer<AnimeModel> CreateComparer(Func<AnimeModel, IComparable> expression, bool isAscending) => new() { new(expression, isAscending ? SortDirection.Ascending : SortDirection.Descending) };

    private static SortExpressionComparer<AnimeModel> SortyByTitle(bool isAscending, bool useEnglishTitles)
    {
        return useEnglishTitles
            ? CreateComparer(x => x.EngTitle, isAscending) 
            : CreateComparer(x => x.Title, isAscending);
    }

    private void CheckNewColumns()
    {
        var defaultColumns = Settings.GetDefaultUserListDataGridSettings().Columns;
        var newColumnsAdded = false;
        foreach (var item in defaultColumns)
        {
            if (DataGridSettings.Columns.FirstOrDefault(x => x.Name == item.Name) is not null)
            {
                continue;
            }

            newColumnsAdded = true;
            DataGridSettings.Columns.Add(item);
        }

        if (newColumnsAdded)
        {
            SaveDataGridSettings.Execute(Unit.Default);
        }
    }
}
