using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.UserControls;

public sealed partial class AnimeCard : UserControl
{
    public static readonly DependencyProperty AnimeProperty =
        DependencyProperty.Register("Anime", typeof(AnimeModel), typeof(AnimeCard), new PropertyMetadata(null));
    public static readonly DependencyProperty FlyoutProperty =
        DependencyProperty.Register("Flyout", typeof(MenuFlyout), typeof(AnimeCard), new PropertyMetadata(null));
    public static readonly DependencyProperty ShowNextEpisodeTimeProperty =
        DependencyProperty.Register("ShowNextEpisodeTime", typeof(bool), typeof(AnimeCard), new PropertyMetadata(false));

    public bool ShowNextEpisodeTime
    {
        get { return (bool)GetValue(ShowNextEpisodeTimeProperty); }
        set { SetValue(ShowNextEpisodeTimeProperty, value); }
    }

    public AnimeModel Anime
    {
        get { return (AnimeModel)GetValue(AnimeProperty); }
        set { SetValue(AnimeProperty, value); }
    }

    public MenuFlyout Flyout
    {
        get { return (MenuFlyout)GetValue(FlyoutProperty); }
        set { SetValue(FlyoutProperty, value); }
    }

    public DisplayMode DisplayMode { get; set; } = DisplayMode.Grid;
    public bool ShowAddToList { get; set; } = true;
    public static bool HasTouch { get; } = Windows.Devices.Input.PointerDevice.GetPointerDevices().Any(x => x.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Touch);

    public AnimeCard()
    {
        InitializeComponent();
        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(anime => ShowNextEpisodeTime = anime.NextEpisodeAt is not null)
            .SelectMany(x => x.WhenAnyValue(y => y.NextEpisodeAt).DistinctUntilChanged())
            .Subscribe(date => ShowNextEpisodeTime = date is not null);

        Loaded += (_, _) =>
        {
            if (!HasTouch)
            {
                MoreButton.Visibility = Visibility.Collapsed;
            }
        };
    }

    public Visibility AddToListButtonVisibility(AnimeModel a)
    {
        if (a is null || ShowAddToList == false)
        {
            return Visibility.Collapsed;
        }

        return a.Tracking is null ? Visibility.Visible : Visibility.Collapsed;
    }

    public string GetTime(DateTime? airingAt)
    {
        if (!ShowNextEpisodeTime)
        {
            return string.Empty;
        }

        return airingAt is null
            ? string.Empty
            : $"EP{Anime?.AiredEpisodes + 1}: {(airingAt.Value - DateTime.Now).HumanizeTimeSpan()}";
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
}
