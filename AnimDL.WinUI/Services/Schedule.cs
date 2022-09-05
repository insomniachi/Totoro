using System.Net.Http;
using System.Text.Json.Nodes;
using CommunityToolkit.WinUI.Notifications;
using MalApi.Interfaces;

namespace AnimDL.WinUI.Services;

public class Schedule : ISchedule
{
    public Dictionary<long, TimeRemaining> Dictionary { get; set; } = new();
    private DateTime _lastUpdatedAt;
    private bool _isRefreshing;
    private readonly HttpClient _httpClient;
    private readonly IMalClient _malClient;

    public Schedule(IMessageBus messageBus,
                    IMalClient client,
                    HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UI.Core.Constants.UserAgent);

        Observable.StartAsync(() => FetchSchedule());

        messageBus.Listen<MinuteTick>()
                  .Where(_ => !_isRefreshing)
                  .ObserveOn(RxApp.TaskpoolScheduler)
                  .SubscribeOn(RxApp.TaskpoolScheduler)
                  .Subscribe(_ =>
                  {
                      var now = DateTime.Now;
                      var diff = now - _lastUpdatedAt;
                      foreach (var kv in Dictionary)
                      {
                          var item = kv.Value;
                          item.LastUpdatedAt = now;
                          item.TimeSpan -= diff;

                          if (item.TimeSpan.TotalSeconds < 0)
                          {
                              var task = ShowToast(kv.Key);
                          }
                      }
                      _lastUpdatedAt = now;

                      var expired = Dictionary.Where(x => x.Value.TimeSpan.TotalSeconds <= 0).Select(x => x.Key);
                      foreach (var item in expired)
                      {
                          Dictionary.Remove(item);
                      }
                  });
        _malClient = client;
    }

    private async Task ShowToast(long key)
    {
        var anime = await _malClient.Anime().WithId(key).WithField(x => x.UserStatus).Find();
        if (anime.UserStatus is null)
            return;

        new ToastContentBuilder().SetToastScenario(ToastScenario.Reminder)
            .AddText("New episode aired").AddText(anime.Title).Show();
    }

    public async Task FetchSchedule()
    {
        _isRefreshing = true;

        Dictionary.Clear();
        var response = await _httpClient.GetAsync("https://animixplay.to/assets/s/schedule.json");
        var json = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(json).AsArray();
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        
        _lastUpdatedAt = DateTime.Now;
        
        foreach (var item in node)
        {
            var time = long.Parse(item["time"].ToString());
            var tr = new TimeRemaining(Convert(now, time), DateTimeOffset.FromUnixTimeMilliseconds(now).LocalDateTime);
            Dictionary.Add(long.Parse(item["malid"].ToString()), tr);
        }

        _isRefreshing = false;
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
}
