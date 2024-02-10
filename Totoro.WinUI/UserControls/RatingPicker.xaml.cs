using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Totoro.WinUI.UserControls;

public sealed partial class RatingPicker : UserControl
{
    private readonly string[] _ratingNames =
    [
        "(0) - No Score",
        "(1) - Appalling",
        "(2) - Horring",
        "(3) - Very Bad",
        "(4) - Bad",
        "(5) - Average",
        "(6) - Fine",
        "(7) - Good",
        "(8) - Very Good",
        "(9) - Great",
        "(10) - Masterpiece"
    ];

    public AnimeModel Anime
    {
        get { return (AnimeModel)GetValue(AnimeProperty); }
        set { SetValue(AnimeProperty, value); }
    }

    public bool IsCompact
    {
        get { return (bool)GetValue(IsCompactProperty); }
        set { SetValue(IsCompactProperty, value); }
    }

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register("IsCompact", typeof(bool), typeof(RatingPicker), new PropertyMetadata(false));

    public static readonly DependencyProperty AnimeProperty =
        DependencyProperty.Register("Anime", typeof(AnimeModel), typeof(RatingPicker), new PropertyMetadata(null));

    public ICommand UpdateRating { get; }

    public RatingPicker()
    {
        InitializeComponent();

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .SelectMany(x => x.WhenAnyValue(x => x.Tracking))
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(tracking =>
            {
                var text = tracking.Score == 0 ? "-" : tracking.Score.ToString();
                if (IsCompact)
                {
                    CompactRatingText.Text = text;
                }
                else
                {
                    RatingText.Text = text;
                }
            });

        UpdateRating = ReactiveCommand.Create<int>(async score =>
        {
            Anime.Tracking = await App.GetService<ITrackingServiceContext>().Update(Anime.Id, new Tracking { Score = score });
        });

        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

    }

    public MenuFlyout CreateFlyout(AnimeModel anime)
    {
        if (anime is null)
        {
            return null;
        }

        var score = anime?.Tracking?.Score ?? 0;
        if (score > 10)
        {
            Visibility = Visibility.Collapsed;
            return null;
        }

        var flyout = new MenuFlyout();
        for (int i = 0; i < _ratingNames.Length; i++)
        {
            flyout.Items.Add(new RadioMenuFlyoutItem
            {
                Command = UpdateRating,
                CommandParameter = i,
                IsChecked = score == i,
                Text = _ratingNames[i],
                GroupName = anime.Id.ToString()
            });
        }
        return flyout;
    }

    private void StackPanel_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout(sender as StackPanel);
    }

    private void StackPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if(FlyoutBase.GetAttachedFlyout(sender as StackPanel) is not MenuFlyout flyout)
        {
            return;
        }

        var score = Anime?.Tracking?.Score ?? 0;
        if(score > 10)
        {
            return;
        }

        ((RadioMenuFlyoutItem)flyout.Items[score]).IsChecked = true;
    }
}
