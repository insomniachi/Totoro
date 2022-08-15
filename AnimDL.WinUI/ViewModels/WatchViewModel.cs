using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using AnimDL.Api;
using AnimDL.Core.Models;
using AnimDL.UI.Core.Contracts;
using AnimDL.UI.Core.Models;
using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Core.Contracts;
using AnimDL.WinUI.Views;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AnimDL.WinUI.ViewModels;

public class WatchViewModel : NavigatableViewModel
{
    private readonly ITrackingService _trackingService;
    private readonly IViewService _viewService;
    private readonly ISettings _settings;
    private readonly IPlaybackStateStorage _playbackStateStorage;
    private readonly IDiscordRichPresense _discordRichPresense;
    private ObservableAsPropertyHelper<IProvider> _provider;
    private readonly SourceCache<SearchResult, string> _searchResultCache = new(x => x.Title);
    private readonly ReadOnlyObservableCollection<SearchResult> _searchResults;

    public WatchViewModel(IProviderFactory providerFactory,
                          ITrackingService trackingService,
                          IViewService viewService,
                          ISettings settings,
                          IPlaybackStateStorage playbackStateStorage,
                          IDiscordRichPresense discordRichPresense)
    {
        _trackingService = trackingService;
        _viewService = viewService;
        _settings = settings;
        _playbackStateStorage = playbackStateStorage;
        _discordRichPresense = discordRichPresense;
        
        SelectedProviderType = _settings.DefaultProviderType;
        SearchResultPicked = ReactiveCommand.CreateFromTask<SearchResult>(FetchEpisodes);

        _searchResultCache
            .Connect()
            .RefCount()
            .Sort(SortExpressionComparer<SearchResult>.Ascending(x => x.Title))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _searchResults)
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);

        _provider = this.WhenAnyValue(x => x.SelectedProviderType)
            .Select(providerFactory.GetProvider)
            .ToProperty(this, x => x.Provider, providerFactory.GetProvider(ProviderType.AnimixPlay));

        this.WhenAnyValue(x => x.Query)
            .Throttle(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
            .SelectMany(x => Provider.Catalog.Search(x).ToListAsync().AsTask())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(FilterDubsIfEnabled)
            .Subscribe(x => _searchResultCache.EditDiff(x, (first, second) => first.Title == second.Title), RxApp.DefaultExceptionHandler.OnNext);

        this.WhenAnyValue(x => x.CurrentPlayerTime)
            .Where(_ => Anime is not null)
            .Where(_ => Anime.Tracking.WatchedEpisodes <= CurrentEpisode)
            .Where(x => CurrentMediaDuration - x <= 135)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(_ => IncrementEpisode());

        this.WhenAnyValue(x => x.CurrentEpisode)
            .Where(x => x > 0)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(FetchUrlForEp)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(url => Url = url);
    }

    [Reactive] public string Query { get; set; }
    [Reactive] public ProviderType SelectedProviderType { get; set; } = ProviderType.AnimixPlay;
    [Reactive] public ObservableCollection<int> Episodes { get; set; } = new();
    [Reactive] public string Url { get; set; }
    [Reactive] public bool IsSuggestionListOpen { get; set; }
    [Reactive] public double CurrentPlayerTime { get; set; }
    [Reactive] public int CurrentEpisode { get; set; }
    [Reactive] public bool NavigatedToWithParameter { get; set; }
    [Reactive] public string VideoPlayerRequestMessage { get; set; }
    public double CurrentMediaDuration { get; set; }
    public AnimeModel Anime { get; set; }
    public SearchResult SelectedResult { get; set; }
    public List<ProviderType> Providers { get; } = Enum.GetValues<ProviderType>().Cast<ProviderType>().ToList();
    public IProvider Provider => _provider.Value;
    public ICommand SearchResultPicked { get; }
    public ReadOnlyObservableCollection<SearchResult> SearchResult => _searchResults;
    public TimeSpan TimeRemaining => TimeSpan.FromSeconds(CurrentMediaDuration - CurrentPlayerTime);


    public Task<Unit> OnVideoPlayerMessageRecieved(WebMessage message)
    {
        switch (message.MessageType)
        {
            case WebMessageType.Ready:
                break;
            case WebMessageType.TimeUpdate:
                CurrentPlayerTime = double.Parse(message.Content);
                if (Anime is not null)
                {
                    _playbackStateStorage.Update(Anime.Id, CurrentEpisode, CurrentPlayerTime);
                }
                break;
            case WebMessageType.DurationUpdate:
                CurrentMediaDuration = double.Parse(message.Content);
                break;
            case WebMessageType.Ended:
                if (Anime is not null)
                {
                    _discordRichPresense.Clear();
                    IncrementEpisode();
                    CurrentEpisode++;
                }
                break;
            case WebMessageType.CanPlay:
                if (Anime is not null)
                {
                    var time = _playbackStateStorage.GetTime(Anime.Id, CurrentEpisode);
                    VideoPlayerRequestMessage = JsonSerializer.Serialize(new { MessageType = "Play", StartTime = time });
                }
                break;
            case WebMessageType.Pause:
                if(_settings.UseDiscordRichPresense)
                {
                    _discordRichPresense.UpdateDetails("Paused");
                    _discordRichPresense.ClearTimer();
                }
                break;
            case WebMessageType.Play:
            case WebMessageType.Seeked:
                TryDiscordRpcStartWatching();
                break;
        }

        return Task.FromResult(Unit.Default);
    }

    public async Task FetchEpisodes(SearchResult result)
    {
        Episodes.Clear();
        var count = await Provider.StreamProvider.GetNumberOfStreams(result.Url);
        var obs = Observable.Range(1, count).ObserveOn(RxApp.MainThreadScheduler);
        obs.Subscribe(x => Episodes.Add(x));
        await obs.LastAsync();
        SelectedResult = result;
        
        if (Anime is not null && Episodes.Contains(Anime.Tracking.WatchedEpisodes ?? 0 + 1))
        {
            CurrentEpisode = Anime.Tracking.WatchedEpisodes ?? 0 + 1;
        }
    }

    public async Task<string> FetchUrlForEp(int ep)
    {
        var epStream = await Provider.StreamProvider.GetStreams(SelectedResult.Url, ep..ep).ToListAsync();
        return epStream[0].Qualities.Values.ElementAt(0).Url;
    }

    private void IncrementEpisode()
    {
        _playbackStateStorage.Reset(Anime.Id, CurrentEpisode);

        var tracking = new Tracking() { WatchedEpisodes = CurrentEpisode };

        if (CurrentEpisode == Anime.TotalEpisodes)
        {
            tracking.Status = UI.Core.Models.AnimeStatus.Completed;
        }

        _trackingService.Update(Anime.Id, tracking)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(x => Anime.Tracking = x);
    }

    public override async Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if (!parameters.ContainsKey("Anime"))
        {
            return;
        }

        NavigatedToWithParameter = true;
        Anime = parameters["Anime"] as AnimeModel;
        var results = await Provider.Catalog.Search(Anime.Title).ToListAsync();

        var selected = results.Count == 1
            ? results[0]
            : await _viewService.ChoooseSearchResult(results, SelectedProviderType);

        if (selected is null)
        {
            return; // TODO : what to do here ?
        }

        await FetchEpisodes(selected);
    }

    private List<SearchResult> FilterDubsIfEnabled(List<SearchResult> results)
    {
        if (_settings.PreferSubs)
        {
            results.RemoveAll(x => x.Title.Contains("(DUB)", StringComparison.OrdinalIgnoreCase) || x.Title.Contains("[DUB]", StringComparison.OrdinalIgnoreCase));
        }

        return results;
    }

    public override Task OnNavigatedFrom()
    {
        if(_settings.UseDiscordRichPresense)
        {
            _discordRichPresense.Clear();
        }

        return Task.CompletedTask;
    }

    private void TryDiscordRpcStartWatching()
    {
        if (_settings.UseDiscordRichPresense == false)
        {
            return;
        }

        if (Anime is null)
        {
            _discordRichPresense.SetPresense(SelectedResult.Title, CurrentEpisode, TimeRemaining);
        }
        else
        {
            _discordRichPresense.SetPresense(Anime, CurrentEpisode, TimeRemaining);
        }
    }
}
