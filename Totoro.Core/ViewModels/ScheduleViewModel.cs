using Totoro.Core.Helpers;

namespace Totoro.Core.ViewModels;

public class ScheduleViewModel : NavigatableViewModel, IHaveState
{
    private readonly SourceCache<AnimeModel, long> _animeCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<AnimeModel> _anime;
    private readonly ObservableAsPropertyHelper<DayOfWeek> _filter;
    private readonly ITrackingService _trackingService;
    private readonly ISystemClock _systemClock;

    public ScheduleViewModel(ITrackingService trackingService,
                             ISystemClock systemClock)
    {
        _trackingService = trackingService;
        _systemClock = systemClock;

        this.WhenAnyValue(x => x.SelectedDay)
            .WhereNotNull()
            .Select(dailySchedule => dailySchedule.DayOfWeek)
            .ToProperty(this, x => x.Filter, out _filter, DayOfWeek.Sunday);

        _animeCache
            .Connect()
            .RefCount()
            .Filter(this.WhenAnyValue(x => x.Filter).Select(FilterByDay))
            .Bind(out _anime)
            .DisposeMany()
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);
    }

    [Reactive] public ScheduleModel SelectedDay { get; set; }
    [Reactive] public List<ScheduleModel> WeeklySchedule { get; set; } = new();
    public ReadOnlyObservableCollection<AnimeModel> Anime => _anime;
    public WeeklyScheduleModel Schedule { get; } = new();
    public DayOfWeek Filter => _filter.Value;

    public void RestoreState(IState state)
    {
        var anime = state.GetValue<IEnumerable<AnimeModel>>(nameof(Anime));
        InitSchedule(anime);
        _animeCache.Edit(x => x.AddOrUpdate(anime));
        WeeklySchedule = Schedule.ToList();
        SelectedDay = WeeklySchedule.FirstOrDefault(x => x.DayOfWeek == state.GetValue<DayOfWeek>(nameof(SelectedDay)));
    }
    
    public void StoreState(IState state)
    {
        state.AddOrUpdate(_animeCache.Items, nameof(Anime));
        if (Anime.Any())
        {
            state.AddOrUpdate(SelectedDay.DayOfWeek, nameof(SelectedDay)); 
        }
    }

    public Task SetInitialState()
    {
        var current = AnimeHelpers.CurrentSeason();

        _trackingService.GetCurrentlyAiringTrackedAnime()
                        .Do(InitSchedule)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(list =>
                        {
                            WeeklySchedule = Schedule.ToList();
                            this.RaisePropertyChanged(nameof(Schedule));
                            SelectedDay = GetSelectedDay();
                            _animeCache.Edit(x => x.AddOrUpdate(list));
                        });

        return Task.CompletedTask;
    }

    private void InitSchedule(IEnumerable<AnimeModel> anime)
    {
        var grouping = anime.GroupBy(x => x.BroadcastDay);

        foreach (var item in grouping.Where(x => x.Key is not null))
        {
            Schedule[item.Key.Value].Count = item.Count();
        }
    }

    private ScheduleModel GetSelectedDay() => WeeklySchedule.FirstOrDefault(x => x.DayOfWeek == _systemClock.Today.DayOfWeek) ?? GetNextDayWithAiringAnime();

    private ScheduleModel GetNextDayWithAiringAnime()
    {
        var days = Enum.GetValues<DayOfWeek>();
        var today = _systemClock.Today.DayOfWeek;
        var todayIndex = days.IndexOf(today);
        var index = NextCyclicValue(todayIndex, days);

        while(index != todayIndex)
        {
            if (Schedule[days[index]] is { Count: > 0 } day)
            {
                return day;
            }

            index = NextCyclicValue(index, days);
        }

        return null; // This should only happen when you are watching nothing.
    }

    private static int NextCyclicValue<T>(int index, T[] values)
    {
        return index == values.Length - 1 ? 0 : index + 1;
    }

    private static Func<AnimeModel, bool> FilterByDay(DayOfWeek day) => a => a.BroadcastDay == day;

}
