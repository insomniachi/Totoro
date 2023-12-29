using System.Reactive.Concurrency;
using System.Text.RegularExpressions;

namespace Totoro.Core.ViewModels;


public partial class AnimeCollectionFilter : ReactiveObject
{
    [Reactive] public AnimeStatus ListStatus { get; set; } = AnimeStatus.Watching;
    [Reactive] public string SearchText { get; set; }
    [Reactive] public string Year { get; set; }
    [Reactive] public AiringStatus? AiringStatus { get; set; }
    [Reactive] public ObservableCollection<string> Genres { get; set; } = [];

    [GeneratedRegex(@"(19[5-9][0-9])|(20\d{2})")]
    private partial Regex YearRegex();

    public bool IsVisible(AnimeModel model)
    {
        if (model.Tracking is null)
        {
            return false;
        }

        var listStatusCheck = model.Tracking.Status == ListStatus;
        var searchTextStatus = string.IsNullOrEmpty(SearchText) ||
                               model.Title.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase) ||
                               model.AlternativeTitles.Any(x => x.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase));
        var yearCheck = string.IsNullOrEmpty(Year) || !YearRegex().IsMatch(Year) || model.Season.Year.ToString() == Year;
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
    private readonly ReadOnlyObservableCollection<AnimeModel> _anime;
    private readonly HashSet<string> _genres = [];

    public UserListViewModel(ITrackingServiceContext trackingService,
                             IViewService viewService,
                             ISettings settings,
                             IConnectivityService connectivityService,
                             ILocalSettingsService localSettingsService)
    {
        _trackingService = trackingService;
        _viewService = viewService;
        IsAuthenticated = trackingService.IsAuthenticated;
        ListType = settings.DefaultListService;

        ChangeCurrentViewCommand = ReactiveCommand.Create<AnimeStatus>(x => Filter.ListStatus = x);
        RefreshCommand = ReactiveCommand.CreateFromTask(SetInitialState, this.WhenAnyValue(x => x.IsLoading).Select(x => !x));
        SetDisplayMode = ReactiveCommand.Create<DisplayMode>(x => Mode = x);
        ResetDataGridColumns = ReactiveCommand.Create(() => DataGridSettings = Settings.GetDefaultUserListDataGridSettings());
        SaveDataGridSettings = ReactiveCommand.Create(() => localSettingsService.SaveSetting(Settings.UserListDataGridSettings, DataGridSettings));
        SetSortProperty = ReactiveCommand.Create<string>(columnName => SelectedSortProperty = columnName);
        SetSortOrder = ReactiveCommand.Create<bool>(isAscending => IsSortByAscending = isAscending);
        Mode = settings.ListDisplayMode;
        DataGridSettings = localSettingsService.ReadSetting(Settings.UserListDataGridSettings);
        GridViewSettings = settings.UserListGridViewSettings;
        (SelectedSortProperty, IsSortByAscending) = DataGridSettings.Sort;
        CheckNewColumns();

        var sort = this.WhenAnyValue(x => x.DataGridSettings)
            .WhereNotNull()
            .SelectMany(settings => settings.WhenAnyValue(x => x.Sort))
            .DistinctUntilChanged()
            .Do(_ => SaveDataGridSettings.Execute(Unit.Default))
            .Select(GetSortComparer);

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

        this.WhenAnyValue(x => x.Filter).SelectMany(x => x.WhenAnyPropertyChanged())
            .Subscribe(x =>
            {
                _animeCache.Refresh();
            });

        _animeCache
            .Connect()
            .RefCount()
            .AutoRefresh(x => x.Tracking)
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

    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public DisplayMode Mode { get; set; }
    [Reactive] public List<string> Genres { get; set; }
    [Reactive] public AnimeCollectionFilter Filter { get; set; } = new();
    [Reactive] public DataGridSettings DataGridSettings { get; set; }
    [Reactive] public GridViewSettings GridViewSettings { get; set; }
    [Reactive] public string SelectedSortProperty { get; set; }
    [Reactive] public bool IsSortByAscending { get; set; }
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
        IsLoading = true;
        _genres.Clear();
        _animeCache.Clear();
        _trackingService.GetAnime()
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Finally(() =>
                        {
                            RxApp.MainThreadScheduler.Schedule(() => Genres = new(_genres));
                        })
                        .Subscribe(list =>
                        {
                            _animeCache.AddOrUpdate(list);
                            foreach (var anime in list)
                            {
                                foreach (var genre in anime.Genres)
                                {
                                    _genres.Add(genre);
                                }
                            }
                            IsLoading = false;
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
        Filter.RaisePropertyChanged(nameof(Filter.ListStatus));
    }

    public async Task<Unit> UpdateAnime(IAnimeModel model) => await _viewService.UpdateTracking(model);

    private static IComparer<AnimeModel> GetSortComparer(DataGridSort sort)
    {
        if (sort is null)
        {
            return null;
        }

        var direction = sort.IsAscending ? SortDirection.Ascending : SortDirection.Descending;

        return sort switch
        {
            { ColumnName: "Title" } => CreateComparer(x => x.Title, sort.IsAscending),
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

    private static SortExpressionComparer<AnimeModel> CreateComparer(Func<AnimeModel, IComparable> expression, bool isAscending) => new() { new(expression, isAscending ? SortDirection.Ascending : SortDirection.Descending) };

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
