using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System.Net.Http;
using Totoro.Core;
using Totoro.Plugins.Anime.Contracts;

namespace Totoro.WinUI.Services;

public class AiredEpisodeToastService : IAiredEpisodeToastService
{
    public Dictionary<long, TimeRemaining> Dictionary { get; set; } = [];

    private readonly IAnimeServiceContext _animeService;
    private readonly HttpClient _httpClient;
    private readonly IAiredEpisodeNotifier _notifier;
    private readonly IStreamPageMapper _streamPageMapper;
    private readonly ISettings _settings;
    private bool _isStarted;

    public AiredEpisodeToastService(IAnimeServiceContext animeService,
                                    HttpClient httpClient,
                                    IAiredEpisodeNotifier notifier,
                                    IStreamPageMapper streamPageMapper,
                                    ISettings settings)
    {
        _animeService = animeService;
        _httpClient = httpClient;
        _notifier = notifier;
        _streamPageMapper = streamPageMapper;
        _settings = settings;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.UserAgent);
    }

    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _notifier.Start();
        _isStarted = true;

        _notifier
        .OnNewEpisode
        .Subscribe(x =>
        {
            _ = ShowToast(x);
        });
    }

    private async Task ShowToast(IAiredAnimeEpisode epInfo)
    {
        var id = await _streamPageMapper.GetIdFromUrl(epInfo.Url, _settings.DefaultProviderType);

        if (id is null)
        {
            return;
        }

        var anime = await _animeService.GetInformation(id.Value);
        var ep = epInfo.Episode;
        if (anime.Tracking is null)
        {
            return;
        }

        if (anime.Tracking is not { Status: AnimeStatus.Watching } || (anime.Tracking.WatchedEpisodes ?? 0) <= ep)
        {
            return;
        }

        var notification = new AppNotificationBuilder()
            .SetScenario(AppNotificationScenario.Default)
            .AddText("New Episode")
            .AddText(anime.Title)
            .AddText(epInfo.Episode.ToString())
            .BuildNotification();

        AppNotificationManager.Default.Show(notification);

        return;
    }
}
