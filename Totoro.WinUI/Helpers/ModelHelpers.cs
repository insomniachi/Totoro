using Humanizer;
using Microsoft.UI.Xaml;

namespace Totoro.WinUI.Helpers;

public static class ModelHelpers
{
    public static string GetTimeOfAiring(this AiredEpisode episode)
    {
        if (episode is IHaveCreatedTime ihct)
        {
            if ((DateTime.Now - ihct.CreatedAt).TotalDays > 27) // hack for now Allanime moths are from 0-11, need to fix in AnimDL
            {
                return ihct.CreatedAt.AddMonths(1).Humanize();
            }
            else
            {
                return ihct.CreatedAt.Humanize();
            }
        }

        return string.Empty;
    }

    public static Visibility GetTimeOfAiringVisibility(this AiredEpisode episode) => Converters.BooleanToVisibility(episode is IHaveCreatedTime);
}
