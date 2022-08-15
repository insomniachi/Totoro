﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AnimDL.UI.Core.Contracts;
using AnimDL.UI.Core.Models;
using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Core.Contracts;
using AnimDL.WinUI.Helpers;
using DynamicData;
using DynamicData.Binding;
using MalApi.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AnimDL.WinUI.ViewModels;

public class SeasonalViewModel : NavigatableViewModel, IHaveState
{
    private readonly IAnimeService _animeService;
    private readonly IViewService _viewService;
    private readonly SourceCache<SeasonalAnimeModel, long> _animeCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<SeasonalAnimeModel> _anime;

    public SeasonalViewModel(IAnimeService animeService,
                             IViewService viewService)
    {
        _animeService = animeService;
        _viewService = viewService;

        _animeCache
            .Connect()
            .RefCount()
            .Filter(this.WhenAnyValue(x => x.Season).WhereNotNull().Select(FilterBySeason))
            .Sort(SortExpressionComparer<SeasonalAnimeModel>.Ascending(x => x.Popularity))
            .Bind(out _anime)
            .DisposeMany()
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);

        this.WhenAnyValue(x => x.SeasonFilter).Subscribe(SwitchSeasonFilter);


        SetSeasonCommand = ReactiveCommand.Create<string>(SwitchSeasonFilter);
        AddToListCommand = ReactiveCommand.CreateFromTask<AnimeModel>(AddToList);
    }

    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public Season Season { get; set; }
    [Reactive] public string SeasonFilter { get; set; } = "Current";

    public Season Current => AnimeHelpers.CurrentSeason();
    public Season Next => AnimeHelpers.NextSeason();
    public Season Prev => AnimeHelpers.PrevSeason();
    public ReadOnlyObservableCollection<SeasonalAnimeModel> Anime => _anime;
    public ICommand SetSeasonCommand { get; }
    public ICommand AddToListCommand { get; }

    public Task SetInitialState()
    {
        _animeService.GetSeasonalAnime()
                     .ObserveOn(RxApp.MainThreadScheduler)
                     .Subscribe(list =>
                     {
                         _animeCache.Edit(x => x.AddOrUpdate(list));

                     }).DisposeWith(Garbage);

        Season = Current;

        return Task.CompletedTask;
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

    private async Task AddToList(AnimeModel a) => await _viewService.UpdateAnimeStatus(a);

    private Func<SeasonalAnimeModel, bool> FilterBySeason(Season s) => x => x.Season == s;

}
