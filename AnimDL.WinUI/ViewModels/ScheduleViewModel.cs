
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AnimDL.UI.Core.Contracts;
using AnimDL.UI.Core.Models;
using AnimDL.WinUI.Core.Contracts;
using AnimDL.WinUI.Helpers;
using AnimDL.WinUI.Models;
using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AnimDL.WinUI.ViewModels;

public class ScheduleViewModel : NavigatableViewModel, IHaveState
{
    private readonly SourceCache<ScheduledAnimeModel, long> _animeCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<ScheduledAnimeModel> _anime;
    private readonly ObservableAsPropertyHelper<DayOfWeek> _filter;
    private readonly ITrackingService _trackingService;

    public ScheduleViewModel(ITrackingService trackingService)
    {
        _trackingService = trackingService;

        this.WhenAnyValue(x => x.SelectedDay)
            .WhereNotNull()
            .Select(schedule => Enum.Parse<DayOfWeek>($"{schedule.Day[..1].ToUpper()}{schedule.Day[1..]}"))
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
    public ReadOnlyObservableCollection<ScheduledAnimeModel> Anime => _anime;
    public WeeklyScheduleModel Schedule { get; } = new();
    public DayOfWeek Filter => _filter.Value;

    public void RestoreState(IState state)
    {
        var anime = state.GetValue<IEnumerable<ScheduledAnimeModel>>(nameof(Anime));
        InitSchedule(anime);
        _animeCache.Edit(x => x.AddOrUpdate(anime));
        SelectedDay = state.GetValue<ScheduleModel>(nameof(SelectedDay));
    }

    public Task SetInitialState()
    {
        var current = AnimeHelpers.CurrentSeason();

        _trackingService.GetAiringAnime()
                        .Do(list => InitSchedule(list))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(list => 
                        {
                            this.RaisePropertyChanged(nameof(Schedule));
                            var schedule = Schedule.ToList();
                            SelectedDay = schedule.FirstOrDefault(x => x.Day == DateTime.Today.DayOfWeek.ToString().ToLower()) ?? schedule.FirstOrDefault();

                            _animeCache.Edit(x => x.AddOrUpdate(list.Where(x => x.AiringStatus == AiringStatus.CurrentlyAiring)));
                        });

        return Task.CompletedTask;
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(_animeCache.Items, nameof(Anime));
        state.AddOrUpdate(SelectedDay);
    }

    private void InitSchedule(IEnumerable<ScheduledAnimeModel> anime)
    {
        var grouping = anime.GroupBy(x => x.BroadcastDay);

        foreach (var item in grouping.Where(x => x.Key is not null))
        {
            Schedule[item.Key.Value].Count = item.Count();
        }
    }

    private static Func<ScheduledAnimeModel, bool> FilterByDay(DayOfWeek day) => a => a.BroadcastDay == day;

}
