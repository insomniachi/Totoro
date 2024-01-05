using System.Reactive.Concurrency;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Torrents.Contracts;
using Totoro.Plugins.Torrents.Models;

namespace Totoro.Core.ViewModels.About;

public class AnimeEpisodesTorrentViewModel : BaseAboutAnimeViewModel
{
    private readonly ITorrentTracker _catalog;
    private readonly IAnimeIdService _animeIdService;
    private readonly ISimklService _simklService;

    public AnimeEpisodesTorrentViewModel(IAnimeIdService animeIdService,
                                         ISimklService simklService,
                                         IPluginFactory<ITorrentTracker> torrentCatalogFactory,
                                         ISettings settings)
    {
        _animeIdService = animeIdService;
        _simklService = simklService;
        _catalog = torrentCatalogFactory.CreatePlugin(settings.DefaultTorrentTrackerType);

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(GetEpisodes)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(episodes =>
            {
                Episodes = episodes;
                SelectedEpisode = Episodes.FirstOrDefault(x => x.EpisodeNumber == (Anime.Tracking?.WatchedEpisodes ?? 0) + 1) ?? Episodes.LastOrDefault();
            });

        this.WhenAnyValue(x => x.SelectedEpisode)
            .WhereNotNull()
            .Do(_ => RxApp.MainThreadScheduler.Schedule(() => IsLoading = true))
            .Select(x => x.EpisodeNumber)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(SearchTorrents)
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToPropertyEx(this, x => x.Torrents);

        this.WhenAnyValue(x => x.Torrents)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => IsLoading = false);

    }

    [Reactive] public EpisodeModel SelectedEpisode { get; set; }
    [Reactive] public ObservableCollection<EpisodeModel> Episodes { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [ObservableAsProperty] public List<TorrentModel> Torrents { get; }

    private async Task<List<TorrentModel>> SearchTorrents(int episode)
    {
        if(_catalog is null)
        {
            return [];
        }

        var torrents = await _catalog.Search($"{Anime.Title} - {episode.ToString().PadLeft(2, '0')}").ToListAsync();
        return torrents;
    }

    private async Task<ObservableCollection<EpisodeModel>> GetEpisodes(AnimeModel anime)
    {
        var malId = anime.MalId ?? (await _animeIdService.GetId(anime.Id)).MyAnimeList;
        if (malId is null)
        {
            return [];
        }

        ObservableCollection<EpisodeModel> episodes = new(await _simklService.GetEpisodes(malId.Value));
        var lastEpisodeNumber = episodes.LastOrDefault()?.EpisodeNumber ?? 0;
        var count = anime.AiredEpisodes - lastEpisodeNumber;
        if (count > 0)
        {
            foreach (var ep in Enumerable.Range(lastEpisodeNumber + 1, count))
            {
                episodes.Add(new EpisodeModel { EpisodeNumber = ep });
            }
        }

        return episodes;
    }
}