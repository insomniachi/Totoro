using System.Reactive.Linq;
using Humanizer;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.UserControls;

public sealed partial class AnimeCard : UserControl
{
    public static readonly DependencyProperty AnimeProperty =
        DependencyProperty.Register("Anime", typeof(AnimeModel), typeof(AnimeCard), new PropertyMetadata(null, OnChanged));

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = d as AnimeCard;
        if (e.NewValue is AnimeModel)
        {
            card.Update();
        }
    }

    private readonly IViewService _viewService = App.GetService<IViewService>();
    private readonly INavigationService _navigationService = App.GetService<INavigationService>();
    //private static readonly IAnimeScheduleService _animeScheduleService = App.GetService<IAnimeScheduleService>();

    public AnimeModel Anime
    {
        get { return (AnimeModel)GetValue(AnimeProperty); }
        set { SetValue(AnimeProperty, value); }
    }

    public MenuFlyout Flyout { get; set; }
    public ICommand UpdateStatusCommand { get; }
    public ICommand WatchCommand { get; }
    public ICommand MoreCommand { get; }
    public DisplayMode DisplayMode { get; set; } = DisplayMode.Grid;

    public AnimeCard()
    {
        InitializeComponent();
        UpdateStatusCommand = ReactiveCommand.CreateFromTask<AnimeModel>(_viewService.UpdateTracking);
        WatchCommand = ReactiveCommand.Create<AnimeModel>(anime =>
        {
            _navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>() { ["Anime"] = anime });
        });
        MoreCommand = ReactiveCommand.Create<long>(id =>
        {
            _navigationService.NavigateTo<AboutAnimeViewModel>(parameter: new Dictionary<string, object>() { ["Id"] = id });
        });

        //this.WhenAnyValue(x => x.Anime)
        //    .WhereNotNull()
        //    .Where(x => x.Tracking is { Status : AnimeStatus.Watching })
        //    .ObserveOn(RxApp.MainThreadScheduler)
        //    .Subscribe(async anime =>
        //    {
        //        var nextEp = await _animeScheduleService.GetNextAiringEpisode(anime.Id);
        //        var unwatched = nextEp - 1 - (anime.Tracking.WatchedEpisodes ?? 0);
        //        if (unwatched is > 0)
        //        {
        //            InfoBadge.Visibility = Visibility.Visible;
        //            InfoBadge.Value = unwatched ?? -1;
        //        }
        //    });

    }

    private void Update() => NextEpisodeInText.Text = GetTime(Anime);

    public Visibility AddToListButtonVisibility(AnimeModel a)
    {
        if (a is null)
        {
            return Visibility.Collapsed;
        }

        return a.Tracking is null ? Visibility.Visible : Visibility.Collapsed;
    }

    public Visibility NextEpisodeInVisibility(AnimeModel a)
    {
        //if (a is not ScheduledAnimeModel m)
        //{
        //    return Visibility.Collapsed;
        //}

        //return m.TimeRemaining is not null ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public string GetTime(AnimeModel a)
    {
        //if (a is not ScheduledAnimeModel m)
        //{
        //    return string.Empty;
        //}

        //return m.TimeRemaining is TimeRemaining tr ? ToString(tr.TimeSpan) : string.Empty;
        return string.Empty;
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
        if(Anime is null)
        {
            return -1;
        }    

        if(Anime.Tracking is null || Anime.Tracking.WatchedEpisodes is null)
        {
            return -1;
        }

        if(airedEpisodes == 0)
        {
            return -1;
        }

        return (airedEpisodes - Anime.Tracking.WatchedEpisodes.Value);
    }

}
