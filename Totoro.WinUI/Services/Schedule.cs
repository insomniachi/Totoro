using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CommunityToolkit.WinUI.Notifications;
using Totoro.Core;

namespace Totoro.WinUI.Services;

public partial class Schedule : ISchedule
{
    public Dictionary<long, TimeRemaining> Dictionary { get; set; } = new();

    private readonly IAnimeServiceContext _animeService;
    private readonly HttpClient _httpClient;
    private readonly IStreamPageMapper _streamPageMapper;
    private readonly ISettings _settings;

    public Schedule(IAnimeServiceContext trackingService,
                    HttpClient httpClient,
                    IAiredEpisodeNotifier notifier,
                    IStreamPageMapper streamPageMapper,
                    ISettings settings)
    {
        _animeService = trackingService;
        _httpClient = httpClient;
        _streamPageMapper = streamPageMapper;
        _settings = settings;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.UserAgent);

        notifier
            .OnNewEpisode
            .SelectMany(ShowToast)
            .Subscribe();

    }

    private async Task<Unit> ShowToast(AiredEpisode epInfo)
    {
        var id = await _streamPageMapper.GetMalIdFromUrl(epInfo.Url, _settings.DefaultProviderType);
        var anime = await _animeService.GetInformation(id);
        var ep = epInfo.Episode;
        if (anime.Tracking is null)
        {
            return Unit.Default;
        }

        if (anime.Tracking is not { Status: AnimeStatus.Watching } || (anime.Tracking.WatchedEpisodes ?? 0) <= ep)
        {
            return Unit.Default;
        }

        new ToastContentBuilder()
            .SetToastScenario(ToastScenario.Default)
            .AddText("New Episode")
            .AddText(anime.Title)
            .AddText(epInfo.Episode.ToString())
            .Show();

        return Unit.Default;
    }

    public async Task FetchSchedule()
    {
        Dictionary.Clear();
        var response = await _httpClient.GetAsync("https://animixplay.to/assets/s/schedule.json");
        var json = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(json).AsArray();
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        foreach (var item in node)
        {
            var time = long.Parse(item["time"].ToString());
            var tr = new TimeRemaining(Convert(now, time), DateTimeOffset.FromUnixTimeMilliseconds(now).LocalDateTime);
            Dictionary.Add(long.Parse(item["malid"].ToString()), tr);
        }
    }

    private static TimeSpan Convert(long now, long time)
    {
        double i;

        for (i = 1e3 * (time + 7200) - now; i < -216e5; i += 6048e5)
        {
            // TODO : check if there is a inbuilt way of doing this
        }

        return TimeSpan.FromSeconds(Math.Floor(i / 1e3));
    }

    public TimeRemaining GetTimeTillEpisodeAirs(long malId)
    {
        if (!Dictionary.ContainsKey(malId))
        {
            return null;
        }

        return Dictionary[malId];
    }

    [GeneratedRegex("ep(\\d+)")]
    private static partial Regex EpisodeRegex();
}
