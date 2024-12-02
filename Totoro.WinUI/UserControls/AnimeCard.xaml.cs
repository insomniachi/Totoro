using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Totoro.WinUI.Helpers;
using Totoro.WinUI.Views;

namespace Totoro.WinUI.UserControls;

public sealed partial class AnimeCard : UserControl
{
    public static readonly DependencyProperty AnimeProperty =
        DependencyProperty.Register("Anime", typeof(AnimeModel), typeof(AnimeCard), new PropertyMetadata(null));
    public static readonly DependencyProperty FlyoutProperty =
        DependencyProperty.Register("Flyout", typeof(MenuFlyout), typeof(AnimeCard), new PropertyMetadata(null));
    public static readonly DependencyProperty ShowNextEpisodeTimeProperty =
        DependencyProperty.Register("ShowNextEpisodeTime", typeof(bool), typeof(AnimeCard), new PropertyMetadata(false));
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register("Command", typeof(ICommand), typeof(AnimeCard), new PropertyMetadata(null));
    public static readonly DependencyProperty HasMeanScoreProperty =
        DependencyProperty.Register("HasMeanScore", typeof(bool), typeof(AnimeCard), new PropertyMetadata(false));

    private static readonly ISettings _settings = App.GetService<ISettings>();
    private static readonly IValueConverter _converter = new FuncValueConverter<ImageStretch, Stretch>(x => (Stretch)(int)x);

    public bool HasMeanScore
    {
        get { return (bool)GetValue(HasMeanScoreProperty); }
        set { SetValue(HasMeanScoreProperty, value); }
    }

    public ICommand Command
    {
        get { return (ICommand)GetValue(CommandProperty); }
        set { SetValue(CommandProperty, value); }
    }

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
            .Do(anime => HasMeanScore = anime.MeanScore > 0)
            .Do(anime => ShowNextEpisodeTime = anime.NextEpisodeAt is not null)
            .SelectMany(x => x.WhenAnyValue(y => y.NextEpisodeAt).DistinctUntilChanged())
            .Subscribe(date => ShowNextEpisodeTime = date is not null);

        BindingOperations.SetBinding(GridViewImage, Image.StretchProperty, new Binding
        {
            Source = _settings.UserListGridViewSettings,
            Path = new PropertyPath(nameof(_settings.UserListGridViewSettings.ImageStretch)),
            Converter = _converter
        });

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

    public Visibility IsRewatchingVisibile(AnimeModel anime)
    {
        if(anime is null)
        {
            return Visibility.Collapsed;
        }

        return anime.Tracking?.Status == AnimeStatus.Rewatching
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public string GetTitle(AnimeModel anime)
    {
        if(anime is null)
        {
            return string.Empty;
        }

        return _settings.UseEnglishTitles ? anime.EngTitle : anime.RomajiTitle;
    }

    private void ImageEx_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
	{
		if (e.OriginalSource is not (Image or Grid))
		{
			return;
		}

		Command?.Execute(Anime);
	}
}
