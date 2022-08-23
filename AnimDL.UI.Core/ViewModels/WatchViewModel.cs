using AnimDL.Api;

namespace AnimDL.UI.Core.ViewModels;

public class WatchViewModel : NavigatableViewModel
{
    private readonly ITrackingService _trackingService;
    private readonly IViewService _viewService;
    private readonly ISettings _settings;
    private readonly IPlaybackStateStorage _playbackStateStorage;
    private readonly IDiscordRichPresense _discordRichPresense;
    private readonly ObservableAsPropertyHelper<IProvider> _provider;
    private readonly ObservableAsPropertyHelper<bool> _hasSubAndDub;
    private readonly ObservableAsPropertyHelper<string> _url;
    private ObservableAsPropertyHelper<double> _currentPlayerTime;
    private ObservableAsPropertyHelper<double> _currentMediaDuration;
    private readonly SourceCache<SearchResult, string> _searchResultCache = new(x => x.Title);
    private readonly SourceList<int> _episodesCache = new();
    private readonly ReadOnlyObservableCollection<SearchResult> _searchResults;
    private readonly ReadOnlyObservableCollection<int> _episodes;

    public WatchViewModel(IProviderFactory providerFactory,
                          ITrackingService trackingService,
                          IViewService viewService,
                          ISettings settings,
                          IPlaybackStateStorage playbackStateStorage,
                          IDiscordRichPresense discordRichPresense,
                          IMessageBus messageBus)
    {
        _trackingService = trackingService;
        _viewService = viewService;
        _settings = settings;
        _playbackStateStorage = playbackStateStorage;
        _discordRichPresense = discordRichPresense;

        SelectedProviderType = _settings.DefaultProviderType;
        SearchResultPicked = ReactiveCommand.Create<SearchResult>(x => SelectedAudio = x);

        _searchResultCache
            .Connect()
            .RefCount()
            .Sort(SortExpressionComparer<SearchResult>.Ascending(x => x.Title))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _searchResults)
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);

        _episodesCache
            .Connect()
            .RefCount()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _episodes)
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);

        messageBus
            .Listen<WebMessage>()
            .GroupBy(message => message.MessageType)
            .Subscribe(observable =>
            {
                switch (observable.Key)
                {
                    case WebMessageType.TimeUpdate:
                        observable.Select(messsage => double.Parse(messsage.Content))
                                  .ToProperty(this, nameof(CurrentPlayerTime), out _currentPlayerTime, () => 0.0)
                                  .DisposeWith(Garbage);
                        break;

                    case WebMessageType.DurationUpdate:
                        observable.Select(message => double.Parse(message.Content))
                                  .ToProperty(this, nameof(CurrentMediaDuration), out _currentMediaDuration, () => 0.0)
                                  .DisposeWith(Garbage);
                        break;

                    // if video finished playing, play the next episode
                    case WebMessageType.Ended:
                        observable.Do(_ => _discordRichPresense.Clear())
                                  .Do(_ => UpdateTracking())
                                  .ObserveOn(RxApp.MainThreadScheduler)
                                  .Do(_ => CurrentEpisode++)
                                  .Subscribe().DisposeWith(Garbage);
                        break;

                    /// Auto play from last watched location otherwise start from begining
                    case WebMessageType.CanPlay:
                        observable.Select(_ => playbackStateStorage.GetTime(Anime?.Id ?? 0, CurrentEpisode ?? 0))
                                  .Select(time => JsonSerializer.Serialize(new { MessageType = "Play", StartTime = time }))
                                  .Subscribe(message => VideoPlayerRequestMessage = message)
                                  .DisposeWith(Garbage);
                        break;

                    // Reset discord RPC
                    case WebMessageType.Pause:
                        observable.Where(_ => settings.UseDiscordRichPresense)
                                  .Do(_ => discordRichPresense.UpdateDetails("Paused"))
                                  .Do(_ => discordRichPresense.ClearTimer())
                                  .Subscribe().DisposeWith(Garbage);
                        break;

                    case WebMessageType.Seeked or WebMessageType.Play:
                        observable.Do(_ => TryDiscordRpcStartWatching())
                                  .Subscribe().DisposeWith(Garbage);
                        break;
                };
            });

        // periodically save the current timestamp so that we can resume later
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(x => Anime is not null && x > 10)
            .Subscribe(time => playbackStateStorage.Update(Anime.Id, CurrentEpisode.Value, time));

        this.WhenAnyValue(x => x.SelectedProviderType)
            .Select(providerFactory.GetProvider)
            .ToProperty(this, nameof(Provider), out _provider, () => providerFactory.GetProvider(ProviderType.AnimixPlay));

        this.WhenAnyValue(x => x.SelectedAnimeResult)
            .Select(x => x is { Dub: { }, Sub: { } })
            .ToProperty(this, nameof(HasSubAndDub), out _hasSubAndDub, () => false);

        /// populate searchbox suggestions
        this.WhenAnyValue(x => x.Query)
            .Throttle(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
            .SelectMany(x => Provider.Catalog.Search(x).ToListAsync().AsTask())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(FilterDubsIfEnabled)
            .Subscribe(x => _searchResultCache.EditDiff(x, (first, second) => first.Title == second.Title), RxApp.DefaultExceptionHandler.OnNext);

        /// if we have less than 135 seconds left and we have not completed this episode
        /// set this episode as watched.
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(_ => Anime is not null)
            .Where(_ => Anime.Tracking.WatchedEpisodes <= CurrentEpisode)
            .Where(x => CurrentMediaDuration - x <= 135)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(_ => UpdateTracking());

        /// if we have both sub and dub and switch from sub to dub or vice versa
        /// reset <see cref="CurrentEpisode"/> to null, outherwise it won't trigger changed event.
        this.ObservableForProperty(x => x.UseDub, x => x)
            .Where(_ => HasSubAndDub)
            .Select(useDub => useDub ? SelectedAnimeResult.Dub : SelectedAnimeResult.Sub)
            .Do(_ => CurrentEpisode = null)
            .InvokeCommand(SearchResultPicked);

        /// 1. Select Sub/Dub based in <see cref="UseDub"/> if Dub is not present select Sub
        /// 2. Set <see cref="SelectedAudio"/>
        this.ObservableForProperty(x => x.SelectedAnimeResult, x => x)
            .Select(x => UseDub ? x.Dub ?? x.Sub : x.Sub)
            .ObserveOn(RxApp.MainThreadScheduler)
            .InvokeCommand(SearchResultPicked);

        /// 1. Get the number of Episodes
        /// 2. Populate Episodes list
        /// 3. If we can connect this to a Mal Id, set <see cref="CurrentEpisode"/> to last unwatched ep
        this.ObservableForProperty(x => x.SelectedAudio, x => x)
            .SelectMany(result => Provider.StreamProvider.GetNumberOfStreams(result.Url))
            .Select(count => Enumerable.Range(1, count).ToList())
            .Do(list => _episodesCache.EditDiff(list))
            .Where(_ => Anime is not null)
            .Select(_ => Anime.Tracking?.WatchedEpisodes ?? 0)
            .Where(ep => ep < Anime.TotalEpisodes)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ep => CurrentEpisode = ep + 1);

        /// Scrape url for <see cref="CurrentEpisode"/> and set to <see cref="Url"/>
        this.ObservableForProperty(x => x.CurrentEpisode, x => x)
            .Where(x => x > 0)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(FetchEpUrl)
            .ToProperty(this, nameof(Url), out _url, () => string.Empty);
    }

    [Reactive] public string Query { get; set; }
    [Reactive] public ProviderType SelectedProviderType { get; set; } = ProviderType.AnimixPlay;
    [Reactive] public bool IsSuggestionListOpen { get; set; }
    [Reactive] public int? CurrentEpisode { get; set; }
    [Reactive] public bool HideControls { get; set; }
    [Reactive] public string VideoPlayerRequestMessage { get; set; }
    [Reactive] public bool UseDub { get; set; }
    [Reactive] public (SearchResult Sub, SearchResult Dub) SelectedAnimeResult { get; set; }
    [Reactive] public SearchResult SelectedAudio { get; set; }
    public IProvider Provider => _provider.Value;
    public bool HasSubAndDub => _hasSubAndDub.Value;
    public string Url => _url.Value;
    public double CurrentPlayerTime => _currentPlayerTime?.Value ?? 0;
    public double CurrentMediaDuration => _currentMediaDuration?.Value ?? 0;
    public AnimeModel Anime { get; set; }
    public List<ProviderType> Providers { get; } = Enum.GetValues<ProviderType>().Cast<ProviderType>().ToList();
    public ICommand SearchResultPicked { get; }
    public ReadOnlyObservableCollection<int> Episodes => _episodes;
    public ReadOnlyObservableCollection<SearchResult> SearchResult => _searchResults;
    public TimeSpan TimeRemaining => TimeSpan.FromSeconds(CurrentMediaDuration - CurrentPlayerTime);

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if (!parameters.ContainsKey("Anime"))
        {
            return Task.CompletedTask;
        }

        HideControls = true;
        Anime = parameters["Anime"] as AnimeModel;

        Find(Anime)
            .ToObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => SelectedAnimeResult = x);

        return Task.CompletedTask;
    }
    public override Task OnNavigatedFrom()
    {
        if (_settings.UseDiscordRichPresense)
        {
            _discordRichPresense.Clear();
        }

        return Task.CompletedTask;
    }

    private async Task<(SearchResult Sub, SearchResult Dub)> Find(AnimeModel anime)
    {
        if (Provider.Catalog is IMalCatalog malCatalog)
        {
            return await malCatalog.SearchByMalId(anime.Id);
        }
        else
        {
            var results = await Provider.Catalog.Search(Anime.Title).ToListAsync();

            if (results.Count == 1)
            {
                return (results[0], null);
            }
            else if (results.Count == 2)
            {
                return (results[0], results[1]);
            }
            else
            {
                return (await _viewService.ChoooseSearchResult(results, SelectedProviderType), null);
            }
        }
    }

    private List<SearchResult> FilterDubsIfEnabled(List<SearchResult> results)
    {
        if (_settings.PreferSubs)
        {
            results.RemoveAll(x => x.Title.Contains("(DUB)", StringComparison.OrdinalIgnoreCase)
            || x.Title.Contains("[DUB]", StringComparison.OrdinalIgnoreCase));
        }

        return results;
    }

    private IObservable<string> FetchEpUrl(int? episode)
    {
        return Provider.StreamProvider
                       .GetStreams(SelectedAudio.Url, episode.Value..episode.Value)
                       .ToListAsync().AsTask()
                       .ToObservable()
                       .Select(x => x[0].Qualities.Values.ElementAt(0).Url);
    }

    private void UpdateTracking()
    {
        _playbackStateStorage.Reset(Anime.Id, CurrentEpisode.Value);

        var tracking = new Tracking() { WatchedEpisodes = CurrentEpisode };

        if (CurrentEpisode == Anime.TotalEpisodes)
        {
            tracking.Status = AnimeStatus.Completed;
        }

        _trackingService.Update(Anime.Id, tracking)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(x => Anime.Tracking = x);
    }

    private void TryDiscordRpcStartWatching()
    {
        if (_settings.UseDiscordRichPresense == false)
        {
            return;
        }

        _discordRichPresense.SetPresense(SelectedAudio.Title, CurrentEpisode.Value, TimeRemaining);
    }

}