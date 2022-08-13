
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AnimDL.WinUI.Core.Contracts;
using DynamicData;
using MalApi;
using MalApi.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Linq;
using AnimDL.WinUI.Helpers;
using AnimDL.WinUI.Models;

namespace AnimDL.WinUI.ViewModels;

public class ScheduleViewModel : NavigatableViewModel, IHaveState
{
    private readonly IMalClient _client;
    private readonly SourceCache<Anime, long> _animeCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<Anime> _anime;

    public ScheduleViewModel(IMalClient client)
    {
        _client = client;

        var filter = this.WhenAnyValue(x => x.Filter).Select(FilterByDay);

        _animeCache
            .Connect()
            .RefCount()
            .Filter(filter)
            .Bind(out _anime)
            .DisposeMany()
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);

        this.WhenAnyValue(x => x.SelectedDay)
            .WhereNotNull()
            .Select(x => Enum.Parse<DayOfWeek>($"{x.Key[..1].ToUpper()}{x.Key[1..]}"))
            .Subscribe(x => Filter = x);
    }

    [Reactive] public DayOfWeek Filter { get; set; }
    [Reactive] public ScheduleModel SelectedDay { get; set; }
    public ReadOnlyObservableCollection<Anime> Anime => _anime;
    public WeeklyScheduleModel Schedule { get; } = new();

    public void RestoreState(IState state)
    {
        var anime = state.GetValue<IEnumerable<Anime>>(nameof(Anime));
        InitSchedule(anime);
        _animeCache.Edit(x => x.AddOrUpdate(anime));
        Filter = state.GetValue<DayOfWeek>(nameof(Filter));
    }

    public async Task SetInitialState()
    {
        var userAnime = await _client.Anime().OfUser()
                                     .WithStatus(AnimeStatus.Watching)
                                     .WithField(x => x.Broadcast)
                                     .WithField(x => x.Status)
                                     .WithField(x => x.UserStatus)
                                     .WithField(x => x.TotalEpisodes)
                                     .Find();

        var current = AnimeHelpers.CurrentSeason();
        var userAnimeInCurrentSeason = userAnime.Data.Where(x => x.Status is AiringStatus.CurrentlyAiring);

        
        InitSchedule(userAnimeInCurrentSeason);
        _animeCache.Edit(x => x.AddOrUpdate(userAnimeInCurrentSeason));
        var schedule = Schedule.ToList();
        SelectedDay = schedule.FirstOrDefault(x => x.Key == DateTime.Today.DayOfWeek.ToString().ToLower()) ?? schedule.FirstOrDefault();
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(_animeCache.Items, nameof(Anime));
        state.AddOrUpdate(Filter);
    }

    private void InitSchedule(IEnumerable<Anime> anime)
    {
        var grouping = anime.GroupBy(x => x.Broadcast.DayOfWeek);
        
        foreach (var item in grouping.Where(x => x.Key is not null))
        {
            Schedule[item.Key.Value].Count = item.Count();
        }

        this.RaisePropertyChanged(nameof(Schedule));
    }

    private static Func<Anime, bool> FilterByDay(DayOfWeek day) => a => a.Broadcast.DayOfWeek == day;

}
