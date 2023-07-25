using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReactiveMarbles.ObservableEvents;

namespace Totoro.WinUI.UserControls;

public sealed partial class RatingPicker : UserControl
{
    public AnimeModel Anime
    {
        get { return (AnimeModel)GetValue(AnimeProperty); }
        set { SetValue(AnimeProperty, value); }
    }

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
                RatingText.Text = tracking.Score == 0 ? "-" : tracking.Score.ToString();
            });

        UpdateRating = ReactiveCommand.Create<string>(async score =>
        {
            Anime.Tracking = await App.GetService<ITrackingServiceContext>().Update(Anime.Id, new Tracking { Score = int.Parse(score) });
        });
    }
}
