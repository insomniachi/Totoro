using System.Text;
using AnimDL.UI.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AnimDL.WinUI.UserControls;

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

    private static string ToString(TimeSpan ts)
    {
        var sb = new StringBuilder();

        if (ts.Days > 0)
        {
            sb.Append($"{ts.Days}d ");
        }
        if (ts.Hours > 0)
        {
            sb.Append($"{ts.Hours}h ");
        }
        if (ts.Minutes > 0)
        {
            sb.Append($"{ts.Minutes}m");
        }

        return sb.ToString();
    }

}
