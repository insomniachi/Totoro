using Humanizer;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Totoro.Core;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.UserControls;

public sealed partial class AnimeCard : UserControl
{
    public static readonly DependencyProperty AnimeProperty =
        DependencyProperty.Register("Anime", typeof(AnimeModel), typeof(AnimeCard), new PropertyMetadata(null, OnChanged));
    private IDisposable _garbage;

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = d as AnimeCard;
        if (e.NewValue is ScheduledAnimeModel)
        {
            card.Update();
        }
    }

    private readonly IViewService _viewService = App.GetService<IViewService>();
    private readonly INavigationService _navigationService = App.GetService<INavigationService>();

    public AnimeModel Anime
    {
        get { return (AnimeModel)GetValue(AnimeProperty); }
        set { SetValue(AnimeProperty, value); }
    }

    public MenuFlyout Flyout { get; set; }
    public ICommand UpdateStatusCommand { get; }
    public ICommand WatchCommand { get; }
    public DisplayMode DisplayMode { get; set; } = DisplayMode.Grid;

    public AnimeCard()
    {
        InitializeComponent();
        UpdateStatusCommand = ReactiveCommand.CreateFromTask<AnimeModel>(_viewService.UpdateAnimeStatus);
        WatchCommand = ReactiveCommand.Create<AnimeModel>(anime =>
        {
            _navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>() { ["Anime"] = Anime });
        });
        Loaded += AnimeCard_Loaded;
        Unloaded += AnimeCard_Unloaded;
    }

    private void AnimeCard_Loaded(object sender, RoutedEventArgs e)
    {
        if (Anime is not ScheduledAnimeModel { TimeRemaining: not null })
        {
            return;
        }

        _garbage = MessageBus.Current.Listen<MinuteTick>().ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
        {
            Update();
        });
    }

    private void AnimeCard_Unloaded(object sender, RoutedEventArgs e)
    {
        _garbage?.Dispose();
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
        if (a is not ScheduledAnimeModel m)
        {
            return Visibility.Collapsed;
        }

        return m.TimeRemaining is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    public string GetTime(AnimeModel a)
    {
        if (a is not ScheduledAnimeModel m)
        {
            return string.Empty;
        }

        return m.TimeRemaining is TimeRemaining tr ? ToString(tr.TimeSpan) : string.Empty;
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

    public Dictionary<string,string> GetAdditionalInformation(AnimeModel anime)
    {
        if(anime is not FullAnimeModel fa)
        {
            return new();
        }

        return new Dictionary<string, string>
        {
            ["Episodes"] = $"{(fa.TotalEpisodes is >0 ? fa.TotalEpisodes.ToString() : "Unknown")}",
            ["Genres"] = $"{string.Join(", ", fa.Genres ?? Enumerable.Empty<string>())}",
            ["Score"] = $"{fa.MeanScore}",
            ["Popularity"] = $"#{fa.Popularity}"
        };
    }

}
