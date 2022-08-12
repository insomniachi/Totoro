using AnimDL.WinUI.Contracts.Services;
using AnimDL.WinUI.Core.Contracts;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnimDL.WinUI.Core;

public class Schedule : ISchedule
{
    class State
    {
        public DateTime Time { get; set; }
        public List<ScheduleModel> Data { get; set; }
    }

    class ScheduleModel
    {
        [JsonPropertyName("malid")]
        public long MalId { get; set; }

        [JsonPropertyName("name")]
        public string Title { get; set; }

        [JsonPropertyName("time")]
        public long TimeInSeconds { get; set; }
    }

    public Dictionary<long, TimeSpan> Dictionary { get; set; }

    public Schedule(ILocalSettingsService localSettingsService)
    {
        using var client = new HttpClient();
        var stream = client.GetStreamAsync("https://animixplay.to/assets/s/schedule.json").Result;
        var data = JsonSerializer.Deserialize<List<ScheduleModel>>(stream);
        //localSettingsService.SaveSetting("Schedule", new State { Time = DateTime.Now, Data = data });
        foreach (var item in data)
        {
            Dictionary.Add(item.MalId, TimeSpan.FromSeconds(item.TimeInSeconds));
        }
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
