namespace Totoro.Core.Contracts;

public interface ISchedule
{
    Task FetchSchedule();
    TimeRemaining GetTimeTillEpisodeAirs(long malId);
}
