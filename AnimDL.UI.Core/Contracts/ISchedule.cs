using AnimDL.UI.Core.Models;

namespace AnimDL.UI.Core.Contracts;

public interface ISchedule
{
    Task FetchSchedule();
    TimeRemaining GetTimeTillEpisodeAirs(long malId);
}
