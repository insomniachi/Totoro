
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
    private readonly MalToModelConverter _converter;
    private readonly SourceCache<ScheduledAnimeModel, long> _animeCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<ScheduledAnimeModel> _anime;
    private readonly ObservableAsPropertyHelper<DayOfWeek> _filter;

    public ScheduleViewModel(IMalClient client,
                             MalToModelConverter converter)
    {
        _client = client;
        _converter = converter;

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

    public async Task SetInitialState()
    {
        var userAnime = await _client.Anime().OfUser()
                                     .WithStatus(AnimeStatus.Watching)
                                     .WithField(x => x.Broadcast)
                                     .WithField(x => x.Status)
                                     .WithField(x => x.UserStatus)
                                     .WithField(x => x.TotalEpisodes)
                                     .Find();

        var userAnimeInCurrentSeason = ConvertToModel(userAnime.Data.Where(x => x.Status is AiringStatus.CurrentlyAiring).ToList());
        InitSchedule(userAnimeInCurrentSeason);
        _animeCache.Edit(x => x.AddOrUpdate(userAnimeInCurrentSeason));
        var schedule = Schedule.ToList();
        SelectedDay = schedule.FirstOrDefault(x => x.Day == DateTime.Today.DayOfWeek.ToString().ToLower()) ?? schedule.FirstOrDefault();
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(_animeCache.Items, nameof(Anime));
        state.AddOrUpdate(SelectedDay);
    }

    private List<ScheduledAnimeModel> ConvertToModel(List<Anime> anime)
    {
        var result = new List<ScheduledAnimeModel>();
        anime.ForEach(malModel =>
        {
            result.Add(_converter.Convert<ScheduledAnimeModel>(malModel) as ScheduledAnimeModel);
        });
        return result;
    }

    private void InitSchedule(IEnumerable<ScheduledAnimeModel> anime)
    {
        var grouping = anime.GroupBy(x => x.BroadcastDay);

        foreach (var item in grouping.Where(x => x.Key is not null))
        {
            Schedule[item.Key.Value].Count = item.Count();
        }

        this.RaisePropertyChanged(nameof(Schedule));
    }

    private static Func<ScheduledAnimeModel, bool> FilterByDay(DayOfWeek day) => a => a.BroadcastDay == day;

}
