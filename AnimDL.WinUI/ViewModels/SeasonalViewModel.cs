using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Core.Contracts;
using AnimDL.WinUI.Helpers;
using AnimDL.WinUI.Models;
using DynamicData;
using MalApi;
using MalApi.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AnimDL.WinUI.ViewModels;

public class SeasonalViewModel : NavigatableViewModel, IHaveState
{
    private readonly IMalClient _client;
    private readonly IViewService _viewService;
    private readonly MalToModelConverter _converter;
    private readonly SourceCache<SeasonalAnimeModel, long> _animeCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<SeasonalAnimeModel> _anime;

    public SeasonalViewModel(IMalClient client,
                             IViewService viewService,
                             MalToModelConverter converter)
    {
        _client = client;
        _viewService = viewService;
        _converter = converter;
        SetSeasonCommand = ReactiveCommand.Create<string>(SwitchSeasonFilter);
        AddToListCommand = ReactiveCommand.CreateFromTask<Anime>(AddToList);

        _animeCache
            .Connect()
            .RefCount()
            .Filter(this.WhenAnyValue(x => x.Season).WhereNotNull().Select(FilterBySeason))
            .Bind(out _anime)
            .DisposeMany()
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);

        this.WhenAnyValue(x => x.SeasonFilter).Subscribe(SwitchSeasonFilter);
    }

    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public Season Season { get; set; }
    [Reactive] public string SeasonFilter { get; set; } = "Current";

    public Season Current { get; private set; }
    public Season Next { get; private set; }
    public Season Prev { get; private set; }
    public ReadOnlyObservableCollection<SeasonalAnimeModel> Anime => _anime;
    public ICommand SetSeasonCommand { get; }
    public ICommand AddToListCommand { get; }

    public async Task SetInitialState()
    {
        IsLoading = true;

        Prev = AnimeHelpers.PrevSeason();
        Current = AnimeHelpers.CurrentSeason();
        Next = AnimeHelpers.NextSeason();

        Season = Current;

        var currAnime = await _client
            .Anime()
            .OfSeason(Current.SeasonName, Current.Year)
            .WithField(x => x.UserStatus)
            .WithField(x => x.StartSeason)
            .WithField(x => x.TotalEpisodes)
            .Find();
       
        _animeCache.Edit(x =>
        {
            x.AddOrUpdate(ConvertToModel(currAnime.Data));
        });

        var prevAnime = await _client
            .Anime()
            .OfSeason(Prev.SeasonName, Prev.Year)
            .WithField(x => x.UserStatus)
            .WithField(x => x.StartSeason)
            .WithField(x => x.TotalEpisodes)
            .Find();

        var nextAnime = await _client
            .Anime()
            .OfSeason(Next.SeasonName, Next.Year)
            .WithField(x => x.UserStatus)
            .WithField(x => x.StartSeason)
            .WithField(x => x.TotalEpisodes)
            .Find();

        _animeCache.Edit(x =>
        {
            x.AddOrUpdate(ConvertToModel(prevAnime.Data));
            x.AddOrUpdate(ConvertToModel(nextAnime.Data));
        });

        IsLoading = false;
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(_animeCache.Items, nameof(Anime));
        state.AddOrUpdate(Season);
    }

    public void RestoreState(IState state)
    {
        var anime = state.GetValue<IEnumerable<SeasonalAnimeModel>>(nameof(Anime));
        Season = state.GetValue<Season>(nameof(Season));
        _animeCache.Edit(x => x.AddOrUpdate(anime));
    }

    private void SwitchSeasonFilter(string filter)
    {
        Season = filter switch
        {
            "Current" => Current,
            "Previous" => Prev,
            "Next" => Next,
            _ => throw new InvalidOperationException()
        };
    }

    private List<SeasonalAnimeModel> ConvertToModel(List<Anime> anime)
    {
        var result = new List<SeasonalAnimeModel>();
        anime.ForEach(malModel =>
        {
            result.Add(_converter.Convert<SeasonalAnimeModel>(malModel) as SeasonalAnimeModel);
        });
        return result;
    }

    private async Task AddToList(Anime a) => await _viewService.UpdateAnimeStatus(new AnimeModel { Id = a.Id, Title = a.Title});

    private Func<SeasonalAnimeModel, bool> FilterBySeason(Season s) => x => x.Season.SeasonName == s.SeasonName && x.Season.Year == s.Year;

}
