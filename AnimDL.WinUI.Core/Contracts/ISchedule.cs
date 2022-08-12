namespace AnimDL.WinUI.Core.Contracts;

public interface ISchedule
{
    TimeSpan GetTimeTillEpisodeAirs(long malId);
}
