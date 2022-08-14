using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Core.Contracts;
using AnimDL.WinUI.Models;
using AnimDL.WinUI.Views;
using DynamicData;
using MalApi;
using MalApi.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AnimDL.WinUI.ViewModels;


public class UserListViewModel : NavigatableViewModel, IHaveState
{
    private readonly IMalClient _malClient;
    private readonly INavigationService _navigationService;
    private readonly MalToModelConverter _converter;
    private readonly SourceCache<AnimeModel, long> _animeCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<AnimeModel> _anime;

    public UserListViewModel(IMalClient malClient,
                             INavigationService navigationService,
                             MalToModelConverter converter)
    {
        _malClient = malClient;
        _navigationService = navigationService;
        _converter = converter;
        ItemClickedCommand = ReactiveCommand.Create<AnimeModel>(OnItemClicked);
        ChangeCurrentViewCommand = ReactiveCommand.Create<AnimeStatus>(x => CurrentView = x);
        RefreshCommand = ReactiveCommand.CreateFromTask(SetInitialState);
        SetDisplayMode = ReactiveCommand.Create<DisplayMode>(x => Mode = x);

        _animeCache
            .Connect()
            .RefCount()
            .Filter(this.WhenAnyValue(x => x.CurrentView).Select(FilterByStatusPredicate))
            .Bind(out _anime)
            .DisposeMany()
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);
    }

    [Reactive] public AnimeStatus CurrentView { get; set; } = AnimeStatus.Watching;
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public DisplayMode Mode { get; set; } = DisplayMode.Grid;

    public ReadOnlyObservableCollection<AnimeModel> Anime => _anime;
    public ICommand ItemClickedCommand { get; }
    public ICommand ChangeCurrentViewCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SetDisplayMode { get; }

    private void OnItemClicked(AnimeModel anime)
    {
        _navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object> { ["Anime"] = anime });
    }

    private Func<AnimeModel, bool> FilterByStatusPredicate(AnimeStatus status) => x => x.UserAnimeStatus.Status == status;

    public async Task SetInitialState()
    {
        IsLoading = true;
        _animeCache.Clear();
        var userAnime = await _malClient.Anime()
                                        .OfUser()
                                        .WithField(x => x.UserStatus)
                                        .WithField(x => x.TotalEpisodes)
                                        .WithField(x => x.Broadcast)
                                        .Find();
        
        _animeCache.AddOrUpdate(ConvertToModel(userAnime.Data));
        IsLoading = false;
    }

    private List<AnimeModel> ConvertToModel(List<Anime> anime)
    {
        var result = new List<AnimeModel>();
        anime.ForEach(malModel =>
        {
            result.Add(_converter.Convert<AnimeModel>(malModel));
        });
        return result;
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(_animeCache.Items, nameof(Anime));
        state.AddOrUpdate(CurrentView);
    }

    public void RestoreState(IState state)
    {
        var anime = state.GetValue<IEnumerable<AnimeModel>>(nameof(Anime));
        _animeCache.Edit(x => x.AddOrUpdate(anime));
        CurrentView = state.GetValue<AnimeStatus>(nameof(CurrentView));
    }
}
