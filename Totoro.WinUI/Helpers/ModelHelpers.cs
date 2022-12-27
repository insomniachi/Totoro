using Humanizer;
using Microsoft.UI.Xaml;

namespace Totoro.WinUI.Helpers;

public static class ModelHelpers
{
    public static string GetTimeOfAiring(this AiredEpisode episode)
    {
        if (episode is IHaveCreatedTime ihct)
        {
            return ihct.CreatedAt.Humanize();
        }

        return string.Empty;
    }

    public static Visibility GetTimeOfAiringVisibility(this AiredEpisode episode) => Converters.BooleanToVisibility(episode is IHaveCreatedTime);
}
