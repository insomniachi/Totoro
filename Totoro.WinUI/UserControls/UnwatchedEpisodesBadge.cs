using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.WinUI.UserControls;

public class UnwatchedEpisodesBadge : InfoBadge
{
    public AnimeModel Anime
    {
        get { return (AnimeModel)GetValue(AnimeProperty); }
        set { SetValue(AnimeProperty, value); }
    }

    public static readonly DependencyProperty AnimeProperty =
        DependencyProperty.Register("Anime", typeof(AnimeModel), typeof(UnwatchedEpisodesBadge), new PropertyMetadata(null));

    public UnwatchedEpisodesBadge()
    {
        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .SelectMany(x => x.WhenAnyValue(x => x.AiredEpisodes))
            .Select(airedEpisodes => GetUnwatchedEpsiodes(Anime, airedEpisodes))
            .Where(x => x is > 0)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(unwatchedEpisodes =>
            {
                Value = unwatchedEpisodes;
                Visibility = unwatchedEpisodes > 0 ? Visibility.Visible : Visibility.Collapsed;
            });
    }

    private static int GetUnwatchedEpsiodes(AnimeModel anime, int airedEpisodes)
    {
        if (anime is null)
        {
            return -1;
        }

        if (anime.Tracking is null || anime.Tracking.WatchedEpisodes is null)
        {
            return -1;
        }

        if (airedEpisodes == 0)
        {
            return -1;
        }

        return (airedEpisodes - anime.Tracking.WatchedEpisodes.Value);
    }
}
