using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AnimDL.WinUI.Contracts.Services;
using AnimDL.WinUI.Core.Contracts;
using AnimDL.WinUI.Views;
using DynamicData;
using MalApi;
using MalApi.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AnimDL.WinUI.ViewModels;


public class UserListViewModel : ViewModel, IHaveState
{
    private readonly IMalClient _malClient;
    private readonly INavigationService _navigationService;
    private readonly SourceCache<Anime, long> _animeCache = new(x => x.Id);
    private readonly ReadOnlyObservableCollection<Anime> _anime;

    public UserListViewModel(IMalClient malClient,
                             INavigationService navigationService)
    {
        _malClient = malClient;
        _navigationService = navigationService;

        ItemClickedCommand = ReactiveCommand.Create<Anime>(OnItemClicked);
        ChangeCurrentViewCommand = ReactiveCommand.Create<AnimeStatus>(x => CurrentView = x);
        RefreshCommand = ReactiveCommand.CreateFromTask(SetInitialState);
        SetDisplayMode = ReactiveCommand.Create<DisplayMode>(x => Mode = x);

        var filter = this.WhenAnyValue(x => x.CurrentView)
                         .Select(FilterByStatusPredicate);

        _animeCache
            .Connect()
            .RefCount()
            .Filter(filter)
            .Bind(out _anime)
            .DisposeMany()
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);
    }

    [Reactive] public AnimeStatus CurrentView { get; set; } = AnimeStatus.Watching;
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public DisplayMode Mode { get; set; } = DisplayMode.Grid;

    public ReadOnlyObservableCollection<Anime> Anime => _anime;
    public ICommand ItemClickedCommand { get; }
    public ICommand ChangeCurrentViewCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SetDisplayMode { get; }

    private void OnItemClicked(Anime anime)
    {
        _navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object> { ["Anime"] = anime });
    }

    private Func<Anime, bool> FilterByStatusPredicate(AnimeStatus status) => x => x.UserStatus.Status == status;

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
        
        _animeCache.AddOrUpdate(userAnime.Data);
        IsLoading = false;
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(_animeCache.Items, nameof(Anime));
        state.AddOrUpdate(CurrentView);
    }

    public void RestoreState(IState state)
    {
        var anime = state.GetValue<IEnumerable<Anime>>(nameof(Anime));
        _animeCache.Edit(x => x.AddOrUpdate(anime));
        CurrentView = state.GetValue<AnimeStatus>(nameof(CurrentView));
    }
}
