using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AnimDL.Api;
using AnimDL.Core.Models;
using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Contracts.Services;
using AnimDL.WinUI.Core.Contracts;
using AnimDL.WinUI.Views;
using DynamicData;
using DynamicData.Binding;
using MalApi;
using MalApi.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AnimDL.WinUI.ViewModels;

public class WatchViewModel : ViewModel
{
    private readonly SourceCache<SearchResult, string> _searchResultCache = new(x => x.Title);
    private readonly ReadOnlyObservableCollection<SearchResult> _searchResults;
    private readonly IMalClient _client;
    private readonly IViewService _viewService;
    private readonly ISettings _settings;
    private readonly IPlaybackStateStorage _playbackStateStorage;

    public WatchViewModel(IProviderFactory providerFactory,
                          IMalClient client,
                          IViewService viewService,
                          ISettings settings,
                          IPlaybackStateStorage playbackStateStorage)
    {
        _client = client;
        _viewService = viewService;
        _settings = settings;
        _playbackStateStorage = playbackStateStorage;
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

        this.WhenAnyValue(x => x.SelectedProviderType)
            .Subscribe(x => Provider = providerFactory.GetProvider(x));

        this.WhenAnyValue(x => x.Query)
            .WhereNotNull()
            .Throttle(TimeSpan.FromMilliseconds(500), RxApp.TaskpoolScheduler)
            .SelectMany(async x => await Provider.Catalog.Search(x).ToListAsync())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                if (_settings.PreferSubs)
                {
                    RemoveDubs(x);
                }

                _searchResultCache.EditDiff(x, (first, second) => first.Title == second.Title);
            });

        this.WhenAnyValue(x => x.CurrentPlayerTime)
            .Where(_ => Anime is not null)
            .Where(_ => Anime.UserStatus.WatchedEpisodes <= CurrentEpisode)
            .Where(x => CurrentMediaDuration - x <= 135)
            .Subscribe(async _ => await IncrementEpisode());

        this.WhenAnyValue(x => x.CurrentEpisode)
            .Where(x => x > 0)
            .Subscribe(async x => await FetchUrlForEp(x));
    }

    [Reactive] public string Query { get; set; }
    [Reactive] public ProviderType SelectedProviderType { get; set; }
    [Reactive] public ObservableCollection<int> Episodes { get; set; } = new();
    [Reactive] public string Url { get; set; }
    [Reactive] public bool IsSuggestionListOpen { get; set; }
    [Reactive] public double CurrentPlayerTime { get; set; }
    [Reactive] public int CurrentEpisode { get; set; }
    [Reactive] public bool NavigatedToWithParameter { get; set; }
    [Reactive] public string VideoPlayerRequestMessage { get; set; }
    public double CurrentMediaDuration { get; set; }
    public Anime Anime { get; set; }
    public long? MalId { get; }
    public SearchResult SelectedResult { get; set; }
    public List<ProviderType> Providers { get; set; } = Enum.GetValues<ProviderType>().Cast<ProviderType>().ToList();
    public IProvider Provider { get; private set; }
    public Action<string, int> ShowNotification { get; set; }
    public ReactiveCommand<SearchResult, Unit> SearchResultPicked { get; }
    public ReadOnlyObservableCollection<SearchResult> SearchResult => _searchResults;


    public async Task OnVideoPlayerMessageRecieved(WebMessage message)
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
                    await IncrementEpisode();
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
        }
    }

    public async Task FetchEpisodes(SearchResult result)
    {
        Episodes.Clear();
        var count = await Provider.StreamProvider.GetNumberOfStreams(result.Url);
        var obs = Observable.Range(1, count).ObserveOn(RxApp.MainThreadScheduler);
        obs.Subscribe(x => Episodes.Add(x));
        await obs.LastAsync();
        SelectedResult = result;
        
        if (Anime is not null && Episodes.Contains(Anime.UserStatus.WatchedEpisodes + 1))
        {
            CurrentEpisode = Anime.UserStatus.WatchedEpisodes + 1;
        }
    }

    public async Task FetchUrlForEp(int ep)
    {
        var epStream = await Provider.StreamProvider.GetStreams(SelectedResult.Url, ep..ep).ToListAsync();
        Url = epStream[0].Qualities.Values.ElementAt(0).Url;
    }

    private async Task IncrementEpisode()
    {
        _playbackStateStorage.Reset(Anime.Id, CurrentEpisode);
        
        var request = _client
            .Anime()
            .WithId(Anime.Id)
            .UpdateStatus()
            .WithEpisodesWatched(CurrentEpisode);

        if (CurrentEpisode == Anime.TotalEpisodes)
        {
            request.WithStatus(AnimeStatus.Completed);
        }

        Anime.UserStatus = await request.Publish();
    }

    public override async Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if (parameters.ContainsKey("Anime"))
        {
            NavigatedToWithParameter = true;
            Anime = parameters["Anime"] as Anime;
            var results = await Provider.Catalog.Search(Anime.Title).ToListAsync();

            if (_settings.PreferSubs)
            {
                RemoveDubs(results);
            }

            var selected = results.Count == 1
                ? results[0]
                : await _viewService.ChoooseSearchResult(results, SelectedProviderType);

            await FetchEpisodes(selected);
        }
    }

    private static void RemoveDubs(List<SearchResult> results)
    {
        results.RemoveAll(x => x.Title.Contains("(DUB)", StringComparison.OrdinalIgnoreCase) || x.Title.Contains("[DUB]", StringComparison.OrdinalIgnoreCase));
    }
}
