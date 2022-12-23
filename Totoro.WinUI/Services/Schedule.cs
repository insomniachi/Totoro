using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CommunityToolkit.WinUI.Notifications;
using MalApi.Interfaces;
using Totoro.Core;

namespace Totoro.WinUI.Services;

public partial class Schedule : ISchedule
{
    public Dictionary<long, TimeRemaining> Dictionary { get; set; } = new();
    private readonly HttpClient _httpClient;
    private readonly IMalClient _malClient;

    public Schedule(IMalClient client,
                    HttpClient httpClient,
                    IAiredEpisodeNotifier notifier)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.UserAgent);

        notifier
            .OnNewEpisode
            .SelectMany(ShowToast)
            .Subscribe();

        _malClient = client;
    }

    private async Task<Unit> ShowToast(AiredEpisode epInfo)
    {
        if(epInfo.MalId == 0)
        {
            return Unit.Default;
        }

        var anime = await _malClient.Anime().WithId(epInfo.MalId.Value).WithField(x => x.UserStatus).Find();
        var epMatch = EpisodeRegex().Match(epInfo.EpisodeUrl);
        var ep = epMatch.Success ? int.Parse(epMatch.Groups[1].Value) : 1;

        if(anime.UserStatus is null)
        {
            return Unit.Default;
        }

        if (anime.UserStatus is not { Status : MalApi.AnimeStatus.Watching} || (anime.UserStatus?.WatchedEpisodes ?? 0) <= ep)
        {
            return Unit.Default;
        }

        new ToastContentBuilder()
            .SetToastScenario(ToastScenario.Default)
            .AddText("New Episode")
            .AddText(anime.Title)
            .AddText(epInfo.InfoText)
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
