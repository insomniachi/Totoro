using System.Text.RegularExpressions;

namespace Totoro.Core.ViewModels;


public partial class AnimeCollectionFilter : ReactiveObject
{
    [Reactive] public AnimeStatus ListStatus { get; set; } = AnimeStatus.Watching;
    [Reactive] public string SearchText { get; set; }
    [Reactive] public string Year { get; set; }
    [Reactive] public AiringStatus? AiringStatus { get; set; }
    [Reactive] public ObservableCollection<string> Genres { get; set; } = new();

    [GeneratedRegex(@"(19[5-9][0-9])|(20\d{2})")]
    private partial Regex YearRegex();

    public bool IsVisible(AnimeModel model)
    {
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
    private readonly SourceCache<AnimeModel, long> _searchCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<AnimeModel> _anime;
    private readonly ReadOnlyObservableCollection<AnimeModel> _searchResults;
    private readonly HashSet<string> _genres = new();

    public UserListViewModel(ITrackingServiceContext trackingService,
                             IAnimeServiceContext animeService,
                             IViewService viewService,
                             ISettings settings,
                             IConnectivityService connectivityService,
                             ILocalSettingsService localSettingsService)
    {
        _trackingService = trackingService;
        _viewService = viewService;
        IsAuthenticated = trackingService.IsAuthenticated;

        ChangeCurrentViewCommand = ReactiveCommand.Create<AnimeStatus>(x => Filter.ListStatus = x);
        RefreshCommand = ReactiveCommand.CreateFromTask(SetInitialState);
        SetDisplayMode = ReactiveCommand.Create<DisplayMode>(x => Mode = x);
        ResetDataGridColumns = ReactiveCommand.Create(() => DataGridSettings = Settings.GetDefaultUserListDataGridSettings());
        SaveDataGridSettings = ReactiveCommand.Create(() => localSettingsService.SaveSetting(Settings.UserListDataGridSettings, DataGridSettings));
        Mode = settings.ListDisplayMode;
        DataGridSettings = localSettingsService.ReadSetting(Settings.UserListDataGridSettings);
        CheckNewColumns();

        var sort = this.WhenAnyValue(x => x.DataGridSettings)
            .WhereNotNull()
            .SelectMany(settings => settings.WhenAnyValue(x => x.Sort))
            .Do(_ => SaveDataGridSettings.Execute(Unit.Default))
            .Select(GetSortComparer);

        this.ObservableForProperty(x => x.DataGridSettings, x => x)
            .Select(_ => Unit.Default)
            .InvokeCommand(SaveDataGridSettings);

        this.WhenAnyValue(x => x.DataGridSettings)
            .WhereNotNull()
            .SelectMany(x => x.OnColumnChanged())
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Select(_ => Unit.Default)
            .InvokeCommand(SaveDataGridSettings);

        _animeCache
            .Connect()
            .RefCount()
            .Filter(this.WhenAnyValue(x => x.Filter).SelectMany(x => x.WhenAnyPropertyChanged()).Select(x => (Func<AnimeModel, bool>)x.IsVisible))
            .Sort(sort)
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

        connectivityService
            .Connected
            .Subscribe(_ => FetchAnime())
            .DisposeWith(Garbage);

        this.WhenAnyValue(x => x.Mode)
            .Select(x => x == DisplayMode.List)
            .ToPropertyEx(this, x => x.IsListView);
    }

    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public DisplayMode Mode { get; set; }
    [Reactive] public string SearchText { get; set; }
    [Reactive] public string QuickAddSearchText { get; set; }
    [Reactive] public List<string> Genres { get; set; }
    [Reactive] public AnimeCollectionFilter Filter { get; set; } = new();
    [Reactive] public DataGridSettings DataGridSettings { get; set; }
    [ObservableAsProperty] public bool IsListView { get; }

    public bool IsAuthenticated { get; }
    public ReadOnlyObservableCollection<AnimeModel> QuickSearchResults => _searchResults;
    public ReadOnlyObservableCollection<AnimeModel> Anime => _anime;
    public ICommand ChangeCurrentViewCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SetDisplayMode { get; }
    public ICommand ResetDataGridColumns { get; }
    public ICommand SaveDataGridSettings { get; }

    public Task SetInitialState()
    {
        if (!IsAuthenticated)
        {
            return Task.CompletedTask;
        }

        FetchAnime();

        return Task.CompletedTask;
    }

    private void FetchAnime()
    {
        IsLoading = true;
        _genres.Clear();
        _animeCache.Clear();
        _trackingService.GetAnime()
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Finally(() =>
                        {
                            IsLoading = false;
                            Genres = new(_genres);
                            Filter.RaisePropertyChanged(nameof(Filter.ListStatus));
                        })
                        .Subscribe(list =>
                        {
                            _animeCache.EditDiff(list, (item1, item2) => item1.Id == item2.Id);

                            foreach (var anime in list)
                            {
                                foreach (var genre in anime.Genres)
                                {
                                    _genres.Add(genre);
                                }
                            }

                            IsLoading = false;
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
    }

    public void RestoreState(IState state)
    {
        var anime = state.GetValue<IEnumerable<AnimeModel>>(nameof(Anime));
        _animeCache.Edit(x => x.AddOrUpdate(anime));
        Filter = state.GetValue<AnimeCollectionFilter>(nameof(Filter));
        Filter.RaisePropertyChanged(nameof(Filter.ListStatus));
    }

    public async Task<Unit> UpdateAnime(IAnimeModel model) => await _viewService.UpdateTracking(model);

    public void ClearSearch()
    {
        QuickAddSearchText = string.Empty;
        _searchCache.Clear();
    }

    private static IComparer<AnimeModel> GetSortComparer(DataGridSort sort)
    {
        return sort switch
        {
            { ColumnName: "Title", IsAscending: true } => SortExpressionComparer<AnimeModel>.Ascending(x => x.Title),
            { ColumnName: "Title", IsAscending: false } => SortExpressionComparer<AnimeModel>.Descending(x => x.Title),
            { ColumnName: "User Score", IsAscending: true } => SortExpressionComparer<AnimeModel>.Ascending(x => x.Tracking?.Score),
            { ColumnName: "User Score", IsAscending: false } => SortExpressionComparer<AnimeModel>.Descending(x => x.Tracking?.Score),
            { ColumnName: "Mean Score", IsAscending: true } => SortExpressionComparer<AnimeModel>.Ascending(x => x.MeanScore),
            { ColumnName: "Mean Score", IsAscending: false } => SortExpressionComparer<AnimeModel>.Descending(x => x.MeanScore),
            { ColumnName: "Date Started", IsAscending: true } => SortExpressionComparer<AnimeModel>.Ascending(x => x.Tracking.StartDate),
            { ColumnName: "Date Started", IsAscending: false } => SortExpressionComparer<AnimeModel>.Descending(x => x.Tracking.StartDate),
            { ColumnName: "Date Completed", IsAscending: true } => SortExpressionComparer<AnimeModel>.Ascending(x => x.Tracking.FinishDate),
            { ColumnName: "Date Completed", IsAscending: false } => SortExpressionComparer<AnimeModel>.Descending(x => x.Tracking.FinishDate),
            { ColumnName: "Last Updated", IsAscending: true } => SortExpressionComparer<AnimeModel>.Ascending(x => x.Tracking.UpdatedAt),
            { ColumnName: "Last Updated", IsAscending: false } => SortExpressionComparer<AnimeModel>.Descending(x => x.Tracking.UpdatedAt),
            { ColumnName: "Type", IsAscending: true } => SortExpressionComparer<AnimeModel>.Ascending(x => x.Type),
            { ColumnName: "Type", IsAscending: false } => SortExpressionComparer<AnimeModel>.Descending(x => x.Type),
            _ => SortExpressionComparer<AnimeModel>.Ascending(x => x.Title)
        };
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