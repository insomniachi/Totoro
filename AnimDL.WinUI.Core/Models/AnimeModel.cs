using System.ComponentModel;
using System.Reactive.Linq;
using MalApi;

namespace AnimDL.WinUI.Models;

public class Inpc : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    public void RaisePropertyChanged(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

public class AnimeModel : Inpc
{
    public long Id { get; set; }
    public string Image { get; set; }
    public string Title { get; set; }
    public UserAnimeStatus UserAnimeStatus { get; set; }
    public int? TotalEpisodes { get; set; }
}

public class ScheduledAnimeModel : AnimeModel
{
    public DayOfWeek? BroadcastDay { get; set; }
    public TimeRemaining TimeRemaining { get; set; }
}

public class SeasonalAnimeModel : ScheduledAnimeModel
{
    public Season Season { get; set; }
}

public class TimeRemaining : Inpc
{
    public TimeSpan TimeSpan { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    public TimeRemaining(TimeSpan timeSpan, DateTime lastUpdatedAt)
    {
        TimeSpan = timeSpan;
        LastUpdatedAt = lastUpdatedAt;

        Observable.Timer(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))
                  .Subscribe(_ =>
                  {
                      var now = DateTime.Now;
                      var diff = now - LastUpdatedAt;
                      TimeSpan -= diff;
                      lastUpdatedAt = now;
                      RaisePropertyChanged(nameof(TimeSpan));
                  });
    }
}