using AnimDL.UI.Core.Models;

namespace AnimDL.WinUI.Core.Contracts;

public interface ISchedule
{
    Task FetchSchedule();
    TimeRemaining GetTimeTillEpisodeAirs(long malId);
}
