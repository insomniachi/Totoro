using Humanizer;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Totoro.WinUI.UserControls;

public sealed partial class AnimeCard : UserControl
{
    public static readonly DependencyProperty AnimeProperty =
        DependencyProperty.Register("Anime", typeof(AnimeModel), typeof(AnimeCard), new PropertyMetadata(null));

    public AnimeModel Anime
    {
        get { return (AnimeModel)GetValue(AnimeProperty); }
        set { SetValue(AnimeProperty, value); }
    }

    public MenuFlyout Flyout { get; set; }
    public ICommand WatchCommand { get; }
    public ICommand MoreCommand { get; }
    public DisplayMode DisplayMode { get; set; } = DisplayMode.Grid;
    public bool ShowNextEpisodeTime { get; set; } = false;
    public bool ShowAddToList { get; set; } = true;

    public AnimeCard()
    {
        InitializeComponent();
    }

    public Visibility AddToListButtonVisibility(AnimeModel a)
    {
        if (a is null || ShowAddToList == false)
        {
            return Visibility.Collapsed;
        }

        return a.Tracking is null ? Visibility.Visible : Visibility.Collapsed;
    }

    public Visibility NextEpisodeInVisibility(string text)
    {
        return string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    public string GetTime(DateTime? airingAt)
    {
        if (!ShowNextEpisodeTime)
        {
            return string.Empty;
        }

        return airingAt is null
            ? string.Empty
            : (airingAt.Value - DateTime.Now).Humanize(2);
    }

    private static string ToString(TimeSpan ts) => ts.Humanize(2);

    public Brush GetBorderBrush(AiringStatus status)
    {
        return status switch
        {
            AiringStatus.CurrentlyAiring => new SolidColorBrush(Colors.LimeGreen),
            AiringStatus.FinishedAiring => new SolidColorBrush(Colors.MediumSlateBlue),
            AiringStatus.NotYetAired => new SolidColorBrush(Colors.LightSlateGray),
            _ => new SolidColorBrush(Colors.Navy),
        };
    }

    public Dictionary<string, string> GetAdditionalInformation(AnimeModel anime)
    {
        if (anime is not AnimeModel fa)
        {
            return new();
        }

        return new Dictionary<string, string>
        {
            ["Episodes"] = $"{(fa.TotalEpisodes is > 0 ? fa.TotalEpisodes.ToString() : "Unknown")}",
            ["Genres"] = $"{string.Join(", ", fa.Genres ?? Enumerable.Empty<string>())}",
            ["Score"] = $"{fa.MeanScore}",
            ["Popularity"] = $"#{fa.Popularity}"
        };
    }

    public Visibility InfoBadgeVisibillity(int value) => value > 0 ? Visibility.Visible : Visibility.Collapsed;
    public int UnwatchedEpisodes(int airedEpisodes)
    {
        if (Anime is null)
        {
            return -1;
        }

        if (Anime.Tracking is null || Anime.Tracking.WatchedEpisodes is null)
        {
            return -1;
        }

        if (airedEpisodes == 0)
        {
            return -1;
        }

        return (airedEpisodes - Anime.Tracking.WatchedEpisodes.Value);
    }

}
