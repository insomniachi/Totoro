using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Core.Contracts;
using AnimDL.WinUI.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AnimDL.WinUI.Core;

public class Schedule : ISchedule
{
    public Dictionary<long, TimeSpan> Dictionary { get; set; } = new();

    public Schedule(ILocalSettingsService localSettingsService)
    {
        using var client = new HttpClient();
        using var message = new HttpRequestMessage(HttpMethod.Get, "https://animixplay.to/assets/s/schedule.json");
        message.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.81 Safari/537.36 Edg/104.0.1293.54");

        var response = client.Send(message);
        var json = response.Content.ReadAsStringAsync().Result;
        var node = JsonArray.Parse(json).AsArray();
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        foreach (var item in node)
        {
            var time = long.Parse(item["time"].ToString());
            Dictionary.Add(long.Parse(item["malid"].ToString()), Convert(now,time));
        }

    }

    private TimeSpan Convert(long now, long time)
    {
        double i;
        for (i = 1e3 * (time + 7200) - now; i < -216e5; i += 6048e5); // TODO : check if there is a inbuilt way of doing this
        return TimeSpan.FromSeconds(Math.Floor(i / 1e3));
    }

    public TimeSpan GetTimeTillEpisodeAirs(long malId)
    {
        if (!Dictionary.ContainsKey(malId))
        {
            return TimeSpan.Zero;
        }

        return Dictionary[malId];
    }
}
