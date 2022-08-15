using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AnimDL.UI.Core.Models;
using AnimDL.WinUI.Core.Contracts;
using CommunityToolkit.WinUI.Notifications;
using MalApi.Interfaces;
using System.Linq;
using ReactiveUI;

namespace AnimDL.WinUI.Core;

public class Schedule : ISchedule
{
    public Dictionary<long, TimeRemaining> Dictionary { get; set; } = new();
    private DateTime _lastUpdatedAt;
    private bool _isRefreshing;
    private readonly HttpClient _client = new();
    private readonly IMalClient _malClient;

    public Schedule(IMessageBus messageBus, IMalClient client)
    {
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
            .AddText("New episode aired").AddText(anime.Title).AddHeroImage(new Uri(anime.MainPicture.Large)).Show();
    }

    public async Task FetchSchedule()
    {
        _isRefreshing = true;

        Dictionary.Clear();
        using var message = new HttpRequestMessage(HttpMethod.Get, "https://animixplay.to/assets/s/schedule.json");
        message.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.81 Safari/537.36 Edg/104.0.1293.54");

        var response = _client.Send(message);
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

    private TimeSpan Convert(long now, long time)
    {
        double i;
        for (i = 1e3 * (time + 7200) - now; i < -216e5; i += 6048e5)
            ; // TODO : check if there is a inbuilt way of doing this
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
